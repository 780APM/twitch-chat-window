Press **Ctrl+Shift+F10** to unlock the running Linux overlay without restarting or relaunching the app. KDE registers the shortcut automatically, including when a native Wayland game has focus. Unlocking does not take focus from the game. Customize **Unlock overlay** under **Twitch Chat Window** in Plasma’s **Keyboard → Shortcuts** settings; existing assignments are preserved.

The chat overlay remains the app’s only window, with one taskbar entry. Drag its title freely across monitors, use the bottom-right grip to resize, **−** to minimize and **×** to quit.

### CachyOS / Arch with KDE Plasma

Set up the signed pacman repository once:

```sh
curl -fL https://github.com/780APM/twitch-chat-window/releases/latest/download/install-cachyos.sh -o install-twitch-chat-window.sh
bash install-twitch-chat-window.sh
```

The setup checks the public key fingerprint, adds the repository, and runs `sudo pacman -Syu twitch-chat-window`. Open **Twitch Chat Window** from your app menu afterward. Future updates arrive through regular `sudo pacman -Syu` upgrades.

New signing-key fingerprint: `CDD0942F17CAD1625B4E67F89711FF2636622D60`.

The old separately hosted package repository remains unchanged. Users of it must remove the old `[twitch-chat-window]` section before running the new setup script. This is an explicit key and server migration because the old private signing key is unavailable.

### Portable Linux

Install `gtk3`, `xorg-xwayland` and `kglobalacceld`, extract `TwitchChatWindow-linux-x64.tar.gz`, and run `TwitchChatWindow/twitch-chat-window`. The .NET runtime is included. Linux targets KDE Plasma and uses XWayland for the single movable window; X11 sessions are also supported. If KDE shortcuts are unavailable or the default key is assigned elsewhere, unlock through the taskbar and configure a shortcut in KDE settings.

### Windows

Download and run `TwitchChatWindow.exe` on Windows 10 or 11 with .NET Framework 4.8 or later. Windows behavior is unchanged.

The native pacman package, repository database and file index have detached signatures. `SHA256SUMS` covers the downloads. Actual in-game stacking depends on compositor policy; use windowed mode if a fullscreen game covers the overlay.
