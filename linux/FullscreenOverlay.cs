using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TwitchChatWindow;

// Keep a normal, movable taskbar window, but put only this app above KWin's
// active-fullscreen layer. KeepAbove alone belongs below fullscreen games.
internal static class FullscreenOverlay
{
    private const string RuleId = "{6d5b32b4-a862-4b9e-bf8a-57dac408662a}";
    private const string Gio = "libgio-2.0.so.0";
    private const string Glib = "libglib-2.0.so.0";

    public static void Configure()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException();
        var connection = g_bus_get_sync(2, IntPtr.Zero, out var error);
        if (connection == IntPtr.Zero)
            throw Error(error);
        try
        {
            var result = Call(connection, "supportInformation");
            string support;
            try
            {
                var child = g_variant_get_child_value(result, 0);
                try { support = Marshal.PtrToStringUTF8(g_variant_get_string(child, out _)); }
                finally { g_variant_unref(child); }
            }
            finally { g_variant_unref(result); }
            var version = Regex.Match(support ?? "", @"KWin version:\s*(\d+)\.");
            if (!version.Success || Int32.Parse(version.Groups[1].Value) < 6)
                throw new InvalidOperationException("Fullscreen overlays require KDE Plasma 6 or newer.");

            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (String.IsNullOrEmpty(configHome))
                configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            Directory.CreateDirectory(configHome);
            var path = Path.Combine(configHome, "kwinrulesrc");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var original = File.Exists(path) ? File.ReadAllText(path) : "";
                var replacement = AddRule(original);
                if (original == replacement)
                    return;
                var temporary = path + ".twitch-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(temporary, replacement);
                    if (File.Exists(path))
                    {
                        File.SetUnixFileMode(temporary, File.GetUnixFileMode(path));
                        if (File.ReadAllText(path) != original)
                            continue; // Do not overwrite a simultaneous settings edit.
                        var backup = path + ".before-twitch-chat-window";
                        if (!File.Exists(backup)) File.Copy(path, backup);
                    }
                    else if (original.Length != 0) continue;
                    File.Move(temporary, path, true);
                    result = Call(connection, "reconfigure");
                    g_variant_unref(result);
                    return;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            throw new IOException("KDE window rules changed during setup. Open the app again to retry.");
        }
        finally { g_object_unref(connection); }
    }

    internal static string AddRule(string original)
    {
        var lines = original.Replace("\r\n", "\n").Split('\n').ToList();
        var legacy = Entry(lines, "General", "rules");
        var order = String.IsNullOrEmpty(legacy) ? Entry(lines, "General", "Order") : legacy;
        var ids = order.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(id => id != RuleId).ToList();
        if (String.IsNullOrEmpty(legacy))
        {
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']')) continue;
                var group = trimmed.Substring(1, trimmed.Length - 2);
                if (group != "General" && !group.StartsWith('$') && group != RuleId && !ids.Contains(group))
                    ids.Add(group);
            }
        }
        ids.Insert(0, RuleId);
        // Newer KWin migrates legacy rules/count to Order. Do not recreate the
        // deprecated entries on each launch after that migration has happened.
        if (!String.IsNullOrEmpty(legacy) || String.IsNullOrEmpty(Entry(lines, "General", "Order")))
        {
            SetEntry(lines, "General", "rules", String.Join(',', ids));
            SetEntry(lines, "General", "count", ids.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        SetEntry(lines, "General", "Order", String.Join(',', ids));
        SetEntry(lines, RuleId, "Description", "Twitch Chat Window above fullscreen games");
        SetEntry(lines, RuleId, "Enabled", "true");
        SetEntry(lines, RuleId, "wmclass", "(?i)^io[.]github[.]_780APM[.]TwitchChatWindow$");
        SetEntry(lines, RuleId, "wmclassmatch", "3");
        SetEntry(lines, RuleId, "wmclasscomplete", "false");
        SetEntry(lines, RuleId, "layer", "critical-notification");
        SetEntry(lines, RuleId, "layerrule", "2");
        var newline = original.Contains("\r\n") ? "\r\n" : "\n";
        return String.Join(newline, lines).TrimEnd('\r', '\n') + newline;
    }

    private static string Entry(List<string> lines, string group, string key)
    {
        bool inside = false;
        foreach (var line in lines)
        {
            var text = line.Trim();
            if (text.StartsWith('[')) inside = text == "[" + group + "]";
            else if (inside && text.Contains('='))
            {
                var separator = text.IndexOf('=');
                if (text.Substring(0, separator).Trim() == key)
                    return text.Substring(separator + 1).Trim();
            }
        }
        return "";
    }

    private static void SetEntry(List<string> lines, string group, string key, string value)
    {
        int start = lines.FindIndex(line => line.Trim() == "[" + group + "]");
        if (start < 0)
        {
            lines.Add(""); lines.Add("[" + group + "]"); start = lines.Count - 1;
        }
        int end = start + 1;
        while (end < lines.Count && !lines[end].TrimStart().StartsWith('[')) end++;
        for (int index = start + 1; index < end; index++)
        {
            var separator = lines[index].IndexOf('=');
            if (separator >= 0 && lines[index].Substring(0, separator).Trim() == key)
            {
                lines[index] = key + "=" + value;
                return;
            }
        }
        lines.Insert(end, key + "=" + value);
    }

    private static IntPtr Call(IntPtr connection, string method)
    {
        var result = g_dbus_connection_call_sync(connection, "org.kde.KWin", "/KWin", "org.kde.KWin",
            method, IntPtr.Zero, IntPtr.Zero, 0, 3000, IntPtr.Zero, out var error);
        if (result == IntPtr.Zero) throw Error(error);
        return result;
    }

    private static Exception Error(IntPtr error)
    {
        if (error == IntPtr.Zero) return new InvalidOperationException("KDE's window manager did not respond.");
        try
        {
            var native = Marshal.PtrToStructure<GError>(error);
            return new InvalidOperationException(Marshal.PtrToStringUTF8(native.Message));
        }
        finally { g_error_free(error); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GError { public uint Domain; public int Code; public IntPtr Message; }
    [DllImport(Gio)] private static extern IntPtr g_bus_get_sync(int type, IntPtr cancel, out IntPtr error);
    [DllImport(Gio)] private static extern IntPtr g_dbus_connection_call_sync(IntPtr connection, string bus,
        string path, string type, string method, IntPtr parameters, IntPtr replyType, int flags,
        int timeout, IntPtr cancel, out IntPtr error);
    [DllImport(Glib)] private static extern IntPtr g_variant_get_child_value(IntPtr value, nuint index);
    [DllImport(Glib)] private static extern IntPtr g_variant_get_string(IntPtr value, out nuint length);
    [DllImport(Glib)] private static extern void g_variant_unref(IntPtr value);
    [DllImport(Glib)] private static extern void g_error_free(IntPtr error);
    [DllImport("libgobject-2.0.so.0")] private static extern void g_object_unref(IntPtr value);
}
