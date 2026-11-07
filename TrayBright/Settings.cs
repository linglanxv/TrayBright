using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrayBright;

/// <summary>
/// 持久化设置（记忆功能核心）：保存最后使用的色温，下次开机自动恢复。
/// 文件位置：%AppData%\TrayBright\settings.json
/// </summary>
public class AppSettings
{
    /// <summary>开机时自动恢复上次色温</summary>
    public bool RestoreOnStartup { get; set; } = true;

    /// <summary>跟随系统开机自启</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>调节时的平滑过渡时长（毫秒），0 表示立即生效</summary>
    public int FadeMilliseconds { get; set; } = 250;

    /// <summary>记忆的色温（开尔文，1700-6500），6500 为不偏色</summary>
    public int TemperatureKelvin { get; set; } = ColorTemperature.MaxKelvin;

    [JsonIgnore]
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrayBright");

    [JsonIgnore]
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null) return s;
            }
        }
        catch { /* 损坏则重建 */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* 忽略保存失败，不影响运行 */ }
    }
}
