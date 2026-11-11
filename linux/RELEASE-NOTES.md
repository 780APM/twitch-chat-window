Linux now includes a regular control window that appears in the taskbar alongside the always-on-top chat overlay. Minimize the control window while you play, restore it to unlock the overlay, or use its click-through control. Closing either window quits the app.

This release also adds a local Linux packaging script and a clear installation hint when `gtk-layer-shell` is missing.

### CachyOS / Arch with KDE Plasma Wayland

Download `twitch-chat-window-1.2.0-1-x86_64.pkg.tar.zst` below, then install it from your download folder:

```sh
sudo pacman -U ./twitch-chat-window-1.2.0-1-x86_64.pkg.tar.zst
```

Open **Twitch Chat Window** from the app menu. Pacman installs the required runtime dependencies. This direct-download package is unsigned; it is separate from the older, independently maintained signed pacman repository.

### Portable Linux

Install `gtk-layer-shell`, extract `TwitchChatWindow-linux-x64.tar.gz`, and run `TwitchChatWindow/twitch-chat-window`. The .NET runtime is included. Linux requires a Wayland compositor with Layer Shell, such as KDE Plasma; this release does not add GNOME or X11 support.

### Windows

Download and run `TwitchChatWindow.exe`. Windows 10 or 11 with .NET Framework 4.8 or later is required. The taskbar change applies to Linux; Windows behavior is unchanged.

`SHA256SUMS` contains checksums for all downloads. Use borderless fullscreen or windowed mode for games.
