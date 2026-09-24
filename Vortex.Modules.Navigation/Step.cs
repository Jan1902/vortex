using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation;

/// <summary>Which kind of <see cref="Move"/> a <see cref="Step"/> stands for.</summary>
internal enum StepKind : byte
{
    Walk,
    StepUp,
    Drop,
    JumpGap,
    MineThrough,
    Swim,
    Pillar,
    Bridge,
}

/// <summary>
/// One move the search is considering, with what it costs and which way it
/// goes.
/// </summary>
/// <remarks>
/// <para>
/// A value rather than a <see cref="Move"/>, because the search weighs a great
/// many more moves than it keeps: every way out of every position it looks at.
/// Only the handful that end up in the route are ever turned into moves.
/// </para>
/// <para>
/// The move and its cost are produced together on purpose: what the search
/// decided and what it paid for that decision are the same fact, and the route
/// carries the move along so that nothing downstream has to work out from the
/// geometry what was meant.
/// </para>
/// <para>
/// A step that places a block always places it under where it ends up, so
/// where it goes needs no field of its own.
/// </para>
/// </remarks>
/// <param name="Kind">Which move this is.</param>
/// <param name="To">Where it ends up.</param>
/// <param name="Cost">What it costs, in ticks.</param>
/// <param name="Heading">
/// Which way it goes, as an index into <see cref="Headings"/>, or
/// <see cref="Headings.None"/> for a move that goes nowhere sideways.
/// </param>
/// <param name="Amount">How far a drop falls, or how wide a gap a jump clears.</param>
/// <param name="Sprinting">Whether a jump needs a sprint.</param>
/// <param name="Breaks">What has to be broken first.</param>
internal readonly record struct Step(
    StepKind Kind,
    Cell To,
    double Cost,
    int Heading,
    int Amount = 0,
    bool Sprinting = false,
    BlockList Breaks = default)
{
    /// <summary>How many blocks the step places.</summary>
    public int Places
        => Kind is StepKind.Pillar or StepKind.Bridge ? 1 : 0;

    /// <summary>The same step, going the given way.</summary>
    public Step Towards(int heading)
        => this with { Heading = heading };

    /// <summary>The same step, dearer or cheaper by a factor.</summary>
    public Step Scaled(double factor)
        => this with { Cost = Cost * factor };

    public Move ToMove()
        => Kind switch
        {
            StepKind.Walk => new Walk(To.ToVector3i()),
            StepKind.StepUp => new StepUp(To.ToVector3i()),
            StepKind.Drop => new Drop(To.ToVector3i(), Amount),
            StepKind.JumpGap => new JumpGap(To.ToVector3i(), Amount, Sprinting),
            StepKind.MineThrough => new MineThrough(To.ToVector3i(), Breaks.ToArray()),
            StepKind.Swim => new Swim(To.ToVector3i()),
            StepKind.Pillar => new Pillar(To.ToVector3i()),
            StepKind.Bridge => new Bridge(To.ToVector3i(), To.Below.ToVector3i()),
            _ => throw new InvalidOperationException($"No move for {Kind}"),
        };

    /// <summary>
    /// Whether this is the move a route asks for: the same kind, to the same
    /// place, done the same way.
    /// </summary>
    public bool Matches(Move move)
        => (Cell)move.To == To && move switch
        {
            Walk => Kind == StepKind.Walk,
            StepUp => Kind == StepKind.StepUp,
            Drop => Kind == StepKind.Drop,
            JumpGap jump => Kind == StepKind.JumpGap && jump.Sprinting == Sprinting,

            // What is in the way has to be exactly what the route means to
            // break. Anything else, and something has been built or broken
            // there since.
            MineThrough through => Kind == StepKind.MineThrough && Breaks.SameAs(through.Blocking),

            Swim => Kind == StepKind.Swim,
            Pillar => Kind == StepKind.Pillar,
            Bridge bridge => Kind == StepKind.Bridge && (Cell)bridge.Support == To.Below,

            _ => false,
        };
}

/// <summary>
/// Up to three blocks to break, held without an allocation.
/// </summary>
/// <remarks>
/// Three is the most any move digs through: the two a body fits in and the one
/// over the head to jump into.
/// </remarks>
internal readonly record struct BlockList
{
    private Cell First { get; init; }

    private Cell Second { get; init; }

    private Cell Third { get; init; }

    public int Count { get; private init; }

    public Cell this[int index]
        => index switch
        {
            0 when Count > 0 => First,
            1 when Count > 1 => Second,
            2 when Count > 2 => Third,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    /// <summary>The same list with another block on the end.</summary>
    public BlockList With(Cell block)
        => Count switch
        {
            0 => this with { First = block, Count = 1 },
            1 => this with { Second = block, Count = 2 },
            2 => this with { Third = block, Count = 3 },
            _ => throw new InvalidOperationException("No move breaks more than three blocks"),
        };

    public Vector3i[] ToArray()
    {
        var blocks = new Vector3i[Count];

        for (var i = 0; i < Count; i++)
            blocks[i] = this[i].ToVector3i();

        return blocks;
    }

    /// <summary>Whether this holds the same blocks as a list, in any order.</summary>
    public bool SameAs(IReadOnlyList<Vector3i> blocks)
    {
        if (blocks.Count != Count)
            return false;

        for (var i = 0; i < Count; i++)
            if (!blocks.Contains(this[i].ToVector3i()))
                return false;

        return true;
    }
}
