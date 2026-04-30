#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SDK_DIR="$REPO_ROOT/src/SpacetimeDB.ClientSDK"
PATCH_FILE="$REPO_ROOT/patches/spacetimedb-cacheless.patch"
SDK_VERSION="v1.12.0"

echo "Initializing SpacetimeDB.ClientSDK submodule..."
cd "$REPO_ROOT"
git submodule update --init src/SpacetimeDB.ClientSDK

cd "$SDK_DIR"
git fetch --tags origin
git checkout "$SDK_VERSION"

echo "Applying cacheless patch..."
git apply "$PATCH_FILE"

echo "Done. SDK at $SDK_VERSION with cacheless patch applied."
