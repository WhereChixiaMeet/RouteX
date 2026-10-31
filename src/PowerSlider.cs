using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NetProbe
{
    // Shared DPI/VPN header control. It displays backend state; clicks retain the existing actions.
    public sealed class PowerSlider : DoubleBufferedControl
    {
        readonly Timer animation = new Timer { Interval = 16 };
        readonly string caption;
        bool on, busy, hover, pressed, keyPressed;
        float position, origin;
        long started;
        const double Duration = 320;
        [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, out int value, uint flags);
        static bool MotionEnabled { get { int enabled; return !SystemParametersInfo(0x1042, 0, out enabled, 0) || enabled != 0; } }
        public bool On { get { return on; } }
        public bool Busy { get { return busy; } }
        public PowerSlider(string label)
        {
            caption = label; Text = label; Font = Theme.Button;
            Size = new Size(Theme.S(150), Theme.S(36)); Cursor = Cursors.Hand;
            TabStop = true; AccessibleRole = AccessibleRole.CheckButton;
            AccessibleName = label; AccessibleDefaultActionDescription = "Toggle";
            animation.Tick += (s, e) => Advance();
        }
        static double Elapsed(long stamp) { return (Stopwatch.GetTimestamp() - stamp) * 1000.0 / Stopwatch.Frequency; }
        void Advance()
        {
            double t = Math.Min(1, Elapsed(started) / Duration);
            double ease = 1 - Math.Pow(1 - t, 3);
            position = (float)(origin + ((on ? 1 : 0) - origin) * ease);
            if (t >= 1 && !busy) animation.Stop();
            Invalidate();
        }
        public void SetState(bool active, bool connecting, string text)
        {
            bool changed = on != active || busy != connecting;
            if (on != active)
            {
                if (animation.Enabled) Advance();
                origin = position; on = active; started = Stopwatch.GetTimestamp();
            }
            busy = connecting;
            Text = text;
            if (changed)
            {
                if (IsHandleCreated && Visible && MotionEnabled) animation.Start();
                else { position = on ? 1 : 0; animation.Stop(); }
                if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            }
            Invalidate();
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible) { animation.Stop(); position = on ? 1 : 0; }
            else if (busy && MotionEnabled) animation.Start();
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnLostFocus(EventArgs e) { pressed = keyPressed = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override bool IsInputKey(Keys keyData) { var key = keyData & Keys.KeyCode; return key == Keys.Space || key == Keys.Enter || base.IsInputKey(keyData); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { keyPressed = pressed = true; e.Handled = e.SuppressKeyPress = true; Invalidate(); }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && keyPressed)
            { keyPressed = pressed = false; e.Handled = e.SuppressKeyPress = true; if (Enabled) OnClick(EventArgs.Empty); Invalidate(); }
            base.OnKeyUp(e);
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new SliderAccessibility(this); }
        sealed class SliderAccessibility : ControlAccessibleObject
        {
            readonly PowerSlider owner;
            public SliderAccessibility(PowerSlider control) : base(control) { owner = control; }
            public override AccessibleStates State { get { return base.State | (owner.on ? AccessibleStates.Checked : 0) | (owner.busy ? AccessibleStates.Busy : 0); } }
            public override void DoDefaultAction() { if (owner.Enabled) owner.OnClick(EventArgs.Empty); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 8 || Height < 8) return;
            var g = e.Graphics; Gfx.Backdrop(this, g); Gfx.Hq(g);
            var track = new RectangleF(.75f, .75f, Width - 1.5f, Height - 1.5f);
            float p = Math.Max(0, Math.Min(1, position));
            Color bg = Theme.Mix(Theme.CardHi, Theme.Accent, p);
            Color fg = Theme.Mix(Theme.Text, Theme.AccentFg, p);
            Color border = Theme.Mix(Theme.Border, Theme.Accent, p);
            if (!Enabled) { bg = Theme.Mix(bg, ParentBack, .55); fg = Theme.Dim; border = Theme.Border; }
            else if (pressed) bg = Theme.Mix(bg, Theme.Text, .12);
            else if (hover) { bg = Theme.Mix(bg, fg, .07); border = Theme.Mix(border, Theme.Text, .2); }
            float radius = Theme.R(6); // Same corner radius as the existing FlatButton controls.
            Gfx.FillRound(g, bg, track, radius); Gfx.DrawRound(g, border, track, radius);
            if (Theme.Classic) Gfx.Bevel(g, Rectangle.Round(track), false);
            float pad = Math.Max(2, Theme.S(4)), diameter = track.Height - 2 * pad;
            float x = track.Left + pad + p * (track.Width - 2 * pad - diameter), y = track.Top + pad;
            var knob = new RectangleF(x, y, diameter, diameter);
            Color knobBg = Theme.Mix(Theme.Muted, Theme.AccentFg, p);
            Color icon = Theme.Mix(Theme.Card, Theme.Accent, p);
            if (!Enabled) { knobBg = Theme.Mix(knobBg, ParentBack, .5); icon = Theme.Dim; }
            Gfx.FillRound(g, knobBg, knob, Math.Min(Theme.R(6), diameter / 4));
            // Draw a power symbol (or a spinner while the real backend connects), independent of icon fonts.
            var symbol = new RectangleF(x + diameter * .28f, y + diameter * .28f, diameter * .44f, diameter * .44f);
            using (var pen = new Pen(icon, Math.Max(1.2f, Theme.Scale * 1.3f)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                if (busy) g.DrawArc(pen, symbol, (float)(Elapsed(started) % 900 / 900 * 360), 265);
                else { g.DrawArc(pen, symbol, -48, 276); g.DrawLine(pen, symbol.Left + symbol.Width / 2, symbol.Top - diameter * .07f, symbol.Left + symbol.Width / 2, symbol.Top + symbol.Height * .5f); }
            }
            float left = pad + diameter + Theme.S(5) - p * (diameter + Theme.S(5));
            float right = Theme.S(5) + p * (diameter + Theme.S(5));
            var label = Rectangle.Round(new RectangleF(left, 0, Width - left - right, Height));
            int dot = Text.IndexOf('·'); string status = dot >= 0 ? Text.Substring(dot + 1).Trim() : "";
            string content = caption + (status.Length == 0 ? "" : "  " + status);
            Gfx.Text(g, content, Font, fg, label, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -Theme.S(4), -Theme.S(4)), fg, bg);
        }
        protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
    }
}
