#!/usr/bin/env bash
#
# Usage:
#   ./test-headless.sh                 # one run
#   ./test-headless.sh -n 5            # run the suite 5 times (catch nondeterministic failures)
#   ./test-headless.sh --filter Golden # pass extra args straight through to `dotnet test`
#   RUNS=5 ./test-headless.sh          # same as -n 5
#
# UPDATE_GOLDENS=1 ./test-headless.sh  # regenerate baselines (honored by the tests)

set -euo pipefail

cd "$(dirname "$0")"

RUNS="${RUNS:-1}"

# Peel a leading -n/--runs off the arg list; everything else is forwarded to `dotnet test`.
if [[ "${1:-}" == "-n" || "${1:-}" == "--runs" ]]; then
    RUNS="${2:?missing count after $1}"
    shift 2
fi

if ! command -v xvfb-run >/dev/null 2>&1; then
    echo "error: xvfb-run not found. Install it (Arch: xorg-server-xvfb, Debian/Ubuntu: xvfb)." >&2
    exit 1
fi

# Force the software renderer and the EGL path (a GLES context on X11/Xvfb must come through EGL).
export LIBGL_ALWAYS_SOFTWARE=1
export GALLIUM_DRIVER=llvmpipe
export MESA_LOADER_DRIVER_OVERRIDE=llvmpipe
export SDL_VIDEO_X11_FORCE_EGL=1
export __EGL_VENDOR_LIBRARY_FILENAMES=/usr/share/glvnd/egl_vendor.d/50_mesa.json
# Make RenderGate treat missing/non-software GL as a hard failure instead of a skip, like CI.
export CI=true

status=0
for ((i = 1; i <= RUNS; i++)); do
    if [[ "$RUNS" -gt 1 ]]; then
        echo "===== run $i / $RUNS ====="
    fi
    if ! xvfb-run -a dotnet test tests/NImpeller.Tests/NImpeller.Tests.csproj -c Release "$@"; then
        status=1
        echo "----- run $i FAILED -----" >&2
    fi
done

exit "$status"
