#!/usr/bin/env bash
#
# Rewrites a wasm libimpeller.a in place so it exports only the Impeller C API: the members are
# merged into a single relocatable object and every other defined symbol is made local (see
# wasm-localize-symbols.py). Run by build-impeller-wasm.sh; can also be applied to an existing
# archive, and is idempotent.
#
# Usage: ./wasm-localize-archive.sh <libimpeller.a> [<llvm bin dir>]
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ARCHIVE="$1"
BIN="${2:-}"

if [[ -z "$BIN" ]]; then
  DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  case "$(uname -s)-$(uname -m)" in
    Darwin-arm64) RID=osx-arm64 ;;
    Darwin-x86_64) RID=osx-x64 ;;
    Linux-x86_64) RID=linux-x64 ;;
    Linux-aarch64) RID=linux-arm64 ;;
    *) echo "unsupported host $(uname -s)-$(uname -m)" >&2; exit 1 ;;
  esac
  PACK="$DOTNET_ROOT/packs/Microsoft.NET.Runtime.Emscripten.${EMSCRIPTEN_VERSION:-3.1.56}.Sdk.$RID"
  BIN="$PACK/$({ ls "$PACK" | grep -v -- '-' || ls "$PACK"; } | sort -V | tail -n 1)/tools/bin"
fi

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

if [[ "$("$BIN/llvm-ar" t "$ARCHIVE")" == "impeller.o" ]]; then
  # Already merged; re-linking a merged object trips llvm-ar's name-section validation.
  "$BIN/llvm-ar" p "$ARCHIVE" impeller.o > "$TMP/merged.o"
else
  "$BIN/wasm-ld" --relocatable --whole-archive "$ARCHIVE" --no-whole-archive -o "$TMP/merged.o"
fi
python3 "$SCRIPT_DIR/wasm-localize-symbols.py" "$TMP/merged.o" "$TMP/impeller.o"
"$BIN/llvm-ar" rcs "$TMP/libimpeller.a" "$TMP/impeller.o"
mv "$TMP/libimpeller.a" "$ARCHIVE"
