using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrayBright;

/// <summary>
/// 持久化设置（记忆功能核心）：保存每台显示器最后的亮度，下次开机自动恢复。
/// 文件位置：%AppData%\TrayBright\settings.json
/// </summary>
public class AppSettings
{
    /// <summary>开机时自动恢复上次亮度</summary>
    public bool RestoreOnStartup { get; set; } = true;

    /// <summary>跟随系统开机自启</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>调节时的平滑过渡时长（毫秒），0 表示立即生效</summary>
    public int FadeMilliseconds { get; set; } = 250;

    /// <summary>每台显示器记忆的亮度（0-100），键为显示器标识</summary>
    public Dictionary<string, int> Remembered { get; set; } = new();

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
