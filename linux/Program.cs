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
        gtk_layer_set_keyboard_mode(window.Handle, Keyboard.OnDemand);
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
        var gdkWindow = window.Window;
        if (clickThrough)
        {
            using var empty = new Cairo.Region();
            gdkWindow.InputShapeCombineRegion(empty, 0, 0);
        }
        else
        {
            gdkWindow.InputShapeCombineRegion(null, 0, 0);
        }
        // Commit the new input state even when the compositor is withholding frame callbacks.
        WaylandInput.Commit(gdkWindow, clickThrough);
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

internal static class WaylandInput
{
    private const string Wayland = "libwayland-client.so.0";
    private const uint CreateRegion = 1, SetInputRegion = 5, CommitSurface = 6, DestroyRegion = 0;
    private const uint DestroyProxy = 1;
    private static readonly IntPtr RegionInterface =
        NativeLibrary.GetExport(NativeLibrary.Load(Wayland), "wl_region_interface");

    public static void Commit(Gdk.Window window, bool clickThrough)
    {
        var surface = gdk_wayland_window_get_wl_surface(window.Handle);
        var region = IntPtr.Zero;
        if (clickThrough)
        {
            var compositor = gdk_wayland_display_get_wl_compositor(window.Display.Handle);
            var newId = IntPtr.Zero;
            region = SendWithObject(compositor, CreateRegion, RegionInterface, wl_proxy_get_version(compositor), 0, ref newId);
        }

        var surfaceVersion = wl_proxy_get_version(surface);
        SendWithObject(surface, SetInputRegion, IntPtr.Zero, surfaceVersion, 0, ref region);
        Send(surface, CommitSurface, IntPtr.Zero, surfaceVersion, 0, IntPtr.Zero);
        if (region != IntPtr.Zero)
            Send(region, DestroyRegion, IntPtr.Zero, wl_proxy_get_version(region), DestroyProxy, IntPtr.Zero);
        window.Display.Flush();
    }

    [DllImport("libgdk-3.so.0")] private static extern IntPtr gdk_wayland_window_get_wl_surface(IntPtr window);
    [DllImport("libgdk-3.so.0")] private static extern IntPtr gdk_wayland_display_get_wl_compositor(IntPtr display);
    [DllImport(Wayland)] private static extern uint wl_proxy_get_version(IntPtr proxy);
    [DllImport(Wayland, EntryPoint = "wl_proxy_marshal_array_flags")]
    private static extern IntPtr Send(IntPtr proxy, uint opcode, IntPtr type, uint version, uint flags, IntPtr args);
    [DllImport(Wayland, EntryPoint = "wl_proxy_marshal_array_flags")]
    private static extern IntPtr SendWithObject(IntPtr proxy, uint opcode, IntPtr type, uint version, uint flags, ref IntPtr argument);
}
