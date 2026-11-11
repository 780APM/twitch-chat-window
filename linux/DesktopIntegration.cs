using System;
using System.IO;
using System.Reflection;

namespace TwitchChatWindow;

internal static class DesktopIntegration
{
    public static void InstallAppImageLauncher()
    {
        if (!OperatingSystem.IsLinux())
            return;
        var image = Environment.GetEnvironmentVariable("APPIMAGE");
        if (String.IsNullOrEmpty(image) || !File.Exists(image))
            return;
        try
        {
            var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (String.IsNullOrEmpty(data))
                data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            var install = Path.Combine(data, "twitch-chat-window");
            var applications = Path.Combine(data, "applications");
            Directory.CreateDirectory(install);
            Directory.CreateDirectory(applications);
            var installedImage = Path.Combine(install, "TwitchChatWindow.AppImage");
            if (Path.GetFullPath(image) != Path.GetFullPath(installedImage))
            {
                var temporary = installedImage + "." + Guid.NewGuid().ToString("N");
                try
                {
                    File.Copy(image, temporary);
                    File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                        UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    File.Move(temporary, installedImage, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            var icon = Path.Combine(install, "mark.png");
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("mark.png"))
            using (var destination = File.Create(icon))
                source.CopyTo(destination);
            var launcher = Path.Combine(applications, "io.github._780APM.TwitchChatWindow.desktop");
            var text = "[Desktop Entry]\nType=Application\nName=Twitch Chat Window\n" +
                "Comment=Read live Twitch chat while you play\nExec=" + QuoteExec(installedImage) +
                "\nIcon=" + icon.Replace("\\", "\\\\") +
                "\nTerminal=false\nStartupNotify=true\nStartupWMClass=io.github._780APM.TwitchChatWindow\n" +
                "Categories=Utility;\nKeywords=Twitch;Chat;Overlay;Streaming;\n";
            var staging = launcher + "." + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(staging, text);
                File.Move(staging, launcher, true);
            }
            finally { if (File.Exists(staging)) File.Delete(staging); }
        }
        catch (Exception error)
        {
            // A read-only home directory should not prevent portable use.
            Console.Error.WriteLine("Could not add the application-menu launcher: " + error.Message);
        }
    }

    private static string QuoteExec(string path)
    {
        var argument = path.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("$", "\\$").Replace("`", "\\`");
        return "\"" + argument.Replace("\\", "\\\\").Replace("%", "%%") + "\"";
    }
}
