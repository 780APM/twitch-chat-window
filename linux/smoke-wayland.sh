#!/usr/bin/env bash
set -euo pipefail

executable=$1
runtime=$(mktemp -d)
export XDG_RUNTIME_DIR=$runtime
export XDG_SESSION_TYPE=wayland
export WLR_BACKENDS=headless WLR_LIBINPUT_NO_DEVICES=1 WLR_RENDERER=pixman
export NO_AT_BRIDGE=1
app_id=io.github._780APM.TwitchChatWindow

cleanup() {
    result=$?
    if (( result != 0 )); then
        tail -60 "$runtime/sway.log" "$runtime/overlay.log" 2>/dev/null || true
    fi
    if [[ -n ${overlay_pid:-} ]]; then kill "$overlay_pid" 2>/dev/null || true; fi
    if [[ -n ${sway_pid:-} ]]; then kill "$sway_pid" 2>/dev/null || true; fi
    wait || true
    rm -rf -- "$runtime"
}
trap cleanup EXIT

printf 'output * resolution 1280x720\n' > "$runtime/sway.conf"
sway --config "$runtime/sway.conf" > "$runtime/sway.log" 2>&1 &
sway_pid=$!
for ((attempt=0; attempt<100; attempt++)); do
    sockets=("$runtime"/wayland-*)
    if [[ -S ${sockets[0]} ]]; then
        export WAYLAND_DISPLAY=${sockets[0]##*/}
        break
    fi
    kill -0 "$sway_pid"
    sleep .1
done
: "${WAYLAND_DISPLAY:?Sway did not start}"

WAYLAND_DEBUG=client "$executable" > "$runtime/overlay.log" 2>&1 &
overlay_pid=$!

wait_for_log() {
    local pattern=$1
    for ((attempt=0; attempt<100; attempt++)); do
        if grep -Eq "$pattern" "$runtime/overlay.log"; then return; fi
        kill -0 "$overlay_pid"
        sleep .1
    done
    echo "Missing Wayland event $pattern"
    return 1
}

wait_for_log 'get_layer_surface.*3, "twitch-chat-window"'
wait_for_log 'set_keyboard_interactivity\(2\)'
gapplication action "$app_id" toggle-click-through
wait_for_log 'set_keyboard_interactivity\(0\)'
wait_for_log 'set_input_region\(wl_region'

# Launch again through the normal entry point, as a desktop shortcut would.
locked_line=$(wc -l < "$runtime/overlay.log")
"$executable"
for ((attempt=0; attempt<100; attempt++)); do
    if tail -n "+$locked_line" "$runtime/overlay.log" | grep -Eq 'set_keyboard_interactivity\(2\)'; then
        echo 'Wayland overlay, click-through and launch-to-unlock passed.'
        exit 0
    fi
    kill -0 "$overlay_pid"
    sleep .1
done
echo 'The overlay did not unlock.'
exit 1
