using System;
using System.IO;
using System.Text;

namespace NetProbe
{
    // --awgtest <out> (CI): an Amnezia vpn:// key → AmneziaWG tunnel → mihomo config, without touching the profile.
    // The keys are synthetic (documentation addresses, made-up keys). Writes the mihomo config next to the report so CI
    // can check it with the real mihomo.exe ("-t").
    static class AwgSelfTest
    {
        const string Key = "vpn://AAADmHjafZN9T-JAEMa_SrPxT8Td7QuFqIlR9NCAVYyC1pi1HaRnu223CwiE7347rd6ZS-4oSWd-zzPTzHa6JVEutUgkqIr0rKftn9ykRGQSNonYzwuQy0KSlkW-wp61Jamo9IspmCVv6N7uyG7Xsv7RQ6zesB5vWDtmKDLaQTjmmHhdjP9uGpImCU0WkqeB1KBmIoLnMJQncaygqqwji9G232ZtfmBzw89GY8P2gtvB8OR2-mLSlrU37p9ej84-c2MKVLIUGq5gbbzR-YZn9hUr38uz8pSt3ZV7Mw_oqnz3YOjnPPIeWTEp7azvH5nay8jUOBhkiawfX8fiw8Quxj9YTfG3z-sbQo6Q8a7j0y7vIrEN8SlnNnU6tcUxgNMuc2zmMN-QAXY6fLXoR9Q0opSx40NlMe_YyOb_FAAoPI5g8ZomUTNQd33Bp0X5s3D0xEvdqQ9s1ikvsknOX73FdGXfrMrlOb-c0PyoPgyo5kJB3FQ_Ppxv4EGn8HA_j-xUPU7SdDoZuYLfu7-1u_56uLnB6pM0zVcQDwJ8FbRdXwe0ZfV6BzhUX8ZFnkhdj2YbkTG73em5zOcoB2b5kkqD1FcAhUiTJaDTxenMRoRknld6JDJoVuBbi0YucqVRqvshyPSisTK744Vkh2uFJtyn2oRAKyErpC-FynWO2iIuzAY_GzGGmVik-vQ_ixxDFamk0EmOHwO5g0pbFailMaMqq2bB2_X1iXiD8Hxq9DUY4m9jkd0vdhISLg";
        const string Key3 = "vpn://AAAEanjanVNNb-IwEP0rkbVHksb5oBSJAwL6qbJpqVpoXSE3GYpLYgfHQAHx31djKO1e9rDikDfvzTwPM_aWpEoaLiToijSdl-13TJoO4YWEjeAuX70HpOYQvnonTWdLcl6ZcarkRGBMtozsA0aajLxcSQN6wlN4ZUy2s0xDVTkth_pew6NeeBIGjMluf-C0nF_J_dVt-3407vYHNefXoNf53e8eYsZkosWSG7iBtdNy0vNNUIQ3dD6bd-cduo5X8d008VfzWR1uGypI68-0HM7DotdoMSavU6flhAgKIe3xFvNPpBEPKLLYyyA4ovCIoi90afMQYJplwoP1JSZFCIBnoBOtDKRGKPlfHXeUNCBNwrNMyPd2lgm0sq271MeW72EG6_bEgH4QBVjFd2lwlJBVC4PduaeW_IDU_FUQ-y5tYMENQMlzsYTvotilMWPyln9ecplVUz6DtjFQlMbuL3b3J3GZqeJBc5GDRkFJ3Keo-FsOHaVmAiw7mTCGyksCoPEqJIu3XKT70ZytL4JROf8oIzOs5_GoAXRyOr8ohip4qy9Gq_BuNV-eB9dDX7XsRYBqyjVk--rnp_MNPJkcnh6naZjr52Gej4b9mAeP8VF76K1vN3dY3c5ztYLsKsG-fM_-Tvya02ye4B_qyaxUQuIEAj_0fI_S0DtrRhGuOAFdiQr3chwY5sVuiKNipMbIVFWmzwuwt_-HgxVLpQ0jaFZjpDALm0SDhr9Xj-7jGUA5tv57H3sC2eGzQw98Z1EUYmg0lxVy41Iro1BZZKVNxDhV-XiJvkqiFHqU7HavNYdkMOGL3HT-9cIzqFItSnMotpSsKGJ6GnjBmWc_cXRQAqvYkVKkvmaB9I9RkN0fyM5h4g"; // AmneziaWG 3.x, no description, Amnezia DNS, keepalive range
        const string ServiceKey = "vpn://AAAAgHjaJYxBDoMwDAS_gnyGD_TWC59AKLLCAhZqiBK3KqD-vU64zs7ORRzF-T3MstCjuSgjfcTD6RFhgPgVcAp3cwLo1xp46-omVq56eW84ivmts6WUJSBlY8NoZEL2SaLKHor2vINNX4N_CVErIA";

        public static void Run(string outFile)
        {
            var sb = new StringBuilder();
            bool ok = true;
            Action<bool, string> check = (cond, what) => { sb.AppendLine((cond ? "ok   " : "FAIL ") + what); if (!cond) ok = false; };
            try
            {
                string err;
                var e = TunnelProto.FromLink(Key, out err);
                check(e != null, "decode vpn:// key " + (err ?? ""));
                if (e != null)
                {
                    sb.AppendLine(e.Awg);
                    check(e.Protocol == "awg" && e.Address == "203.0.113.7" && e.Port == 51820, "endpoint " + e.Endpoint);
                    check(e.Name == "Test server", "name " + e.Name);
                    var c = AwgConf.Parse(e.Awg);
                    check(c.I("Jc") == "4" && c.I("Jmin") == "10" && c.I("Jmax") == "50", "Jc / Jmin / Jmax");
                    check(c.I("S1") == "107" && c.I("S2") == "69", "S1 / S2 taken from the JSON fields");
                    check(c.I("H1") == "100000-200000" && c.I("I1") == "<b 0xc2000000011><r 16>", "H1 range and I1");
                    check(c.I("DNS") == "1.1.1.1, 1.0.0.1", "DNS placeholders " + c.I("DNS"));
                    check(c.I("MTU") == "1376", "MTU from the JSON");
                    check(c.Missing() == null && c.HasObfuscation && !c.IsV3, "complete AWG config");
                    var cs = new Settings(); cs.SocksPort = 10811;
                    string json = MihomoConfig.Build(cs, c);
                    File.WriteAllText(outFile + ".mihomo.yaml", json, new UTF8Encoding(false));
                    check(json.Contains("\"amnezia-wg-option\"") && json.Contains("\"jc\":4") && json.Contains("\"s1\":107") && json.Contains("\"h1\":\"100000-200000\""), "mihomo AWG options");
                    check(json.Contains("\"remote-dns-resolve\":true") && json.Contains("\"bind-address\":\"127.0.0.1\"") && json.Contains("\"authentication\""), "mihomo: DNS in the tunnel, local socks with a login");
                    string again; check(AmneziaLink.SetText(e, e.Awg, out again) && again == null, "text editor round trip");
                }
                var e3 = TunnelProto.FromLink(Key3, out err);
                check(e3 != null, "decode AmneziaWG 3.x key " + (err ?? ""));
                if (e3 != null)
                {
                    var c3 = AwgConf.Parse(e3.Awg);
                    check(e3.Name == "Amnezia 203.0.113.9" && e3.Port == 443, "name from the endpoint: " + e3.Name);
                    check(c3.IsV3 && c3.I("RekeyAfterTime") == "100-120" && c3.I("DisableCookies") == "off", "3.x options kept");
                    var cs3 = new Settings(); cs3.SocksPort = 10811;
                    string j3 = MihomoConfig.Build(cs3, c3);
                    File.WriteAllText(outFile + ".mihomo3.yaml", j3, new UTF8Encoding(false));
                    check(j3.Contains("\"version\":3") && j3.Contains("\"header-protection-key\"") && j3.Contains("\"rekey-after-time\":\"100-120\"") && j3.Contains("\"random-trailers\":true") && !j3.Contains("disable-cookies"), "mihomo 3.x options");
                    check(j3.Contains("\"persistent-keepalive\":25") && j3.Contains("\"dns\":[\"172.29.172.254\",\"1.0.0.1\"]") && j3.Contains("\"mtu\":1280"), "keepalive range, Amnezia DNS, MTU");
                }
                var svc = TunnelProto.FromLink(ServiceKey, out err);
                check(svc == null && err != null && err.Contains("Amnezia service"), "service key explained: " + err);
                var bad = TunnelProto.FromLink("vpn://AAAA!!", out err);
                check(bad == null && err != null, "broken key: " + err);
                var vless = TunnelProto.FromLink("vless://00000000-0000-0000-0000-000000000000@example.invalid:443?security=reality&sni=example.invalid&pbk=AAAA&sid=01&type=tcp#demo", out err);
                check(vless != null && vless.Protocol == "vless", "vless links unchanged");
            }
            catch (Exception ex) { sb.AppendLine("EXCEPTION " + ex); ok = false; }
            sb.AppendLine(ok ? "AWG-SELFTEST: OK" : "AWG-SELFTEST: FAILED");
            File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
        }
    }
}
