#!/usr/bin/env bash
set -euo pipefail
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
tools_dir="$root/out/appimage-tools"
appdir="$root/out/TwitchChatWindow.AppDir"
mkdir -p "$tools_dir"
fetch() {
    local url=$1 destination=$2 digest=$3
    if [[ ! -f "$destination" ]] || ! printf '%s  %s\n' "$digest" "$destination" | sha256sum -c --status; then
        curl -fLsS --retry 3 "$url" -o "$destination"
    fi
    printf '%s  %s\n' "$digest" "$destination" | sha256sum -c
    chmod +x "$destination"
}
fetch https://github.com/linuxdeploy/linuxdeploy/releases/download/continuous/linuxdeploy-x86_64.AppImage \
    "$tools_dir/linuxdeploy.AppImage" df908b19f1ed1cdafa79508d415aa12d6c56ce4d56d4149aa8c09286718706e5
fetch https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage \
    "$tools_dir/appimagetool.AppImage" e8c1169c4af66c3184d7ee110713d4be04befe671fa358e2c1591ee840553b03
fetch https://raw.githubusercontent.com/linuxdeploy/linuxdeploy-plugin-gtk/7a3fbc31a9e5075073ff8790f26effbac5f84453/linuxdeploy-plugin-gtk.sh \
    "$tools_dir/linuxdeploy-plugin-gtk.sh" b0f4cbc684a0103a9651f0955b635eaea0096b3a66c0f5a2c2aa337960375171
fetch https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-x86_64 \
    "$tools_dir/runtime-x86_64" 617aa77314d57c8b8e4fae9c3f0b35af7b30d5529cb69e98f64f92510ed98a46
"$root/linux/build.sh"
rm -rf -- "$appdir"
mkdir -p "$appdir/usr/lib/twitch-chat-window" "$appdir/usr/bin" \
    "$appdir/usr/share/twitch-chat-window" "$appdir/usr/lib/gio/modules"
cp -a "$root/out/TwitchChatWindow/." "$appdir/usr/lib/twitch-chat-window/"
# This optional .NET tracing plug-in requires an old LTTng ABI and is not used
# by the application. Keep it out of the portable runtime dependency closure.
rm -f -- "$appdir/usr/lib/twitch-chat-window/libcoreclrtraceptprovider.so"
cat > "$appdir/usr/bin/twitch-chat-window" <<'LAUNCH'
#!/usr/bin/env bash
exec "$(dirname -- "$(readlink -f -- "$0")")/../lib/twitch-chat-window/twitch-chat-window" "$@"
LAUNCH
chmod +x "$appdir/usr/bin/twitch-chat-window"
icu_major=$(pkg-config --modversion icu-uc | cut -d. -f1)
[[ "$icu_major" =~ ^[0-9]+$ ]]
printf 'APPIMAGE_ICU_MAJOR=%s\n' "$icu_major" > "$appdir/usr/share/twitch-chat-window/runtime.env"
cp "$root/linux/appimage/environment.sh" "$appdir/usr/share/twitch-chat-window/"
libs=(--library "$(pkg-config --variable=libdir gtk+-3.0)/libgtk-3.so.0")
for name in icui18n icuuc icudata; do
    # .NET loads the versioned name dynamically. Deploying the unversioned
    # development symlink alone omits libicui18n.so.N on other distributions.
    libs+=(--library "$(pkg-config --variable=libdir icu-uc)/lib$name.so.$icu_major")
done
for name in ssl crypto; do
    libs+=(--library "$(pkg-config --variable=libdir openssl)/lib$name.so.3")
done
export APPIMAGE_EXTRACT_AND_RUN=1 DEPLOY_GTK_VERSION=3 NO_STRIP=1
export PATH="$tools_dir:$PATH"
"$tools_dir/linuxdeploy.AppImage" --appdir "$appdir" \
    --deploy-deps-only "$appdir/usr/lib/twitch-chat-window" "${libs[@]}" \
    --desktop-file "$root/linux/appimage/io.github._780APM.TwitchChatWindow.desktop" \
    --icon-file "$root/branding/mark.png" --icon-filename twitch-chat-window \
    --custom-apprun "$root/linux/appimage/AppRun" --plugin gtk
# Bundle copyright notices and identify the distribution sources for native libs.
mkdir -p "$appdir/usr/share/doc/bundled-libraries"
if command -v dpkg-query >/dev/null; then
    find "$appdir/usr/lib" -type f -name '*.so*' -printf '%f\n' | sort -u > "$root/out/bundled-library-names.txt"
    : > "$root/out/bundled-packages.txt"
    while IFS= read -r name; do
        package=$(dpkg-query -S "*/$name" 2>/dev/null | sed -n '1s/: \/.*//p' || true)
        [[ -z "$package" ]] || printf '%s\n' "$package" >> "$root/out/bundled-packages.txt"
    done < "$root/out/bundled-library-names.txt"
    sort -u "$root/out/bundled-packages.txt" -o "$root/out/bundled-packages.txt"
    : > "$appdir/usr/share/doc/bundled-libraries/build-packages.tsv"
    while IFS= read -r package; do
        dpkg-query -W -f='${binary:Package}\t${Version}\t${source:Package}\t${source:Version}\n' "$package" \
            >> "$appdir/usr/share/doc/bundled-libraries/build-packages.tsv"
        name=${package%%:*}
        if [[ -f "/usr/share/doc/$name/copyright" ]]; then
            mkdir -p "$appdir/usr/share/doc/bundled-libraries/$name"
            cp -L "/usr/share/doc/$name/copyright" "$appdir/usr/share/doc/bundled-libraries/$name/"
        fi
    done < "$root/out/bundled-packages.txt"
fi
cp "$root/linux/appimage/NATIVE-SOURCES.md" "$appdir/usr/share/doc/bundled-libraries/"
cp "$root/linux/appimage/"*-LICENSE.txt "$appdir/usr/share/doc/bundled-libraries/"
chmod -R a+rX "$appdir"
ARCH=x86_64 "$tools_dir/appimagetool.AppImage" --no-appstream \
    --runtime-file "$tools_dir/runtime-x86_64" "$appdir" "$root/out/TwitchChatWindow-x86_64.AppImage"
echo "AppImage: $root/out/TwitchChatWindow-x86_64.AppImage"
