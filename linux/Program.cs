using System;
using Gtk;

namespace TwitchChatWindow;

internal static class Program
{
    private const string AppId = "io.github._780APM.TwitchChatWindow";

    private static int Main()
    {
        DesktopIntegration.InstallAppImageLauncher();
        if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            Console.Error.WriteLine("Twitch Chat Window needs X11 or XWayland. On KDE Wayland, install xorg-xwayland and sign in again.");
            return 1;
        }
        if (!WindowPlatform.IsInstalled())
        {
            Console.Error.WriteLine("Twitch Chat Window needs GTK 3. On CachyOS/Arch, run: sudo pacman -S --needed gtk3 xorg-xwayland");
            return 1;
        }

        GLib.Global.ProgramName = AppId;
        GLib.Global.ApplicationName = "Twitch Chat Window";
        WindowPlatform.UseX11();
        using var app = new Application(AppId, GLib.ApplicationFlags.None);
        using var toggle = new GLib.SimpleAction("toggle-click-through", null);
        using var unlock = new GLib.SimpleAction("unlock-overlay", null);
        using var minimize = new GLib.SimpleAction("minimize", null);
        using var quit = new GLib.SimpleAction("quit", null);
        Overlay overlay = null;
        GlobalShortcut shortcut = null;
        toggle.Activated += (_, _) => overlay?.ToggleClickThrough();
        unlock.Activated += (_, _) => overlay?.Unlock();
        minimize.Activated += (_, _) => overlay?.Iconify();
        quit.Activated += (_, _) => overlay?.Destroy();
        app.AddAction(toggle);
        app.AddAction(unlock);
        app.AddAction(minimize);
        app.AddAction(quit);
        app.Activated += (_, _) =>
        {
            if (overlay != null)
            {
                overlay.Unlock();
                overlay.Deiconify();
                overlay.Present();
                return;
            }
            try { FullscreenOverlay.Configure(); }
            catch (Exception error)
            {
                using var dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Error, ButtonsType.Close,
                    "Could not enable the fullscreen overlay.\n" + error.Message);
                dialog.Run();
                dialog.Destroy();
                return;
            }
            overlay = new Overlay(app);
            overlay.Destroyed += (_, _) =>
            {
                shortcut?.Dispose();
                shortcut = null;
                overlay = null;
            };
            overlay.ShowAll();
            shortcut = GlobalShortcut.TryCreate(() => overlay?.Unlock());
            overlay.SetUnlockHint(shortcut?.Hint ?? "Unlock from the taskbar");
        };
        return ((GLib.Application)app).Run();
    }
}
