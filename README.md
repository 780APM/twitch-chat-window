# Twitch Chat Window

![Twitch Chat Window — Live Twitch chat on one screen](branding/wordmark.png)

A small Twitch chat overlay with adjustable opacity, free positioning and click-through mode.

## Download

Open the **[latest release](https://github.com/780APM/twitch-chat-window/releases/latest)** and choose your operating system:

- **Windows:** `TwitchChatWindow.exe`
- **Linux:** `TwitchChatWindow-x86_64.AppImage`

GitHub also generates source-code archives automatically. You do not need those to use the app.

## Linux — KDE Plasma

1. Download **TwitchChatWindow-x86_64.AppImage**.
2. Right-click it, open **Properties → Permissions**, and allow it to run as an executable.
3. Open the AppImage. It saves a copy for your user account and adds **Twitch Chat Window** to your application menu/search. No sudo, repository setup or .NET installation is needed.
4. Enter a Twitch username and connect. Drag the title to place the window anywhere, including across monitors. Drag the bottom-right grip to resize it.
5. Choose **Click through** before returning to your game. Press **Ctrl+Shift+F10** to unlock the running overlay without restarting it or taking focus from your game.

The Linux AppImage includes .NET, GTK 3 and their supporting libraries. It targets x86-64 Linux desktops with glibc 2.35 or newer, including Ubuntu 22.04+ and current Arch/CachyOS. KDE Plasma provides the global unlock shortcut; a Wayland session also needs XWayland, which is normally already present on Plasma. The app can run in X11 sessions too. If a shortcut is already assigned elsewhere, change **Unlock overlay** under **Twitch Chat Window** in Plasma’s keyboard shortcut settings, or unlock through the taskbar.

The app has one window and one taskbar entry. **−** minimizes it and **×** quits. If a fullscreen game covers the overlay, use windowed mode.

The saved copy is `~/.local/share/twitch-chat-window/TwitchChatWindow.AppImage` (or under `XDG_DATA_HOME` when set). Launching a newer downloaded AppImage replaces that saved copy and updates the launcher. Close an older running version once after upgrading so the next launch loads the new version.

### Previous pacman installations

AppImage replaces our custom pacman distribution. If you ran an earlier repository setup, remove the `Include = /etc/pacman.d/twitch-chat-window.conf` line that setup added to `/etc/pacman.conf`, then remove that app-specific drop-in file. If you added a full `[twitch-chat-window]` section manually, remove only that section. Other repositories should remain unchanged. You can remove the older system package after the AppImage is working. Existing git tags preserve the old packaging source.

## Windows

Download **TwitchChatWindow.exe** and open it on Windows 10 or 11 with .NET Framework 4.8 or later.

Enter a Twitch username and connect. Drag the title to move the overlay, or its edges to resize it. Use **Opacity** to adjust visibility, choose **Click through** before playing, and press **Ctrl+Shift+F10** to unlock it from your game. Use borderless fullscreen or windowed mode if exclusive fullscreen hides the overlay.

The app reads public Twitch chat over a secure WebSocket connection. Timestamps show local time and the feed reconnects automatically if the connection drops.

## Build from source

On Windows, run `powershell -ExecutionPolicy Bypass -File .\Build.ps1`. The script uses the compiler included in Windows .NET Framework and creates `TwitchChatWindow.exe`.

On Linux, install the .NET 10 SDK and run `./linux/build.sh` to publish into `out/TwitchChatWindow`. Run `./linux/build-appimage.sh` on Ubuntu 22.04 to produce `out/TwitchChatWindow-x86_64.AppImage`; the build job in `.github/workflows/build.yml` lists its development dependencies. Set `DOTNET=/path/to/dotnet` if the SDK is outside your PATH. Packaging tools are checked against pinned SHA-256 hashes.

## Publishing

Update the version in `linux/TwitchChatWindow.Linux.csproj` and `linux/RELEASE-NOTES.md`, then push a matching version tag. GitHub Actions builds the AppImage on Ubuntu 22.04, tests the real AppImage on Arch KDE Wayland, builds Windows, and publishes exactly two application files after the checks pass. Checksums appear in the release notes rather than as another download.

The [brand assets](branding/README.md) include the logo, wordmark and Windows icon.
