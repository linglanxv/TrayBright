namespace TrayBright;

/// <summary>
/// 托盘弹出的强度面板（无边框、深色圆角，仿 Twinkle Tray），点击外部自动关闭。
/// 强度 0-100：数字越大色温越强（越暖），0 为关闭（原色）。
/// </summary>
public class PopupForm : Form
{
    private readonly int _currentStrength;
    private readonly Action<int> _onStrengthChanged;
    private readonly Action _onClose;

    private SmoothSlider _slider = null!;

    public PopupForm(int currentStrength, Action<int> onStrengthChanged, Action onClose)
    {
        _currentStrength = Math.Clamp(currentStrength, 0, 100);
        _onStrengthChanged = onStrengthChanged;
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
            Text = "夜间模式 · 强度调节",
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
            TrackStart = Color.FromArgb(228, 230, 236), // 左端：0 关闭（原色）
            TrackEnd = Color.FromArgb(255, 138, 60),    // 右端：100 最强（最暖）
            Value = _currentStrength,
            Label = _currentStrength + "%",
            Margin = new Padding(0, 2, 0, 2)
        };
        _slider.ValueCommitted += v =>
        {
            _slider.Label = v + "%";
            _slider.Invalidate();
            _onStrengthChanged(v);
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
        foreach (var (name, strength) in new[] { ("关闭 0%", 0), ("舒适 42%", 42), ("日落 65%", 65), ("烛光 96%", 96) })
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
            int s = strength;
            btn.Click += (_, _) =>
            {
                _slider.Value = s;
                _slider.Label = s + "%";
                _slider.Invalidate();
                _onStrengthChanged(s);
            };
            presets.Controls.Add(btn);
        }
        root.Controls.Add(presets);

        root.Controls.Add(new Label
        {
            Text = "100 = 色温最强（最暖），0 = 关闭 · 自动记忆",
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
