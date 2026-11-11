# Twitch Chat Window

![Twitch Chat Window — Live Twitch chat on one screen](branding/wordmark.png)

A small, always-on-top overlay that reads live public Twitch chat using only the channel’s username.

## CachyOS with KDE Plasma Wayland

Set up the signed pacman repository once:

```sh
curl -fL https://github.com/780APM/twitch-chat-window/releases/latest/download/install-cachyos.sh -o install-twitch-chat-window.sh
bash install-twitch-chat-window.sh
```

The script checks the repository key’s fingerprint, adds the key and repository, and runs `sudo pacman -Syu twitch-chat-window`. Pacman installs the app and its dependencies and adds **Twitch Chat Window** to your application menu. Pacman keeps its confirmation prompt so you can review the transaction.

After that, install or update with:

```sh
sudo pacman -Syu twitch-chat-window
```

Regular `sudo pacman -Syu` upgrades also update the app. The signing-key fingerprint is `CDD0942F17CAD1625B4E67F89711FF2636622D60`.

Open **Twitch Chat Window** from the app menu. It has one window and one taskbar entry. Drag the **Twitch Chat** title anywhere across your desktop and monitors, and drag the bottom-right grip to resize. **−** minimizes the chat window; **×** quits.

Enter a Twitch username and use **Opacity** to adjust visibility before starting your game.

Choose **Click through** before returning to your game. Press **Ctrl+Shift+F10** to unlock the running overlay without closing, restarting or relaunching it. KDE registers the shortcut automatically, including while another app has focus. You can customize **Unlock overlay** under **Twitch Chat Window** in Plasma’s **Keyboard → Shortcuts** settings. Existing assignments are preserved; another app’s shortcut is never taken over. Restoring the window from the taskbar also unlocks it.

For a portable install, download **TwitchChatWindow-linux-x64.tar.gz** from the [latest GitHub release](https://github.com/780APM/twitch-chat-window/releases/latest), install `gtk3`, `xorg-xwayland` and `kglobalacceld`, then run **twitch-chat-window** from the extracted folder.

The Linux build targets 64-bit KDE Plasma and uses XWayland in a Wayland session to support free movement, a taskbar entry, always-on-top and click-through in a single window. It can also run in an X11 session. Window stacking over games depends on the compositor; use **Windowed** mode if a fullscreen game covers the overlay.

### Updating from an older installation

The old `twitch-chat-window-packages` repository remains unchanged. Its original signing private key was lost, so the current repository is hosted with the application’s releases and has the new fingerprint above. If you added an old `[twitch-chat-window]` section to `/etc/pacman.conf`, remove only that section before running the new setup script. If an older `/etc/pacman.d/twitch-chat-window.conf` exists, back it up and remove its include before the new setup.

After a successful installation, the setup script backs up the user-local launcher supplied by an earlier portable installation, when it recognizes that exact application path. This lets the system package's current launcher appear in the app menu. Other custom launchers are left alone.

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

Update the version in `linux/TwitchChatWindow.Linux.csproj` and `linux/arch/PKGBUILD`, then update `linux/RELEASE-NOTES.md`. Push a matching version tag such as `v1.3.0`. GitHub Actions tests the native package and global unlock shortcut under KWin, builds Windows, and publishes downloads after both builds pass. The package and pacman database are signed using the encrypted private key and passphrase in the `PACMAN_SIGNING_KEY` and `PACMAN_SIGNING_PASSPHRASE` Actions secrets. The release also includes the public key, setup script, portable Linux archive, Windows executable and SHA-256 checksums. A clean Arch container then installs by name using the setup script and checks both package and database signatures.

Back up the signing key and passphrase privately; never commit private key material. `linux/sign-repository.sh` signs a local package with `GNUPGHOME`, `PACMAN_SIGNING_FINGERPRINT` and `PACMAN_SIGNING_PASSPHRASE_FILE` set. The old separate package repository is not changed by this workflow.
