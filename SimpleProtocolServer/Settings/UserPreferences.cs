// 本文件集中保存用户在界面上最后一次使用的选项。
// 使用一个普通数据类可以让窗体只负责读写控件，不必了解 JSON 文件的细节。
using System.Text;
using System.Text.Json;
using SimpleProtocolServer.Protocol;

namespace SimpleProtocolServer.Settings;

/// <summary>主界面和投影界面需要跨程序启动保留的用户选项。</summary>
internal sealed class UserPreferences
{
    /// <summary>首次运行时默认选择“六、单次手动测试”。</summary>
    public int CommandIndex { get; set; } = (int)CommandType.SingleManual;

    /// <summary>null 表示从未保存过，此时继续使用所选命令的协议默认参数。</summary>
    public string? Parameter { get; set; }

    /// <summary>null 表示从未保存过，此时继续使用协议自动生成的报文。</summary>
    public string? SendPreview { get; set; }

    /// <summary>循环列表中的报文也属于用户输入，关闭后原顺序保留。</summary>
    public List<string> CycleMessages { get; set; } = [];

    public decimal CycleIntervalMinutes { get; set; } = 1m;
    public decimal RowIntervalSeconds { get; set; } = 1m;
    public string ImageDirectory { get; set; } = string.Empty;
    public int ProjectionTopologyIndex { get; set; } = 4;
    public int ImageTransformIndex { get; set; }
    public decimal ProjectionIntervalSeconds { get; set; } = 5m;
    public bool CloseSecondScreenOnStop { get; set; } = true;

    /// <summary>测试设备原始 Excel 的默认根目录，仍可在投影界面重新选择。</summary>
    public string DataSourceDirectory { get; set; } =
        @"D:\Program Files\GYTech\Setup_MRTest\ExportFile";

    /// <summary>首次运行默认放到“文档\串扰测试结果”，用户选择后记住新目录。</summary>
    public string DataOutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "串扰测试结果");
}

/// <summary>把 <see cref="UserPreferences"/> 保存到当前 Windows 用户的本地配置目录。</summary>
internal static class UserPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 配置不放在程序安装目录，避免 Program Files 没有写权限，也避免把个人设置提交到 Git。
    /// </summary>
    internal static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SimpleProtocolServer",
        "settings.json");

    /// <summary>读取上次设置；文件不存在或损坏时安全地回到首次运行默认值。</summary>
    internal static UserPreferences Load() => LoadFromPath(SettingsPath);

    /// <summary>此重载让烟雾测试可以在临时目录验证读写，不接触真实用户设置。</summary>
    internal static UserPreferences LoadFromPath(string path)
    {
        try
        {
            if (!File.Exists(path)) return new UserPreferences();

            string json = File.ReadAllText(path, Encoding.UTF8);
            UserPreferences preferences =
                JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions) ??
                new UserPreferences();

            // 旧版本配置可能没有这些集合或字符串；这里补齐后，窗体可以放心使用。
            preferences.CycleMessages ??= [];
            preferences.ImageDirectory ??= string.Empty;
            preferences.DataSourceDirectory ??=
                @"D:\Program Files\GYTech\Setup_MRTest\ExportFile";
            preferences.DataOutputDirectory ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "串扰测试结果");
            return preferences;
        }
        catch (JsonException)
        {
            return new UserPreferences();
        }
        catch (IOException)
        {
            return new UserPreferences();
        }
        catch (UnauthorizedAccessException)
        {
            return new UserPreferences();
        }
    }

    /// <summary>保存到正式配置位置；失败信息交给调用方决定是否显示。</summary>
    internal static bool TrySave(UserPreferences preferences, out string error) =>
        TrySaveToPath(preferences, SettingsPath, out error);

    /// <summary>
    /// 先写临时文件再替换正式文件，程序即使在写入中途退出，也不容易留下半个 JSON。
    /// </summary>
    internal static bool TrySaveToPath(
        UserPreferences preferences,
        string path,
        out string error)
    {
        string temporaryPath = path + ".tmp";
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(preferences, JsonOptions);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, path, overwrite: true);
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // 删除临时文件失败不覆盖真正的保存错误。
            }

            error = ex.Message;
            return false;
        }
    }
}
