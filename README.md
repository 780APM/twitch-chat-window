# Twitch Chat Window

![Twitch Chat Window — Live Twitch chat on one screen](branding/wordmark.png)

A small, always-on-top overlay that reads live public Twitch chat using only the channel’s username.

## CachyOS with KDE Plasma Wayland

Download the native `twitch-chat-window-1.2.0-1-x86_64.pkg.tar.zst` package from the [latest GitHub release](https://github.com/780APM/twitch-chat-window/releases/latest). From the download folder, install it and its dependencies with:

```sh
sudo pacman -U ./twitch-chat-window-1.2.0-1-x86_64.pkg.tar.zst
```

Open **Twitch Chat Window** from the app menu. The chat overlay is the app’s only window, with its own taskbar entry. Drag the **Twitch Chat** title to place it anywhere on your desktop or across monitors, and drag the bottom-right grip to resize it. The **−** button minimizes the chat window; **×** quits.

The separately maintained [signed pacman repository](https://github.com/780APM/twitch-chat-window-packages#install-on-cachyos) supports installation by package name with `sudo pacman -Syu twitch-chat-window`. Its releases may lag the direct downloads above.

Enter a Twitch username and use **Opacity** to adjust visibility before starting your game.

In Plasma **System Settings → Keyboard → Shortcuts**, assign **Ctrl+Shift+F10** or another free shortcut to the command `twitch-chat-window`. After you select **Click through**, use that shortcut to unlock the overlay in-game. Opening the app again also unlocks it.

For a portable install, download **TwitchChatWindow-linux-x64.tar.gz** from the [latest GitHub release](https://github.com/780APM/twitch-chat-window/releases/latest), install `gtk3` and `xorg-xwayland`, then run **twitch-chat-window** from the extracted folder.

The Linux build targets 64-bit KDE Plasma and uses XWayland in a Wayland session to support free movement, a taskbar entry, always-on-top and click-through in a single window. It can also run in an X11 session. Window stacking over games depends on the compositor; use **Windowed** mode if a fullscreen game covers the overlay.

## Windows

1. Download **TwitchChatWindow.exe** from the [latest GitHub release](https://github.com/780APM/twitch-chat-window/releases/latest) and open it.
2. Enter the streamer’s Twitch username and press **Enter** or choose **Connect**.
3. Drag the title to move the overlay, or drag an edge to resize it. Use **Opacity** to adjust visibility.
4. Choose **Click through** before returning to the game. Press **Ctrl+Shift+F10** from the game to unlock the overlay and make it interactive again.

Use **Borderless Fullscreen** or **Windowed** in your game’s display settings. A game using true exclusive fullscreen can hide the Windows overlay.

The EXE runs on Windows 10 or 11 with .NET Framework 4.8 or later. It uses Twitch’s secure IRC endpoint for public chat. Timestamps show local time, based on Twitch’s server timestamp when available. The feed starts with new messages and reconnects automatically if the connection drops.

## Build from source

On Windows, run `powershell -ExecutionPolicy Bypass -File .\Build.ps1` in the project folder. The script uses the compiler included in Windows .NET Framework and creates `TwitchChatWindow.exe` in the same folder.

On Linux, install the .NET 10 SDK, `gtk3` and `xorg-xwayland`, then run `./linux/build.sh`. Launch `out/TwitchChatWindow/twitch-chat-window`. The script also creates `out/TwitchChatWindow-linux-x64.tar.gz`, including the runtime and third-party notices. If the SDK is installed outside your PATH, set `DOTNET=/path/to/dotnet` when running the script.

On CachyOS/Arch, install `base-devel` and run `./linux/build.sh --pacman` to also create a native package in `out/pacman/`. Install it with `sudo pacman -U out/pacman/twitch-chat-window-*.pkg.tar.zst`. This installs the desktop entry, icon and application files; pacman resolves the runtime dependencies. Building the package itself does not require those runtime dependencies to be installed.

The [brand assets](branding/README.md) include the logo, wordmark, and Windows icon.

## Publishing downloads

Update the version in `linux/TwitchChatWindow.Linux.csproj` and `linux/arch/PKGBUILD`, then update `linux/RELEASE-NOTES.md`. Push a matching version tag such as `v1.2.0`. GitHub Actions builds Windows and Linux, tests the installed native package under KWin, and publishes the portable archive, native CachyOS package, Windows executable and SHA-256 checksums as a GitHub release after both builds pass.

Direct-download packages do not update the separate signed pacman repository automatically. Publishing there requires its original package-signing private key; do not replace that key or weaken signature verification to publish an update.
