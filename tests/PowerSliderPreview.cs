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
                form.Hide();
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
