using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace NetProbe
{
    public static class EmbeddedBrowser
    {
        static bool initialized;
        public static void Initialize()
        {
            if (initialized) return; initialized = true;
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
                string name = new AssemblyName(e.Name).Name;
                if (name != "Microsoft.Web.WebView2.Core" && name != "Microsoft.Web.WebView2.WinForms") return null;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RouteX.Browser." + name + ".dll")) {
                    if (stream == null) return null;
                    using (var bytes = new MemoryStream()) { stream.CopyTo(bytes); return Assembly.Load(bytes.ToArray()); }
                }
            };
        }
        public static string LoaderDirectory()
        {
            string dir = Path.Combine(Settings.Sub("runtime"), "webview2", IntPtr.Size == 8 ? "x64" : "x86");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "WebView2Loader.dll");
            byte[] bytes;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RouteX.Browser.Loader." + (IntPtr.Size == 8 ? "x64" : "x86")))
            using (var buffer = new MemoryStream()) { stream.CopyTo(buffer); bytes = buffer.ToArray(); }
            bool matches = false;
            if (File.Exists(path)) using (var sha = SHA256.Create()) matches = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))) == Convert.ToBase64String(sha.ComputeHash(bytes));
            if (!matches) File.WriteAllBytes(path, bytes);
            return dir;
        }
        public static string Html()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RouteX.Browser.Interface.html"))
            using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
        }
    }
}
