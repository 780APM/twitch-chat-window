using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;

if (CultureInfo.GetCultureInfo("sv-SE").NumberFormat.NumberDecimalSeparator != ",")
    throw new Exception("Bundled ICU did not provide locale data.");
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
using var response = await client.GetAsync("https://www.twitch.tv/robots.txt");
response.EnsureSuccessStatusCode();
var appDir = Environment.GetEnvironmentVariable("APPDIR") ?? throw new Exception("APPDIR is missing.");
var mappings = File.ReadAllLines("/proc/self/maps");
foreach (var library in new[] { "libicui18n.so.", "libicuuc.so.", "libicudata.so.", "libssl.so.3", "libcrypto.so.3" })
{
    var loaded = mappings.Where(line => line.Contains(library)).ToArray();
    if (loaded.Length == 0 || loaded.Any(line => !line.Contains(appDir + "/usr/lib/")))
        throw new Exception(library + " was not loaded from the AppImage.");
}
Console.WriteLine("Bundled ICU, OpenSSL and certificate verification passed (Twitch HTTPS status " + (int)response.StatusCode + ").");
