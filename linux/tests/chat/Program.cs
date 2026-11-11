using System;
using System.Globalization;
using System.Net.Http;

if (CultureInfo.GetCultureInfo("sv-SE").NumberFormat.NumberDecimalSeparator != ",")
    throw new Exception("Bundled ICU did not provide locale data.");
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
using var response = await client.GetAsync("https://www.twitch.tv/robots.txt");
Console.WriteLine("Bundled ICU, OpenSSL and certificate verification passed (Twitch HTTPS status " + (int)response.StatusCode + ").");
