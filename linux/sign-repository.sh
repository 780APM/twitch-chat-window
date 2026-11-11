#!/usr/bin/env bash
set -euo pipefail

if [[ $# != 2 ]]; then
    echo "Usage: $0 package.pkg.tar.zst output-directory" >&2
    exit 2
fi
package=$(realpath -- "$1")
output=$(realpath -m -- "$2")
fingerprint=${PACMAN_SIGNING_FINGERPRINT:?Set PACMAN_SIGNING_FINGERPRINT}
passphrase=${PACMAN_SIGNING_PASSPHRASE_FILE:?Set PACMAN_SIGNING_PASSPHRASE_FILE}
[[ "$fingerprint" =~ ^[0-9A-F]{40}$ ]]
[[ $(basename -- "$package") == twitch-chat-window-*-x86_64.pkg.tar.zst ]]
mkdir -p "$output"
cp -- "$package" "$output/"
package="$output/$(basename -- "$package")"
sign() {
    gpg --batch --yes --pinentry-mode loopback --passphrase-file "$passphrase" \
        --local-user "$fingerprint" --detach-sign --output "$1.sig" "$1"
}
sign "$package"
repo-add "$output/twitch-chat-window.db.tar.gz" "$package"
sign "$output/twitch-chat-window.db.tar.gz"
sign "$output/twitch-chat-window.files.tar.gz"
rm -f -- "$output/twitch-chat-window.db.sig" "$output/twitch-chat-window.files.sig"
cp -- "$output/twitch-chat-window.db.tar.gz.sig" "$output/twitch-chat-window.db.sig"
cp -- "$output/twitch-chat-window.files.tar.gz.sig" "$output/twitch-chat-window.files.sig"
gpg --batch --armor --export "$fingerprint" > "$output/twitch-chat-window.asc"
for file in "$package" "$output/twitch-chat-window.db" "$output/twitch-chat-window.files"; do
    gpg --batch --verify "$file.sig" "$file"
done
echo "Signed pacman repository prepared in $output"
