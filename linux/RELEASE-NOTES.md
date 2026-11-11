Linux now uses a single chat window with a taskbar entry. Drag the **Twitch Chat** title to position it freely across your desktop and monitors, and drag its bottom-right grip to resize it. There is no separate control window and no corner-position restriction. **−** minimizes the chat window and **×** quits.

The window uses XWayland in KDE Plasma Wayland sessions, which provides normal desktop movement, taskbar integration, always-on-top and click-through in the same window. X11 sessions are also supported. Click **Click through** before playing; restore the app from the taskbar or launch it again to unlock. A Plasma shortcut to `twitch-chat-window` also unlocks it.

This release includes a local Linux packaging script and automated, tested release downloads.

### CachyOS / Arch

Download `twitch-chat-window-1.2.0-1-x86_64.pkg.tar.zst` below, then install it from your download folder:

```sh
sudo pacman -U ./twitch-chat-window-1.2.0-1-x86_64.pkg.tar.zst
```

Open **Twitch Chat Window** from the app menu. Pacman installs GTK 3, XWayland and the runtime dependencies. This direct-download package is unsigned; it is separate from the independently maintained signed pacman repository.

### Portable Linux

Install `gtk3` and `xorg-xwayland`, extract `TwitchChatWindow-linux-x64.tar.gz`, and run `TwitchChatWindow/twitch-chat-window`. The .NET runtime is included. XWayland is required in a Wayland session; a native Wayland-only environment without XWayland is not supported.

### Windows

Download and run `TwitchChatWindow.exe`. Windows 10 or 11 with .NET Framework 4.8 or later is required. Windows behavior is unchanged.

`SHA256SUMS` contains checksums for all downloads. Window stacking over fullscreen games depends on the compositor; use windowed mode if a game covers the overlay.
