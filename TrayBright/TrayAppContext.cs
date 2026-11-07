using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace TrayBright;

/// <summary>
/// 应用主上下文：托盘图标、弹出面板、平滑过渡、记忆与开机恢复。
/// 强度 0-100：数字越小色温越强（1700K 最暖），100 为关闭（6500K 原色）。
/// </summary>
public class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _fadeTimer;  // 色温平滑过渡
    private readonly System.Windows.Forms.Timer _saveTimer;  // 记忆防抖保存
    private readonly System.Windows.Forms.Timer _wheelTimer; // 滚轮调节去抖
    private int _pendingWheelDelta;                          // 待应用的滚轮变化量

    // 低级鼠标钩子：捕获托盘图标上的滚轮
    private IntPtr _hook = IntPtr.Zero;
    private HookNative.LowLevelMouseProc _hookProc = null!; // 持有委托引用，防止被 GC 回收导致钩子失效
    private Point _lastHoverPos;
    private DateTime _lastHover = DateTime.MinValue;

    private PopupForm? _popup;
    private ToolStripMenuItem _toggleItem = null!; // 右键菜单里的软件开关

    private bool _enabled = true;    // 软件开关（临时停用不退出）
    private int _currentStrength;    // 当前强度 0-100（记忆值）
    private int _appliedStrength = 100; // 实际已应用到屏幕的强度（过渡起点）
    private int _fromStrength;       // 本次过渡的起点（快照）
    private DateTime _transStart;

    public TrayAppContext()
    {
        _settings = AppSettings.Load();
        _currentStrength = Math.Clamp(_settings.Strength, 0, 100);
        _enabled = _currentStrength < 100; // 记忆为 100（关闭）时启动即为停用状态

        _tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "TrayBright 夜间模式",
            Visible = true
        };
        _tray.MouseClick += TrayOnMouseClick;
        _tray.MouseMove += TrayOnMouseMove; // 悬停记录位置，供滚轮钩子判断

        var menu = new ContextMenuStrip();
        menu.Items.Add("调节面板", null, (_, _) => ShowPopup());
        menu.Items.Add(new ToolStripSeparator());

        // 软件开关：临时开/关夜间模式，不退出程序
        _toggleItem = new ToolStripMenuItem("启用夜间模式") { Checked = _enabled };
        _toggleItem.Click += (_, _) => ToggleEnabled();
        menu.Items.Add(_toggleItem);

        var autoStart = new ToolStripMenuItem("开机自动启动") { Checked = _settings.StartWithWindows };
        autoStart.Click += (_, _) =>
        {
            _settings.StartWithWindows = !_settings.StartWithWindows;
            autoStart.Checked = _settings.StartWithWindows;
            ApplyAutoStart();
            ScheduleSave();
        };
        menu.Items.Add(autoStart);

        var restore = new ToolStripMenuItem("开机恢复上次强度") { Checked = _settings.RestoreOnStartup };
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
        _wheelTimer = new System.Windows.Forms.Timer { Interval = 30 }; // 滚轮调节去抖
        _wheelTimer.Tick += WheelTimerTick;

        InstallWheelHook(); // 托盘图标上滚动滚轮微调强度

        // 记忆功能：开机按上次位置恢复
        if (_settings.RestoreOnStartup && _enabled)
            ApplyStrength(_currentStrength);
        UpdateTrayText();
    }

    /// <summary>强度转开尔文色温：0 → 1700K（最强），100 → 6500K（原色）</summary>
    private static int KelvinFromStrength(int strength) =>
        ColorTemperature.MinKelvin + strength * (ColorTemperature.MaxKelvin - ColorTemperature.MinKelvin) / 100;

    /// <summary>软件开关：临时停用（恢复原色）或重新启用（恢复记忆强度），不退出程序</summary>
    private void ToggleEnabled()
    {
        _enabled = !_enabled;
        _toggleItem.Checked = _enabled;
        if (_enabled)
            ApplyStrength(_currentStrength);
        else
        {
            _fadeTimer.Stop();
            ColorTemperature.Reset();
            _appliedStrength = 100;
        }
        UpdateTrayText();
        ScheduleSave();
    }

    private void UpdateTrayText() =>
        _tray.Text = !_enabled || _currentStrength >= 100
            ? "TrayBright 夜间模式（已关闭）"
            : $"TrayBright 夜间模式（强度 {_currentStrength}%）";

    /// <summary>应用强度并记忆；滑块/滚轮调节会自动把开关置为启用</summary>
    private void ApplyStrength(int strength)
    {
        strength = Math.Clamp(strength, 0, 100);
        _settings.Strength = strength; // 记忆
        _currentStrength = strength;

        if (!_enabled) // 调节即视为重新启用
        {
            _enabled = true;
            _toggleItem.Checked = true;
        }

        if (_settings.FadeMilliseconds <= 0)
        {
            ColorTemperature.Apply(KelvinFromStrength(strength));
            _appliedStrength = strength;
        }
        else
        {
            _fromStrength = _appliedStrength;
            _transStart = DateTime.UtcNow;
            _fadeTimer.Start();
        }
        ScheduleSave(); // 防抖写盘
        UpdateTrayText();
    }

    private void FadeTick(object? sender, EventArgs e)
    {
        double t = (DateTime.UtcNow - _transStart).TotalMilliseconds / Math.Max(1, _settings.FadeMilliseconds);
        int target = _currentStrength;
        if (t >= 1)
        {
            ColorTemperature.Apply(KelvinFromStrength(target));
            _appliedStrength = target;
            _fadeTimer.Stop();
            return;
        }
        int s = (int)Math.Round(_fromStrength + (target - _fromStrength) * t);
        ColorTemperature.Apply(KelvinFromStrength(s));
    }

    private void TrayOnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ShowPopup();
    }

    /// <summary>记录鼠标悬停在托盘图标上的位置与时间（滚轮钩子的判断依据）</summary>
    private void TrayOnMouseMove(object? sender, MouseEventArgs e)
    {
        _lastHoverPos = Cursor.Position;
        _lastHover = DateTime.UtcNow;
    }

    private void InstallWheelHook()
    {
        try
        {
            _hookProc = HookProc;
            _hook = HookNative.SetWindowsHookEx(HookNative.WH_MOUSE_LL,
                _hookProc, HookNative.GetModuleHandle(null), 0);
        }
        catch { _hook = IntPtr.Zero; }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == HookNative.WM_MOUSEWHEEL &&
            (DateTime.UtcNow - _lastHover).TotalMilliseconds < 500)
        {
            var info = Marshal.PtrToStructure<HookNative.MSLLHOOKSTRUCT>(lParam);
            int dx = info.pt.x - _lastHoverPos.X, dy = info.pt.y - _lastHoverPos.Y;
            if (dx * dx + dy * dy <= 32 * 32) // 滚轮发生时鼠标基本未离开托盘图标
            {
                _pendingWheelDelta = Math.Clamp(_pendingWheelDelta + Math.Sign((short)(info.mouseData >> 16)) * 2, -200, 200);
                _wheelTimer.Start();
                return new IntPtr(1); // 吞掉滚轮事件，避免传给其他程序
            }
        }
        return HookNative.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>滚轮去抖应用：强度 ±2/格 并自动记忆</summary>
    private void WheelTimerTick(object? sender, EventArgs e)
    {
        if (_pendingWheelDelta == 0) { _wheelTimer.Stop(); return; }
        int delta = _pendingWheelDelta;
        _pendingWheelDelta = 0;
        ApplyStrength(_currentStrength + delta);
    }

    private void ShowPopup()
    {
        if (_popup != null) { _popup.Close(); return; }
        _popup = new PopupForm(_currentStrength, ApplyStrength, () => _popup = null);
        _popup.Show();
        _popup.Activate();
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
        _wheelTimer?.Stop();
        if (_hook != IntPtr.Zero) HookNative.UnhookWindowsHookEx(_hook);
        ColorTemperature.Reset(); // 退出时恢复系统原色，避免残留暖色
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }

    /// <summary>运行时绘制托盘图标（月亮意象，无需外部 ico 文件）</summary>
    private static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Color.FromArgb(64, 72, 100));
            g.FillEllipse(bg, 2, 2, 28, 28);
            using var white = new SolidBrush(Color.White);
            g.FillEllipse(white, 8, 8, 16, 16);
            using var cut = new SolidBrush(Color.FromArgb(64, 72, 100));
            g.FillEllipse(cut, 13, 6, 16, 16); // 月牙效果
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}

internal static class HookNative
{
    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEWHEEL = 0x020A;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
}
