using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace NetProbe
{
    // Runtime Xray rule representation used by RoutingSnapshot; no editable routing table.
    public sealed class ConfigRouteRule
    {
        public bool Enabled = true;
        public string Name = "New rule", Kind = "domain", Values = "", Outbound = "direct", Network = "", Ports = "";
    }

    // Helpers for inspecting active runtime routes; no editable rule sets.
    public static class ConfigRouting
    {
        public static readonly string[] Kinds = { "domain", "ip", "process", "all" };
        public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }; }
        public static string[] Tokens(string s) { return (s ?? "").Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray(); }
        public static Dictionary<string, object> Field(string key, object value) { return new Dictionary<string, object> { { key, value } }; }
        public static Dictionary<string, object> Logical(string mode, IEnumerable<object> rules) { return new Dictionary<string, object> { { "type", "logical" }, { "mode", mode }, { "rules", rules.ToArray() } }; }

        // ---- v2rayN value syntax ----
        static void Add(Dictionary<string, List<string>> f, string key, string v) { List<string> l; if (!f.TryGetValue(key, out l)) f[key] = l = new List<string>(); if (!l.Contains(v)) l.Add(v); }
        static void AddGeo(Dictionary<string, List<string>> f, IEnumerable<object> geo)
        {
            foreach (Dictionary<string, object> g in geo) foreach (var kv in g) foreach (var v in (IEnumerable<string>)kv.Value) Add(f, kv.Key, v);
        }
        static string Ascii(string host) { return new System.Globalization.IdnMapping().GetAscii(host.Trim().TrimEnd('.')).ToLowerInvariant(); }
        // domain:x (suffix), full:x, keyword:x, regexp:x, geosite:x; plain text = keyword (as in v2ray / v2rayN); # = comment
        public static Dictionary<string, List<string>> Domains(IEnumerable<string> values, Settings st)
        {
            var f = new Dictionary<string, List<string>>();
            foreach (var raw in values)
            {
                string val = raw.Trim(); if (val.Length == 0 || val.StartsWith("#")) continue;
                if (val.StartsWith("ext:") || val.StartsWith("ext-domain:")) throw new FormatException("External geodata files (ext:) are not supported: " + val);
                int colon = val.IndexOf(':');
                string kind = colon < 0 ? "" : val.Substring(0, colon).ToLowerInvariant(), v = colon < 0 ? val : val.Substring(colon + 1).Trim();
                if (kind == "geosite") { AddGeo(f, GeoData.Read(st, true, v)); continue; }
                if (v.Length == 0) throw new FormatException("Empty domain value: " + val);
                if (kind == "domain") Add(f, "domain_suffix", Ascii(v));
                else if (kind == "full") Add(f, "domain", Ascii(v));
                else if (kind == "regexp") { new Regex(v, RegexOptions.None, TimeSpan.FromMilliseconds(50)); Add(f, "domain_regex", v); }
                else if (kind == "keyword" || kind == "dotless") Add(f, "domain_keyword", v.ToLowerInvariant());
                else if (kind == "") Add(f, "domain_keyword", v.ToLowerInvariant());
                else throw new FormatException("Unknown domain prefix: " + val + " (use domain:, full:, keyword:, regexp: or geosite:)");
            }
            return f;
        }
        // CIDR / IP, geoip:x, geoip:private; "!" in front = everything except
        public static Dictionary<string, object> Ips(IEnumerable<string> values, Settings st, bool negative)
        {
            var f = new Dictionary<string, List<string>>(); bool priv = false;
            foreach (var raw in values)
            {
                string val = raw.Trim(); if (val.Length == 0 || val.StartsWith("#") || val.StartsWith("!") != negative) continue;
                if (negative) val = val.Substring(1).Trim();
                if (val.StartsWith("ext:") || val.StartsWith("ext-ip:")) throw new FormatException("External geodata files (ext:) are not supported: " + val);
                if (val.Equals("geoip:private", StringComparison.OrdinalIgnoreCase)) { priv = true; continue; }
                if (val.StartsWith("geoip:", StringComparison.OrdinalIgnoreCase)) { AddGeo(f, GeoData.Read(st, false, val.Substring(6).Trim())); continue; }
                string[] p = val.Split('/'); IPAddress ip; int bits;
                if (p.Length > 2 || !IPAddress.TryParse(p[0], out ip) || (p.Length == 2 && (!int.TryParse(p[1], out bits) || bits < 0 || bits > ip.GetAddressBytes().Length * 8))) throw new FormatException("Invalid IP/CIDR: " + raw);
                Add(f, "ip_cidr", p.Length == 1 ? ip + (ip.GetAddressBytes().Length == 4 ? "/32" : "/128") : ip + "/" + p[1]);
            }
            var res = f.ToDictionary(kv => kv.Key, kv => (object)kv.Value.ToArray());
            if (priv) res["ip_is_private"] = true;
            return res;
        }
        // exe name (".exe" is added, sing-box compares it exactly on Windows) or full path
        public static Dictionary<string, object> Processes(IEnumerable<string> values)
        {
            var names = new List<string>(); var paths = new List<string>();
            foreach (var raw in values)
            {
                string v = raw.Trim(); if (v.Length == 0 || v.StartsWith("#")) continue;
                if (v.IndexOfAny(new[] { '\\', '/' }) >= 0)
                {
                    v = v.Replace('/', '\\');
                    if (!Path.IsPathRooted(v)) throw new FormatException("Use a full process path or an executable name: " + raw);
                    v = Path.GetFullPath(v);
                    if (!paths.Contains(v, StringComparer.OrdinalIgnoreCase)) paths.Add(v);
                }
                else
                {
                    if (!v.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) v += ".exe";
                    if (!names.Contains(v, StringComparer.OrdinalIgnoreCase)) names.Add(v);
                }
            }
            var res = new Dictionary<string, object>();
            if (names.Count > 0) res["process_name"] = names.ToArray();
            if (paths.Count > 0) res["process_path"] = paths.ToArray();
            return res;
        }
        public static Dictionary<string, object> Matcher(ConfigRouteRule r, Settings st)
        {
            if (!Kinds.Contains(r.Kind ?? "")) throw new FormatException("Unsupported runtime rule.");
            var vals = Tokens(r.Values);
            if (r.Kind == "all") return new Dictionary<string, object>();
            Dictionary<string, object> m;
            if (r.Kind == "process") m = Processes(vals);
            else if (r.Kind == "ip") m = Ips(vals, st, false);
            else m = Domains(vals, st).ToDictionary(kv => kv.Key, kv => (object)kv.Value.ToArray()); // Xray: plain value = keyword
            if (m.Count == 0) throw new FormatException("Rule values cannot be empty.");
            return m;
        }
    }

    public static class RuDetect
    {
        public static readonly string[] SiteCategories = { "category-ru", "ru-available-only-inside", "ru-blocked" };
        public static readonly string[] IpCategories = { "ru", "ru-blocked" };
        public const string RuGeoBase = "https://github.com/runetfreedom/russia-v2ray-rules-dat/releases/latest/download/";

        public static List<string> DirectDomains(Settings st)
        {
            var l = new List<string>();
            foreach (var c in new[] { "category-ru", "ru-available-only-inside" }) if (GeoData.Has(st, true, c)) l.Add("geosite:" + c);
            l.AddRange(new[] { "domain:ru", "domain:su", "domain:xn--p1ai" });
            return l;
        }
        public static List<string> DirectIps(Settings st) { return GeoData.Has(st, false, "ru") ? new List<string> { "geoip:ru" } : new List<string>(); }
        public static bool RuGeoInstalled(Settings st) { return File.Exists(GeoData.RuFile(st, true)) && File.Exists(GeoData.RuFile(st, false)); }

        // host → which RU lists contain it. ips: addresses it resolves to (may be empty).
        public static string Report(Settings st, string host, IPAddress[] ips)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(host))
            {
                string h; try { h = new System.Globalization.IdnMapping().GetAscii(host.TrimEnd('.')).ToLowerInvariant(); } catch { h = host.ToLowerInvariant(); }
                bool tld = h.EndsWith(".ru") || h.EndsWith(".su") || h.EndsWith(".xn--p1ai") || h == "ru" || h == "su";
                parts.Add((tld ? "✓" : "✗") + " .ru/.su/.рф");
                foreach (var c in SiteCategories)
                {
                    if (!GeoData.Has(st, true, c)) { parts.Add("· geosite:" + c + " (not installed)"); continue; }
                    bool hit = GeoData.Read(st, true, c).Any(g => RoutingSnapshot.Matches((Dictionary<string, object>)g, null, null, null, h) == true);
                    parts.Add((hit ? "✓" : "✗") + " geosite:" + c);
                }
            }
            foreach (var c in IpCategories)
            {
                if (!GeoData.Has(st, false, c)) { parts.Add("· geoip:" + c + " (not installed)"); continue; }
                if (ips == null || ips.Length == 0) continue;
                var geo = GeoData.Read(st, false, c);
                var hits = ips.Where(ip => geo.Any(g => RoutingSnapshot.Matches((Dictionary<string, object>)g, null, null, ip, null) == true)).ToList();
                parts.Add((hits.Count > 0 ? "✓" : "✗") + " geoip:" + c);
            }
            return string.Join("   ", parts);
        }

        // RU geodata of runetfreedom (v2rayN Russia preset): geosite ru-blocked / ru-available-only-inside, geoip ru-blocked.
        // Saved next to the Xray geodata under separate names, so an Xray update does not replace them.
        public static string Install(Settings st, Action<string> status)
        {
            Directory.CreateDirectory(st.XrayPath);
            foreach (var site in new[] { true, false })
            {
                string name = site ? "geosite.dat" : "geoip.dat", dst = GeoData.RuFile(st, site), tmp = dst + ".download";
                status("Downloading " + name + "…");
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
                var rq = (HttpWebRequest)WebRequest.Create(RuGeoBase + name);
                rq.UserAgent = "NetProbe/" + MainForm.Version; rq.Timeout = 60000; rq.ReadWriteTimeout = 60000;
                using (var r = rq.GetResponse()) using (var src = r.GetResponseStream()) using (var f = File.Create(tmp)) src.CopyTo(f);
                GeoData.Verify(tmp, site);
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(tmp, dst);
            }
            return "RU geodata installed";
        }
    }

    // Read the installed Xray geodata, without downloading extra routing databases.
    // Wire format: v2ray routercommon/common.proto (GeoSiteList / GeoIPList).
    // Categories are indexed once per file and decoded on demand (the RU lists are large).
    public static class GeoData
    {
        sealed class Reader
        {
            public byte[] Data; public int Pos, End;
            public Reader(byte[] data) { Data = data; End = data.Length; }
            public ulong Varint()
            {
                ulong n = 0;
                for (int shift = 0; shift < 64; shift += 7) { if (Pos >= End) throw new FormatException("Truncated geodata."); byte b = Data[Pos++]; if (shift == 63 && b > 1) throw new FormatException("Invalid geodata integer."); n |= (ulong)(b & 127) << shift; if (b < 128) return n; }
                throw new FormatException("Invalid geodata integer.");
            }
            public Reader Message() { ulong length = Varint(); if (length > (ulong)(End - Pos)) throw new FormatException("Invalid geodata length."); var r = new Reader(Data) { Pos = Pos, End = Pos + (int)length }; Pos = r.End; return r; }
            public string Str() { var r = Message(); return Encoding.UTF8.GetString(Data, r.Pos, r.End - r.Pos); }
            public void Skip(int wire) { if (wire == 0) Varint(); else if (wire == 2) Message(); else { int n = wire == 1 ? 8 : wire == 5 ? 4 : -1; if (n < 0 || n > End - Pos) throw new FormatException("Invalid geodata field."); Pos += n; } }
        }
        sealed class Cache { public long Length; public DateTime Time; public Dictionary<string, int[]> Index; public Dictionary<string, List<object>> Groups; }
        static readonly Dictionary<string, Cache> cache = new Dictionary<string, Cache>(StringComparer.OrdinalIgnoreCase);

        public static string MainFile(Settings st, bool site) { return Path.Combine(st.XrayPath, site ? "geosite.dat" : "geoip.dat"); }
        public static string RuFile(Settings st, bool site) { return Path.Combine(st.XrayPath, site ? "geosite-ru.dat" : "geoip-ru.dat"); }

        static Cache Open(string path)
        {
            var fi = new FileInfo(path); if (!fi.Exists) return null;
            if (fi.Length > 256 * 1024 * 1024) throw new FormatException("Geodata file too large.");
            Cache c;
            if (cache.TryGetValue(path, out c) && c.Length == fi.Length && c.Time == fi.LastWriteTimeUtc) return c;
            c = new Cache { Length = fi.Length, Time = fi.LastWriteTimeUtc, Index = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase), Groups = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase) };
            var root = new Reader(File.ReadAllBytes(path));
            while (root.Pos < root.End)
            {
                int tag = (int)root.Varint(); if (tag != 10) { root.Skip(tag & 7); continue; }
                var group = root.Message(); int start = group.Pos;
                while (group.Pos < group.End)
                {
                    tag = (int)group.Varint();
                    if (tag == 10) { string code = group.Str(); if (!c.Index.ContainsKey(code)) c.Index[code] = new[] { start, group.End }; break; }
                    group.Skip(tag & 7);
                }
            }
            cache[path] = c;
            return c;
        }
        static List<object> Decode(byte[] data, int start, int end, bool site)
        {
            var group = new Reader(data) { Pos = start, End = end }; bool inverse = false; var fields = new Dictionary<string, List<string>>();
            while (group.Pos < group.End)
            {
                int tag = (int)group.Varint();
                if (!site && tag == 24) { inverse = group.Varint() != 0; continue; }
                if (tag != 18) { group.Skip(tag & 7); continue; }
                var e = group.Message(); string key, value;
                if (site)
                {
                    int type = 0; value = null;
                    while (e.Pos < e.End) { tag = (int)e.Varint(); if (tag == 8) type = (int)e.Varint(); else if (tag == 18) value = e.Str(); else e.Skip(tag & 7); }
                    key = type == 0 ? "domain_keyword" : type == 1 ? "domain_regex" : type == 2 ? "domain_suffix" : type == 3 ? "domain" : null;
                }
                else
                {
                    byte[] ip = null; int bits = 0;
                    while (e.Pos < e.End) { tag = (int)e.Varint(); if (tag == 10) { var b = e.Message(); ip = new byte[b.End - b.Pos]; Buffer.BlockCopy(b.Data, b.Pos, ip, 0, ip.Length); } else if (tag == 16) bits = (int)e.Varint(); else e.Skip(tag & 7); }
                    if (ip == null || (ip.Length != 4 && ip.Length != 16) || bits < 0 || bits > ip.Length * 8) throw new FormatException("Invalid geodata CIDR.");
                    key = "ip_cidr"; value = new IPAddress(ip) + "/" + bits;
                }
                if (key == null || string.IsNullOrEmpty(value)) throw new FormatException("Invalid geodata entry.");
                List<string> l; if (!fields.TryGetValue(key, out l)) fields[key] = l = new List<string>(); l.Add(value);
            }
            if (inverse) return new List<object>();
            return fields.Select(kv => (object)ConfigRouting.Field(kv.Key, kv.Value.ToArray())).ToList();
        }
        static List<object> Find(string path, string category, bool site)
        {
            var c = Open(path); int[] at;
            if (c == null || !c.Index.TryGetValue(category, out at)) return null;
            List<object> res;
            if (!c.Groups.TryGetValue(category, out res)) c.Groups[category] = res = Decode(File.ReadAllBytes(path), at[0], at[1], site);
            return res.Count == 0 ? null : res;
        }
        public static List<object> Read(Settings st, bool site, string category)
        {
            if (!Regex.IsMatch(category ?? "", "^[a-zA-Z0-9_!.-]+$")) throw new FormatException("Invalid geodata category: " + category);
            string main = MainFile(st, site), ru = RuFile(st, site);
            lock (cache)
            {
                if (!File.Exists(main) && !File.Exists(ru)) throw new FileNotFoundException("Install the Xray geodata first: " + Path.GetFileName(main));
                var r = Find(main, category, site) ?? Find(ru, category, site);
                if (r == null) throw new FormatException("Geodata category not found: " + (site ? "geosite:" : "geoip:") + category + (RuDetect.RuGeoInstalled(st) ? "" : " (RU lists need the RU geodata)"));
                return r;
            }
        }
        public static bool Has(Settings st, bool site, string category)
        {
            try { lock (cache) { foreach (var p in new[] { MainFile(st, site), RuFile(st, site) }) { var c = Open(p); if (c != null && c.Index.ContainsKey(category)) return true; } } }
            catch { }
            return false;
        }
        public static void Verify(string path, bool site)
        {
            lock (cache)
            {
                var c = Open(path); cache.Remove(path);
                if (c == null || c.Index.Count == 0) throw new FormatException("Downloaded geodata is empty or invalid: " + Path.GetFileName(path));
                if (!c.Index.ContainsKey(site ? "ru-blocked" : "ru")) throw new FormatException("Downloaded geodata has no RU lists: " + Path.GetFileName(path));
            }
        }
    }
}
