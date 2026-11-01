// RouteX probe agent: a small long-lived helper started once per monitor channel as
// C:\ProgramData\NetProbe\bin\agent\netprobe-vpn.exe / netprobe-direct.exe.
// The process name is what WireSock / sing-box route on, so the VPN copy goes through the
// tunnel and the direct copy does not, exactly as the curl copies did. Instead of a new curl
// process every poll, RouteX sends one request per line on stdin and reads one result line.
//
// Request : url \t timeoutSec \t proxy \t bindIp \t family(0 v4, 1 v6, 2 any)
// Result  : R \t code \t dns \t connect \t tls \t ttfb \t total \t lat \t remoteIp \t sizeDown \t error \t base64(body)
// Times in milliseconds, -1 = not measured. The agent exits when stdin closes, when the parent
// process exits, or after 10 minutes without requests (RouteX starts it again on demand).
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;

namespace RouteXProbe
{
    static class Agent
    {
        const int IdleExitMs = 10 * 60 * 1000;
        const int MaxBody = 64 * 1024;
        static long lastRequest = Environment.TickCount;

        [STAThread]
        static int Main(string[] args)
        {
            int parent = 0;
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--parent") int.TryParse(args[i + 1], out parent);
            try { ServicePointManager.CheckCertificateRevocationList = false; } catch { }
            var watch = new Thread(() => Watch(parent)) { IsBackground = true };
            watch.Start();
            var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            if (args.Length > 0 && args[0] == "--once")
            {
                output.WriteLine(Format(Probe(args.Length > 1 ? args[1] : "https://www.cloudflare.com/cdn-cgi/trace", 10, args.Length > 2 ? args[2] : "", "", 2)));
                return 0;
            }
            output.WriteLine("READY\t1");
            string line;
            while ((line = input.ReadLine()) != null)
            {
                Interlocked.Exchange(ref lastRequest, Environment.TickCount);
                var f = line.Split('\t');
                if (f.Length == 0 || f[0].Length == 0) continue;
                if (f[0] == "PING") { output.WriteLine("PONG"); continue; }
                int t, fam;
                if (f.Length < 2 || !int.TryParse(f[1], out t)) t = 10;
                if (f.Length < 5 || !int.TryParse(f[4], out fam)) fam = 2;
                Result r;
                try { r = Probe(f[0], Math.Max(1, Math.Min(120, t)), f.Length > 2 ? f[2] : "", f.Length > 3 ? f[3] : "", fam); }
                catch (Exception ex) { r = new Result { Error = "Probe failed: " + ex.Message }; }
                output.WriteLine(Format(r));
                Interlocked.Exchange(ref lastRequest, Environment.TickCount);
            }
            return 0;
        }

        static void Watch(int parent)
        {
            Process p = null;
            try { if (parent > 0) p = Process.GetProcessById(parent); } catch { Environment.Exit(0); }
            while (true)
            {
                Thread.Sleep(2000);
                try { if (p != null && p.HasExited) Environment.Exit(0); } catch { }
                if (unchecked(Environment.TickCount - Interlocked.Read(ref lastRequest)) > IdleExitMs) Environment.Exit(0);
            }
        }

        public class Result
        {
            public int Code;
            public double Dns = -1, Connect = -1, Tls = -1, Ttfb = -1, Total = -1, Lat = -1;
            public string RemoteIp = "", Error = "";
            public long Size;
            public byte[] Body = new byte[0];
        }

        static string N(double v) { return v < 0 ? "-1" : v.ToString("0.###", CultureInfo.InvariantCulture); }
        static string Clean(string s) { return (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '); }

        static string Format(Result r)
        {
            return string.Join("\t", new[] { "R", r.Code.ToString(CultureInfo.InvariantCulture), N(r.Dns), N(r.Connect), N(r.Tls), N(r.Ttfb), N(r.Total), N(r.Lat), Clean(r.RemoteIp), r.Size.ToString(CultureInfo.InvariantCulture), Clean(r.Error), Convert.ToBase64String(r.Body) });
        }

        class ProbeError : Exception { public ProbeError(string m) : base(m) { } }

        public static Result Probe(string url, int timeoutSec, string proxy, string bindIp, int family)
        {
            var r = new Result();
            var sw = Stopwatch.StartNew();
            long deadline = timeoutSec * 1000L;
            Socket sock = null;
            Stream stream = null;
            Timer killer = null;
            bool timedOut = false;
            try
            {
                Uri u;
                if (!Uri.TryCreate(url, UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https")) throw new ProbeError("Unsupported URL");
                bool https = u.Scheme == "https";
                string host = u.IdnHost;
                if (host.StartsWith("[") && host.EndsWith("]")) host = host.Substring(1, host.Length - 2);
                int port = u.Port > 0 ? u.Port : (https ? 443 : 80);
                // the whole request has one deadline, like curl --max-time
                killer = new Timer(_ => { timedOut = true; try { if (sock != null) sock.Close(); } catch { } }, null, (int)deadline, Timeout.Infinite);

                Uri px = null;
                if (!string.IsNullOrEmpty(proxy))
                {
                    string ps = proxy.Contains("://") ? proxy : "http://" + proxy;
                    if (!Uri.TryCreate(ps, UriKind.Absolute, out px)) throw new ProbeError("Proxy error: bad proxy address");
                }
                string pScheme = px != null ? px.Scheme.ToLowerInvariant() : "";
                bool remoteDns = pScheme == "socks5h" || pScheme == "socks4a" || pScheme == "http" || pScheme == "https";
                if (px != null && pScheme != "socks5" && pScheme != "socks5h" && pScheme != "socks4" && pScheme != "socks4a" && pScheme != "http") throw new ProbeError("Proxy error: unsupported proxy type " + pScheme);

                // DNS of the target (not done locally when the proxy resolves it)
                IPAddress[] targets = null;
                if (!remoteDns)
                {
                    IPAddress lit;
                    if (IPAddress.TryParse(host, out lit)) targets = new[] { lit };
                    else
                    {
                        try { targets = Resolve(host, deadline - sw.ElapsedMilliseconds); }
                        catch (ProbeError) { throw; }
                        catch (Exception) { throw new ProbeError("DNS error: host not resolved"); }
                    }
                    targets = Filter(targets, !string.IsNullOrEmpty(bindIp) ? 0 : family);
                    if (targets.Length == 0) throw new ProbeError("DNS error: host not resolved");
                }
                r.Dns = sw.Elapsed.TotalMilliseconds;

                // TCP connect (to the proxy, or to the target)
                IPAddress[] connectTo;
                int connectPort;
                if (px != null)
                {
                    IPAddress pip;
                    if (!IPAddress.TryParse(px.Host.Trim('[', ']'), out pip))
                    {
                        try { connectTo = Resolve(px.Host, deadline - sw.ElapsedMilliseconds); }
                        catch { throw new ProbeError("Could not resolve proxy"); }
                    }
                    else connectTo = new[] { pip };
                    connectPort = px.Port > 0 ? px.Port : 1080;
                }
                else { connectTo = targets; connectPort = port; }
                Exception last = null;
                foreach (var ip in connectTo)
                {
                    if (sw.ElapsedMilliseconds >= deadline) break;
                    var s = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        if (!string.IsNullOrEmpty(bindIp))
                        {
                            IPAddress b;
                            if (!IPAddress.TryParse(bindIp, out b)) throw new ProbeError("Interface bind failed");
                            if (b.AddressFamily != ip.AddressFamily) { s.Close(); continue; }
                            try { s.Bind(new IPEndPoint(b, 0)); } catch { throw new ProbeError("Interface bind failed"); }
                        }
                        sock = s;
                        var ar = s.BeginConnect(ip, connectPort, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne((int)Math.Max(1, deadline - sw.ElapsedMilliseconds))) { timedOut = true; throw new ProbeError("Timeout"); }
                        s.EndConnect(ar);
                        r.RemoteIp = ip.ToString();
                        last = null;
                        break;
                    }
                    catch (ProbeError) { try { s.Close(); } catch { } sock = null; throw; }
                    catch (Exception ex) { last = ex; try { s.Close(); } catch { } sock = null; }
                }
                if (sock == null) throw timedOut ? new ProbeError("Timeout") : new ProbeError("Connection failed" + (last != null ? " (" + last.Message + ")" : ""));
                int rest = (int)Math.Max(1, deadline - sw.ElapsedMilliseconds);
                sock.ReceiveTimeout = rest; sock.SendTimeout = rest;
                stream = new NetworkStream(sock, true);

                if (pScheme == "socks5" || pScheme == "socks5h") Socks5(stream, px, host, targets, port);
                else if (pScheme == "socks4" || pScheme == "socks4a") Socks4(stream, px, host, targets, port);
                else if (pScheme == "http" && https) HttpConnect(stream, host, port);
                r.Connect = sw.Elapsed.TotalMilliseconds - r.Dns;

                if (https)
                {
                    var ssl = new SslStream(stream, false, (a, cert, chain, errors) => errors == SslPolicyErrors.None);
                    try { ssl.AuthenticateAsClient(host, null, SslProtocols.None, false); }
                    catch (AuthenticationException ex) { throw new ProbeError(ex.Message.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0 ? "Certificate error" : "TLS handshake failed"); }
                    catch (Exception ex) { if (timedOut) throw new ProbeError("Timeout"); throw new ProbeError("TLS handshake failed (" + ex.Message + ")"); }
                    stream = ssl;
                    r.Tls = sw.Elapsed.TotalMilliseconds - r.Dns - r.Connect;
                }

                // request: same headers as the curl probe; direct HTTP through an HTTP proxy uses the absolute URL
                string target = pScheme == "http" && !https ? u.AbsoluteUri : u.PathAndQuery;
                string req = "GET " + target + " HTTP/1.1\r\nHost: " + u.Authority + "\r\nUser-Agent: NetProbe/1.0\r\nAccept: */*\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n";
                var rb = Encoding.ASCII.GetBytes(req);
                double pre = sw.Elapsed.TotalMilliseconds;
                stream.Write(rb, 0, rb.Length);
                stream.Flush();

                var buf = new byte[16384];
                var head = new MemoryStream();
                int n = stream.Read(buf, 0, buf.Length);
                if (n <= 0) throw new ProbeError("Empty reply from server");
                double first = sw.Elapsed.TotalMilliseconds;
                r.Ttfb = first;
                r.Lat = first - pre;
                head.Write(buf, 0, n);
                int hEnd;
                while ((hEnd = HeaderEnd(head)) < 0)
                {
                    if (head.Length > 65536) throw new ProbeError("Receive failure (bad header)");
                    n = stream.Read(buf, 0, buf.Length);
                    if (n <= 0) throw new ProbeError("Receive failure (connection reset)");
                    head.Write(buf, 0, n);
                }
                var all = head.ToArray();
                string headers = Encoding.ASCII.GetString(all, 0, hEnd);
                var lines = headers.Split(new[] { "\r\n" }, StringSplitOptions.None);
                var status = lines[0].Split(' ');
                int code;
                if (status.Length < 2 || !status[0].StartsWith("HTTP/") || !int.TryParse(status[1], out code)) throw new ProbeError("Receive failure (bad status line)");
                r.Code = code;
                long length = -1; bool chunked = false;
                foreach (var l in lines)
                {
                    int c = l.IndexOf(':'); if (c <= 0) continue;
                    string k = l.Substring(0, c).Trim(), v = l.Substring(c + 1).Trim();
                    if (k.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) long.TryParse(v, out length);
                    else if (k.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) && v.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0) chunked = true;
                }
                var body = new MemoryStream();
                body.Write(all, hEnd + 4, all.Length - hEnd - 4);
                bool noBody = code == 204 || code == 304 || (code >= 100 && code < 200);
                if (!noBody)
                {
                    while (true)
                    {
                        if (!chunked && length >= 0 && body.Length >= length) break;
                        if (chunked && ChunkedDone(body)) break;
                        if (body.Length >= MaxBody) break;
                        n = stream.Read(buf, 0, buf.Length);
                        if (n <= 0) break;
                        body.Write(buf, 0, n);
                    }
                }
                var raw = body.ToArray();
                r.Body = chunked ? Dechunk(raw) : raw;
                if (r.Body.Length > MaxBody) { var cut = new byte[MaxBody]; Array.Copy(r.Body, cut, MaxBody); r.Body = cut; }
                r.Size = raw.Length;
                r.Total = sw.Elapsed.TotalMilliseconds;
            }
            catch (ProbeError ex) { r.Error = timedOut ? "Timeout" : ex.Message; }
            catch (Exception ex)
            {
                if (timedOut) r.Error = "Timeout";
                else if (ex is IOException && ex.InnerException is SocketException) r.Error = SockErr((SocketException)ex.InnerException);
                else if (ex is SocketException) r.Error = SockErr((SocketException)ex);
                else r.Error = "Receive failure (" + ex.Message + ")";
            }
            finally
            {
                try { if (killer != null) killer.Dispose(); } catch { }
                try { if (stream != null) stream.Dispose(); } catch { }
                try { if (sock != null) sock.Close(); } catch { }
            }
            if (r.Error != "" && r.Code > 0 && r.Lat >= 0 && r.Total < 0) { r.Error = ""; r.Total = sw.Elapsed.TotalMilliseconds; } // headers arrived, body was cut: the answer still counts
            return r;
        }

        static string SockErr(SocketException e)
        {
            switch (e.SocketErrorCode)
            {
                case SocketError.TimedOut: return "Timeout";
                case SocketError.ConnectionReset: case SocketError.ConnectionAborted: return "Receive failure (connection reset)";
                case SocketError.ConnectionRefused: case SocketError.HostUnreachable: case SocketError.NetworkUnreachable: return "Connection failed";
                default: return "Receive failure (" + e.SocketErrorCode + ")";
            }
        }

        static IPAddress[] Resolve(string host, long ms)
        {
            var ar = Dns.BeginGetHostAddresses(host, null, null);
            if (!ar.AsyncWaitHandle.WaitOne((int)Math.Max(1, ms))) throw new ProbeError("Timeout");
            return Dns.EndGetHostAddresses(ar);
        }

        static IPAddress[] Filter(IPAddress[] a, int family)
        {
            var l = new System.Collections.Generic.List<IPAddress>();
            foreach (var ip in a)
            {
                if (family == 0 && ip.AddressFamily != AddressFamily.InterNetwork) continue;
                if (family == 1 && ip.AddressFamily != AddressFamily.InterNetworkV6) continue;
                l.Add(ip);
            }
            return l.ToArray();
        }

        static int HeaderEnd(MemoryStream m)
        {
            var b = m.GetBuffer(); int len = (int)m.Length;
            for (int i = 0; i + 3 < len; i++) if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10) return i;
            return -1;
        }

        static bool ChunkedDone(MemoryStream m)
        {
            var b = m.GetBuffer(); int len = (int)m.Length;
            // last chunk "0\r\n\r\n" at the end of the stream
            return len >= 5 && b[len - 5] == (byte)'0' && b[len - 4] == 13 && b[len - 3] == 10 && b[len - 2] == 13 && b[len - 1] == 10;
        }

        static byte[] Dechunk(byte[] raw)
        {
            var o = new MemoryStream(); int i = 0;
            while (i < raw.Length)
            {
                int e = i; while (e + 1 < raw.Length && !(raw[e] == 13 && raw[e + 1] == 10)) e++;
                if (e + 1 >= raw.Length) break;
                string sz = Encoding.ASCII.GetString(raw, i, e - i).Split(';')[0].Trim();
                int n; if (!int.TryParse(sz, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n) || n <= 0) break;
                i = e + 2;
                int take = Math.Min(n, raw.Length - i);
                o.Write(raw, i, take);
                i += n + 2;
            }
            return o.ToArray();
        }

        static void ReadExact(Stream s, byte[] b, int n)
        {
            int got = 0;
            while (got < n) { int k = s.Read(b, got, n - got); if (k <= 0) throw new ProbeError("Proxy error: connection closed"); got += k; }
        }

        static void Socks5(Stream s, Uri px, string host, IPAddress[] targets, int port)
        {
            string user = "", pass = "";
            if (!string.IsNullOrEmpty(px.UserInfo)) { var ui = px.UserInfo.Split(new[] { ':' }, 2); user = Uri.UnescapeDataString(ui[0]); pass = ui.Length > 1 ? Uri.UnescapeDataString(ui[1]) : ""; }
            bool auth = user != "";
            s.Write(auth ? new byte[] { 5, 2, 0, 2 } : new byte[] { 5, 1, 0 }, 0, auth ? 4 : 3);
            var b = new byte[2]; ReadExact(s, b, 2);
            if (b[0] != 5 || b[1] == 0xFF) throw new ProbeError("Proxy error: SOCKS5 rejected");
            if (b[1] == 2)
            {
                var ub = Encoding.UTF8.GetBytes(user); var pb = Encoding.UTF8.GetBytes(pass);
                var m = new MemoryStream(); m.WriteByte(1); m.WriteByte((byte)ub.Length); m.Write(ub, 0, ub.Length); m.WriteByte((byte)pb.Length); m.Write(pb, 0, pb.Length);
                var a = m.ToArray(); s.Write(a, 0, a.Length);
                ReadExact(s, b, 2); if (b[1] != 0) throw new ProbeError("Proxy error: SOCKS5 authentication failed");
            }
            var q = new MemoryStream(); q.WriteByte(5); q.WriteByte(1); q.WriteByte(0);
            if (targets != null)
            {
                var ip = targets[0]; var ab = ip.GetAddressBytes();
                q.WriteByte(ip.AddressFamily == AddressFamily.InterNetworkV6 ? (byte)4 : (byte)1); q.Write(ab, 0, ab.Length);
            }
            else { var hb = Encoding.ASCII.GetBytes(host); q.WriteByte(3); q.WriteByte((byte)hb.Length); q.Write(hb, 0, hb.Length); }
            q.WriteByte((byte)(port >> 8)); q.WriteByte((byte)port);
            var qa = q.ToArray(); s.Write(qa, 0, qa.Length);
            var h = new byte[4]; ReadExact(s, h, 4);
            if (h[1] != 0) throw new ProbeError(h[1] == 4 ? "DNS error: host not resolved" : "Proxy error: SOCKS5 reply " + h[1]);
            int skip = h[3] == 1 ? 4 : h[3] == 4 ? 16 : -1;
            if (skip < 0) { var l = new byte[1]; ReadExact(s, l, 1); skip = l[0]; }
            var rest = new byte[skip + 2]; ReadExact(s, rest, rest.Length);
        }

        static void Socks4(Stream s, Uri px, string host, IPAddress[] targets, int port)
        {
            var q = new MemoryStream(); q.WriteByte(4); q.WriteByte(1); q.WriteByte((byte)(port >> 8)); q.WriteByte((byte)port);
            IPAddress v4 = null;
            if (targets != null) foreach (var t in targets) if (t.AddressFamily == AddressFamily.InterNetwork) { v4 = t; break; }
            if (targets != null && v4 == null) throw new ProbeError("Proxy error: SOCKS4 needs IPv4");
            q.Write(v4 != null ? v4.GetAddressBytes() : new byte[] { 0, 0, 0, 1 }, 0, 4);
            var ub = Encoding.ASCII.GetBytes(Uri.UnescapeDataString(px.UserInfo ?? "")); q.Write(ub, 0, ub.Length); q.WriteByte(0);
            if (v4 == null) { var hb = Encoding.ASCII.GetBytes(host); q.Write(hb, 0, hb.Length); q.WriteByte(0); }
            var qa = q.ToArray(); s.Write(qa, 0, qa.Length);
            var h = new byte[8]; ReadExact(s, h, 8);
            if (h[1] != 90) throw new ProbeError("Proxy error: SOCKS4 reply " + h[1]);
        }

        static void HttpConnect(Stream s, string host, int port)
        {
            string hp = (host.Contains(":") ? "[" + host + "]" : host) + ":" + port;
            var a = Encoding.ASCII.GetBytes("CONNECT " + hp + " HTTP/1.1\r\nHost: " + hp + "\r\nUser-Agent: NetProbe/1.0\r\nProxy-Connection: Keep-Alive\r\n\r\n");
            s.Write(a, 0, a.Length);
            var m = new MemoryStream(); var one = new byte[1];
            while (HeaderEnd(m) < 0)
            {
                if (m.Length > 16384) throw new ProbeError("Proxy error: bad CONNECT reply");
                int k = s.Read(one, 0, 1); if (k <= 0) throw new ProbeError("Proxy error: connection closed");
                m.WriteByte(one[0]);
            }
            var st = Encoding.ASCII.GetString(m.ToArray()).Split(' ');
            if (st.Length < 2 || st[1] != "200") throw new ProbeError("Proxy error: CONNECT " + (st.Length > 1 ? st[1] : "failed"));
        }
    }
}
