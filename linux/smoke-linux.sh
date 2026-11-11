#!/usr/bin/env bash
set -euo pipefail

executable=$(realpath -- "$1")
compositor=$(realpath -- "$2")
runtime=$(mktemp -d)
export SMOKE_RUNTIME=$runtime SMOKE_EXECUTABLE=$executable
checker_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
export SMOKE_POINTER="$runtime/drag-window"
export SMOKE_GAME="$runtime/focus-target"
export SMOKE_CHECKER="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/check-window.py"
export XDG_RUNTIME_DIR=$runtime XDG_SESSION_TYPE=wayland WAYLAND_DISPLAY=wayland-overlay-test
export LIBGL_ALWAYS_SOFTWARE=1 NO_AT_BRIDGE=1 QT_FORCE_STDERR_LOGGING=1
export GSETTINGS_BACKEND=memory GTK_USE_PORTAL=0
export XDG_CONFIG_HOME="$runtime/config" XDG_CACHE_HOME="$runtime/cache" XDG_DATA_HOME="$runtime/data"
mkdir -p "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME" "$XDG_DATA_HOME"
dbus-update-activation-environment WAYLAND_DISPLAY XDG_RUNTIME_DIR XDG_SESSION_TYPE \
    XDG_CONFIG_HOME XDG_CACHE_HOME XDG_DATA_HOME QT_QPA_PLATFORM=wayland

cleanup() {
    result=$?
    if (( result != 0 )); then
        tail -n 80 "$runtime/kwin.log" "$runtime/overlay.log" || true
    fi
    if [[ -n ${kwin_pid:-} ]]; then kill "$kwin_pid" 2>/dev/null || true; fi
    wait || true
    rm -rf -- "$runtime"
}
trap cleanup EXIT
wayland-scanner client-header "$checker_dir/tests/fake-input.xml" "$runtime/fake-input-client.h"
wayland-scanner private-code "$checker_dir/tests/fake-input.xml" "$runtime/fake-input.c"
cc -Wall -Wextra -Werror -I "$runtime" "$checker_dir/tests/drag-window.c" "$runtime/fake-input.c" \
    $(pkg-config --cflags --libs wayland-client) -o "$SMOKE_POINTER"
cc -Wall -Wextra -Werror "$checker_dir/tests/focus-target.c" \
    $(pkg-config --cflags --libs gtk+-3.0) -o "$SMOKE_GAME"
cat > "$runtime/session" <<'SESSION'
#!/usr/bin/env bash
python3 "$SMOKE_CHECKER" > "$SMOKE_RUNTIME/result.log" 2>&1
result=$?
cat "$SMOKE_RUNTIME/result.log"
if (( result == 0 )); then touch "$SMOKE_RUNTIME/passed"; fi
exit "$result"
SESSION
chmod +x "$runtime/session"
"$compositor" --virtual --width 1280 --height 720 --output-count 2 --socket "$WAYLAND_DISPLAY" \
    --xwayland --no-lockscreen --exit-with-session "$runtime/session" \
    > "$runtime/kwin.log" 2>&1 &
kwin_pid=$!
# Bound the test even if the compositor or session fails to exit.
for ((attempt=0; attempt<600; attempt++)); do
    if ! kill -0 "$kwin_pid" 2>/dev/null; then break; fi
    if [[ -f "$runtime/passed" ]]; then break; fi
    sleep .1
done
cat "$runtime/result.log" 2>/dev/null || true
test -f "$runtime/passed"
