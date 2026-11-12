using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using static NetProbe.Tr;

namespace NetProbe
{
    static class Program
    {
        public static int WM_SHOWME, WM_QUITME;

        static void Crash(Exception ex)
        {
            string text = ex == null ? "Unknown error" : ex.ToString();
            string file = "";
            try { file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NetProbe-crash.txt"); System.IO.File.WriteAllText(file, DateTime.Now + "\r\n" + text); } catch { }
            try { Logger.Write(LogLevel.Error, "Fatal: " + text); } catch { }
            try { MessageBox.Show(Tx("RouteX не смог запуститься:\r\n\r\n") + text + (file != "" ? Tx("\r\n\r\nСохранено в ") + file : ""), "RouteX", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }

#if UI_PREVIEW
        static void RunUiPreview(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException+=(sender,eventArgs)=>{
                try{System.IO.File.AppendAllText(System.IO.Path.Combine(Settings.Sub("runtime"),"interface-error.txt"),DateTime.Now+"\r\n"+eventArgs.Exception+"\r\n\r\n");Logger.Write(LogLevel.Error,"Interface exception: "+eventArgs.Exception);}catch{}
                using(var dialog=new ThreadExceptionDialog(eventArgs.Exception))if(dialog.ShowDialog()==DialogResult.Abort)Application.Exit();
            };
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try { Native.SetProcessDPIAware();using(var g=Graphics.FromHwnd(IntPtr.Zero))Theme.Scale=g.DpiX/96f; } catch { }
            string root=Settings.Root;
            WM_SHOWME=Native.RegisterWindowMessage("RouteX_UI_Preview_Show_"+NameMigration.AutorunTaskName(root));WM_QUITME=Native.RegisterWindowMessage("RouteX_UI_Preview_Quit_"+NameMigration.AutorunTaskName(root));
            if(!System.IO.File.Exists(System.IO.Path.Combine(root,"UI-PREVIEW.txt"))){MessageBox.Show("Extract the UI preview ZIP into its own folder, then run RouteX.exe there.","RouteX UI preview");return;}
            if(System.IO.File.Exists(System.IO.Path.Combine(Settings.Dir,"settings.ini"))&&!Settings.Load().UiPreviewProfile){MessageBox.Show("This folder contains an existing RouteX configuration. Use a separate empty folder for the UI preview. Your configuration was not changed.","RouteX UI preview");return;}
            bool created;
            using(var mutex=new Mutex(true,"RouteX_UI_Preview_"+NameMigration.AutorunTaskName(root),out created)){
                if(!created){MessageBox.Show("The UI preview is already open.","RouteX UI preview");return;}
                MainForm.PrepareUiPreview();
                bool backgroundSmoke=Array.IndexOf(args,"--ui-preview-background-smoke")>=0;
                var preview=new MainForm(backgroundSmoke);
                if(backgroundSmoke)preview.StartUiPreviewBackgroundSmoke();
                if(Array.IndexOf(args,"--ui-preview-smoke")>=0)preview.StartUiPreviewSmoke();
                if(Array.IndexOf(args,"--ui-preview-reveal-smoke")>=0)preview.StartUiPreviewRevealSmoke();
                Application.Run(preview);
            }
        }
#endif
        [STAThread]
        static void Main(string[] args)
        {
            int awgTest = Array.FindIndex(args, a => a == "--awgtest"); // CI check of AmneziaWG keys, before anything touches the profile
            if (awgTest >= 0 && awgTest + 1 < args.Length) { AwgSelfTest.Run(args[awgTest + 1]); return; }
#if WEB_UI
            UiStartupTiming.Mark("process entry");
#endif
            EmbeddedBrowser.Initialize();
            MainForm.HadProfileAtLaunch=System.IO.File.Exists(System.IO.Path.Combine(Settings.Dir,"settings.ini"));
#if UI_PREVIEW
            try { RunUiPreview(args); }
            catch(Exception ex){System.IO.File.WriteAllText(System.IO.Path.Combine(Settings.Root,"preview-error.txt"),ex.ToString());Environment.ExitCode=1;}
            return;
#endif
            SelfUpdate.WaitForPrevious(args);
            try { if (NameMigration.HandleHelper(args)) return; }
            catch (Exception ex) { Crash(ex); return; }
            try { NameMigration.ResolveInstallation(args); }
            catch (Exception ex) { Crash(ex); return; }
            try { WM_SHOWME = Native.RegisterWindowMessage("NetProbe_ShowMe_7f3a"); WM_QUITME = Native.RegisterWindowMessage("NetProbe_QuitMe_7f3a"); } catch { }
            bool created;
            using (var mutex = new Mutex(true, "NetProbe2_SingleInstance_7f3a", out created))
            {
                // --replace: попросить запущенную копию закрыться и занять её место (обновление без ручного выхода)
                if (!created && (Array.IndexOf(args, "--replace") >= 0 || NameMigration.HasLegacyInstance()))
                {
                    try { Native.PostMessage((IntPtr)0xffff, WM_QUITME, IntPtr.Zero, IntPtr.Zero); } catch { }
                    try { created = mutex.WaitOne(15000); } catch (AbandonedMutexException) { created = true; }
                }
                if (!created && Array.IndexOf(args, "--shots") < 0 && Array.IndexOf(args, "--selftest") < 0 && Array.IndexOf(args, "--speedtest") < 0) { try { Native.PostMessage((IntPtr)0xffff, WM_SHOWME, IntPtr.Zero, IntPtr.Zero); } catch { } return; }
                try { if (NameMigration.Bootstrap(args)) return; }
                catch (Exception ex) { Crash(ex); return; }
                try { Native.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) Theme.Scale = g.DpiX / 96f; } catch { }
                Application.ThreadException += (s, e) => Logger.Write(LogLevel.Error, "Unhandled: " + e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);
                bool min = Array.Exists(args, a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
                MainForm.Updated = Array.Exists(args, a => a == "--updated");
                MainForm.Reapply3 = NameMigration.Pending || Array.Exists(args, a => a == "--reapply");
                int ti = Array.FindIndex(args, a => a == "--selftest");
                if (ti >= 0 && ti + 1 < args.Length) { SelfTest.Run(args[ti + 1]); return; }
                int sp = Array.FindIndex(args, a => a == "--speedtest");
                if (sp >= 0 && sp + 1 < args.Length) { SelfTest.Speed(args[sp + 1], sp + 2 < args.Length && !args[sp + 2].StartsWith("--") ? args[sp + 2] : null); return; }
                int si = Array.FindIndex(args, a => a == "--shots");
                if (si >= 0 && si + 1 < args.Length) MainForm.ShotsDir = args[si + 1];
                MainForm.ShotsQuick = Array.IndexOf(args, "--quick") >= 0;
                try { Application.Run(new MainForm(min)); }
                catch (Exception ex) { Crash(ex); }
                GC.KeepAlive(mutex);
            }
        }
    }
}
