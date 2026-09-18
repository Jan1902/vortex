#!/usr/bin/env bash
#
# Regenerates the protocol data that the source generators consume.
#
# The data comes from the vanilla server's own data generator, so it is always
# exactly what the target version speaks. Updating Vortex to a new Minecraft
# version starts by running this against that version.
#
# Usage:
#   scripts/update-protocol-data.sh [version]
#
# Defaults to the version the test server runs.

set -euo pipefail

VERSION="${1:-1.21.1}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONTAINER="vortex-data-generator"

echo "Generating protocol data for Minecraft ${VERSION}..."

cleanup() {
    docker rm -f "${CONTAINER}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

cleanup

# itzg's image downloads the server jar for us; we only need it on disk, not running.
docker run -d --name "${CONTAINER}" \
    -e EULA=TRUE \
    -e VERSION="${VERSION}" \
    -e TYPE=VANILLA \
    itzg/minecraft-server:latest >/dev/null

echo "Waiting for the server jar to be downloaded..."
for _ in $(seq 1 120); do
    if docker exec "${CONTAINER}" sh -c 'ls /data/minecraft_server.*.jar' >/dev/null 2>&1; then
        break
    fi
    sleep 2
done

JAR="$(docker exec "${CONTAINER}" sh -c 'ls /data/minecraft_server.*.jar' | tr -d '\r')"
if [ -z "${JAR}" ]; then
    echo "Could not find the server jar for ${VERSION}" >&2
    exit 1
fi

echo "Running the data generator (${JAR})..."
docker exec "${CONTAINER}" sh -c \
    "cd /tmp && java -DbundlerMainClass=net.minecraft.data.Main -jar ${JAR} --reports" >/dev/null

echo "Copying reports into the repository..."
docker cp "${CONTAINER}:/tmp/generated/reports/packets.json" \
    "${REPO_ROOT}/Vortex.Modules.Networking.CodeGeneration/Resources/packets.json"

echo
echo "Done. Review the diff, then rebuild:"
echo "  git diff --stat Vortex.Modules.Networking.CodeGeneration/Resources/"
echo "  dotnet build Vortex.sln"
echo
echo "Packet IDs are regenerated automatically. Packet *layouts* are not -"
echo "fields that changed between versions still have to be adjusted by hand."
