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
            // Prefer the side-by-side assemblies shipped next to the executable:
            // standard probing then loads them from disk and no in-memory
            // Assembly.Load(byte[]) ever runs (behavioral AV heuristics treat
            // in-memory loading as dropper-like activity).
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Microsoft.Web.WebView2.Core.dll"))) return;
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
            string arch = IntPtr.Size == 8 ? "x64" : "x86";
            // Prefer the loader shipped next to the executable (webview2\<arch>\):
            // nothing is written to disk at runtime in that case.
            string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "webview2", arch);
            if (File.Exists(Path.Combine(bundled, "WebView2Loader.dll"))) return bundled;
            // Fallback for the portable single-file layout: extract once into the
            // runtime directory, reusing the file when its hash already matches.
            string dir = Path.Combine(Settings.Sub("runtime"), "webview2", arch);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "WebView2Loader.dll");
            byte[] bytes;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RouteX.Browser.Loader." + arch))
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
