#!/usr/bin/env bash
#
# Builds libimpeller.a for wasm32-emscripten from a Flutter engine checkout and drops it, together
# with impeller.h, into external/impeller_sdk/wasm/ (the same layout DownloadImpeller produces for
# native platforms).
#
# The archive is compiled with the Emscripten toolchain that ships in the .NET SDK
# (Microsoft.NET.Runtime.Emscripten.<ver>.Sdk pack) rather than Flutter's own emsdk, so that the
# object ABI matches what `dotnet publish` links against in a browser-wasm app.
#
# Usage:
#   ./build-impeller-wasm.sh                       # release build
#   FLUTTER_ROOT=/path/to/flutter ./build-impeller-wasm.sh
#   RUNTIME_MODE=debug ./build-impeller-wasm.sh
#
# Requirements:
#   - A gclient-synced Flutter engine checkout (FLUTTER_ROOT, default ../flutter) with
#     download_emsdk=True (only needed so `tools/gn --wasm` is happy; the emsdk itself is not used).
#   - The .NET `wasm-tools-net10` workload (provides the Emscripten 3.1.56 packs).

set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
FLUTTER_ROOT="${FLUTTER_ROOT:-$SCRIPT_DIR/../flutter}"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
EMSCRIPTEN_VERSION="${EMSCRIPTEN_VERSION:-3.1.56}"
RUNTIME_MODE="${RUNTIME_MODE:-release}"
OUT_NAME="wasm_${RUNTIME_MODE}"

ENGINE_SRC="$FLUTTER_ROOT/engine/src"
SDK_OUT="$SCRIPT_DIR/external/impeller_sdk/wasm"
SHIM="$SCRIPT_DIR/external/emsdk-dotnet-$EMSCRIPTEN_VERSION"

case "$(uname -s)-$(uname -m)" in
  Darwin-arm64) RID=osx-arm64 ;;
  Darwin-x86_64) RID=osx-x64 ;;
  Linux-x86_64) RID=linux-x64 ;;
  Linux-aarch64) RID=linux-arm64 ;;
  *) echo "unsupported host $(uname -s)-$(uname -m)" >&2; exit 1 ;;
esac

latest_pack() {
  # $1 = pack name prefix (without RID). Prints the newest installed version directory.
  local dir="$DOTNET_ROOT/packs/$1.$RID"
  [[ -d "$dir" ]] || { echo "missing .NET pack $1.$RID (install the wasm-tools workload)" >&2; exit 1; }
  # Prefer stable versions; fall back to prereleases only if that's all there is.
  { ls "$dir" | grep -v -- '-' || ls "$dir"; } | sort -V | tail -n 1
}

PACK_BASE="Microsoft.NET.Runtime.Emscripten.$EMSCRIPTEN_VERSION"
SDK_PACK="$DOTNET_ROOT/packs/$PACK_BASE.Sdk.$RID/$(latest_pack "$PACK_BASE.Sdk")/tools"
NODE_PACK="$DOTNET_ROOT/packs/$PACK_BASE.Node.$RID/$(latest_pack "$PACK_BASE.Node")/tools"
CACHE_PACK="$DOTNET_ROOT/packs/$PACK_BASE.Cache.$RID/$(latest_pack "$PACK_BASE.Cache")/tools"

echo "Emscripten: $SDK_PACK"

# Flutter's GN toolchain expects an emsdk-style layout: $emsdk_dir/upstream/emscripten/emcc and
# $emsdk_dir/.emscripten. Synthesize one from the .NET packs.
mkdir -p "$SHIM/upstream"
ln -sfn "$SDK_PACK/emscripten" "$SHIM/upstream/emscripten"
ln -sfn "$SDK_PACK/bin" "$SHIM/upstream/bin"
cat > "$SHIM/.emscripten" <<EOF
LLVM_ROOT = '$SDK_PACK/bin'
BINARYEN_ROOT = '$SDK_PACK'
NODE_JS = '$NODE_PACK/bin/node'
CACHE = '$CACHE_PACK/emscripten/cache'
FROZEN_CACHE = True
COMPILER_ENGINE = NODE_JS
JS_ENGINES = [NODE_JS]
EOF

cd "$ENGINE_SRC"

# tools/gn runs under vpython3 from depot_tools.
DEPOT_TOOLS="${DEPOT_TOOLS:-$SCRIPT_DIR/../depot_tools}"
[[ -d "$DEPOT_TOOLS" ]] && export PATH="$DEPOT_TOOLS:$PATH"

if [[ ! -f "out/$OUT_NAME/build.ninja" ]]; then
  ./flutter/tools/gn --wasm --runtime-mode "$RUNTIME_MODE" --no-rbe --no-goma --no-lto \
    --no-enable-unittests --gn-args "emsdk_dir=\"$SHIM\""
fi

NINJA="$ENGINE_SRC/flutter/third_party/ninja/ninja"
[[ -x "$NINJA" ]] || NINJA=ninja

# //flutter/wasm:impeller_sdk builds the toolkit in its own toolchain (see flutter/wasm/BUILD.gn),
# so outputs land under out/<config>/impeller_sdk/.
"$NINJA" -C "out/$OUT_NAME" flutter/wasm:impeller_sdk

rm -rf "$SDK_OUT"
mkdir -p "$SDK_OUT"
unzip -q -o "out/$OUT_NAME/zip_archives/wasm/impeller_sdk.zip" -d "$SDK_OUT"

# Merge the archive into one object and hide everything but the Impeller C API, so the bundled
# Skia/HarfBuzz/ICU don't collide with other archives (SkiaSharp, HarfBuzzSharp) in the final link.
"$SCRIPT_DIR/wasm-localize-archive.sh" "$SDK_OUT/lib/libimpeller.a" "$SDK_PACK/bin"

echo "Wrote $SDK_OUT/lib/libimpeller.a ($(du -h "$SDK_OUT/lib/libimpeller.a" | cut -f1))"
