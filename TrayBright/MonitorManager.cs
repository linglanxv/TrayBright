using System.Management;
using System.Runtime.InteropServices;

namespace TrayBright;

public class MonitorInfo
{
    public string Id = "";              // 稳定标识（用于记忆亮度）
    public string Description = "";     // 显示器名称
    public IntPtr HPhysical;            // DDC/CI 物理显示器句柄；内置屏为 0（走 WMI）
    public int Current = -1;            // 当前亮度（0-100 归一化）
    public bool IsDdc => HPhysical != IntPtr.Zero;

    public bool SetBrightness(int pct)
    {
        pct = Math.Clamp(pct, 0, 100);
        Current = pct;
        if (IsDdc)
        {
            try
            {
                if (!NativeMethods.GetMonitorBrightness(HPhysical, out uint min, out uint _, out uint max))
                    return false;
                uint value = min + (uint)((max - min) * pct / 100.0);
                return NativeMethods.SetMonitorBrightness(HPhysical, value);
            }
            catch { return false; }
        }
        else
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("WmiSetBrightness", new object[] { (ushort)1, (byte)pct });
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}

/// <summary>
/// 显示器管理：枚举所有显示器，外接屏走 DDC/CI（dxva2），笔记本内置屏走 WMI。
/// </summary>
public static class MonitorManager
{
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();

        NativeMethods.MonitorEnumProc proc = (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
        {
            try
            {
                if (NativeMethods.GetNumberOfPhysicalMonitorsFromHMonitor(hMonitor, out uint count) && count > 0)
                {
                    var phys = new NativeMethods.PHYSICAL_MONITOR[count];
                    if (NativeMethods.GetPhysicalMonitorsFromHMonitor(hMonitor, phys))
                    {
                        for (int i = 0; i < phys.Length; i++)
                        {
                            var m = new MonitorInfo
                            {
                                HPhysical = phys[i].hPhysicalMonitor,
                                Description = phys[i].szPhysicalMonitorDescription
                            };
                            m.Id = StableId(m);
                            if (NativeMethods.GetMonitorBrightness(m.HPhysical, out uint min, out uint cur, out uint max) && max > min)
                                m.Current = (int)Math.Round((cur - min) * 100.0 / (max - min));
                            list.Add(m);
                        }
                    }
                }
            }
            catch { /* 单个显示器失败不影响其他 */ }
            return true;
        };
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);

        // 内置屏幕（DDC 不可用时通过 WMI 找到）
        if (!list.Any(m => !m.IsDdc))
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var m = new MonitorInfo
                    {
                        HPhysical = IntPtr.Zero,
                        Description = "内置屏幕",
                        Id = "internal:" + (obj["InstanceName"]?.ToString() ?? "0")
                    };
                    if (byte.TryParse(obj["CurrentBrightness"]?.ToString(), out byte b))
                    {
                        int levels = obj["Levels"] is uint lv && lv > 0 ? (int)lv : 100;
                        m.Current = Math.Clamp(b * 100 / levels, 0, 100);
                    }
                    list.Add(m);
                    break;
                }
            }
            catch { }
        }

        // 去重（同一物理屏可能重复）
        return list.GroupBy(m => m.Id).Select(g => g.First()).ToList();
    }

    private static string StableId(MonitorInfo m)
    {
        if (!m.IsDdc) return "internal:" + m.Description;
        // 用显示器描述文本作为稳定标识，重装/重启后仍能对应
        return "ddc:" + (string.IsNullOrWhiteSpace(m.Description) ? "unknown" : m.Description.Trim());
    }
}

internal static class NativeMethods
{
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int L, T, R, B; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("dxva2.dll")]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMonitor(IntPtr hMonitor, out uint count);

    [DllImport("dxva2.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetPhysicalMonitorsFromHMonitor(IntPtr hMonitor, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll")]
    public static extern bool GetMonitorBrightness(IntPtr handle, out uint min, out uint current, out uint max);

    [DllImport("dxva2.dll")]
    public static extern bool SetMonitorBrightness(IntPtr handle, uint brightness);
}
