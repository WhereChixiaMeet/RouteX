using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace NetProbe
{
    // AmneziaWG (5.1.0). A tunnel is stored like the VLESS ones (config\tunnels\<name>.json, Protocol = "awg") with the
    // whole wg-quick text, AWG parameters included, in ServerEntry.Awg. It runs on mihomo (MetaCubeX, it has the
    // amneziawg-go device) behind the same sing-box TUN as the xray tunnels: same Include / Exclude lists, same router
    // states, failover and kill switch. WireSock tunnels (*.conf) are not touched.
    public static class AwgKeys
    {
        // obfuscation keys of AmneziaWG 1.0 / 1.5 / 2.0, as Amnezia writes them in [Interface]
        public static readonly string[] Ints = { "Jc", "Jmin", "Jmax", "S1", "S2", "S3", "S4" };
        public static readonly string[] Strings = { "H1", "H2", "H3", "H4", "I1", "I2", "I3", "I4", "I5", "J1", "J2", "J3" };
        public const string Itime = "Itime";
        public static IEnumerable<string> All { get { return Ints.Concat(Strings).Concat(new[] { Itime }); } }
    }

    // wg-quick text: [Interface] + the first [Peer]
    public class AwgConf
    {
        public readonly Dictionary<string, string> Iface = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Peer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static AwgConf Parse(string text)
        {
            var c = new AwgConf();
            string section = ""; bool peerSeen = false, inFirstPeer = false;
            foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var t = raw.Trim();
                if (t.Length == 0 || t.StartsWith("#") || t.StartsWith(";")) continue;
                if (t.StartsWith("["))
                {
                    section = t.Trim('[', ']').Trim().ToLowerInvariant();
                    if (section == "peer") { inFirstPeer = !peerSeen; peerSeen = true; }
                    continue;
                }
                int i = t.IndexOf('=');
                if (i <= 0) continue;
                string k = t.Substring(0, i).Trim(), v = t.Substring(i + 1).Trim();
                if (section == "interface") c.Iface[k] = v;
                else if (section == "peer" && inFirstPeer) c.Peer[k] = v;
            }
            return c;
        }

        public string I(string k) { string v; return Iface.TryGetValue(k, out v) ? v : ""; }
        public string P(string k) { string v; return Peer.TryGetValue(k, out v) ? v : ""; }

        // "host:port", "[v6]:port"
        public static bool SplitEndpoint(string ep, out string host, out int port)
        {
            host = ""; port = 0; ep = (ep ?? "").Trim();
            if (ep.StartsWith("["))
            {
                int j = ep.IndexOf(']');
                if (j < 0) return false;
                host = ep.Substring(1, j - 1);
                return ep.Length > j + 2 && ep[j + 1] == ':' && int.TryParse(ep.Substring(j + 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out port) && port > 0 && port < 65536;
            }
            int c = ep.LastIndexOf(':');
            if (c <= 0) return false;
            host = ep.Substring(0, c);
            return int.TryParse(ep.Substring(c + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out port) && port > 0 && port < 65536;
        }

        static IEnumerable<string> List(string v) { return (v ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0); }
        public IEnumerable<string> Addresses { get { return List(I("Address")); } }
        public IEnumerable<string> Dns { get { return List(I("DNS")); } }
        public IEnumerable<string> AllowedIps { get { return List(P("AllowedIPs")); } }

        public bool HasObfuscation { get { return AwgKeys.All.Any(k => { string v = I(k); return v != "" && v != "0"; }); } }

        // what is missing for a connection (null - nothing)
        public string Missing()
        {
            var m = new List<string>();
            if (I("PrivateKey") == "") m.Add("PrivateKey");
            if (!Addresses.Any()) m.Add("Address");
            if (P("PublicKey") == "") m.Add("PublicKey");
            string h; int p;
            if (!SplitEndpoint(P("Endpoint"), out h, out p)) m.Add("Endpoint");
            return m.Count == 0 ? null : "missing " + string.Join(", ", m);
        }
    }

    // vpn://… keys of the Amnezia app: base64url of qCompress(JSON) - 4 bytes of the length (big endian) + a zlib stream.
    // Inside: containers[]; the AWG / WireGuard one has last_config (a JSON string) with the wg-quick text in "config".
    public static class AmneziaLink
    {
        public static bool Is(string s) { return (s ?? "").Trim().StartsWith("vpn://", StringComparison.OrdinalIgnoreCase); }

        public static string Payload(string link)
        {
            string b = new string(link.Trim().Substring(6).Where(c => !char.IsWhiteSpace(c)).ToArray()).TrimEnd('=');
            b = b.Replace('-', '+').Replace('_', '/');
            if (b.Length % 4 == 1) throw new FormatException("the key is cut off");
            b = b.PadRight(b.Length + (4 - b.Length % 4) % 4, '=');
            byte[] raw = Convert.FromBase64String(b);
            if (raw.Length > 0 && raw[0] == (byte)'{') return Encoding.UTF8.GetString(raw); // an uncompressed key
            if (raw.Length < 8) throw new FormatException("the key is too short");
            long size = ((long)raw[0] << 24) | ((long)raw[1] << 16) | ((long)raw[2] << 8) | raw[3];
            if (size <= 0 || size > 16 * 1024 * 1024) throw new FormatException("this is not an Amnezia key");
            int off = 4;
            if ((raw[4] & 0x0F) == 8 && ((raw[4] << 8) | raw[5]) % 31 == 0) off = 6; // zlib header; DeflateStream wants raw deflate
            using (var ms = new MemoryStream(raw, off, raw.Length - off))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outp = new MemoryStream())
            {
                ds.CopyTo(outp);
                return Encoding.UTF8.GetString(outp.ToArray());
            }
        }

        static string S(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture).Trim() : ""; }

        // vpn:// → wg-quick text with the AWG parameters; name = server description
        public static string Decode(string link, out string name, out string err)
        {
            name = ""; err = null;
            Dictionary<string, object> root;
            try
            {
                root = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.DeserializeObject(Payload(link)) as Dictionary<string, object>;
                if (root == null) throw new FormatException("no JSON inside");
            }
            catch (Exception ex) { err = "The vpn:// key is not readable (" + ex.Message + "). Copy the whole key from the Amnezia app."; return null; }

            name = S(root, "description");
            string dns1 = S(root, "dns1"), dns2 = S(root, "dns2"), rootHost = S(root, "hostName"), def = S(root, "defaultContainer");
            var containers = root.ContainsKey("containers") ? root["containers"] as object[] : null;
            // candidates: AWG first (the default container of the key before the others), then plain WireGuard
            var found = new List<Tuple<int, Dictionary<string, object>, Dictionary<string, object>>>();
            var seen = new List<string>();
            if (containers != null)
                foreach (var co in containers)
                {
                    var c = co as Dictionary<string, object>;
                    if (c == null) continue;
                    string cname = S(c, "container");
                    if (cname != "") seen.Add(cname);
                    foreach (var kv in c)
                    {
                        var proto = kv.Value as Dictionary<string, object>;
                        if (proto == null) continue;
                        string key = kv.Key.ToLowerInvariant();
                        bool awg = key.Contains("awg") || cname.ToLowerInvariant().Contains("awg");
                        bool wg = key.Contains("wireguard");
                        if (!awg && !wg) continue;
                        int rank = (awg ? 0 : 2) + (cname == def ? 0 : 1);
                        found.Add(Tuple.Create(rank, c, proto));
                    }
                }
            foreach (var f in found.OrderBy(x => x.Item1))
            {
                string text = FromContainer(f.Item3, dns1, dns2, rootHost);
                if (text == null) continue;
                var conf = AwgConf.Parse(text);
                if (conf.Missing() != null) continue;
                if (name == "") { string h; int p; AwgConf.SplitEndpoint(conf.P("Endpoint"), out h, out p); name = "Amnezia " + h; }
                return text;
            }
            if (root.ContainsKey("api_config") || root.ContainsKey("auth_data") || root.ContainsKey("api_key") || root.ContainsKey("config_version"))
                err = "This vpn:// key is from the Amnezia service (Premium / Free): there is no tunnel config inside, the Amnezia app gets it from Amnezia's servers. Share a key from your own server (Amnezia app - server - Share - AmneziaWG).";
            else
                err = "There is no AmneziaWG / WireGuard config in this vpn:// key" + (seen.Count > 0 ? " (inside: " + string.Join(", ", seen) + ")" : "") + ". In the Amnezia app share the AmneziaWG protocol of the server.";
            return null;
        }

        static string FromContainer(Dictionary<string, object> proto, string dns1, string dns2, string rootHost)
        {
            Dictionary<string, object> lc = null;
            object lco;
            if (proto.TryGetValue("last_config", out lco))
            {
                if (lco is Dictionary<string, object>) lc = (Dictionary<string, object>)lco;
                else
                    try { lc = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.DeserializeObject(Convert.ToString(lco)) as Dictionary<string, object>; }
                    catch { lc = null; }
            }
            if (lc == null) return null;
            string text = S(lc, "config");
            if (text.IndexOf("[Interface]", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // older keys: no ready text, only the fields
                string priv = S(lc, "client_priv_key"), ip = S(lc, "client_ip"), pub = S(lc, "server_pub_key");
                if (priv == "" || pub == "") return null;
                var sb0 = new StringBuilder();
                sb0.Append("[Interface]\nPrivateKey = ").Append(priv).Append("\nAddress = ").Append(ip.Contains("/") || ip == "" ? ip : ip + "/32").Append("\nDNS = $PRIMARY_DNS, $SECONDARY_DNS\n");
                sb0.Append("\n[Peer]\nPublicKey = ").Append(pub).Append('\n');
                if (S(lc, "psk_key") != "") sb0.Append("PresharedKey = ").Append(S(lc, "psk_key")).Append('\n');
                sb0.Append("AllowedIPs = 0.0.0.0/0, ::/0\n");
                text = sb0.ToString();
            }
            text = text.Replace("$PRIMARY_DNS", dns1 != "" ? dns1 : "1.1.1.1").Replace("$SECONDARY_DNS", dns2 != "" ? dns2 : "1.0.0.1");
            var conf = AwgConf.Parse(text);

            // rebuild a clean text: keys of the original in their order, then what only the JSON fields have
            var iface = new List<KeyValuePair<string, string>>(conf.Iface);
            var peer = new List<KeyValuePair<string, string>>(conf.Peer);
            Action<List<KeyValuePair<string, string>>, string, string> add = (l, k, v) =>
            {
                if (string.IsNullOrWhiteSpace(v) || l.Any(p => p.Key.Equals(k, StringComparison.OrdinalIgnoreCase) && p.Value.Trim() != "")) return;
                l.RemoveAll(p => p.Key.Equals(k, StringComparison.OrdinalIgnoreCase));
                l.Add(new KeyValuePair<string, string>(k, v.Trim()));
            };
            // AWG parameters sometimes live only in the JSON (last_config or the container) and not in the text
            foreach (var k in AwgKeys.All)
            {
                string v = S(lc, k);
                if (v == "") v = S(proto, k);
                add(iface, k, v);
            }
            if (conf.I("MTU") == "") add(iface, "MTU", S(lc, "mtu") != "" ? S(lc, "mtu") : S(proto, "mtu"));
            string ep = conf.P("Endpoint"), h0; int p0;
            if (ep.Contains("$") || !AwgConf.SplitEndpoint(ep, out h0, out p0))
            {
                string host = S(lc, "hostName"); if (host == "") host = rootHost;
                string port = S(lc, "port"); if (port == "") port = S(proto, "port");
                peer.RemoveAll(p => p.Key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase));
                if (host != "" && port != "") peer.Add(new KeyValuePair<string, string>("Endpoint", (host.Contains(":") ? "[" + host + "]" : host) + ":" + port));
            }
            if (conf.P("PersistentKeepalive") == "") add(peer, "PersistentKeepalive", S(lc, "persistent_keep_alive") != "" ? S(lc, "persistent_keep_alive") : "25");
            var sb = new StringBuilder("[Interface]\r\n");
            foreach (var p in iface) sb.Append(p.Key).Append(" = ").Append(p.Value).Append("\r\n");
            sb.Append("\r\n[Peer]\r\n");
            foreach (var p in peer) sb.Append(p.Key).Append(" = ").Append(p.Value).Append("\r\n");
            return sb.ToString();
        }

        // vpn:// → tunnel entry (Protocol "awg")
        public static ServerEntry ToEntry(string link, out string err)
        {
            string name;
            string text = Decode(link, out name, out err);
            if (text == null) return null;
            var e = new ServerEntry { Protocol = "awg", Name = name, Network = "udp", Security = "none" };
            if (!SetText(e, text, out err)) return null;
            return e;
        }

        // the wg-quick text of an AWG tunnel (from the key or the text editor) → entry fields
        public static bool SetText(ServerEntry e, string text, out string err)
        {
            err = null;
            var c = AwgConf.Parse(text);
            string miss = c.Missing();
            if (miss != null) { err = "AmneziaWG config: " + miss; return false; }
            string host; int port;
            AwgConf.SplitEndpoint(c.P("Endpoint"), out host, out port);
            e.Protocol = "awg"; e.Network = "udp"; e.Security = "none";
            e.Address = host; e.Port = port;
            e.Awg = text.Replace("\r\n", "\n").Replace("\n", "\r\n").Trim() + "\r\n";
            return true;
        }
    }

    // mihomo config: the AWG outbound and a socks inbound on 127.0.0.1 with the per-run login of the tunnel.
    // JSON is valid YAML, so the serializer writes it.
    public static class MihomoConfig
    {
        static int Int(string v, out bool ok) { int n; ok = int.TryParse((v ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n); return n; }

        public static string Build(Settings cs, AwgConf c)
        {
            string host; int port;
            AwgConf.SplitEndpoint(c.P("Endpoint"), out host, out port);
            var proxy = new Dictionary<string, object>
            {
                { "name", "awg" }, { "type", "wireguard" }, { "server", host }, { "port", port },
                { "private-key", c.I("PrivateKey") }, { "public-key", c.P("PublicKey") },
                { "udp", true }
            };
            string v4 = c.Addresses.FirstOrDefault(a => !a.Contains(":")), v6 = c.Addresses.FirstOrDefault(a => a.Contains(":"));
            if (v4 != null) proxy["ip"] = v4;
            if (v6 != null) proxy["ipv6"] = v6;
            if (c.P("PresharedKey") != "") proxy["pre-shared-key"] = c.P("PresharedKey");
            var allowed = c.AllowedIps.ToArray();
            if (allowed.Length > 0) proxy["allowed-ips"] = allowed;
            bool ok; int n;
            n = Int(c.I("MTU"), out ok); if (ok && n >= 576 && n <= 9000) proxy["mtu"] = n;
            n = Int(c.P("PersistentKeepalive"), out ok); if (ok && n > 0) proxy["persistent-keepalive"] = n;
            // names of sites are resolved inside the tunnel by the DNS servers of the config, never by the local provider
            var dns = c.Dns.Where(d => { System.Net.IPAddress a; return System.Net.IPAddress.TryParse(d, out a); }).ToArray();
            proxy["remote-dns-resolve"] = true;
            proxy["dns"] = dns.Length > 0 ? dns : new[] { "1.1.1.1", "1.0.0.1" };
            if (c.HasObfuscation)
            {
                var o = new Dictionary<string, object>();
                foreach (var k in AwgKeys.Ints) { n = Int(c.I(k), out ok); if (ok && n != 0) o[k.ToLowerInvariant()] = n; }
                foreach (var k in AwgKeys.Strings) { string v = c.I(k); if (v != "") o[k.ToLowerInvariant()] = v; }
                long it; if (long.TryParse(c.I(AwgKeys.Itime), NumberStyles.Integer, CultureInfo.InvariantCulture, out it) && it != 0) o["itime"] = it;
                proxy["amnezia-wg-option"] = o;
            }
            var cfg = new Dictionary<string, object>
            {
                { "socks-port", cs.SocksPort },
                { "allow-lan", false }, { "bind-address", "127.0.0.1" },
                // other programs on this PC cannot use the port (and cannot find an open local proxy by scanning)
                { "authentication", new[] { TunnelAuth.User + ":" + TunnelAuth.Pass } }, { "skip-auth-prefixes", new string[0] },
                { "mode", "rule" }, { "log-level", "warning" }, { "ipv6", true },
                { "find-process-mode", "off" }, { "geo-auto-update", false },
                { "profile", new Dictionary<string, object> { { "store-selected", false }, { "store-fake-ip", false } } },
                { "dns", new Dictionary<string, object> { { "enable", false } } },
                { "proxies", new object[] { proxy } },
                { "rules", new[] { "MATCH,awg" } }
            };
            return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(cfg);
        }
    }

    // The AmneziaWG core is installed on demand: in the background right after an Amnezia key is added, or at the first
    // connection. One download at a time.
    public static class MihomoCore
    {
        static readonly object lk = new object();
        public static string Ensure(Settings st)
        {
            lock (lk)
            {
                if (File.Exists(MihomoProcess.Exe(st))) return null;
                Logger.Write(LogLevel.Info, "AmneziaWG core (mihomo) is not installed - downloading from GitHub (MetaCubeX/mihomo)");
                string res;
                try { res = Updater.UpdateMihomo(st, s => { }); } catch (Exception ex) { res = ex.Message; }
                Logger.Write(LogLevel.Info, "mihomo: " + res);
                return File.Exists(MihomoProcess.Exe(st)) ? null : res;
            }
        }
        public static void EnsureAsync(Settings st)
        {
            if (File.Exists(MihomoProcess.Exe(st))) return;
            var copy = st.Clone();
            System.Threading.ThreadPool.QueueUserWorkItem(_ => { try { Ensure(copy); } catch { } });
        }
    }

    // mihomo.exe: like XrayProcess, without output redirection, so it survives a self-update of RouteX
    public class MihomoProcess
    {
        Process p;
        public event Action<int> Exited;
        public bool Running { get { try { return p != null && !p.HasExited; } catch { return false; } } }
        public int Pid { get { try { return p != null && !p.HasExited ? p.Id : 0; } catch { return 0; } } }

        public static string Exe(Settings st) { return Path.Combine(st.MihomoPath, "mihomo.exe"); }

        static readonly HashSet<int> live = new HashSet<int>();
        public static bool IsOurs(int pid) { lock (live) return live.Contains(pid); }
        static void Track(Process pr) { try { lock (live) live.Add(pr.Id); } catch { } }
        static void Untrack(Process pr) { try { lock (live) live.Remove(pr.Id); } catch { } }

        public string Start(Settings st, string config, string cfgFile)
        {
            Stop();
            string exe = Exe(st);
            if (!File.Exists(exe)) return "mihomo.exe not found (AmneziaWG core)";
            File.WriteAllText(cfgFile, config, new UTF8Encoding(false));
            // -d: its home (cache) in the mihomo folder, not in the user profile
            var psi = new ProcessStartInfo(exe, "-d \"" + st.MihomoPath.TrimEnd('\\') + "\" -f \"" + cfgFile + "\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = st.MihomoPath };
            try
            {
                var pr = new Process { StartInfo = psi, EnableRaisingEvents = true };
                pr.Exited += (s, e) => { Untrack(pr); int c = -1; try { c = pr.ExitCode; } catch { } if (pr == p) { var h = Exited; if (h != null) h(c); } };
                p = pr; // before Start: an instant exit must still be ours
                try { pr.Start(); } catch { p = null; throw; }
                Track(pr);
                return null;
            }
            catch (Exception ex) { return "mihomo: " + ex.Message; }
        }

        // the config is checked by mihomo itself before the start ("-t"): a clear error instead of "exited at once"
        public static string Test(Settings st, string cfgFile)
        {
            try
            {
                var r = Proc.Run(Exe(st), "-t -d \"" + st.MihomoPath.TrimEnd('\\') + "\" -f \"" + cfgFile + "\"", 15000);
                if (r.Exit == 0) return null;
                string line = ((r.Out ?? "") + "\n" + (r.Err ?? "")).Split('\n').Select(x => x.Trim()).LastOrDefault(x => x.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0) ?? "";
                return "mihomo rejected the AmneziaWG config" + (line != "" ? ": " + line : "");
            }
            catch { return null; } // the check itself failed: let the start decide
        }

        public void Attach(Process pr)
        {
            pr.EnableRaisingEvents = true;
            Track(pr);
            pr.Exited += (s, e) => { Untrack(pr); int c = -1; try { c = pr.ExitCode; } catch { } if (pr == p) { var h = Exited; if (h != null) h(c); } };
            p = pr;
        }

        public void Stop()
        {
            var pr = p; p = null;
            if (pr == null) return;
            try { if (!pr.HasExited) { pr.Kill(); pr.WaitForExit(3000); } } catch { }
            Untrack(pr);
            try { pr.Dispose(); } catch { }
        }
    }
}
