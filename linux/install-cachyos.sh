#!/usr/bin/env bash
set -euo pipefail

pacman_options=()
if [[ ${1:-} == --non-interactive && $# == 1 ]]; then
    pacman_options=(--noconfirm)
elif (( $# != 0 )); then
    echo "Usage: $0 [--non-interactive]" >&2
    exit 2
fi

# This public fingerprint pins the repository's signing key. Private key material
# is never distributed with the installer or application.
fingerprint=CDD0942F17CAD1625B4E67F89711FF2636622D60
server=https://github.com/780APM/twitch-chat-window/releases/latest/download
repository=/etc/pacman.d/twitch-chat-window.conf
command -v pacman >/dev/null || { echo 'This installer needs CachyOS or Arch Linux.' >&2; exit 1; }
for tool in curl gpg; do
    command -v "$tool" >/dev/null || { echo "Install $tool before continuing." >&2; exit 1; }
done
# Keep an existing repository's trust and URL intact. Migrating from the old
# package repository is explicit, rather than silently changing an existing one.
if [[ -f "$repository" ]]; then
    if ! grep -Fxq "Server = $server" "$repository" ||
       ! grep -Fxq 'SigLevel = PackageRequired DatabaseRequired TrustedOnly' "$repository"; then
        echo "A different $repository already exists. See the README migration instructions." >&2
        exit 1
    fi
elif grep -Eq '^[[:space:]]*\[twitch-chat-window\]' /etc/pacman.conf; then
    echo 'The old twitch-chat-window repository is already configured. See the README migration instructions.' >&2
    exit 1
fi
runtime=$(mktemp -d)
trap 'rm -rf -- "$runtime"' EXIT
mkdir -m700 "$runtime/gnupg"
curl -fLsS "$server/twitch-chat-window.asc" -o "$runtime/key.asc"
actual=$(GNUPGHOME="$runtime/gnupg" gpg --batch --show-keys --with-colons "$runtime/key.asc" |
    awk -F: '$1 == "pub" { primary = 1; next } primary && $1 == "fpr" { print $10; primary = 0 }')
if [[ "$actual" != "$fingerprint" ]]; then
    echo 'The downloaded package key does not match the pinned fingerprint. Nothing was installed.' >&2
    exit 1
fi
printf 'Twitch Chat Window signing key: %s\n' "$fingerprint"
printf '[twitch-chat-window]\nSigLevel = PackageRequired DatabaseRequired TrustedOnly\nServer = %s\n' \
    "$server" > "$runtime/repository.conf"
privilege=()
if (( EUID != 0 )); then
    command -v sudo >/dev/null || { echo 'Run this installer as root, or install sudo.' >&2; exit 1; }
    privilege=(sudo)
fi
"${privilege[@]}" pacman-key --add "$runtime/key.asc"
"${privilege[@]}" pacman-key --lsign-key "$fingerprint"
"${privilege[@]}" install -m644 "$runtime/repository.conf" "$repository"
if ! grep -Eq '^[[:space:]]*Include[[:space:]]*=[[:space:]]*/etc/pacman.d/twitch-chat-window\.conf[[:space:]]*$' /etc/pacman.conf; then
    printf '\nInclude = %s\n' "$repository" | "${privilege[@]}" tee -a /etc/pacman.conf >/dev/null
fi
"${privilege[@]}" pacman -Syu "${pacman_options[@]}" twitch-chat-window
# Migrate the user-local launcher supplied before pacman installation existed.
# Keep a backup and leave the old runtime intact; the system desktop entry then
# launches the current package rather than silently opening the older copy.
desktop="${XDG_DATA_HOME:-$HOME/.local/share}/applications/io.github._780APM.TwitchChatWindow.desktop"
portable="$HOME/.local/share/twitch-chat-window"
if (( EUID != 0 )) && [[ -f "$desktop" ]] &&
   grep -Fxq "Exec=$portable/twitch-chat-window" "$desktop" &&
   grep -Fxq "TryExec=$portable/twitch-chat-window" "$desktop" &&
   grep -Fxq "Icon=$portable/mark.png" "$desktop"; then
    backup=$(mktemp "${desktop}.before-pacman.XXXXXX")
    mv -- "$desktop" "$backup"
    printf 'Backed up the older local launcher to %s. The app menu now uses the pacman package.\n' "$backup"
fi
printf '\nOpen Twitch Chat Window from your application menu. Ctrl+Shift+F10 unlocks the overlay.\n'
