namespace TrayBright;

/// <summary>
/// 托盘弹出的亮度面板（无边框、深色圆角，仿 Twinkle Tray），点击外部自动关闭。
/// </summary>
public class PopupForm : Form
{
    private readonly List<MonitorInfo> _monitors;
    private readonly Action<MonitorInfo, int> _onBrightnessChanged;
    private readonly Action _onClose;

    public PopupForm(List<MonitorInfo> monitors, Dictionary<string, int> remembered,
        Action<MonitorInfo, int> onBrightnessChanged, Action onClose)
    {
        _monitors = monitors;
        _onBrightnessChanged = onBrightnessChanged;
        _onClose = onClose;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(39, 39, 46);
        DoubleBuffered = true;
        MinimumSize = new Size(320, 60);

        BuildUi(remembered);
        PositionNearTray();
    }

    private void BuildUi(Dictionary<string, int> remembered)
    {
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(14, 12, 14, 10),
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.FromArgb(39, 39, 46)
        };

        var title = new Label
        {
            Text = "亮度调节",
            ForeColor = Color.FromArgb(235, 235, 240),
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(2, 0, 0, 6)
        };
        root.Controls.Add(title);

        if (_monitors.Count == 0)
        {
            root.Controls.Add(new Label
            {
                Text = "未检测到可调亮度的显示器",
                ForeColor = Color.FromArgb(200, 120, 120),
                AutoSize = true,
                Margin = new Padding(2, 6, 0, 6)
            });
        }

        int index = 0;
        foreach (var m in _monitors)
        {
            var row = new Panel { Width = 330, Height = 40, Margin = new Padding(0, 2, 0, 2), BackColor = Color.FromArgb(39, 39, 46) };

            var icon = new Label
            {
                Text = "☀",
                ForeColor = Color.FromArgb(255, 190, 90),
                Font = new Font("Segoe UI Symbol", 11f),
                AutoSize = false,
                Size = new Size(24, 34),
                Location = new Point(0, 3),
                TextAlign = ContentAlignment.MiddleCenter
            };
            row.Controls.Add(icon);

            int start = m.Current >= 0
                ? m.Current
                : remembered.TryGetValue(m.Id, out int r) ? r : 70;

            var slider = new SmoothSlider
            {
                Location = new Point(26, 3),
                Size = new Size(250, 34),
                Value = start,
                Label = start + "%",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            slider.ValueCommitted += v =>
            {
                slider.Label = v + "%";
                slider.Invalidate();
                _onBrightnessChanged(m, v);
            };
            row.Controls.Add(slider);

            root.Controls.Add(row);
            index++;
        }

        var hint = new Label
        {
            Text = "松开滑块即自动记忆，下次开机恢复",
            ForeColor = Color.FromArgb(130, 130, 145),
            Font = new Font("Microsoft YaHei UI", 8f),
            AutoSize = true,
            Margin = new Padding(2, 4, 0, 0)
        };
        root.Controls.Add(hint);

        Controls.Add(root);
        ClientSize = new Size(358, root.PreferredSize.Height);
        Region = System.Drawing.Region.FromHrgn(NativeRound.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 14, 14));
    }

    private void PositionNearTray()
    {
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        var pos = new Point(Cursor.Position.X - Width / 2, wa.Bottom - Height - 12);
        pos.X = Math.Clamp(pos.X, wa.Left + 8, wa.Right - Width - 8);
        pos.Y = Math.Max(wa.Top + 8, pos.Y);
        Location = pos;
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _onClose();
        base.OnFormClosed(e);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessDialogKey(keyData);
    }

    private static class NativeRound
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);
    }
}
