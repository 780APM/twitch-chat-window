using System.Runtime.InteropServices;
using Gtk;

namespace TwitchChatWindow;

internal static class WindowPlatform
{
    public static bool IsInstalled()
    {
        if (!NativeLibrary.TryLoad("libgtk-3.so.0", out var handle))
            return false;
        NativeLibrary.Free(handle);
        return true;
    }

    // XWayland gives the same window taskbar integration, interactive movement,
    // keep-above and input shaping. Native Wayland restricts absolute positioning.
    public static void UseX11() => gdk_set_allowed_backends("x11");

    public static void SetClickThrough(Window window, bool enabled)
    {
        if (enabled)
        {
            using var empty = new Cairo.Region();
            window.Window.InputShapeCombineRegion(empty, 0, 0);
        }
        else
        {
            window.Window.InputShapeCombineRegion(null, 0, 0);
        }
        window.Display.Flush();
    }

    [DllImport("libgdk-3.so.0")]
    private static extern void gdk_set_allowed_backends(string backends);
}
