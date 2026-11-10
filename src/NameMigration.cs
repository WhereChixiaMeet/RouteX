using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

namespace NetProbe
{
    // Preserve the data directory and legacy network identities. Only the main executable changes name.
    public static class NameMigration
    {
        public sealed class Plan
        {
            public string Root, Id, Token, Hash, Legacy;
            public int ParentPid, ReadyTimeoutMs = 90000;
            public long ParentStarted;
            public string[] Arguments = new string[0];
            public bool RestartOnFailure = true;
        }
        static Plan pending;
        static readonly object integrationLock = new object();
        public static bool Pending { get { return pending != null; } }
        public static bool LegacyFallback { get; private set; }
        public static string LaunchRoot { get; private set; }
        static JavaScriptSerializer Json() { return new JavaScriptSerializer(); }
        static string Full(string p) { string full = Path.GetFullPath(p); return full.Length > Path.GetPathRoot(full).Length ? full.TrimEnd(Path.DirectorySeparatorChar) : full; }
        static bool Same(string a, string b) { return string.Equals(Full(a), Full(b), StringComparison.OrdinalIgnoreCase); }
        public static string Canonical(string root) { return Path.Combine(Full(root), "RouteX.exe"); }
        static string Folder(Plan p) { return Path.Combine(Full(p.Root), "config", "runtime", "name-migration", p.Id); }
        static string PlanFile(Plan p) { return Path.Combine(Folder(p), "plan.json"); }
        static string Hash(string file) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        static string Arg(string[] args, string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        static string[] SafeArgs(string[] args) { return args.Where(a => a == "--updated" || a == "--minimized" || a == "--reapply").Distinct().ToArray(); }
        internal static string ChooseRoot(string sourceRoot, IEnumerable<string> candidates)
        {
            if (File.Exists(Path.Combine(sourceRoot, "config", "settings.ini"))) return Full(sourceRoot);
            var roots = candidates.Where(r => !string.IsNullOrWhiteSpace(r) && File.Exists(Path.Combine(r, "config", "settings.ini"))).Select(Full).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (roots.Length > 1) throw new IOException("Multiple RouteX installations were found. Run this executable from the installation folder you want to update.");
            return roots.Length == 1 ? roots[0] : Full(sourceRoot);
        }
        static IEnumerable<string> RunningRoots()
        {
            var roots = new List<string>();
            foreach (string name in new[] { "NetProbe", "RouteX" }) foreach (var process in Process.GetProcessesByName(name)) using (process)
            {
                if (process.Id == Process.GetCurrentProcess().Id) continue;
                string path = ProcInfo.ImagePath(process.Id);
                if (!string.IsNullOrWhiteSpace(path) && IsOurLegacy(path)) roots.Add(Path.GetDirectoryName(path));
            }
            return roots;
        }
        static IEnumerable<string> ScheduledRoots()
        {
            var roots = new List<string>(); object service = null, folder = null, tasks = null;
            try
            {
                var type = Type.GetTypeFromProgID("Schedule.Service"); service = Activator.CreateInstance(type);
                type.InvokeMember("Connect", BindingFlags.InvokeMethod, null, service, null);
                folder = type.InvokeMember("GetFolder", BindingFlags.InvokeMethod, null, service, new object[] { "\\" });
                tasks = folder.GetType().InvokeMember("GetTasks", BindingFlags.InvokeMethod, null, folder, new object[] { 1 });
                foreach (object task in (IEnumerable)tasks)
                {
                    try
                    {
                        string name = Convert.ToString(task.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, task, null));
                        if (!(name == "NetProbe" || name.StartsWith("NetProbe ", StringComparison.OrdinalIgnoreCase) || name.StartsWith("RouteX ", StringComparison.OrdinalIgnoreCase))) continue;
                        string xml = Convert.ToString(task.GetType().InvokeMember("Xml", BindingFlags.GetProperty, null, task, null));
                        var doc = new XmlDocument { XmlResolver = null }; doc.LoadXml(xml);
                        var node = doc.SelectSingleNode("//*[local-name()='Actions']/*[local-name()='Exec']/*[local-name()='Command']");
                        if (node == null) continue; string exe = node.InnerText.Trim().Trim('"');
                        if (IsOurLegacy(exe)) roots.Add(Path.GetDirectoryName(exe));
                    }
                    catch { }
                    finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(task); }
                }
            }
            catch { }
            finally { foreach (object obj in new[] { tasks, folder, service }) if (obj != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(obj); }
            return roots;
        }
        public static void ResolveInstallation(string[] args)
        {
            string source = Path.GetDirectoryName(Application.ExecutablePath); LaunchRoot = Full(source);
            if (args.Any(a => a == "--migration-plan" || a == "--migration-skip" || a == "--shots" || a == "--selftest" || a == "--speedtest") || File.Exists(Path.Combine(source, "config", "settings.ini"))) return;
            var running = RunningRoots().ToArray();
            LaunchRoot = ChooseRoot(source, running.Length > 0 ? running : ScheduledRoots());
        }
        internal static void Validate(Plan p)
        {
            Guid id, token;
            if (p == null || !Guid.TryParseExact(p.Id, "N", out id) || !Guid.TryParseExact(p.Token, "N", out token)) throw new InvalidDataException("Invalid migration identity.");
            if (string.IsNullOrWhiteSpace(p.Root) || !Directory.Exists(p.Root) || !Path.IsPathRooted(p.Root)) throw new InvalidDataException("Invalid installation directory.");
            if (!Same(Path.GetDirectoryName(p.Legacy), p.Root) || !p.Legacy.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Legacy executable must be in the same installation directory.");
            if (p.ReadyTimeoutMs < 250 || p.ReadyTimeoutMs > 120000 || p.ParentPid < 0) throw new InvalidDataException("Invalid migration timeout or parent.");
            if (p.Arguments == null || !p.Arguments.SequenceEqual(SafeArgs(p.Arguments))) throw new InvalidDataException("Invalid restart arguments.");
            if (p.Hash == null || p.Hash.Length != 64 || p.Hash.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid payload digest.");
        }
        internal static Plan Prepare(string source, string current, string[] arguments)
        {
            string root = Path.GetDirectoryName(Full(current));
            var p = new Plan { Root = root, Legacy = Full(current), Id = Guid.NewGuid().ToString("N"), Token = Guid.NewGuid().ToString("N"), Arguments = SafeArgs(arguments), Hash = Hash(source) };
            using (var process = Process.GetCurrentProcess()) { p.ParentPid = process.Id; p.ParentStarted = process.StartTime.ToUniversalTime().Ticks; }
            Validate(p); Directory.CreateDirectory(Folder(p));
            File.Copy(source, Path.Combine(Folder(p), "payload.exe"));
            File.Copy(source, Path.Combine(Folder(p), "runner.exe"));
            if (Hash(Path.Combine(Folder(p), "payload.exe")) != p.Hash) throw new IOException("Staged update checksum mismatch.");
            File.WriteAllText(PlanFile(p), Json().Serialize(p), new UTF8Encoding(false));
            return p;
        }
        internal static void LaunchPrepared(Plan p)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Folder(p), "runner.exe"), "--migration-helper " + Quote(PlanFile(p))) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = p.Root });
        }
        public static void LaunchUpdate(string source)
        {
            var p = Prepare(source, Application.ExecutablePath, new[] { "--updated" });
            LaunchPrepared(p);
        }
        // Called before the single-instance mutex; helpers must not acquire the application's mutex.
        public static bool HandleHelper(string[] args)
        {
            string file = Arg(args, "--migration-helper"); if (file == null) return false;
            var p = Json().Deserialize<Plan>(File.ReadAllText(file)); Validate(p);
            if (!Same(file, PlanFile(p)) || !Same(Application.ExecutablePath, Path.Combine(Folder(p), "runner.exe"))) throw new InvalidDataException("Unexpected migration helper location.");
            Environment.ExitCode = RunPlan(p); return true;
        }
        public static bool Bootstrap(string[] args)
        {
            string file = Arg(args, "--migration-plan");
            if (file != null)
            {
                var p = Json().Deserialize<Plan>(File.ReadAllText(file)); Validate(p);
                if (!Same(file, PlanFile(p)) || !Same(Application.ExecutablePath, Canonical(p.Root)) || Arg(args, "--migration-token") != p.Token || Hash(Application.ExecutablePath) != p.Hash) throw new InvalidDataException("Invalid migration startup acknowledgement.");
                pending = p; return false;
            }
            if (Array.IndexOf(args, "--migration-skip") >= 0) { LegacyFallback = true; return false; }
            string root = LaunchRoot ?? Path.GetDirectoryName(Application.ExecutablePath);
            if (args.Any(a => a == "--shots" || a == "--selftest" || a == "--speedtest") || Same(Application.ExecutablePath, Canonical(root))) return false;
            string launcher = Same(Path.GetDirectoryName(Application.ExecutablePath), root) ? Application.ExecutablePath : (IsOurLegacy(Path.Combine(root, "NetProbe.exe")) ? Path.Combine(root, "NetProbe.exe") : Canonical(root));
            var plan = Prepare(Application.ExecutablePath, launcher, args);
            LaunchPrepared(plan); return true;
        }
        static void CopyTree(string source, string destination, bool root)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            foreach (string directory in Directory.GetDirectories(source))
            {
                if ((root && Path.GetFileName(directory).Equals("runtime", StringComparison.OrdinalIgnoreCase)) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)), false);
            }
        }
        static void Log(Plan p, string text) { try { File.AppendAllText(Path.Combine(Folder(p), "migration.log"), DateTime.Now.ToString("o") + " " + text + Environment.NewLine); } catch { } }
        static void WaitParent(Plan p)
        {
            if (p.ParentPid == 0) return;
            Process parent;
            try { parent = Process.GetProcessById(p.ParentPid); } catch (ArgumentException) { return; }
            using (parent)
            {
                if (parent.StartTime.ToUniversalTime().Ticks != p.ParentStarted) return;
                if (!parent.WaitForExit(45000)) throw new IOException("The previous application did not exit; no files were replaced.");
            }
        }
        internal static int RunPlan(Plan p)
        {
            Validate(p);
            string dir = Folder(p), target = Canonical(p.Root), payload = Path.Combine(dir, "payload.exe"), backup = Path.Combine(dir, "previous-target.bak");
            string temporary = Path.Combine(p.Root, "RouteX.exe." + p.Id + ".tmp");
            bool installed = false, hadTarget = false; Process child = null;
            try
            {
                WaitParent(p);
                if (Hash(payload) != p.Hash) throw new IOException("Staged update checksum mismatch.");
                CopyTree(Path.Combine(p.Root, "config"), Path.Combine(dir, "config-backup"), true);
                string handoff = Path.Combine(p.Root, "config", "runtime", "handoff.ini");
                if (File.Exists(handoff)) File.Copy(handoff, Path.Combine(dir, "handoff.bak"), true);
                if (File.Exists(p.Legacy)) File.Copy(p.Legacy, Path.Combine(dir, "previous-launcher.bak"), true);
                hadTarget = File.Exists(target);
                File.Copy(payload, temporary, true);
                if (Hash(temporary) != p.Hash) throw new IOException("Update checksum mismatch before replacement.");
                if (hadTarget) File.Replace(temporary, target, backup); else File.Move(temporary, target);
                installed = true;
                child = Process.Start(new ProcessStartInfo(target, string.Join(" ", p.Arguments) + " --migration-plan " + Quote(PlanFile(p)) + " --migration-token " + p.Token) { UseShellExecute = false, WorkingDirectory = p.Root });
                string ack = Path.Combine(dir, "ready.txt"); var watch = Stopwatch.StartNew(); bool ready = false, clean = false;
                while (watch.ElapsedMilliseconds < p.ReadyTimeoutMs)
                {
                    if (child.HasExited) throw new IOException("The new application exited before confirming startup.");
                    if (File.Exists(ack))
                    {
                        string[] lines = File.ReadAllLines(ack);
                        if (lines.Length >= 2 && lines[0] == p.Token) { ready = true; clean = lines[1] == "clean"; break; }
                    }
                    Thread.Sleep(100);
                }
                if (!ready) throw new IOException("The new application did not confirm startup; restoring the previous executable.");
                if (clean && !Same(p.Legacy, target) && File.Exists(p.Legacy))
                {
                    // Only remove the exact old launcher after startup and integration have succeeded.
                    try { if (Hash(p.Legacy) == Hash(Path.Combine(dir, "previous-launcher.bak"))) File.Delete(p.Legacy); }
                    catch (Exception cleanup) { Log(p, "Legacy cleanup postponed: " + cleanup.Message); }
                }
                Log(p, clean ? "Committed: RouteX.exe is ready." : "RouteX.exe is ready; Windows integration continues in the application.");
                return 0;
            }
            catch (Exception ex)
            {
                Log(p, "ROLLBACK: " + ex.Message);
                if (child != null) try { if (!child.HasExited) { child.Kill(); child.WaitForExit(5000); } } catch { }
                try
                {
                    if (installed)
                    {
                        if (hadTarget) File.Copy(backup, target, true); else if (File.Exists(target)) File.Delete(target);
                        CopyTree(Path.Combine(dir, "config-backup"), Path.Combine(p.Root, "config"), false);
                        if (File.Exists(Path.Combine(dir, "handoff.bak"))) File.Copy(Path.Combine(dir, "handoff.bak"), Path.Combine(p.Root, "config", "runtime", "handoff.ini"), true);
                    }
                    if (p.RestartOnFailure && File.Exists(p.Legacy)) Process.Start(new ProcessStartInfo(p.Legacy, "--migration-skip " + string.Join(" ", p.Arguments)) { UseShellExecute = false, WorkingDirectory = p.Root });
                }
                catch (Exception restore) { Log(p, "Rollback requires attention: " + restore.Message); }
                return 1;
            }
            finally { if (child != null) child.Dispose(); try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
        // Every update leaves its staging folder behind (two copies of the exe, the previous exe, a copy of config):
        // useful for a rollback right after the update, dead weight a few days later. Unfinished downloads too.
        public static void CleanupLeftovers()
        {
            try
            {
                string runtime = Path.Combine(Settings.Root, "config", "runtime");
                string keep = pending != null ? Folder(pending) : null;
                string staging = Path.Combine(runtime, "name-migration");
                if (Directory.Exists(staging))
                    foreach (string dir in Directory.GetDirectories(staging))
                        try
                        {
                            if (keep != null && Same(dir, keep)) continue;
                            if ((DateTime.UtcNow - Directory.GetCreationTimeUtc(dir)).TotalDays < 3) continue;
                            Directory.Delete(dir, true);
                        }
                        catch { }
                if (Directory.Exists(runtime))
                    foreach (string dir in Directory.GetDirectories(runtime, "download-*"))
                        try { if ((DateTime.UtcNow - Directory.GetCreationTimeUtc(dir)).TotalDays >= 1) Directory.Delete(dir, true); } catch { }
            }
            catch { }
        }
        public static bool HasLegacyInstance()
        {
            string root = LaunchRoot ?? Path.GetDirectoryName(Application.ExecutablePath);
            foreach (string name in new[] { "NetProbe", "RouteX" }) foreach (var process in Process.GetProcessesByName(name)) using (process)
            { if (process.Id != Process.GetCurrentProcess().Id) { string path = ProcInfo.ImagePath(process.Id); if (!string.IsNullOrWhiteSpace(path) && Same(Path.GetDirectoryName(path), root) && IsOurLegacy(path)) return true; } }
            return false;
        }
        static bool IsOurLegacy(string file)
        {
            if (!File.Exists(file)) return false;
            try { var v = FileVersionInfo.GetVersionInfo(file); return v.ProductName == "RouteX" || v.ProductName == "NetProbe"; } catch { return false; }
        }
        public static string AutorunTaskName(string root) { return "RouteX " + ServerEntry.MakeId(Full(root).ToLowerInvariant()).Substring(0, 12); }
        internal static string[] TaskNames(string root)
        {
            var names = new List<string> { AutorunTaskName(root) };
            foreach (string exe in new[] { "NetProbe.exe", "RouteX.exe" }) names.Add("NetProbe " + ServerEntry.MakeId(Path.Combine(Full(root), exe).ToLowerInvariant()).Substring(0, 6));
            if (Full(root).EndsWith("\\NetProbe", StringComparison.OrdinalIgnoreCase)) names.Add("NetProbe");
            return names.Distinct().ToArray();
        }
        internal static bool IsOurTask(string xml, string root)
        {
            try
            {
                var doc = new XmlDocument { XmlResolver = null }; doc.LoadXml(xml);
                var commands = doc.SelectNodes("//*[local-name()='Actions']/*[local-name()='Exec']/*[local-name()='Command']");
                if (commands.Count != 1 || doc.SelectNodes("//*[local-name()='Actions']/*").Count != 1) return false;
                string exe = commands[0].InnerText.Trim().Trim('"');
                return Same(exe, Canonical(root)) || Same(exe, Path.Combine(root, "NetProbe.exe"));
            }
            catch { return false; }
        }
        internal static Func<string, string, int, Encoding, Proc.Res> TaskCommand = (exe, arguments, timeout, encoding) => Proc.Run(exe, arguments, timeout, encoding);
        public static bool ConfigureAutorun(string root, bool on)
        {
            lock (integrationLock)
            {
                string canonical = Canonical(root), name = AutorunTaskName(root);
                if (on)
                {
                    var existing = TaskCommand("schtasks.exe", "/Query /TN " + Quote(name) + " /XML", 10000, Proc.Oem);
                    if (existing.Exit == 0 && !IsOurTask(existing.Out, root)) return false;
                    string file = Path.Combine(root, "config", "runtime", "routex-autorun.xml"); Directory.CreateDirectory(Path.GetDirectoryName(file));
                    using (var writer = XmlWriter.Create(file, new XmlWriterSettings { Encoding = Encoding.Unicode, Indent = true }))
                    {
                        writer.WriteStartElement("Task", "http://schemas.microsoft.com/windows/2004/02/mit/task"); writer.WriteAttributeString("version", "1.2");
                        writer.WriteStartElement("Triggers"); writer.WriteStartElement("LogonTrigger"); writer.WriteElementString("Enabled", "true"); writer.WriteElementString("Delay", "PT15S"); writer.WriteEndElement(); writer.WriteEndElement();
                        writer.WriteStartElement("Principals"); writer.WriteStartElement("Principal"); writer.WriteAttributeString("id", "Author"); writer.WriteElementString("UserId", System.Security.Principal.WindowsIdentity.GetCurrent().User.Value); writer.WriteElementString("LogonType", "InteractiveToken"); writer.WriteElementString("RunLevel", "HighestAvailable"); writer.WriteEndElement(); writer.WriteEndElement();
                        writer.WriteStartElement("Settings"); writer.WriteElementString("MultipleInstancesPolicy", "IgnoreNew"); writer.WriteElementString("DisallowStartIfOnBatteries", "false"); writer.WriteElementString("StopIfGoingOnBatteries", "false"); writer.WriteElementString("ExecutionTimeLimit", "PT0S"); writer.WriteEndElement();
                        writer.WriteStartElement("Actions"); writer.WriteAttributeString("Context", "Author"); writer.WriteStartElement("Exec"); writer.WriteElementString("Command", canonical); writer.WriteElementString("Arguments", "--minimized"); writer.WriteElementString("WorkingDirectory", Full(root)); writer.WriteEndElement(); writer.WriteEndElement(); writer.WriteEndElement();
                    }
                    var create = TaskCommand("schtasks.exe", "/Create /F /TN " + Quote(name) + " /XML " + Quote(file), 15000, Proc.Oem);
                    if (create.Exit != 0) { Logger.Write(LogLevel.Warn, "RouteX autorun migration: " + create.Err + create.Out); return false; }
                    var verify = TaskCommand("schtasks.exe", "/Query /TN " + Quote(name) + " /XML", 10000, Proc.Oem);
                    if (verify.Exit != 0 || !IsOurTask(verify.Out, root)) return false;
                }
                bool ok = true;
                foreach (string candidate in TaskNames(root).Where(n => !on || n != name))
                {
                    var query = TaskCommand("schtasks.exe", "/Query /TN " + Quote(candidate) + " /XML", 10000, Proc.Oem);
                    if (query.Exit != 0) continue;
                    if (!IsOurTask(query.Out, root)) { ok = false; continue; }
                    if (TaskCommand("schtasks.exe", "/Delete /F /TN " + Quote(candidate), 10000, Proc.Oem).Exit != 0) ok = false;
                }
                return ok;
            }
        }
        internal static bool RewriteShortcut(Type type, object shell, string file, string legacy, string canonical, string backup)
        {
            object shortcut = null;
            try
            {
                shortcut = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { file });
                var st = shortcut.GetType(); string target = Convert.ToString(st.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
                if (string.IsNullOrWhiteSpace(target) || !Same(target, legacy)) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(file, backup);
                string icon = Convert.ToString(st.InvokeMember("IconLocation", BindingFlags.GetProperty, null, shortcut, null));
                int comma = icon.LastIndexOf(','); string iconFile = (comma >= 0 ? icon.Substring(0, comma) : icon).Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(iconFile) && Same(iconFile, legacy)) st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { canonical + (comma >= 0 ? icon.Substring(comma) : ",0") });
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { canonical });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null); return true;
            }
            finally { if (shortcut != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut); }
        }
        static bool UpdateShortcuts(string root, string backup)
        {
            bool ok = true; string legacy = Path.Combine(root, "NetProbe.exe"); object shell = null;
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar") }.Where(Directory.Exists).Distinct().ToList();
            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell"); shell = Activator.CreateInstance(type); int index = 0;
                foreach (string folder in roots)
                {
                    string[] files; try { files = Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories); } catch { ok = false; continue; }
                    foreach (string file in files)
                    {
                        try
                        {
                            RewriteShortcut(type, shell, file, legacy, Canonical(root), Path.Combine(backup, (++index).ToString() + ".lnk"));
                        }
                        catch { ok = false; }
                    }
                }
            }
            catch { ok = false; }
            finally { if (shell != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
            return ok;
        }
        public static void AfterStartup(Settings settings)
        {
            string root = Settings.Root, legacy = Path.Combine(root, "NetProbe.exe"); var plan = pending;
            if (!Same(Application.ExecutablePath, Canonical(root)) || (plan == null && !IsOurLegacy(legacy))) return;
            // Confirm a usable UI before doing Windows integration. Integration failures keep the old launcher;
            // they must not cause a watchdog rollback after tasks or shortcuts have already been updated.
            if (plan != null) File.WriteAllText(Path.Combine(Folder(plan), "ready.txt"), plan.Token + Environment.NewLine + "keep");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool clean = false;
                try
                {
                    string backup = plan != null ? Folder(plan) : Path.Combine(root, "config", "runtime", "name-migration", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(backup);
                    clean = ConfigureAutorun(root, settings.Autorun);
                    clean = UpdateShortcuts(root, Path.Combine(backup, "shortcuts")) && clean;
                    foreach (string old in new[] { plan != null ? plan.Legacy : legacy, legacy }.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        if (!clean || Same(old, Canonical(root)) || !IsOurLegacy(old) || HasLegacyInstance()) continue;
                        string saved = Path.Combine(backup, plan != null && Same(old, plan.Legacy) ? "previous-launcher.bak" : "previous-NetProbe.bak");
                        if (!File.Exists(saved)) File.Copy(old, saved);
                        if (Hash(old) == Hash(saved)) File.Delete(old);
                    }
                }
                catch (Exception ex) { Logger.Write(LogLevel.Warn, "RouteX name migration: " + ex.Message); }
                finally
                {
                    if (plan != null)
                    {
                        Log(plan, clean ? "Windows integration completed." : "Windows integration incomplete; legacy launcher retained.");
                    }
                    if (!clean) Logger.Write(LogLevel.Warn, "RouteX is running; the legacy launcher is retained until Windows integration is complete.");
                }
            });
        }
    }
}
