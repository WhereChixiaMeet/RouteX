using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NetProbe;

class PowerSliderPreview
{
    sealed class PreviewForm : MainForm
    {
        public PreviewForm() : base(false) { ShowInTaskbar=false; StartPosition=FormStartPosition.Manual; Location=new Point(-30000,-30000); }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnLoad(EventArgs e) { }
        protected override void OnShown(EventArgs e) { }
    }
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); checks++; }
    static FieldInfo Field(string n) { return typeof(MainForm).GetField(n, BindingFlags.Instance | BindingFlags.NonPublic); }
    static void State(PowerSlider control, bool on) { control.SetState(on, false, (control.AccessibleName ?? "VPN") + " · " + (on ? "ON" : "OFF")); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var settings = new Settings { Lang = "en", Theme = 0, Accent = "#ef4444", AutoStartMonitor = false, SpeedOn = false, ShowWidget = false, Autorun = false, DomainsMigrated = true, LogToFile = false };
            settings.AppIconPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "..", "..", "src", "app.ico"));
            settings.Save(); MainForm.ShotsDir = output;
            using (var form = new PreviewForm())
            {
                foreach (var timerField in typeof(MainForm).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                    if (timerField.FieldType == typeof(Timer)) { var timer = timerField.GetValue(form) as Timer; if (timer != null) timer.Stop(); }
                var tray = Field("tray").GetValue(form) as NotifyIcon; if (tray != null) tray.Visible = false;
                form.Size = new Size(1220, 800);
                form.Show(); form.PerformLayout();
                var top = ((IEnumerable)Field("topButtons").GetValue(form)).Cast<object>().ToList();
                Check(top.Count >= 9, "shared sliders exist on all nine header pages");
                foreach (var row in top)
                {
                    var vpn = (PowerSlider)row.GetType().GetField("Vpn").GetValue(row);
                    var dpi = (PowerSlider)row.GetType().GetField("Dpi").GetValue(row);
                    Check(vpn.Size == new Size(150,36) && dpi.Size == vpn.Size && vpn.Top == dpi.Top && vpn.Left-dpi.Right == 8, "header geometry preserved");
                }
                using (var bmp = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bmp, new Rectangle(Point.Empty,bmp.Size)); bmp.Save(Path.Combine(output,"routex-routing-preview.png")); }
                var lists = (ListCard[])Field("listCards").GetValue(form);
                var include = lists.First(c => c.Box.Items.Contains(ListCard.BuiltIn));
                Check(include.Box.DisplayText(ListCard.BuiltIn) == "RouteX.Checker", "checker has the friendly display label");
                Check(include.Box.Items.Contains("netprobe-vpn.exe") && !include.Box.Items.Contains("RouteX.Checker"), "checker keeps its original routing identity");
                Check(include.Box.ItemIcon(ListCard.BuiltIn) != null, "checker displays the application icon");
                Check(include.Box.DisplayText("chrome.exe") == null && include.Box.ItemIcon("chrome.exe") == null, "other application rows retain normal rendering");
                using (var bmp = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(output,"routex-checker-preview.png")); }
                // Exercise the production presentation method without starting a real VPN.
                var applyLock=typeof(MainForm).GetMethod("ApplyTunLockUi",BindingFlags.Instance|BindingFlags.NonPublic);
                applyLock.Invoke(form,new object[]{true});
                Check(lists.All(c=>!c.BAdd.Enabled&&!c.BDel.Enabled&&!c.BPaste.Enabled&&!c.Box.AllowDrop),"locked lists disable editing buttons and incoming drops");
                Check(lists.All(c=>c.Box.Enabled),"locked lists remain readable and selectable");
                Check(lists.All(c=>c.Box.DragItems(new System.Collections.Generic.List<string>{"chrome.exe"}).Count==0),"locked lists cannot start outgoing drag operations");
                Check(include.Box.CopyItems(new System.Collections.Generic.List<string>{"chrome.exe"}).Single()=="chrome.exe","locked lists still allow copying ordinary entries");
                var hint=(Label)Field("lRouteHint").GetValue(form);
                Check(hint.Text=="TUN settings are locked. Turn off VPN to edit.","locked lists display the inline English explanation");
                var status=(StatusBoard)Field("board").GetValue(form);status.State="Connected";status.Lv=Level.Good;status.Line1="VPN active";status.Line2="Routing rules are read-only until disconnected";
                foreach(var row in top){var vpn=(PowerSlider)row.GetType().GetField("Vpn").GetValue(row);vpn.Enabled=true;vpn.SetState(true,false,"VPN · ON");typeof(PowerSlider).GetField("position",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(vpn,1f);}
                using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(output,"routex-locked-proposal.png"));}
                applyLock.Invoke(form,new object[]{false});
                Check(lists.All(c=>c.BAdd.Enabled&&c.BDel.Enabled&&c.BPaste.Enabled&&c.Box.AllowDrop),"disconnect restores editing buttons and drop targets");
                Check(hint.Text!="TUN settings are locked. Turn off VPN to edit.","disconnect restores the ordinary routing hint");
                form.Hide();
            }
            using(var embedded=AppIconArt.Load(null,SystemIcons.Application,64))
            using(var custom=AppIconArt.Load(settings.AppIconPath,SystemIcons.Application,64))
            {
                bool same=true;int opaque=0,expected=0;
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    var pixel=embedded.GetPixel(x,y);if(pixel.ToArgb()!=custom.GetPixel(x,y).ToArgb())same=false;
                    if(pixel.A>20){opaque++;if(pixel.R<=pixel.G+3&&pixel.G<=pixel.B+3)expected++;}
                }
                Check(same,"embedded app icon matches the original PNG frame in the custom ICO");
                Check(opaque>500&&expected>opaque*.98,"app icon contains the expected blue/white artwork instead of pixel noise");
            }
            using (var board = new Bitmap(760, 410)) using (var g = Graphics.FromImage(board))
            {
                g.Clear(Color.FromArgb(18,20,25));
                int row=0;
                foreach (ThemeKind theme in Enum.GetValues(typeof(ThemeKind)))
                {
                    Theme.AccentColor = theme == ThemeKind.Light ? Color.FromArgb(59,130,246) : Color.FromArgb(239,68,68);
                    Theme.Apply(theme);
                    using (var panel = new Panel { Size = new Size(740,72), BackColor = Theme.Bg })
                    using (var dpi = new PowerSlider("DPI") { Location=new Point(400,18) })
                    using (var vpn = new PowerSlider("VPN") { Location=new Point(558,18) })
                    {
                        panel.Controls.Add(new Label { Text=theme.ToString(), ForeColor=Theme.Text, Location=new Point(18,26), AutoSize=true, Font=Theme.Body });
                        panel.Controls.Add(dpi); panel.Controls.Add(vpn); State(dpi,false); State(vpn,true);
                        using(var image=new Bitmap(panel.Width,panel.Height)) {panel.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));g.DrawImageUnscaled(image,10,10+row*80);}
                        Check(vpn.On && !dpi.On, "on/off rendering in " + theme);
                    }
                    row++;
                }
                board.Save(Path.Combine(output,"routex-slider-themes.png"));
            }
            Theme.AccentColor=Color.FromArgb(239,68,68); Theme.Apply(ThemeKind.Dark);
            string frames=Path.Combine(output,"frames");Directory.CreateDirectory(frames);
            var sliderPosition=typeof(PowerSlider).GetField("position",BindingFlags.Instance|BindingFlags.NonPublic);
            using(var panel=new Panel {Size=new Size(350,70),BackColor=Theme.Bg})
            using(var dpi=new PowerSlider("DPI"){Location=new Point(18,17)})
            using(var vpn=new PowerSlider("VPN"){Location=new Point(176,17)})
            {
                panel.Controls.Add(dpi);panel.Controls.Add(vpn);State(dpi,false);
                for(int frame=0;frame<64;frame++)
                {
                    double t=frame<12?0:frame<24?(frame-12)/11.0:frame<40?1:frame<52?1-(frame-40)/11.0:0;
                    bool active=frame>=12 && frame<40; State(vpn,active);
                    double progress=frame>=40&&frame<52?Math.Pow(t,3):1-Math.Pow(1-t,3);
                    sliderPosition.SetValue(vpn,(float)progress);
                    using(var image=new Bitmap(panel.Width,panel.Height)){panel.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(frames,frame.ToString("D3")+".png"));}
                }
            }
            foreach (Color accent in new[]{Color.FromArgb(239,68,68),Color.FromArgb(59,130,246),Color.FromArgb(168,85,247),Color.FromArgb(34,197,94)})
            {
                Theme.AccentColor=accent;Theme.Apply(ThemeKind.Dark);
                using(var button = new PowerSlider("VPN")) using(var bmp = new Bitmap(150,36))
                {
                    State(button,true);button.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));
                    var pixel=bmp.GetPixel(75,3);
                    Check(Math.Abs(pixel.R-Theme.Accent.R)<4 && Math.Abs(pixel.G-Theme.Accent.G)<4 && Math.Abs(pixel.B-Theme.Accent.B)<4,"active fill follows selected accent " + accent);
                    button.AccessibilityObject.DoDefaultAction();
                    int clicked=0;button.Click+=(s,e)=>clicked++;
                    button.AccessibilityObject.DoDefaultAction();Check(clicked==1 && button.On,"click requests action without optimistic state change");
                    button.Enabled=false;button.AccessibilityObject.DoDefaultAction();Check(clicked==1,"disabled slider ignores activation");
                }
            }
            Console.WriteLine("TOTAL " + checks + " checks; no VPN started; preview only.");
            var asset = typeof(SelfUpdate).GetMethod("AppAsset",BindingFlags.Static|BindingFlags.NonPublic);
            Check((bool)asset.Invoke(null,new object[]{"NetProbe.exe"}),"updater accepts the single published NetProbe.exe asset");
            Check((bool)asset.Invoke(null,new object[]{"RouteX.exe"}),"updater remains compatible with older release names");
            Check(!(bool)asset.Invoke(null,new object[]{"source.zip"}),"updater ignores source-only archives");
            Console.WriteLine("TOTAL " + checks + " checks.");
            return 0;
        }
        catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
