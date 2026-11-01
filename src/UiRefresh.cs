using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static NetProbe.Tr;

namespace NetProbe
{
    public partial class MainForm
    {
        DarkCombo routingSource;
        bool syncingRoutingSource;
        Label HeroLabel(string text, Control parent)
        {
            var label = Ui.Label(text, Theme.Small, Theme.Muted);
            label.BackColor = Theme.Card; label.AutoSize = false; label.Height = Theme.S(20);
            parent.Controls.Add(label); return label;
        }
        void SyncRoutingSource()
        {
            if (routingSource == null || routingSource.IsDisposed) return;
            syncingRoutingSource = true;
            routingSource.SelectedIndex = ActiveSource() == Source.Subs ? 1 : 0;
            routingSource.Enabled = st.HybridMode && !TunSettingsLocked;
            syncingRoutingSource = false;
        }
        void ConfigureFontCard(CardPanel card)
        {
            var rows = card.Controls.OfType<Panel>().Where(p => p.Controls.OfType<DarkCombo>().Any()).ToList();
            if (rows.Count != 4) return;
            var labels = card.Controls.OfType<Label>().OrderBy(l => l.Top).ToList();
            var reset = card.Controls.OfType<FlatButton>().FirstOrDefault();
            string[] samples = { Tx("Подключить"), Tx("Маршрутизация"), Tx("Настройки подключения"), "VPN · Direct" };
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; var combo = row.Controls.OfType<DarkCombo>().First();
                var size = row.Controls.OfType<Spin>().First();
                var unit = row.Controls.OfType<Label>().First(); unit.Visible = false;
                var preview = new Label { Text = samples[i], AutoSize = false, ForeColor = Theme.Text, BackColor = Theme.Card, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
                row.Controls.Add(preview); row.BackColor = Theme.Card;
                Action update = () => {
                    Font font;
                    try { font = new Font(combo.SelectedItem as string ?? "Segoe UI", (float)size.Value); }
                    catch { font = new Font("Segoe UI", (float)size.Value); }
                    var old = preview.Font; preview.Font = font;
                    if (old != null && old != Theme.Body && old != Control.DefaultFont) old.Dispose();
                };
                combo.SelectedIndexChanged += (s, e) => update(); size.ValueChanged += (s, e) => update(); update();
                preview.Disposed += (s, e) => { if (preview.Font != null) preview.Font.Dispose(); };
                row.Resize += (s, e) => {
                    int sizeW = Theme.S(68), gap = Theme.S(14), fontW = Math.Max(Theme.S(130), (row.Width - sizeW - gap * 2) * 3 / 5);
                    combo.SetBounds(0, Theme.S(10), fontW, combo.Height);
                    size.SetBounds(combo.Right + gap, Theme.S(10), sizeW, size.Height);
                    preview.SetBounds(size.Right + gap, 0, Math.Max(0, row.Width - size.Right - gap), row.Height);
                };
            }
            card.Width = Theme.S(760); card.Height = Theme.S(302);
            Action layout = () => {
                int pad = Theme.S(16), labelW = Theme.S(150), y = Theme.S(46), rowH = Theme.S(54);
                if (reset != null) { reset.Text = Tx("Сбросить"); reset.AutoW(); reset.Location = new Point(card.Width - pad - reset.Width, Theme.S(7)); }
                for (int i = 0; i < rows.Count; i++) {
                    if (i < labels.Count) { labels[i].AutoSize = false; labels[i].AutoEllipsis = true; labels[i].SetBounds(pad, y, labelW - Theme.S(8), rowH); labels[i].TextAlign = ContentAlignment.MiddleLeft; }
                    rows[i].SetBounds(pad + labelW, y, Math.Max(Theme.S(260), card.Width - pad * 2 - labelW), rowH);
                    y += rowH;
                }
            };
            card.Resize += (s, e) => layout(); layout();
        }
        int SideRowMask { get { return (st.SideVpn ? 1 : 0) | (st.SideDirect ? 2 : 0) | (st.SideDownload ? 4 : 0) | (st.SideUpload ? 8 : 0); } }
        void ConfigureMiniMenu()
        {
            miniMon.RowMask = SideRowMask;
            miniMon.SetParts(st.SidePing && MonOn, st.SideSpeedOn);
            var menu = new ContextMenuStrip { Renderer = new ThemedMenuRenderer(), Font = Theme.Body };
            var gear = new FlatButton("") { Glyph = "\uE713", IconOnly = true, Size = new Size(Theme.S(24), Theme.S(22)) };
            var names = new[] { "VPN ping", "Direct ping", "Download", "Upload" };
            for (int i = 0; i < names.Length; i++) {
                int n = i; var item = new ToolStripMenuItem(names[i]) { CheckOnClick = true };
                item.Click += (s, e) => {
                    if (n == 0) st.SideVpn = item.Checked; else if (n == 1) st.SideDirect = item.Checked; else if (n == 2) st.SideDownload = item.Checked; else st.SideUpload = item.Checked;
                    st.Save(); miniMon.RowMask = SideRowMask; miniMon.SetParts(st.SidePing && MonOn, st.SideSpeedOn); if (laySide != null) laySide();
                }; menu.Items.Add(item);
            }
            menu.Opening += (s, e) => { for (int i = 0; i < 4; i++) { var item = (ToolStripMenuItem)menu.Items[i]; item.Checked = (SideRowMask & (1 << i)) != 0; item.Enabled = i < 2 ? st.SidePing : st.SideSpeedOn; } };
            gear.Click += (s, e) => menu.Show(gear, new Point(0, gear.Height));
            miniMon.Controls.Add(gear); miniMon.Resize += (s, e) => gear.Location = new Point(miniMon.Width - gear.Width - Theme.S(4), 0);
            gear.Location = new Point(miniMon.Width - gear.Width - Theme.S(4), 0); miniMon.ContextMenuStrip = menu;
            miniMon.Disposed += (s, e) => menu.Dispose();
        }
        void ApplyCompactServers()
        {
            if (srvGrid == null) return;
            applyingCols = true;
            try {
                foreach (DataGridViewColumn col in srvGrid.Columns) col.Visible = !st.SubsCompact || new[] { "f", "n", "t", "d", "sp", "td" }.Contains(col.Name);
                srvGrid.RowTemplate.Height = Theme.S(st.SubsCompact ? 48 : 34);
                foreach (DataGridViewRow row in srvGrid.Rows) row.Height = srvGrid.RowTemplate.Height;
                if (st.SubsCompact) {
                    srvGrid.Columns["f"].Width = Theme.S(40); srvGrid.Columns["t"].Width = Theme.S(120);
                    srvGrid.Columns["d"].Width = Theme.S(105); srvGrid.Columns["sp"].Width = Theme.S(110); srvGrid.Columns["td"].Width = Theme.S(105);
                    srvGrid.Columns["n"].Width = Math.Max(Theme.S(140), srvGrid.ClientSize.Width - Theme.S(500));
                }
            } finally { applyingCols = false; }
        }
    }
}
