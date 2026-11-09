# Twitch Chat Window

![Twitch Chat Window — Live Twitch chat on one screen](branding/wordmark.png)

A small, always-on-top chat overlay for Windows. It reads live public Twitch chat using only the channel’s username.

1. Download **TwitchChatWindow.exe** from the [latest GitHub release](https://github.com/780APM/twitch-chat-window/releases/latest) and open it.
2. Enter the streamer’s Twitch username and press **Enter** or choose **Connect**.
3. Drag the title to move the overlay, or drag an edge to resize it. Use **Opacity** to adjust visibility.
4. Choose **Click through** before returning to the game. Press **Ctrl+Shift+F10** from the game to unlock the overlay and make it interactive again.

Use **Borderless Fullscreen** or **Windowed** in your game’s display settings. A game using true exclusive fullscreen can hide the Windows overlay.

The EXE runs on Windows 10 or 11 with .NET Framework 4.8 or later. It uses Twitch’s secure IRC endpoint for public chat. Timestamps show local time, based on Twitch’s server timestamp when available. The feed starts with new messages and reconnects automatically if the connection drops.

## Build from source

Run `powershell -ExecutionPolicy Bypass -File .\Build.ps1` in the project folder. The script uses the compiler included in Windows .NET Framework and creates `TwitchChatWindow.exe` in the same folder.

The [brand assets](branding/README.md) include the logo, wordmark, and Windows icon.
