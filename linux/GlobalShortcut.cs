using System;
using System.Runtime.InteropServices;

namespace TwitchChatWindow;

// Use KDE's compositor-wide shortcut service instead of an X11 passive grab:
// the latter cannot reliably receive keys while a native Wayland game is focused.
internal sealed class GlobalShortcut : IDisposable
{
    private const string Gio = "libgio-2.0.so.0";
    private const string Glib = "libglib-2.0.so.0";
    private const string Service = "org.kde.kglobalaccel";
    private const string Manager = "org.kde.KGlobalAccel";
    private const string Component = "io.github._780APM.TwitchChatWindow";
    private const string ActionId = "['" + Component + "', 'unlock-overlay', 'Twitch Chat Window', 'Unlock overlay']";
    // Qt::CTRL | Qt::SHIFT | Qt::Key_F10.
    private const int DefaultKey = 0x07000039;
    private readonly SignalCallback pressed;
    private IntPtr connection;
    private uint subscription;
    private bool registered;

    public string Hint { get; private set; } = "Unlock from the taskbar";

    private GlobalShortcut(Action unlock)
    {
        pressed = (_, _, _, _, _, parameters, _) =>
        {
            var action = g_variant_get_child_value(parameters, 1);
            try
            {
                if (Marshal.PtrToStringUTF8(g_variant_get_string(action, out _)) == "unlock-overlay")
                    unlock();
            }
            finally { g_variant_unref(action); }
        };
    }

    public static GlobalShortcut TryCreate(Action unlock)
    {
        var shortcut = new GlobalShortcut(unlock);
        try
        {
            shortcut.Register();
            return shortcut;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Global unlock shortcut unavailable: " + error.Message);
            shortcut.Dispose();
            return null;
        }
    }

    private void Register()
    {
        connection = g_bus_get_sync(2, IntPtr.Zero, out var error);
        if (connection == IntPtr.Zero)
            throw NativeError(error);
        CallAndRelease("doRegister", "(" + ActionId + ",)");
        registered = true;
        // Set the default, then activate with autoloading so KDE preserves the
        // user's existing assignment and never steals another app's shortcut.
        CallAndRelease("setShortcut", "(" + ActionId + ", [" + DefaultKey + "], uint32 8)");
        var result = Call("setShortcut", "(" + ActionId + ", [" + DefaultKey + "], uint32 2)");
        try
        {
            var keys = g_variant_get_child_value(result, 0);
            try
            {
                if (g_variant_n_children(keys) == 0)
                {
                    Console.Error.WriteLine("Assign Unlock overlay under Twitch Chat Window in KDE Keyboard Shortcuts. The default key is already assigned or disabled.");
                }
                else
                {
                    var key = g_variant_get_child_value(keys, 0);
                    try
                    {
                        Hint = g_variant_get_int32(key) == DefaultKey ? "Ctrl+Shift+F10 to unlock" : "Use your KDE unlock shortcut";
                    }
                    finally { g_variant_unref(key); }
                }
            }
            finally { g_variant_unref(keys); }
        }
        finally { g_variant_unref(result); }

        result = Call("getComponent", "('" + Component + "',)");
        string path;
        try
        {
            var child = g_variant_get_child_value(result, 0);
            try { path = Marshal.PtrToStringUTF8(g_variant_get_string(child, out _)); }
            finally { g_variant_unref(child); }
        }
        finally { g_variant_unref(result); }
        subscription = g_dbus_connection_signal_subscribe(connection, Service,
            "org.kde.kglobalaccel.Component", "globalShortcutPressed", path, Component, 0,
            pressed, IntPtr.Zero, IntPtr.Zero);
    }

    private void CallAndRelease(string method, string arguments) => g_variant_unref(Call(method, arguments));

    private IntPtr Call(string method, string arguments)
    {
        var parameters = g_variant_parse(IntPtr.Zero, arguments, IntPtr.Zero, IntPtr.Zero, out var error);
        if (parameters == IntPtr.Zero)
            throw NativeError(error);
        g_variant_ref_sink(parameters);
        try
        {
            var result = g_dbus_connection_call_sync(connection, Service, "/kglobalaccel", Manager,
                method, parameters, IntPtr.Zero, 0, 2000, IntPtr.Zero, out error);
            if (result == IntPtr.Zero)
                throw NativeError(error);
            return result;
        }
        finally { g_variant_unref(parameters); }
    }

    private static Exception NativeError(IntPtr error)
    {
        if (error == IntPtr.Zero)
            return new InvalidOperationException("KDE's shortcut service did not respond.");
        try
        {
            var native = Marshal.PtrToStructure<GError>(error);
            return new InvalidOperationException(Marshal.PtrToStringUTF8(native.Message));
        }
        finally { g_error_free(error); }
    }

    public void Dispose()
    {
        if (connection == IntPtr.Zero)
            return;
        if (subscription != 0)
            g_dbus_connection_signal_unsubscribe(connection, subscription);
        if (registered)
        {
            try { CallAndRelease("setInactive", "(" + ActionId + ",)"); }
            catch (Exception) { /* The session bus may already be stopping. */ }
        }
        g_object_unref(connection);
        connection = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GError { public uint Domain; public int Code; public IntPtr Message; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SignalCallback(IntPtr connection, IntPtr sender, IntPtr path,
        IntPtr type, IntPtr signal, IntPtr parameters, IntPtr userData);
    [DllImport(Gio)] private static extern IntPtr g_bus_get_sync(int type, IntPtr cancel, out IntPtr error);
    [DllImport(Gio)] private static extern IntPtr g_dbus_connection_call_sync(IntPtr connection, string bus,
        string path, string type, string method, IntPtr parameters, IntPtr replyType, int flags,
        int timeout, IntPtr cancel, out IntPtr error);
    [DllImport(Gio)] private static extern uint g_dbus_connection_signal_subscribe(IntPtr connection,
        string sender, string type, string member, string path, string arg0, int flags,
        SignalCallback callback, IntPtr data, IntPtr destroy);
    [DllImport(Gio)] private static extern void g_dbus_connection_signal_unsubscribe(IntPtr connection, uint id);
    [DllImport(Glib)] private static extern IntPtr g_variant_parse(IntPtr type, string text,
        IntPtr limit, IntPtr end, out IntPtr error);
    [DllImport(Glib)] private static extern IntPtr g_variant_ref_sink(IntPtr value);
    [DllImport(Glib)] private static extern void g_variant_unref(IntPtr value);
    [DllImport(Glib)] private static extern IntPtr g_variant_get_child_value(IntPtr value, nuint index);
    [DllImport(Glib)] private static extern nuint g_variant_n_children(IntPtr value);
    [DllImport(Glib)] private static extern int g_variant_get_int32(IntPtr value);
    [DllImport(Glib)] private static extern IntPtr g_variant_get_string(IntPtr value, out nuint length);
    [DllImport(Glib)] private static extern void g_error_free(IntPtr error);
    [DllImport("libgobject-2.0.so.0")] private static extern void g_object_unref(IntPtr value);
}
