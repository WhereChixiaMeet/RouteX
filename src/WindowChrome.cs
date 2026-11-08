using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NetProbe
{
    // RouteX 5.0: with the web interface the window has no system caption; the interface draws its own title bar
    // (back / forward, "RouteX", minimise / maximise / close) and asks for the window actions through the "window"
    // command. The window stays a normal resizable one (shadow, snap, Win+arrows, side and bottom borders):
    // WM_NCCALCSIZE only gives the caption to the client area. A thin strip above the interface keeps the top border
    // resizable, because the WebView covers everything else. The classic built-in interface keeps the system caption.
    public partial class MainForm
    {
        [StructLayout(LayoutKind.Sequential)] struct ChromeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

        const int WM_NCCALCSIZE = 0x0083, WM_NCHITTEST = 0x0084, HTCLIENT = 1, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
        bool chromeOn;
        int ChromeStrip { get { return Math.Max(3, Theme.S(4)); } }

        // turn the custom title bar on / off (web interface ⇄ classic interface) and keep the top strip right
        void RefreshChrome()
        {
            if (!IsHandleCreated || IsDisposed) return;
            bool on = WebUiShown;
            var pad = on && WindowState != FormWindowState.Maximized ? new Padding(0, ChromeStrip, 0, 0) : Padding.Empty;
            if (Padding != pad) Padding = pad;
            if (on) BackColor = Theme.Side; // the strip has the title bar's color
            if (on == chromeOn) return;
            chromeOn = on;
            try { SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); } catch { } // NOSIZE NOMOVE NOZORDER NOACTIVATE FRAMECHANGED
        }

        bool ChromeWndProc(ref Message m)
        {
            if (!chromeOn) return false;
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // let Windows compute the normal frame, then give the caption back to the client area
                var before = (ChromeRect)Marshal.PtrToStructure(m.LParam, typeof(ChromeRect));
                base.WndProc(ref m);
                var r = (ChromeRect)Marshal.PtrToStructure(m.LParam, typeof(ChromeRect));
                // maximised windows hang over the screen edge by the frame width: keep the content on screen
                r.Top = before.Top + (IsZoomed(Handle) ? GetSystemMetrics(33 /*SM_CYSIZEFRAME*/) + GetSystemMetrics(92 /*SM_CXPADDEDBORDER*/) : 0);
                Marshal.StructureToPtr(r, m.LParam, false);
                m.Result = IntPtr.Zero;
                return true;
            }
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                int hit = (int)m.Result;
                // Windows still computes the old caption from the window style: never answer caption / buttons
                // (2 caption, 3 menu, 8 min, 9 max, 20 close, 21 help) for what is now client area
                if (hit == 2 || hit == 3 || hit == 8 || hit == 9 || hit == 20 || hit == 21) m.Result = (IntPtr)HTCLIENT;
                if (!IsZoomed(Handle))
                {
                    int x = (short)((long)m.LParam & 0xFFFF), y = (short)(((long)m.LParam >> 16) & 0xFFFF);
                    var p = PointToClient(new System.Drawing.Point(x, y));
                    if (p.Y >= 0 && p.Y < ChromeStrip && p.X >= 0 && p.X < ClientSize.Width)
                    {
                        int corner = Theme.S(10);
                        m.Result = (IntPtr)(p.X < corner ? HTTOPLEFT : p.X >= ClientSize.Width - corner ? HTTOPRIGHT : HTTOP);
                    }
                }
                return true;
            }
            return false;
        }

        // the title bar of the interface: drag, double click / maximise button, minimise, close
        void BrowserWindowCommand(string op)
        {
            switch (op)
            {
                case "drag":
                    // started on the first mouse move with the button held; a released button would leave the window stuck to the cursor
                    if (Control.MouseButtons == MouseButtons.Left && WindowState != FormWindowState.Minimized)
                    {
                        Native.ReleaseCapture();
                        Native.SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, (IntPtr)2 /*HTCAPTION*/, IntPtr.Zero);
                    }
                    break;
                case "max": WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; break;
                case "min": WindowState = FormWindowState.Minimized; break;
                case "close": Close(); break; // FormClosing decides: to the tray or exit
            }
        }
    }
}
