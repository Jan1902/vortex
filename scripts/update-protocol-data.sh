#!/usr/bin/env bash
#
# Regenerates the game data that the source generators consume: packet IDs,
# registries, blocks, items, tags, recipes and block loot tables.
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
    "cd /tmp && java -DbundlerMainClass=net.minecraft.data.Main -jar ${JAR} --reports --server" >/dev/null

GENERATED="/tmp/generated"
NETWORKING="${REPO_ROOT}/Vortex.Modules.Networking.Abstraction/Resources"
DATA="${REPO_ROOT}/Vortex.Data/Resources"

# Copies one generated file or folder, replacing what was there so entries that
# no longer exist in the new version disappear as well.
copy() {
    local source="$1" target="$2"

    rm -rf "${target}"
    mkdir -p "$(dirname "${target}")"
    docker cp "${CONTAINER}:${GENERATED}/${source}" "${target}" >/dev/null
}

echo "Copying the data into the repository..."
copy reports/packets.json                 "${NETWORKING}/packets.json"
copy reports/registries.json              "${DATA}/registries.json"
copy reports/blocks.json                  "${DATA}/blocks.json"
copy reports/items.json                   "${DATA}/items.json"
copy data/minecraft/tags/block            "${DATA}/tags/block"
copy data/minecraft/tags/item             "${DATA}/tags/item"
copy data/minecraft/tags/entity_type      "${DATA}/tags/entity_type"
copy data/minecraft/recipe                "${DATA}/recipe"
copy data/minecraft/loot_table/blocks     "${DATA}/loot_table/blocks"

echo
echo "Done. Review the diff, then rebuild:"
echo "  git diff --stat Vortex.Modules.Networking.Abstraction/Resources/ Vortex.Data/Resources/"
echo "  dotnet build Vortex.sln"
echo
echo "Packet IDs, registries, blocks, tags, recipes and loot tables are all"
echo "regenerated from this. Packet *layouts* are not - fields that changed"
echo "between versions still have to be adjusted by hand."
