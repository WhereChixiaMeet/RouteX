using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace NetProbe
{
    // Экспорт / импорт всех настроек одним JSON: settings.ini, списки, туннели, DPI-стратегии, серверы, подписки
    public static class Backup
    {
        static Dictionary<string, object> Files(string dir, string mask)
        {
            var d = new Dictionary<string, object>();
            try { foreach (var f in Directory.GetFiles(dir, mask)) d[Path.GetFileName(f)] = File.ReadAllText(f, Encoding.UTF8); } catch { }
            return d;
        }

        public static string Export()
        {
            var root = new Dictionary<string, object>
            {
                { "app", "NetProbe" }, { "version", MainForm.Version }, { "exported", DateTime.Now.ToString("s") },
                { "settings", File.Exists(Path.Combine(Settings.Dir, "settings.ini")) ? File.ReadAllText(Path.Combine(Settings.Dir, "settings.ini"), Encoding.UTF8) : "" },
                { "lists", Files(Settings.Sub("lists"), "*.txt") },
                { "tunnels", Files(Settings.Sub("tunnels"), "*.conf").Concat(Files(Settings.Sub("tunnels"), "*.json")).ToDictionary(kv => kv.Key, kv => kv.Value) }, // WireGuard + VLESS/VMess/Trojan
                { "dpi", Files(Settings.Sub("dpi"), "*.txt") },
                { "servers", File.Exists(Path.Combine(Settings.Dir, "servers.json")) ? File.ReadAllText(Path.Combine(Settings.Dir, "servers.json"), Encoding.UTF8) : "" },
                { "subscriptions", File.Exists(Path.Combine(Settings.Dir, "subscriptions.json")) ? File.ReadAllText(Path.Combine(Settings.Dir, "subscriptions.json"), Encoding.UTF8) : "" },
                { "characters", CharacterFiles() },
                // 5.0.3: the user's own DPI lists live in the zapret folder, not in config\lists, and were not in the backup
                { "zapretUser", string.IsNullOrEmpty(AppLists.ZapretLists) ? new Dictionary<string, object>() : Files(AppLists.ZapretLists, "*-user.txt") }
            };
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(root);
        }

        // Optional extension: legacy readers ignore it, legacy backups need not contain it.
        static readonly string[] CharacterThemes = { "chisa", "kurumi" };
        static readonly string[] CharacterNames = { "frame.png", "widget.png", "pic3.png", "pic4.png", "window.png" };
        static Dictionary<string, object> CharacterFiles()
        {
            var files = new Dictionary<string, object>();
            foreach (string theme in CharacterThemes)
                foreach (string name in CharacterNames) {
                    string path = Path.Combine(Settings.Dir, "themes", theme, name);
                    if (File.Exists(path)) files[theme + "/" + name] = Convert.ToBase64String(File.ReadAllBytes(path));
                }
            return files;
        }

        static Dictionary<string, byte[]> ReadCharacters(object value)
        {
            var files = value as Dictionary<string, object>;
            if (files == null) throw new FormatException("Invalid character backup.");
            var decoded = new Dictionary<string, byte[]>();
            foreach (var file in files) {
                string[] parts = file.Key.Split('/');
                if (parts.Length != 2 || !CharacterThemes.Contains(parts[0]) || !CharacterNames.Contains(parts[1]))
                    throw new FormatException("Invalid character file name.");
                decoded[file.Key] = Convert.FromBase64String(Convert.ToString(file.Value));
            }
            return decoded;
        }

        static void WriteFiles(object o, string dir)
        {
            var d = o as Dictionary<string, object>; if (d == null) return;
            foreach (var kv in d)
            {
                string name = Path.GetFileName(kv.Key);
                if (name.Length == 0) continue;
                File.WriteAllText(Path.Combine(dir, name), Convert.ToString(kv.Value), new UTF8Encoding(false));
            }
        }

        // Возвращает null при успехе или текст ошибки. Существующие файлы с теми же именами перезаписываются.
        public static string Import(string json)
        {
            Dictionary<string, object> root;
            try { root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(json); }
            catch (Exception ex) { return ex.Message; }
            if (root == null || !root.ContainsKey("app") || Convert.ToString(root["app"]) != "NetProbe") return Tr.Tx("Это не файл настроек RouteX");
            Dictionary<string, byte[]> characters = null;
            try { if (root.ContainsKey("characters")) characters = ReadCharacters(root["characters"]); }
            catch (Exception ex) { return ex.Message; }
            Func<string, string> s = k => root.ContainsKey(k) ? Convert.ToString(root[k]) : "";
            if (s("settings") != "") File.WriteAllText(Path.Combine(Settings.Dir, "settings.ini"), s("settings"), new UTF8Encoding(false));
            if (root.ContainsKey("lists")) WriteFiles(root["lists"], Settings.Sub("lists"));
            if (root.ContainsKey("tunnels")) WriteFiles(root["tunnels"], Settings.Sub("tunnels"));
            if (root.ContainsKey("dpi")) WriteFiles(root["dpi"], Settings.Sub("dpi"));
            if (root.ContainsKey("zapretUser") && !string.IsNullOrEmpty(AppLists.ZapretLists))
            {
                var user = root["zapretUser"] as Dictionary<string, object>;
                if (user != null)
                    try
                    {
                        Directory.CreateDirectory(AppLists.ZapretLists);
                        WriteFiles(user.Where(kv => Path.GetFileName(kv.Key).EndsWith("-user.txt", StringComparison.OrdinalIgnoreCase)).ToDictionary(kv => kv.Key, kv => kv.Value), AppLists.ZapretLists);
                    }
                    catch { }
            }
            if (s("servers") != "") File.WriteAllText(Path.Combine(Settings.Dir, "servers.json"), s("servers"), new UTF8Encoding(false));
            if (s("subscriptions") != "") File.WriteAllText(Path.Combine(Settings.Dir, "subscriptions.json"), s("subscriptions"), new UTF8Encoding(false));
            if (characters != null) foreach (var file in characters) {
                string path = Path.Combine(Settings.Dir, "themes", file.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, file.Value);
            }
            return null;
        }
    }
}
