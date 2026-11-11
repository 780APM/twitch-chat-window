#!/usr/bin/env bash
set -euo pipefail

if [[ $# -gt 1 || ( $# -eq 1 && $1 != --pacman ) ]]; then
    echo "Usage: $0 [--pacman]" >&2
    exit 2
fi

root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
dotnet=${DOTNET:-dotnet}
if ! command -v "$dotnet" >/dev/null; then
    echo 'Install the .NET 10 SDK, or set DOTNET to its executable path.' >&2
    exit 1
fi
if [[ ${1:-} == --pacman ]] && ! command -v makepkg >/dev/null; then
    echo 'Install base-devel on CachyOS/Arch to build a pacman package.' >&2
    exit 1
fi
dotnet_root=$(dirname -- "$(readlink -f -- "$(command -v "$dotnet")")")
for notice in LICENSE.txt ThirdPartyNotices.txt; do
    if [[ ! -f "$dotnet_root/$notice" ]]; then
        echo "Missing .NET notice: $dotnet_root/$notice" >&2
        exit 1
    fi
done

publish="$root/out/TwitchChatWindow"
mkdir -p "$root/out"
rm -rf -- "$publish"
"$dotnet" publish "$root/linux/TwitchChatWindow.Linux.csproj" \
    -c Release -r linux-x64 --self-contained -p:DebugType=None -o "$publish"
cp "$root/README.md" "$publish/README.md"
mkdir -p "$publish/licenses"
cp "$dotnet_root/LICENSE.txt" "$publish/licenses/dotnet-license.txt"
cp "$dotnet_root/ThirdPartyNotices.txt" "$publish/licenses/dotnet-third-party-notices.txt"
cp "$root/linux/licenses/gtksharp-license.txt" "$publish/licenses/gtksharp-license.txt"
printf '%s\n' 'GtkSharp 3.24.24.95 source code' 'https://github.com/GtkSharp/GtkSharp' \
    > "$publish/licenses/gtksharp-source.txt"
tar -C "$root/out" -czf "$root/out/TwitchChatWindow-linux-x64.tar.gz" TwitchChatWindow
echo "Portable archive: $root/out/TwitchChatWindow-linux-x64.tar.gz"

if [[ ${1:-} == --pacman ]]; then
    staging=$(mktemp -d "$root/out/pacman-build.XXXXXX")
    trap 'rm -rf -- "$staging"' EXIT
    mkdir -p "$root/out/pacman"
    cp "$root/linux/arch/PKGBUILD" "$root/linux/arch/"*.desktop \
        "$root/branding/mark.png" "$root/out/TwitchChatWindow-linux-x64.tar.gz" "$staging/"
    (
        cd -- "$staging"
        makepkg --geninteg >> PKGBUILD
        # These dependencies are needed at runtime, not to assemble the package.
        PKGDEST="$root/out/pacman" makepkg --nodeps --noconfirm --force
    )
    echo "Pacman package: $root/out/pacman/"
fi
