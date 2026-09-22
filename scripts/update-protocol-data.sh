#!/usr/bin/env bash
#
# Regenerates the game data that the source generators consume: packet IDs,
# registries, blocks, items, enchantments, tags, recipes and block loot tables,
# plus entity metadata names and block hardness.
#
# The data comes from the vanilla server's own data generator, so it is always
# exactly what the target version speaks. Only what the game does not export
# at all comes from PrismarineJS. Updating Vortex to a new Minecraft
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
copy data/minecraft/enchantment           "${DATA}/enchantment"
copy data/minecraft/recipe                "${DATA}/recipe"
copy data/minecraft/loot_table/blocks     "${DATA}/loot_table/blocks"

# Some things the game does not export at all: which index of an entity's
# metadata means what, and how hard a block is to break. PrismarineJS extracts
# those per version; its dataPaths.json names the folder that covers this one,
# which is often an older version's.
PRISMARINE="https://raw.githubusercontent.com/PrismarineJS/minecraft-data/master/data"
PATHS="$(curl -sSfL "${PRISMARINE}/dataPaths.json" | tr -d ' \n\r' | sed 's/.*"pc":{//')"

# Fetches one of PrismarineJS's files for this version into Resources/prismarine.
prismarine() {
    local kind="$1" path

    path="$(echo "${PATHS}" \
        | grep -o "\"${VERSION}\":{[^}]*}" \
        | head -1 \
        | grep -o "\"${kind}\":\"[^\"]*\"" \
        | cut -d'"' -f4 || true)"

    if [ -z "${path}" ]; then
        echo "PrismarineJS has no ${kind} data for ${VERSION} yet" >&2
        exit 1
    fi

    mkdir -p "${DATA}/prismarine"
    curl -sSfL "${PRISMARINE}/${path}/${kind}.json" -o "${DATA}/prismarine/${kind}.json"
    echo "  ${kind}: PrismarineJS ${path}"
}

echo "Fetching what the game does not export from PrismarineJS..."
prismarine entities
prismarine blocks

echo
echo "Done. Review the diff, then rebuild:"
echo "  git diff --stat Vortex.Modules.Networking.Abstraction/Resources/ Vortex.Data/Resources/"
echo "  dotnet build Vortex.sln"
echo
echo "Packet IDs, registries, blocks, tags, recipes, loot tables, entity"
echo "metadata names and block hardness are all regenerated from this."
echo "Packet *layouts* are not - fields that changed between versions still"
echo "have to be adjusted by hand."
