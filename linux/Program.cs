using System;
using System.Runtime.InteropServices;
using Gtk;

namespace TwitchChatWindow;

internal static class Program
{
    private const string AppId = "io.github._780APM.TwitchChatWindow";

    private static int Main()
    {
        if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            Console.Error.WriteLine("Twitch Chat Window needs a Wayland session with Layer Shell, such as KDE Plasma on CachyOS.");
            return 1;
        }

        LayerShell.UseWaylandOnly();
        using var app = new Application(AppId, GLib.ApplicationFlags.None);
        using var toggle = new GLib.SimpleAction("toggle-click-through", null);
        Overlay overlay = null;
        toggle.Activated += (_, _) => overlay?.ToggleClickThrough();
        app.AddAction(toggle);
        app.Activated += (_, _) =>
        {
            if (overlay != null)
            {
                overlay.Unlock();
                return;
            }

            if (!LayerShell.IsSupported())
            {
                using var dialog = new MessageDialog(null, DialogFlags.Modal, MessageType.Error, ButtonsType.Close,
                    "This Wayland session does not provide Layer Shell. Open the app in a KDE Plasma Wayland session.");
                dialog.Run();
                dialog.Destroy();
                return;
            }

            overlay = new Overlay(app);
            overlay.Destroyed += (_, _) => overlay = null;
            overlay.ShowAll();
        };
        return ((GLib.Application)app).Run();
    }
}

internal static class LayerShell
{
    private const string Library = "libgtk-layer-shell.so.0";
    private enum Layer { Background, Bottom, Top, Overlay }
    private enum Edge { Left, Right, Top, Bottom }
    private enum Keyboard { None, Exclusive, OnDemand }

    public static void UseWaylandOnly() => gdk_set_allowed_backends("wayland");
    public static bool IsSupported() => gtk_layer_is_supported() != 0;

    public static void Initialize(Window window)
    {
        gtk_layer_init_for_window(window.Handle);
        gtk_layer_set_namespace(window.Handle, "twitch-chat-window");
        gtk_layer_set_layer(window.Handle, Layer.Overlay);
        SetClickThrough(window, false);
    }

    public static void Position(Window window, bool right, bool bottom, int horizontal, int vertical)
    {
        gtk_layer_set_anchor(window.Handle, Edge.Left, right ? 0 : 1);
        gtk_layer_set_anchor(window.Handle, Edge.Right, right ? 1 : 0);
        gtk_layer_set_anchor(window.Handle, Edge.Top, bottom ? 0 : 1);
        gtk_layer_set_anchor(window.Handle, Edge.Bottom, bottom ? 1 : 0);
        gtk_layer_set_margin(window.Handle, right ? Edge.Right : Edge.Left, horizontal);
        gtk_layer_set_margin(window.Handle, bottom ? Edge.Bottom : Edge.Top, vertical);
    }

    public static void SetClickThrough(Window window, bool clickThrough)
    {
        gtk_layer_set_keyboard_mode(window.Handle, clickThrough ? Keyboard.None : Keyboard.OnDemand);
        if (clickThrough)
        {
            using var empty = new Cairo.Region();
            window.InputShapeCombineRegion(empty);
        }
        else
        {
            window.InputShapeCombineRegion(null);
        }
    }

    [DllImport("libgdk-3.so.0")] private static extern void gdk_set_allowed_backends(string backends);
    [DllImport(Library)] private static extern int gtk_layer_is_supported();
    [DllImport(Library)] private static extern void gtk_layer_init_for_window(IntPtr window);
    [DllImport(Library)] private static extern void gtk_layer_set_namespace(IntPtr window, string name);
    [DllImport(Library)] private static extern void gtk_layer_set_layer(IntPtr window, Layer layer);
    [DllImport(Library)] private static extern void gtk_layer_set_keyboard_mode(IntPtr window, Keyboard mode);
    [DllImport(Library)] private static extern void gtk_layer_set_anchor(IntPtr window, Edge edge, int anchor);
    [DllImport(Library)] private static extern void gtk_layer_set_margin(IntPtr window, Edge edge, int margin);
}
