# Test server

A local Minecraft server for developing and testing Vortex against a real,
unmodified vanilla target.

**Compression is enabled** (vanilla default threshold of 256 bytes). That is
deliberate: a bot that cannot handle compression cannot talk to any normal
server, so the default setup is the realistic one.

## Usage

Start:

```bash
docker compose -f docker/docker-compose.yml up -d
```

Watch the log (server is ready when it prints `Done (x.xxxs)!`):

```bash
docker compose -f docker/docker-compose.yml logs -f mc
```

Stop, keeping the world:

```bash
docker compose -f docker/docker-compose.yml down
```

Reset the world completely:

```bash
docker compose -f docker/docker-compose.yml down -v
```

Send a server command (e.g. to check who is connected):

```bash
docker exec vortex-mc rcon-cli list
```

## Without compression

For isolating whether a bug lives in the compression path, a second server runs
on **port 25566** with compression disabled:

```bash
docker compose -f docker/docker-compose.yml --profile nocompression up -d mc-nocompression
```

## Configuration

Override via environment variables, e.g. to test against a newer protocol:

| Variable | Default | Purpose |
|---|---|---|
| `MC_VERSION` | `1.21.1` | Server version. Vortex currently targets protocol 767 = 1.21/1.21.1. |
| `MC_TYPE` | `VANILLA` | `PAPER` starts faster but may deviate slightly from vanilla protocol behaviour. |
| `MC_COMPRESSION` | `256` | Compression threshold. `-1` disables it. |

```bash
MC_VERSION=26.2 docker compose -f docker/docker-compose.yml up -d
```

The world is flat, structures and mob spawning are off, and view distance is
low — startup is fast and test runs are reproducible.
