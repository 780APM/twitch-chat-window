using System;
using TwitchChatWindow;

const string id = "{6d5b32b4-a862-4b9e-bf8a-57dac408662a}";
void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

foreach (var original in new[] {
    "",
    "[General]\ncount=2\nrules=1,2\n\n[1]\nDescription=Game\nwmclass=game\n\n[2]\nDescription=Browser\nwmclass=browser\n",
    "[General]\nOrder=game,browser\n\n[game]\nDescription=Game\nwmclass=game\n\n[browser]\nDescription=Browser\nwmclass=browser\n",
    "[General]\r\nOrder = game\r\n\r\n[game]\r\nDescription=Game\r\nwmclass=game\r\n"
})
{
    var updated = FullscreenOverlay.AddRule(original);
    Require(FullscreenOverlay.AddRule(updated) == updated, "Repeated setup changed the rules.");
    Require(updated.Contains("layer=critical-notification"), "Fullscreen layer missing.");
    Require(updated.Contains("wmclass=(?i)^io[.]github[.]_780APM[.]TwitchChatWindow$"), "Rule must match only this app.");
    Require(updated.Split("[" + id + "]").Length == 2, "Duplicate rule group.");
    foreach (var line in original.Replace("\r\n", "\n").Split('\n'))
    {
        if (line.StartsWith("Description=") || line.StartsWith("wmclass="))
            Require(updated.Contains(line), "An unrelated rule was modified: " + line);
    }
    if (original.Contains("Order="))
        Require(!updated.Contains("\nrules="), "Legacy metadata was recreated after migration.");
    if (original.Contains("rules=1,2"))
        Require(updated.Contains("rules=" + id + ",1,2"), "Legacy rules were dropped.");
    if (original.Contains("Order=game,browser"))
        Require(updated.Contains("Order=" + id + ",game,browser"), "Modern rule order changed.");
}
Console.WriteLine("KDE rule setup preserves legacy and modern rules and is idempotent.");
