using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace NetProbe {
    public static class MainForm { public const string Version = "4.0"; }
    public static class Tr { public static string Tx(string s) { return s; } }
    public static class Settings {
        public static string Dir;
        public static string Sub(string name) { string p = Path.Combine(Dir, name); Directory.CreateDirectory(p); return p; }
    }
}
class BackupCompatibility {
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static void Main() {
        var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        NetProbe.Settings.Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(NetProbe.Settings.Dir);
        string settings = "CharHosts=frame=w;widget=d\nCharLayout=chisa.frame=12,34,220\nCharOpacity=73\n";
        File.WriteAllText(Path.Combine(NetProbe.Settings.Dir, "settings.ini"), settings);
        foreach (string theme in new[] { "chisa", "kurumi" }) {
            string folder = NetProbe.Settings.Sub("themes/" + theme);
            foreach (string name in new[] { "frame", "widget", "pic3", "pic4", "window" }) File.WriteAllBytes(Path.Combine(folder, name + ".png"), new byte[] { 137, 80, 78, 71, (byte)name.Length });
        }
        string backup = NetProbe.Backup.Export();
        var current = json.Deserialize<Dictionary<string, object>>(backup);
        Check((string)current["app"] == "NetProbe", "legacy format identifier retained");
        Check(((Dictionary<string, object>)current["characters"]).Count == 10, "both Character themes and all image slots exported");
        File.WriteAllBytes(Path.Combine(NetProbe.Settings.Dir, "themes/chisa/frame.png"), new byte[] { 0 });
        Check(NetProbe.Backup.Import(backup) == null, "new backup imported");
        Check(File.ReadAllBytes(Path.Combine(NetProbe.Settings.Dir, "themes/chisa/frame.png")).SequenceEqual(new byte[] { 137, 80, 78, 71, 5 }), "Character image restored byte for byte");
        Check(File.ReadAllText(Path.Combine(NetProbe.Settings.Dir, "settings.ini")) == settings, "Character settings preserved");
        current.Remove("characters"); current["version"] = "3.6.0";
        Check(NetProbe.Backup.Import(json.Serialize(current)) == null, "old backup without images accepted");
        Check(File.ReadAllBytes(Path.Combine(NetProbe.Settings.Dir, "themes/chisa/frame.png")).Length == 5, "old backup leaves installed Character art intact");
        current["characters"] = new Dictionary<string, object> { { "../escape.png", "AA==" } };
        Check(NetProbe.Backup.Import(json.Serialize(current)) != null, "character path traversal rejected");
        current["characters"] = new Dictionary<string, object> { { "chisa/frame.png", "broken" } };
        Check(NetProbe.Backup.Import(json.Serialize(current)) != null, "invalid image encoding rejected before import");
    }
}
