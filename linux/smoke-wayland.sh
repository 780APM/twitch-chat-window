#!/usr/bin/env bash
set -euo pipefail

executable=$1
compositor=$2
runtime=$(mktemp -d)
export XDG_RUNTIME_DIR=$runtime
export XDG_SESSION_TYPE=wayland
export LIBGL_ALWAYS_SOFTWARE=1 NO_AT_BRIDGE=1
export QT_FORCE_STDERR_LOGGING=1
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
    local logfile=${3:-"$runtime/overlay.log"}
    for ((attempt=0; attempt<100; attempt++)); do
        if tail -n "+$start" "$logfile" | grep -E "$pattern" >/dev/null; then return; fi
        kill -0 "$overlay_pid"
        sleep .1
    done
    echo "Missing Wayland event $pattern"
    return 1
}

wait_for_log 'get_layer_surface.*3, "twitch-chat-window"'
wait_for_log 'set_keyboard_interactivity\(2\)'
wait_for_log 'wl_surface.*\.attach\(wl_buffer'
# A regular xdg-toplevel with the desktop entry's app ID gives Plasma a taskbar item.
wait_for_log 'get_toplevel\('
wait_for_log "set_app_id\(\"$app_id\"\)"
# Check KWin's taskbar flag as well as the client's Wayland surface type.
cat > "$runtime/check-taskbar.js" <<'SCRIPT'
function checkTaskbar() {
    for (const window of workspace.windowList()) {
        if (window.resourceClass === 'io.github._780APM.TwitchChatWindow' &&
            window.normalWindow && !window.skipTaskbar) {
            console.warn('Twitch Chat taskbar check passed');
        }
    }
}
workspace.windowAdded.connect(checkTaskbar);
checkTaskbar();
SCRIPT
script_id=$(gdbus call --session --dest org.kde.KWin --object-path /Scripting \
    --method org.kde.kwin.Scripting.loadScript "$runtime/check-taskbar.js" | tr -cd '0-9')
test -n "$script_id"
gdbus call --session --dest org.kde.KWin --object-path "/Scripting/Script$script_id" \
    --method org.kde.kwin.Script.run >/dev/null
wait_for_log 'Twitch Chat taskbar check passed' 1 "$runtime/kwin.log"
minimize_start=$(( $(wc -l < "$runtime/overlay.log") + 1 ))
gapplication action "$app_id" minimize-controls
wait_for_log 'set_minimized\(' "$minimize_start"
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
# Reactivation must reuse the taskbar window rather than creating another one.
test "$(grep -c 'get_toplevel(' "$runtime/overlay.log")" -eq 1
gapplication action "$app_id" quit
for ((attempt=0; attempt<100; attempt++)); do
    if ! kill -0 "$overlay_pid" 2>/dev/null; then break; fi
    sleep .1
done
if kill -0 "$overlay_pid" 2>/dev/null; then
    echo 'Quitting the control window left the overlay running.'
    exit 1
fi
wait "$overlay_pid"
unset overlay_pid
echo 'Wayland overlay, taskbar window, minimize, click-through, launch-to-unlock and quit passed.'
