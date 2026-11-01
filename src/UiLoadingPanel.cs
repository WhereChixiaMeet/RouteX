#if WEB_UI
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
namespace NetProbe {
    sealed class UiLoadingPanel : Control {
        readonly Timer animation=new Timer{Interval=33};readonly Bitmap logo;float angle;
        public UiLoadingPanel(){DoubleBuffered=true;Dock=DockStyle.Fill;BackColor=Theme.Bg;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("NetProbe.AppIcon.png"))if(stream!=null)using(var image=Image.FromStream(stream))logo=new Bitmap(image);
            animation.Tick+=(s,e)=>{angle=(angle+6)%360;Invalidate();};animation.Start();
        }
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float x=Width/2f,y=Height/2f;
            if(logo!=null)g.DrawImage(logo,x-28,y-96,56,56);
            using(var format=new StringFormat{Alignment=StringAlignment.Center})using(var title=new Font(Theme.Body.FontFamily,19,FontStyle.Bold))using(var foreground=new SolidBrush(Theme.Text))using(var muted=new SolidBrush(Theme.Muted)){
                g.DrawString("RouteX",title,foreground,new RectangleF(x-150,y-23,300,35),format);g.DrawString("Loading",Theme.Body,muted,new RectangleF(x-150,y+21,300,25),format);
            }
            using(var track=new Pen(Color.FromArgb(35,Theme.AccentColor),3))using(var arc=new Pen(Theme.AccentColor,3){StartCap=LineCap.Round,EndCap=LineCap.Round}){var ring=new RectangleF(x-10,y+62,20,20);g.DrawEllipse(track,ring);g.DrawArc(arc,ring,angle,100);}
        }
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);animation.Enabled=Visible;}
        protected override void Dispose(bool disposing){if(disposing){animation.Dispose();logo?.Dispose();}base.Dispose(disposing);}
    }
}
#endif
