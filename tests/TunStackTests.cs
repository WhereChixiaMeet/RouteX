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
