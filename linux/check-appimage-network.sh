#!/usr/bin/env bash
set -euo pipefail
image=$(realpath -- "$1")
harness=$(realpath -- "$2")
temporary=$(mktemp -d)
trap 'rm -rf -- "$temporary"' EXIT
(cd "$temporary" && "$image" --appimage-extract > /dev/null)
export APPDIR="$temporary/squashfs-root"
for file in chat-smoke chat-smoke.dll chat-smoke.deps.json chat-smoke.runtimeconfig.json; do
    cp "$harness/$file" "$APPDIR/usr/lib/twitch-chat-window/"
done
source "$APPDIR/usr/share/twitch-chat-window/environment.sh"
"$APPDIR/usr/lib/twitch-chat-window/chat-smoke"
