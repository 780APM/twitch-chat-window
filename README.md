# Twitch Chat Window

![Twitch Chat Window — Live Twitch chat on one screen](branding/wordmark.png)

A small, always-on-top overlay that reads live public Twitch chat using only the channel’s username.

## CachyOS with KDE Plasma Wayland

1. Install the native overlay library with `sudo pacman -S --needed gtk-layer-shell`.
2. Extract **TwitchChatWindow-linux-x64.tar.gz** from the [latest GitHub release](https://github.com/780APM/twitch-chat-window/releases/latest). Open **twitch-chat-window** inside the extracted folder.
3. Enter a Twitch username. Use **Opacity** and **Position** to adjust the overlay before starting your game.
4. In Plasma **System Settings → Keyboard → Shortcuts**, add a command shortcut pointing to the full path of **twitch-chat-window**. Set it to **Ctrl+Shift+F10** or another free key combination. After you select **Click through**, use that shortcut to unlock the overlay in-game. Opening the executable again also unlocks it.

The Linux build targets 64-bit KDE Plasma Wayland. The **Position** controls use a screen corner and pixel offsets. Use the game’s **Borderless Fullscreen** or **Windowed** mode.

## Windows

1. Download **TwitchChatWindow.exe** from the [latest GitHub release](https://github.com/780APM/twitch-chat-window/releases/latest) and open it.
2. Enter the streamer’s Twitch username and press **Enter** or choose **Connect**.
3. Drag the title to move the overlay, or drag an edge to resize it. Use **Opacity** to adjust visibility.
4. Choose **Click through** before returning to the game. Press **Ctrl+Shift+F10** from the game to unlock the overlay and make it interactive again.

Use **Borderless Fullscreen** or **Windowed** in your game’s display settings. A game using true exclusive fullscreen can hide the Windows overlay.

The EXE runs on Windows 10 or 11 with .NET Framework 4.8 or later. It uses Twitch’s secure IRC endpoint for public chat. Timestamps show local time, based on Twitch’s server timestamp when available. The feed starts with new messages and reconnects automatically if the connection drops.

## Build from source

On Windows, run `powershell -ExecutionPolicy Bypass -File .\Build.ps1` in the project folder. The script uses the compiler included in Windows .NET Framework and creates `TwitchChatWindow.exe` in the same folder.

On Linux, use the .NET 10 SDK and run `dotnet publish linux/TwitchChatWindow.Linux.csproj -c Release -r linux-x64 --self-contained -o out/linux`. Launch `out/linux/twitch-chat-window`.

The [brand assets](branding/README.md) include the logo, wordmark, and Windows icon.
