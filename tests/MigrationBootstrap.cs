// Exercises the production migration source in real subprocesses without the application's VPN/UI startup.
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
[assembly: System.Reflection.AssemblyProduct("RouteX")]
[assembly: System.Reflection.AssemblyFileVersion("3.5.1.0")]
namespace NetProbe
{
    public class Settings { public static string Root { get { return Path.GetDirectoryName(Application.ExecutablePath); } } public bool Autorun; }
    public enum LogLevel { Warn, Error }
    public static class Logger { public static void Write(LogLevel level,string message) {} }
    public static class ServerEntry { public static string MakeId(string value) { using(var md5=MD5.Create()) return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant(); } }
    public static class ProcInfo { public static string ImagePath(int id) { try{using(var p=Process.GetProcessById(id))return p.MainModule.FileName;}catch{return "";} } }
    public static class Proc
    {
        public sealed class Res { public int Exit=1; public string Out="",Err=""; }
        public static Encoding Oem { get { return Encoding.Default; } }
        public static Res Run(string file,string args,int timeout,Encoding enc) { throw new Exception("System integration must not run in the bootstrap test."); }
    }
    class MigrationBootstrap
    {
        [STAThread] static int Main(string[] args)
        {
            try
            {
                if(NameMigration.HandleHelper(args))return Environment.ExitCode;
                // The production helper must wait for this launcher to release its installation mutex and exit.
                string key="RouteX-Migration-Test-"+ServerEntry.MakeId(Settings.Root);
                using(var mutex=new Mutex(true,key))
                {
                    int install=Array.IndexOf(args,"--test-install-root");
                    if(install>=0) typeof(NameMigration).GetProperty("LaunchRoot").GetSetMethod(true).Invoke(null,new object[]{args[install+1]});
                    if(NameMigration.Bootstrap(args))return 0;
                    if(NameMigration.LegacyFallback){File.WriteAllText(Path.Combine(Settings.Root,"fallback-ready.txt"),"legacy fallback");return 0;}
                    int i=Array.IndexOf(args,"--migration-plan");
                    if(i<0)throw new Exception("Canonical child did not receive its transaction.");
                    string file=args[i+1];var p=new JavaScriptSerializer().Deserialize<NameMigration.Plan>(File.ReadAllText(file));
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(file),"ready.txt"),p.Token+Environment.NewLine+"clean");
                    File.WriteAllText(Path.Combine(Settings.Root,"bootstrap-ready.txt"),Path.GetFileName(Application.ExecutablePath));
                    Thread.Sleep(1800);return 0;
                }
            }catch(Exception ex){File.WriteAllText(Path.Combine(Settings.Root,"bootstrap-error.txt"),ex.ToString());return 1;}
        }
    }
}
