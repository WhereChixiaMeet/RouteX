using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NetProbe;

class UiReleasePreview
{
    sealed class PreviewForm : MainForm
    {
        public PreviewForm() : base(false) { ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Location = new Point(-30000,-30000); }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnLoad(EventArgs e) { }
        protected override void OnShown(EventArgs e) { }
    }
    static int checks;
    static FieldInfo F(string n) { return typeof(MainForm).GetField(n, BindingFlags.NonPublic|BindingFlags.Instance); }
    static T Get<T>(MainForm form,string n) { return (T)F(n).GetValue(form); }
    static object Call(MainForm form,string n,params object[] args) { return typeof(MainForm).GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,args); }
    static void Check(bool value,string message) { if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message); }
    static void Quiet(MainForm form)
    {
        foreach(var field in typeof(MainForm).GetFields(BindingFlags.NonPublic|BindingFlags.Instance))if(field.FieldType==typeof(Timer)){var timer=field.GetValue(form)as Timer;if(timer!=null)timer.Stop();}
        var tray=Get<NotifyIcon>(form,"tray");tray.Visible=false;
        Get<Label>(form,"sideStatus").Text="Disconnected\nDPI/VPN (auto)\nPreview profile";
        Get<Label>(form,"infoLabel").Text="Preview profile · Ethernet · DNS: 192.0.2.53";
    }
    static void Capture(MainForm form,string path)
    {
        Quiet(form);form.PerformLayout();Application.DoEvents();Quiet(form);
        using(var image=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){
            using(var whole=new Bitmap(form.Width,form.Height)){
                form.DrawToBitmap(whole,new Rectangle(Point.Empty,whole.Size));
                var origin=form.PointToScreen(Point.Empty);var offset=new Point(origin.X-form.Left,origin.Y-form.Top);
                using(var g=Graphics.FromImage(image))g.DrawImage(whole,new Rectangle(Point.Empty,image.Size),new Rectangle(offset,image.Size),GraphicsUnit.Pixel);
            }image.Save(path,ImageFormat.Png);
        }
    }
    [STAThread]static int Main(string[] args)
    {
        try{
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
            var st=new Settings { Lang="en",Theme=0,Accent="239,68,68",AutoStartMonitor=false,SpeedOn=false,ShowWidget=false,Autorun=false,DomainsMigrated=true,LogToFile=false,AutoConnect=false,XrayAutoConnect=false,MonDefaults312=true,SpeedDefaults=true,LockDefault321=true,PrimaryTunnel="Personal config",BackupTunnel="Backup config",BackupEnabled=true,SubsCompact=true };
            st.Save();
            TunnelProto.Save("Personal config",new ServerEntry{Protocol="vless",Address="192.0.2.10",Port=443,UserId="00000000-0000-4000-8000-000000000001",Security="reality",Network="xhttp",Sni="example.com",Pbk="demo-public-key",Sid="0123456789abcdef",Name="Personal config"});
            TunnelProto.Save("Backup config",new ServerEntry{Protocol="vless",Address="192.0.2.20",Port=443,UserId="00000000-0000-4000-8000-000000000002",Security="tls",Network="tcp",Sni="example.com",Name="Backup config"});
            AppLists.Set(AppLists.HybridApps,new[]{ListCard.BuiltIn,"chrome.exe","Discord.exe","ChatGPT.exe"});
            AppLists.Set(AppLists.HybridExcl,new[]{"brave.exe","steam.exe"});
            for(int i=0;i<22;i++)DpiConfigs.Write(i==0?"general":"general (ALT"+i+")","--wf-tcp=80,443 --wf-udp=443\r\n--filter-tcp=80,443 --dpi-desync=fake,multisplit\r\n--filter-udp=443 --dpi-desync=fake");
            ServerStore.Load();ServerStore.AddMany(new[]{new ServerEntry{Id="demo-de",Link="vless://00000000-0000-4000-8000-000000000011@192.0.2.50:443?security=reality&type=xhttp&sni=example.com&pbk=demo#Germany",Name="Germany",Protocol="vless",Address="192.0.2.50",Port=443,UserId="00000000-0000-4000-8000-000000000011",Security="reality",Network="xhttp",Sni="example.com",Pbk="demo",Favorite=true,Delay=52,Speed=64},new ServerEntry{Id="demo-fi",Link="vless://00000000-0000-4000-8000-000000000012@192.0.2.51:443?security=reality&type=tcp&sni=example.com&pbk=demo#Finland",Name="Finland",Protocol="vless",Address="192.0.2.51",Port=443,UserId="00000000-0000-4000-8000-000000000012",Security="reality",Network="tcp",Sni="example.com",Pbk="demo",Delay=68,Speed=81}},"");
            MainForm.ShotsDir=output;
            using(var form=new PreviewForm()){
                form.Size=new Size(1440,960);form.Show();Quiet(form);
                var lists=Get<ListCard[]>(form,"listCards");
                Check(Get<StatusBoard>(form,"board").Height>=230,"native connection hero contains profile and backup selectors");
                Check(Get<DarkCombo>(form,"cbPrimary").Parent==Get<StatusBoard>(form,"board"),"existing tunnel selector is reused in the hero");
                Check(lists.All(l=>l.Box!=null)&&lists.Any(l=>l.Box.Items.Contains(ListCard.BuiltIn)),"routing lists and checker identity are preserved");
                Capture(form,Path.Combine(output,"routing.png"));
                Call(form,"ApplyTunLockUi",true);Check(lists.All(l=>!l.BAdd.Enabled&&!l.Box.AllowDrop),"VPN editing lock remains intact");Call(form,"ApplyTunLockUi",false);
                var pages=Get<System.Collections.Generic.List<Control>>(form,"pages");
                for(int i=0;i<10;i++){Call(form,"SelectPage",i);Capture(form,Path.Combine(output,"page-"+i+".png"));Check(pages[i].Visible,"native page "+i+" renders");}
                Call(form,"SelectPage",1);Call(form,"FillServers");var grid=Get<DataGridView>(form,"srvGrid");Console.WriteLine("Board state="+Get<StatusBoard>(form,"xBoard").State+" reserve="+Get<StatusBoard>(form,"xBoard").Reserve+" width="+Get<StatusBoard>(form,"xBoard").Width);Check(grid.Rows.Count==2&&grid.Columns.Cast<DataGridViewColumn>().Count(c=>c.Visible)==6,"compact subscriptions keep all servers and six visible columns");
                Capture(form,Path.Combine(output,"subscriptions.png"));
                Call(form,"SelectPage",2);
                var vpnCard=Get<IndicatorCard>(form,"cardVpn");var directCard=Get<IndicatorCard>(form,"cardDir");
                vpnCard.Sub="VLESS · REALITY · XHTTP · TUN · Preview";vpnCard.Value="52";vpnCard.Level=Level.Good;vpnCard.Line1="IP 198.51.100.20 · DE · HTTP 200";vpnCard.Line2="DNS 17 · TCP 35 · TLS 56 · total 143 ms";vpnCard.Line3="avg 52 · min 45 · max 71 · jitter 2.1 · loss 0.0% · preview";
                directCard.Sub="Ethernet · Preview";directCard.Value="12";directCard.Level=Level.Good;directCard.Line1="IP 192.0.2.15 · RU · HTTP 200";directCard.Line2="DNS 9 · TCP 12 · TLS 22 · total 43 ms";directCard.Line3="avg 12 · min 9 · max 22 · jitter 1.3 · loss 0.0% · preview";
                var chart=Get<LatencyChart>(form,"chart");chart.Clear();for(int i=0;i<100;i++){chart.A.Add(52+Math.Sin(i*.4)*4);chart.B.Add(12+Math.Sin(i*.25)*2);chart.TA.Add(DateTime.Now.AddSeconds((i-100)*3));chart.TB.Add(DateTime.Now.AddSeconds((i-100)*3));}
                Capture(form,Path.Combine(output,"monitor.png"));
                var live=Get<Settings>(form,"st");live.SubsCompact=false;Call(form,"ApplyCompactServers");Check(grid.Columns.Cast<DataGridViewColumn>().Count(c=>c.Visible)==10,"all server columns can be restored");live.SubsCompact=true;
                Call(form,"SelectPage",4);var search=Get<TextBox>(form,"tbStratSearch");search.Text="ALT21";Check(Get<ThemedList>(form,"lbStrat").Items.Count==1,"DPI search filters all 22 strategies");search.Text="";Check(Get<ThemedList>(form,"lbStrat").Items.Count==22,"clearing search restores every strategy");
                var mini=Get<MiniMonitor>(form,"miniMon");mini.RowMask=0;mini.SetParts(true,true);Check(!mini.Any,"all four sidebar indicators can be hidden");mini.RowMask=15;mini.SetParts(true,false);Check(mini.Height==76,"turning speed off leaves only two ping rows");
                foreach(ThemeKind theme in Enum.GetValues(typeof(ThemeKind))){live.Theme=(int)theme;Theme.Apply(theme);Call(form,"RebuildUi");Quiet(form);Call(form,"SelectPage",8);Call(form,"ShowSettingsTab","look");Capture(form,Path.Combine(output,"settings-"+theme+".png"));Check(Get<TabStrip>(form,"setTabs").Tabs.Any(t=>t.Key=="char")==Theme.Personal,"character tab condition in "+theme);}
                Check(!Get<XrayManager>(form,"xm").Active,"preview never starts Xray");
                form.Hide();
            }
            Console.WriteLine("TOTAL "+checks+" checks. Test data only; no VPN started.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
