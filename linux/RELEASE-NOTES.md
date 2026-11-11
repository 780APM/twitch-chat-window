### Windows

Download **TwitchChatWindow.exe** and open it. Requires Windows 10/11 with .NET Framework 4.8 or later.

### Linux

Download **TwitchChatWindow-x86_64.AppImage**, allow it to run as an executable in **Properties → Permissions**, and open it. It includes .NET and GTK, saves a copy for your user account, and adds **Twitch Chat Window** to your application-menu search. No sudo or repository setup is needed.

Requires **KDE Plasma 6 or newer** on x86-64 Linux with glibc 2.35 or newer, including current CachyOS/Arch. Wayland sessions also need XWayland.

**Fixed: the overlay now stays above fullscreen Wayland and X11 games.** First launch automatically adds a KDE window rule scoped to this app, preserving its normal taskbar entry and free positioning. Existing window rules are preserved and backed up before the change.

### Controls

- Drag the title to move the single chat window anywhere across your monitors.
- Drag its bottom-right grip to resize it.
- Choose **Click through**, then press **Ctrl+Shift+F10** to unlock without restarting the app or taking focus from the game.

The custom pacman repository is retired. Previous users should remove its app-specific repository entry as described in the [migration instructions](https://github.com/780APM/twitch-chat-window#previous-pacman-installations).

GitHub’s two **Source code** archives are automatic; you do not need them to run the app.
