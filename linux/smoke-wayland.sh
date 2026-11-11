#!/usr/bin/env bash
set -euo pipefail

executable=$1
compositor=$2
runtime=$(mktemp -d)
export XDG_RUNTIME_DIR=$runtime
export XDG_SESSION_TYPE=wayland
export LIBGL_ALWAYS_SOFTWARE=1 NO_AT_BRIDGE=1
app_id=io.github._780APM.TwitchChatWindow

cleanup() {
    result=$?
    if (( result != 0 )); then
        tail -n 80 "$runtime/kwin.log" "$runtime/overlay.log" || true
    fi
    if [[ -n ${overlay_pid:-} ]]; then kill "$overlay_pid" 2>/dev/null || true; fi
    if [[ -n ${kwin_pid:-} ]]; then kill "$kwin_pid" 2>/dev/null || true; fi
    wait || true
    rm -rf -- "$runtime"
}
trap cleanup EXIT

export WAYLAND_DISPLAY=wayland-overlay-test
"$compositor" --virtual --width 1280 --height 720 --socket "$WAYLAND_DISPLAY" \
    --no-lockscreen --no-global-shortcuts > "$runtime/kwin.log" 2>&1 &
kwin_pid=$!
for ((attempt=0; attempt<100; attempt++)); do
    if [[ -S "$runtime/$WAYLAND_DISPLAY" ]]; then break; fi
    kill -0 "$kwin_pid"
    sleep .1
done
test -S "$runtime/$WAYLAND_DISPLAY"

WAYLAND_DEBUG=client "$executable" > "$runtime/overlay.log" 2>&1 &
overlay_pid=$!

wait_for_log() {
    local pattern=$1
    local start=${2:-1}
    for ((attempt=0; attempt<100; attempt++)); do
        if tail -n "+$start" "$runtime/overlay.log" | grep -E "$pattern" >/dev/null; then return; fi
        kill -0 "$overlay_pid"
        sleep .1
    done
    echo "Missing Wayland event $pattern"
    return 1
}

wait_for_log 'get_layer_surface.*3, "twitch-chat-window"'
wait_for_log 'set_keyboard_interactivity\(2\)'
wait_for_log 'wl_surface.*\.attach\(wl_buffer'
lock_start=$(( $(wc -l < "$runtime/overlay.log") + 1 ))
gapplication action "$app_id" toggle-click-through
wait_for_log 'set_keyboard_interactivity\(0\)' "$lock_start"
wait_for_log 'set_input_region\(wl_region' "$lock_start"
wait_for_log 'wl_surface.*\.commit\(' "$lock_start"

# Launch again through the normal entry point, as a desktop shortcut would.
unlock_start=$(( $(wc -l < "$runtime/overlay.log") + 1 ))
"$executable"
wait_for_log 'set_keyboard_interactivity\(2\)' "$unlock_start"
wait_for_log 'set_input_region\(nil\)' "$unlock_start"
wait_for_log 'wl_surface.*\.commit\(' "$unlock_start"
echo 'Wayland overlay, click-through and launch-to-unlock passed.'
