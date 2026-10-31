using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NetProbe;

class TunStackTests
{
    static int checks;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS: " + label); }
    static Dictionary<string, object> Obj(object value) { return (Dictionary<string, object>)value; }
    static Dictionary<string, object> Inbound(Dictionary<string, object> config) { return Obj(((IEnumerable)config["inbounds"]).Cast<object>().First()); }
    static Dictionary<string, object> Build(Settings settings, string file)
    {
        var lists = TunLists.FromLists(new[] { "browser.exe" }, new[] { "direct.exe" });
        string json = SingBoxConfig.Build(settings, lists); File.WriteAllText(file, json);
        return Obj(ConfigRouting.Json().DeserializeObject(json));
    }
    static int Main()
    {
        try
        {
            Logger.ToFile = false;
            var settings = new Settings { TunMode = true, BypassRu = false };
            Check(settings.TunStack == "mixed", "Default retains mixed for existing installations");
            var primaryFlag = typeof(Settings).GetField("PrimaryXrayTunnel", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (bool primary in new[] { false, true })
            {
                primaryFlag.SetValue(settings, primary);
                settings.TunStack = "mixed"; var mixed = Build(settings, primary ? "configs-mixed.json" : "subscriptions-mixed.json");
                settings.TunStack = "gvisor"; var gvisor = Build(settings, primary ? "configs-gvisor.json" : "subscriptions-gvisor.json");
                Check((string)Inbound(mixed)["stack"] == "mixed" && (string)Inbound(gvisor)["stack"] == "gvisor", "Selected stack reaches the " + (primary ? "Configs" : "subscriptions") + " TUN config");
                Inbound(gvisor)["stack"] = "mixed";
                Check(ConfigRouting.Json().Serialize(mixed) == ConfigRouting.Json().Serialize(gvisor), "Stack selection leaves routing, DNS, MTU and credentials unchanged");
            }
            settings.Save(); var reloaded = Settings.Load();
            Check(reloaded.TunStack == "gvisor", "gvisor persists through settings save/load");
            Check(settings.Clone().TunStack == "gvisor", "Tunnel settings clone preserves selected stack");
            var copy = new Settings(); copy.CopyFrom(settings); Check(copy.TunStack == "gvisor", "Settings import/copy preserves selected stack");
            File.AppendAllText(Path.Combine(Settings.Dir, "settings.ini"), "\r\nRuleSetsEnabled=1\r\nConfigRulesEnabled=1\r\n");
            File.WriteAllText(Path.Combine(Settings.Dir, "routing.json"), "{invalid obsolete editor data}");
            var migrated = Settings.Load(); primaryFlag.SetValue(migrated, true);
            Check(typeof(Settings).GetField("RuleSetsEnabled") == null && typeof(Settings).GetField("ConfigRulesEnabled") == null, "Old editor switches cannot enable hidden routing");
            var standard = Build(migrated, "standard-migrated.json");
            Check((string)Obj(standard["route"])["final"] == "direct", "Include lists remain active despite old enabled editor settings and routing.json");
            var tunnelSettings = typeof(Settings).Assembly.GetTypes().Select(t => t.GetMethod("TunSettings", BindingFlags.NonPublic | BindingFlags.Static)).First(m => m != null);
            migrated.BypassRu = true;
            var effective = (Settings)tunnelSettings.Invoke(null, new object[] { migrated });
            Check(effective.BypassRu, "Configs tunnel preserves RU bypass");
            var server = new ServerEntry { Protocol = "vless", Address = "192.0.2.1", Port = 443, UserId = "11111111-1111-1111-1111-111111111111" };
            string xray = XrayConfig.Build(effective, server);
            Check(xray.Contains("geosite:category-ru") && xray.Contains("geoip:ru") && xray.Contains("domain:ru") && xray.Contains("domain:su") && xray.Contains("domain:xn--p1ai"), "Xray retains all standard RU domain and IP bypass rules");
            foreach (string invalid in new[] { "invalid", "", null })
            {
                settings.TunStack = invalid;
                Check((string)Inbound(Build(settings, "invalid-stack.json"))["stack"] == "mixed", "Invalid value cannot reach the core: " + (invalid ?? "null"));
                settings.Clamp(); Check(settings.TunStack == "mixed", "Invalid saved stack falls back to mixed");
            }
            Console.WriteLine("TOTAL: " + checks + " offline checks; no VPN or network changes."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
