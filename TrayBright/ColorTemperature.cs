using System.Runtime.InteropServices;

namespace TrayBright;

/// <summary>
/// 色温（夜间模式）控制：通过显卡 Gamma Ramp 软件级调整全屏色温，
/// 效果等同 f.lux / Windows 夜间模式，对任何显示器（含无 DDC/CI 的外接屏）都有效。
/// </summary>
public static class ColorTemperature
{
    public const int MinKelvin = 1700; // 烛光暖橙
    public const int MaxKelvin = 6500; // 标准日光（不偏色）

    /// <summary>将指定开尔文色温应用到所有屏幕；6500K 及以上视为恢复原色</summary>
    public static bool Apply(int kelvin)
    {
        if (kelvin >= MaxKelvin) { Reset(); return true; }
        kelvin = Math.Clamp(kelvin, 1500, 10000);
        float[] gains = KelvinToRgbGains(kelvin);

        bool ok = false;
        foreach (var screen in Screen.AllScreens)
        {
            IntPtr hdc = GdiNative.CreateDC(screen.DeviceName, null, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero) continue;
            try
            {
                var ramp = BuildRamp(gains);
                ok |= GdiNative.SetDeviceGammaRamp(hdc, ref ramp);
            }
            finally { GdiNative.DeleteDC(hdc); }
        }
        return ok;
    }

    /// <summary>恢复系统默认色温（原始 Gamma Ramp）</summary>
    public static void Reset()
    {
        foreach (var screen in Screen.AllScreens)
        {
            IntPtr hdc = GdiNative.CreateDC(screen.DeviceName, null, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero) continue;
            try
            {
                var ramp = NewRamp();
                for (int i = 0; i < 256; i++)
                {
                    ushort v = (ushort)(i * 256);
                    ramp.Red[i] = v;
                    ramp.Green[i] = v;
                    ramp.Blue[i] = v;
                }
                GdiNative.SetDeviceGammaRamp(hdc, ref ramp);
            }
            finally { GdiNative.DeleteDC(hdc); }
        }
    }

    /// <summary>开尔文色温转 RGB 增益（Tanner Helland 近似算法），返回 [R,G,B] 均为 0~1</summary>
    public static float[] KelvinToRgbGains(int kelvin)
    {
        double t = kelvin / 100.0;
        double r, g, b;
        if (t <= 66)
        {
            r = 255;
            g = 99.4708025861 * Math.Log(t) - 161.1195681661;
        }
        else
        {
            r = 329.698727446 * Math.Pow(t - 60, -0.1332047592);
            g = 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
        }
        if (t >= 66) b = 255;
        else if (t <= 19) b = 0;
        else b = 138.5177312231 * Math.Log(t - 10) - 305.0447927307;

        return new float[]
        {
            (float)Math.Clamp(r / 255.0, 0, 1),
            (float)Math.Clamp(g / 255.0, 0, 1),
            (float)Math.Clamp(b / 255.0, 0, 1)
        };
    }

    /// <summary>创建并初始化数组字段的 RAMP（结构体 new 后数组字段为 null，必须手动分配）</summary>
    private static GdiNative.RAMP NewRamp() => new()
    {
        Red = new ushort[256],
        Green = new ushort[256],
        Blue = new ushort[256]
    };

    private static GdiNative.RAMP BuildRamp(float[] gains)
    {
        var ramp = NewRamp();
        for (int i = 0; i < 256; i++)
        {
            ramp.Red[i] = (ushort)Math.Clamp(i * 256 * gains[0], 0, 65535);
            ramp.Green[i] = (ushort)Math.Clamp(i * 256 * gains[1], 0, 65535);
            ramp.Blue[i] = (ushort)Math.Clamp(i * 256 * gains[2], 0, 65535);
        }
        return ramp;
    }
}

internal static class GdiNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    [DllImport("gdi32.dll")]
    public static extern bool SetDeviceGammaRamp(IntPtr hdc, ref RAMP ramp);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateDC(string? lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);
}
