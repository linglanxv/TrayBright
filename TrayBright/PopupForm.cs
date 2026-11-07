namespace TrayBright;

/// <summary>
/// 托盘弹出的色温面板（无边框、深色圆角，仿 Twinkle Tray），点击外部自动关闭。
/// </summary>
public class PopupForm : Form
{
    private readonly int _currentKelvin;
    private readonly Action<int> _onTempChanged;
    private readonly Action _onClose;

    private SmoothSlider _slider = null!;

    public PopupForm(int currentKelvin, Action<int> onTempChanged, Action onClose)
    {
        _currentKelvin = Math.Clamp(currentKelvin, ColorTemperature.MinKelvin, ColorTemperature.MaxKelvin);
        _onTempChanged = onTempChanged;
        _onClose = onClose;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(39, 39, 46);
        DoubleBuffered = true;

        BuildUi();
        PositionNearTray();
    }

    private static int KelvinFromValue(float v) =>
        ColorTemperature.MinKelvin + (int)Math.Round(v / 100f * (ColorTemperature.MaxKelvin - ColorTemperature.MinKelvin));

    private static float ValueFromKelvin(int kelvin) =>
        (kelvin - ColorTemperature.MinKelvin) * 100f / (ColorTemperature.MaxKelvin - ColorTemperature.MinKelvin);

    private void BuildUi()
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

        root.Controls.Add(new Label
        {
            Text = "夜间模式 · 色温调节",
            ForeColor = Color.FromArgb(235, 235, 240),
            Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(2, 0, 0, 6)
        });

        _slider = new SmoothSlider
        {
            Width = 330,
            Height = 36,
            ShowFill = false,
            TrackStart = Color.FromArgb(255, 138, 60),  // 暖橙（低色温）
            TrackEnd = Color.FromArgb(150, 185, 255),   // 冷蓝白（高色温）
            Value = ValueFromKelvin(_currentKelvin),
            Label = _currentKelvin + "K",
            Margin = new Padding(0, 2, 0, 2)
        };
        _slider.ValueCommitted += v =>
        {
            int kelvin = KelvinFromValue(v);
            _slider.Label = kelvin + "K";
            _slider.Invalidate();
            _onTempChanged(kelvin);
        };
        root.Controls.Add(_slider);

        // 预设按钮行
        var presets = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Color.FromArgb(39, 39, 46)
        };
        foreach (var (name, kelvin) in new[] { ("烛光 1900K", 1900), ("日落 3400K", 3400), ("舒适 4500K", 4500), ("日光 6500K", 6500) })
        {
            var btn = new Button
            {
                Text = name,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(220, 220, 228),
                BackColor = Color.FromArgb(52, 52, 62),
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Margin = new Padding(2, 0, 2, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 82);
            int k = kelvin;
            btn.Click += (_, _) =>
            {
                _slider.Value = ValueFromKelvin(k);
                _slider.Label = k + "K";
                _slider.Invalidate();
                _onTempChanged(k);
            };
            presets.Controls.Add(btn);
        }
        root.Controls.Add(presets);

        root.Controls.Add(new Label
        {
            Text = "拖动滑块或滚轮微调 · 自动记忆，下次开机恢复",
            ForeColor = Color.FromArgb(130, 130, 145),
            Font = new Font("Microsoft YaHei UI", 8f),
            AutoSize = true,
            Margin = new Padding(2, 4, 0, 0)
        });

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
