using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using static NetProbe.Tr;

namespace NetProbe
{
    // Long-lived monitor probes. Before 4.0.7 every poll started a new curl copy (netprobe-vpn.exe /
    // netprobe-direct.exe), i.e. a new process every couple of seconds. Now one small agent process per
    // channel (bin\agent\netprobe-vpn.exe / netprobe-direct.exe, source in ProbeAgent\) stays running and
    // takes one request per stdin line. The process names are unchanged, so WireSock / sing-box route them
    // like the curl copies. Anything the agent cannot do (redirects, downloads, uploads, DoH, https proxies)
    // and any agent failure fall back to curl.
    public sealed class ProbeAgentHost
    {
        public const string ResourceName = "RouteX.ProbeAgent.exe";
        public static string Dir { get { return Path.Combine(CurlProber.WorkDir, "bin", "agent"); } }
        public static string PathOf(string name) { return Path.Combine(Dir, name); }

        static readonly Dictionary<string, ProbeAgentHost> hosts = new Dictionary<string, ProbeAgentHost>(StringComparer.OrdinalIgnoreCase);
        static byte[] image;
        static bool imageLoaded;

        static byte[] Image()
        {
            if (imageLoaded) return image;
            imageLoaded = true;
            try
            {
                using (var s = typeof(ProbeAgentHost).Assembly.GetManifestResourceStream(ResourceName))
                {
                    if (s == null) return null;
                    var m = new MemoryStream(); s.CopyTo(m); image = m.ToArray();
                }
            }
            catch { image = null; }
            return image;
        }

        public static ProbeAgentHost For(string name)
        {
            lock (hosts)
            {
                ProbeAgentHost h;
                if (!hosts.TryGetValue(name, out h)) hosts[name] = h = new ProbeAgentHost(name);
                return h;
            }
        }

        public static void StopAll()
        {
            List<ProbeAgentHost> l; lock (hosts) l = new List<ProbeAgentHost>(hosts.Values);
            foreach (var h in l) try { h.Stop(); } catch { }
        }

        readonly string name;
        readonly object lk = new object();
        Process proc;
        StreamWriter input;
        BlockingCollection<string> lines;
        int startFails;
        DateTime retryAfter = DateTime.MinValue;
        bool installed, loggedStart;

        ProbeAgentHost(string name) { this.name = name; }

        public bool Available { get { return Image() != null && DateTime.Now >= retryAfter; } }

        bool Install()
        {
            if (installed) return true;
            var img = Image();
            if (img == null) return false;
            string path = PathOf(name);
            Directory.CreateDirectory(Dir);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    if (File.Exists(path) && SameBytes(path, img)) { installed = true; return true; }
                    File.WriteAllBytes(path, img);
                    installed = true;
                    Logger.Write(LogLevel.Info, Tx("Пробник: ") + path);
                    return true;
                }
                catch (IOException)
                {
                    // an agent of a previous RouteX version still holds the file: it exits with its parent, help it along
                    foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name)))
                    {
                        try { if (string.Equals(ProcInfo.ImagePath(p.Id), path, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(2000); } } catch { }
                        p.Dispose();
                    }
                }
                catch (Exception ex) { Logger.Write(LogLevel.Warn, Tx("Не удалось создать ") + path + " (" + ex.Message + ")"); return false; }
            }
            return false;
        }

        static bool SameBytes(string path, byte[] img)
        {
            var fi = new FileInfo(path);
            if (fi.Length != img.Length) return false;
            var b = File.ReadAllBytes(path);
            for (int i = 0; i < b.Length; i++) if (b[i] != img[i]) return false;
            return true;
        }

        bool EnsureRunning()
        {
            if (proc != null)
            {
                bool alive; try { alive = !proc.HasExited; } catch { alive = false; }
                if (alive) return true;
                Cleanup();
            }
            if (DateTime.Now < retryAfter || !Install()) return false;
            try
            {
                var psi = new ProcessStartInfo(PathOf(name), "--parent " + Process.GetCurrentProcess().Id)
                {
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true, RedirectStandardOutput = true, StandardOutputEncoding = new UTF8Encoding(false),
                    WorkingDirectory = Dir
                };
                var p = Process.Start(psi);
                var q = new BlockingCollection<string>();
                var reader = p.StandardOutput;
                new Thread(() =>
                {
                    try { string l; while ((l = reader.ReadLine()) != null) q.Add(l); } catch { }
                    try { q.CompleteAdding(); } catch { }
                }) { IsBackground = true, Name = "probe-agent-" + name }.Start();
                proc = p; lines = q;
                input = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                string ready;
                if (!q.TryTake(out ready, 8000) || ready == null || !ready.StartsWith("READY")) throw new Exception("no READY from agent");
                startFails = 0;
                if (!loggedStart) { loggedStart = true; Logger.Write(LogLevel.Info, Tx("Пробник запущен: ") + name + " (pid " + p.Id + ")"); }
                return true;
            }
            catch (Exception ex)
            {
                Cleanup();
                startFails++;
                if (startFails >= 3) { retryAfter = DateTime.Now.AddMinutes(10); startFails = 0; }
                Logger.Write(LogLevel.Warn, Tx("Пробник ") + name + Tx(" не запустился (") + ex.Message + Tx(") — опрос через curl"));
                return false;
            }
        }

        void Cleanup()
        {
            try { if (input != null) input.Dispose(); } catch { }
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
            try { if (proc != null) proc.Dispose(); } catch { }
            proc = null; input = null; lines = null;
        }

        // on exit: no lock (a request may be in flight); the agent also quits by itself when RouteX exits
        public void Stop() { var p = proc; try { if (p != null && !p.HasExited) p.Kill(); } catch { } }

        // One request, one result line split on tabs; null = the agent is unavailable or failed (caller falls back to curl)
        public string[] Request(string request, int waitMs)
        {
            lock (lk)
            {
                if (!EnsureRunning()) return null;
                try
                {
                    input.WriteLine(request);
                    string l;
                    var sw = Stopwatch.StartNew();
                    while (lines.TryTake(out l, (int)Math.Max(1, waitMs - sw.ElapsedMilliseconds)))
                    {
                        if (l.StartsWith("R\t")) return l.Split('\t');
                    }
                    throw new Exception(lines.IsAddingCompleted ? "agent exited" : "no answer in " + waitMs + " ms");
                }
                catch (Exception ex)
                {
                    Logger.Write(LogLevel.Warn, Tx("Пробник ") + name + ": " + ex.Message + Tx(" — перезапуск"));
                    Cleanup();
                    return null;
                }
            }
        }
    }

    // curl prober whose monitor requests go to the long-lived agent of the same process name
    public class AgentProber : CurlProber
    {
        public string AgentName;
        public Func<bool> UseAgent; // VPN: only while the running tunnel config routes the agent

        static bool AgentProxy(string p)
        {
            if (string.IsNullOrEmpty(p)) return true;
            int i = p.IndexOf("://", StringComparison.Ordinal);
            if (i < 0) return true;
            switch (p.Substring(0, i).ToLowerInvariant()) { case "http": case "socks5": case "socks5h": case "socks4": case "socks4a": return true; default: return false; }
        }

        public override ProbeResult Http(string url, int t, bool follow, bool discard, CancelFlag cf)
        {
            if (follow || discard || ViaShell || string.IsNullOrEmpty(AgentName) || !AgentProxy(Proxy)) return base.Http(url, t, follow, discard, cf);
            bool use = true; try { if (UseAgent != null) use = UseAgent(); } catch { }
            var host = ProbeAgentHost.For(AgentName);
            if (!use || !host.Available) return base.Http(url, t, follow, discard, cf);
            if (cf != null && cf.Cancelled) return new ProbeResult { Error = "Cancelled" };
            string req = string.Join("\t", new[] { url.Replace("\t", ""), t.ToString(CultureInfo.InvariantCulture), NoProxy ? "" : (Proxy ?? ""), BindIp ?? "", (!string.IsNullOrEmpty(BindIp) ? 0 : IpFamily).ToString(CultureInfo.InvariantCulture) });
            var f = host.Request(req, t * 1000 + 4000);
            if (f == null || f.Length < 12) return base.Http(url, t, follow, discard, cf);
            var r = new ProbeResult();
            r.Code = (int)Math.Max(0, D(f[1]));
            r.Dns = D(f[2]); r.Connect = D(f[3]); r.Tls = D(f[4]); r.Ttfb = D(f[5]); r.Total = D(f[6]); r.Lat = D(f[7]);
            r.RemoteIp = f[8]; r.SizeDown = Math.Max(0, D(f[9]));
            if (r.Total > 0) r.SpeedDown = r.SizeDown / (r.Total / 1000.0);
            r.Error = f[10];
            try { r.Body = Encoding.UTF8.GetString(Convert.FromBase64String(f[11])); } catch { r.Body = ""; }
            ParseTrace(r);
            r.Exit = r.Error == "" ? 0 : 1;
            r.Ok = r.Error == "" && r.Code > 0 && r.Code < 500;
            if (r.Error == "" && !r.Ok) r.Error = "HTTP " + r.Code;
            return r;
        }
    }
}
