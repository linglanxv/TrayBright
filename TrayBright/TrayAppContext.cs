using Microsoft.Win32;

namespace TrayBright;

/// <summary>
/// 应用主上下文：托盘图标、弹出面板、平滑过渡、记忆与开机恢复。
/// </summary>
public class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _fadeTimer; // 亮度平滑过渡
    private readonly System.Windows.Forms.Timer _saveTimer; // 记忆防抖保存
    private PopupForm? _popup;
    private List<MonitorInfo> _monitors = new();

    // 过渡状态：显示器Id -> (起点, 终点, 起始时间)
    private readonly Dictionary<string, (int From, int To, DateTime Start)> _transitions = new();

    public TrayAppContext()
    {
        _settings = AppSettings.Load();

        _tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "TrayBright 亮度调节",
            Visible = true
        };
        _tray.MouseClick += TrayOnMouseClick;
        _tray.MouseWheel += TrayOnMouseWheel;

        var menu = new ContextMenuStrip();
        menu.Items.Add("亮度调节面板", null, (_, _) => ShowPopup());
        menu.Items.Add(new ToolStripSeparator());
        var autoStart = new ToolStripMenuItem("开机自动启动") { Checked = _settings.StartWithWindows };
        autoStart.Click += (_, _) =>
        {
            _settings.StartWithWindows = !_settings.StartWithWindows;
            autoStart.Checked = _settings.StartWithWindows;
            ApplyAutoStart();
            ScheduleSave();
        };
        menu.Items.Add(autoStart);

        var restore = new ToolStripMenuItem("开机恢复上次亮度") { Checked = _settings.RestoreOnStartup };
        restore.Click += (_, _) =>
        {
            _settings.RestoreOnStartup = !_settings.RestoreOnStartup;
            restore.Checked = _settings.RestoreOnStartup;
            ScheduleSave();
        };
        menu.Items.Add(restore);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60fps 过渡
        _fadeTimer.Tick += FadeTick;
        _saveTimer = new System.Windows.Forms.Timer { Interval = 600 };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _settings.Save(); };

        RefreshMonitors();

        // 记忆功能：开机按上次位置恢复
        if (_settings.RestoreOnStartup)
            RestoreRemembered();
    }

    private void RefreshMonitors()
    {
        _monitors = MonitorManager.GetMonitors();
        _tray.Text = _monitors.Count > 0
            ? $"TrayBright 亮度调节（{_monitors.Count} 台显示器）"
            : "TrayBright 亮度调节";
    }

    private void RestoreRemembered()
    {
        foreach (var m in _monitors)
        {
            if (_settings.Remembered.TryGetValue(m.Id, out int v) && v is >= 0 and <= 100)
                BeginTransition(m, v);
        }
    }

    private void TrayOnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ShowPopup();
    }

    /// <summary>鼠标悬停托盘图标上滚动滚轮：所有显示器整体 ±5%，并自动记忆</summary>
    private void TrayOnMouseWheel(object? sender, MouseEventArgs e)
    {
        int delta = Math.Sign(e.Delta) * 5;
        RefreshMonitors();
        foreach (var m in _monitors)
        {
            int target = Math.Clamp((m.Current < 0 ? 50 : m.Current) + delta, 0, 100);
            _settings.Remembered[m.Id] = target;
            BeginTransition(m, target);
        }
        ScheduleSave();
        _tray.Text = $"亮度 {(_monitors.Count > 0 ? _monitors[0].Current.ToString() : "--")}%";
    }

    private void ShowPopup()
    {
        if (_popup != null) { _popup.Close(); return; }
        RefreshMonitors();
        _popup = new PopupForm(_monitors, _settings.Remembered, OnBrightnessChanged, () => _popup = null);
        _popup.Show();
        _popup.Activate();
    }

    private void OnBrightnessChanged(MonitorInfo m, int value)
    {
        _settings.Remembered[m.Id] = value; // 记忆
        BeginTransition(m, value);
        ScheduleSave();                     // 防抖写盘
    }

    private void BeginTransition(MonitorInfo m, int target)
    {
        if (_settings.FadeMilliseconds <= 0)
        {
            m.SetBrightness(target);
            return;
        }
        int from = m.Current >= 0 ? m.Current : target;
        _transitions[m.Id] = (from, target, DateTime.UtcNow);
        if (!_fadeTimer.Enabled) _fadeTimer.Start();
    }

    private void FadeTick(object? sender, EventArgs e)
    {
        var byId = _monitors.ToDictionary(m => m.Id);
        bool any = false;
        foreach (var (id, (from, to, start)) in _transitions.ToList())
        {
            if (!byId.TryGetValue(id, out var m)) { _transitions.Remove(id); continue; }
            double t = (DateTime.UtcNow - start).TotalMilliseconds / Math.Max(1, _settings.FadeMilliseconds);
            if (t >= 1)
            {
                m.SetBrightness(to);
                _transitions.Remove(id);
            }
            else
            {
                int value = (int)Math.Round(from + (to - from) * t);
                m.SetBrightness(value);
                any = true;
            }
        }
        if (!any && _transitions.Count == 0) _fadeTimer.Stop();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void ApplyAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;
            if (_settings.StartWithWindows)
                key.SetValue("TrayBright", $"\"{Application.ExecutablePath}\"");
            else
                key.DeleteValue("TrayBright", false);
        }
        catch { }
    }

    private void ExitApp()
    {
        _settings.Save();
        _fadeTimer?.Stop();
        _saveTimer?.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }

    /// <summary>运行时绘制托盘图标（月亮+太阳意象，无需外部 ico 文件）</summary>
    private static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Color.FromArgb(255, 150, 80));
            g.FillEllipse(bg, 2, 2, 28, 28);
            using var white = new SolidBrush(Color.White);
            g.FillEllipse(white, 8, 8, 16, 16);
            using var cut = new SolidBrush(Color.FromArgb(255, 150, 80));
            g.FillEllipse(cut, 13, 6, 16, 16); // 月牙效果
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
