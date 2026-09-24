using Microsoft.Extensions.Logging;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// Keeps the player's own state in sync with the server and runs the physics loop.
/// </summary>
internal class PlayerManager(
    ILogger<PlayerManager> logger,
    INetworkingManager networking,
    PlayerPhysics physics,
    MovementController movement) : IPlayerManager, IDisposable
{
    /// <summary>The server runs at 20 ticks per second.</summary>
    private static readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The vanilla client sends its position at least this often, even standing still,
    /// so the server does not consider the connection idle.
    /// </summary>
    private const int MaximumTicksBetweenUpdates = 20;

    /// <summary>Movement below this is not worth a packet.</summary>
    private const double MovementEpsilon = 0.0003;

    private readonly object _stateLock = new();

    private Vector3d _position = Vector3d.Zero;
    private Vector3d _velocity = Vector3d.Zero;
    private bool _onGround;

    /// <summary>
    /// Whether the last step was stopped by geometry. Carried across ticks
    /// because running into something is only known once the step has been
    /// taken, and is reacted to on the tick after the bump.
    /// </summary>
    private bool _wasBlocked;
    private bool _inWater;
    private float _yaw;
    private float _pitch;

    /// <summary>Assumed full until the server says otherwise.</summary>
    private float _health = 20;

    private Vector3d _lastSentPosition = Vector3d.Zero;
    private float _lastSentYaw;
    private float _lastSentPitch;
    private int _ticksSinceUpdate;

    /// <summary>
    /// Set while a teleport has been applied but not yet acknowledged. Movement
    /// sent in that window is measured against the server's old position and
    /// rejected as moving too quickly.
    /// </summary>
    private bool _awaitingTeleportConfirmation;

    private CancellationTokenSource? _tickLoop;

    public Vector3d Position
    {
        get { lock (_stateLock) return _position; }
    }

    public Vector3d Velocity
    {
        get { lock (_stateLock) return _velocity; }
    }

    public bool IsOnGround
    {
        get { lock (_stateLock) return _onGround; }
    }

    public float Yaw
    {
        get { lock (_stateLock) return _yaw; }
    }

    public float Pitch
    {
        get { lock (_stateLock) return _pitch; }
    }

    public float Health
    {
        get { lock (_stateLock) return _health; }
    }

    public bool IsAlive => Health > 0;

    public bool IsPositionSynchronized
    {
        get { lock (_stateLock) return !_awaitingTeleportConfirmation; }
    }

    public bool IsSpawned => _tickLoop is not null;

    /// <summary>
    /// Records the health the server reported.
    /// </summary>
    public void UpdateHealth(float health)
    {
        lock (_stateLock)
            _health = health;
    }

    public void Look(float yaw, float pitch)
    {
        lock (_stateLock)
        {
            _yaw = yaw;
            _pitch = Math.Clamp(pitch, -90f, 90f);
        }
    }

    public void LookAt(Vector3d target)
    {
        Vector3d eyes;

        lock (_stateLock)
            eyes = _position with { Y = _position.Y + PlayerPhysics.Height };

        var delta = target - eyes;
        var horizontalDistance = Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);

        // Minecraft measures yaw from south, turning clockwise.
        var yaw = (float)(Math.Atan2(-delta.X, delta.Z) * 180 / Math.PI);
        var pitch = (float)(-Math.Atan2(delta.Y, horizontalDistance) * 180 / Math.PI);

        Look(yaw, pitch);
    }

    /// <summary>
    /// Applies a position sent by the server and starts the physics loop on first spawn.
    /// </summary>
    public void Synchronize(SynchronizePlayerPosition packet)
    {
        lock (_stateLock)
        {
            // A set flag means the value is relative to the current one.
            _position = new Vector3d(
                packet.Flags.HasFlag(PositionFlags.X) ? _position.X + packet.X : packet.X,
                packet.Flags.HasFlag(PositionFlags.Y) ? _position.Y + packet.Y : packet.Y,
                packet.Flags.HasFlag(PositionFlags.Z) ? _position.Z + packet.Z : packet.Z);

            _yaw = packet.Flags.HasFlag(PositionFlags.Y_ROT) ? _yaw + packet.Yaw : packet.Yaw;
            _pitch = packet.Flags.HasFlag(PositionFlags.X_ROT) ? _pitch + packet.Pitch : packet.Pitch;

            // A teleport cancels whatever motion was in progress.
            _velocity = Vector3d.Zero;

            _lastSentPosition = _position;
            _lastSentYaw = _yaw;
            _lastSentPitch = _pitch;
            _ticksSinceUpdate = 0;

            _awaitingTeleportConfirmation = true;
        }

        // A teleport invalidates whatever the player was walking towards, and
        // the caller has to be able to tell that apart from a deliberate stop.
        movement.Desynchronize();

        logger.LogInformation("Player position synchronized to {X:F2} {Y:F2} {Z:F2}",
            packet.X, packet.Y, packet.Z);

        StartTicking();
    }

    /// <summary>
    /// Releases the movement loop once the teleport has been acknowledged.
    /// </summary>
    public void TeleportConfirmed()
    {
        lock (_stateLock)
            _awaitingTeleportConfirmation = false;
    }

    private void StartTicking()
    {
        if (_tickLoop is not null)
            return;

        _tickLoop = new CancellationTokenSource();

        _ = Task.Run(() => RunTickLoop(_tickLoop.Token));

        logger.LogInformation("Started physics loop");
    }

    private async Task RunTickLoop(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_tickInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await Tick();
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception e)
        {
            logger.LogError(e, "Physics loop stopped unexpectedly");
        }
    }

    private async Task Tick()
    {
        MovementState state;

        lock (_stateLock)
            state = new MovementState(_position, _velocity, _onGround, _wasBlocked, _inWater);

        var step = physics.Step(state.Position, state.Velocity, state.OnGround, movement.Tick(state));

        lock (_stateLock)
        {
            _position = step.Position;
            _velocity = step.Velocity;
            _onGround = step.OnGround;
            _wasBlocked = step.Blocked;
            _inWater = step.InWater;
        }

        await SendMovement();
    }

    private async Task SendMovement()
    {
        Vector3d position;
        float yaw;
        float pitch;
        bool onGround;

        lock (_stateLock)
        {
            if (_awaitingTeleportConfirmation)
                return;

            position = _position;
            yaw = _yaw;
            pitch = _pitch;
            onGround = _onGround;
        }

        var moved = Math.Abs(position.X - _lastSentPosition.X) > MovementEpsilon
            || Math.Abs(position.Y - _lastSentPosition.Y) > MovementEpsilon
            || Math.Abs(position.Z - _lastSentPosition.Z) > MovementEpsilon;

        var turned = yaw != _lastSentYaw || pitch != _lastSentPitch;

        _ticksSinceUpdate++;

        if (!moved && !turned && _ticksSinceUpdate < MaximumTicksBetweenUpdates)
            return;

        if (moved && turned)
            await networking.SendPacket(new SetPlayerPositionAndRotation(position.X, position.Y, position.Z, yaw, pitch, onGround));
        else if (moved)
            await networking.SendPacket(new SetPlayerPosition(position.X, position.Y, position.Z, onGround));
        else
            await networking.SendPacket(new SetPlayerOnGround(onGround));

        _lastSentPosition = position;
        _lastSentYaw = yaw;
        _lastSentPitch = pitch;
        _ticksSinceUpdate = 0;
    }

    public void Dispose()
    {
        _tickLoop?.Cancel();
        _tickLoop?.Dispose();
        _tickLoop = null;
    }
}
