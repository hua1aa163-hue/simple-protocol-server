using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTestClient.Models;

namespace AutoTestClient.Settings;

/// <summary>将设置写入用户本地目录，避免 EXE 目录无写权限时丢失退出值。</summary>
public sealed class SettingsStore
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string FilePath { get; }

    public SettingsStore(string? filePath = null)
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoTestClient");
        FilePath = filePath ?? Path.Combine(directory, "settings.json");
    }

    public TestPlanConfiguration Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                var defaults = new TestPlanConfiguration();
                defaults.Normalize();
                return defaults;
            }
            string json = File.ReadAllText(FilePath);
            var result = JsonSerializer.Deserialize<TestPlanConfiguration>(json, _options) ?? new TestPlanConfiguration();
            result.Normalize();
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 配置损坏时以默认值启动；调用方可在日志中提示，不应阻止首屏打开。
            var defaults = new TestPlanConfiguration();
            defaults.Normalize();
            return defaults;
        }
    }

    public void Save(TestPlanConfiguration configuration)
    {
        configuration.Normalize();
        string? directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        string tempPath = FilePath + ".tmp";
        string json = JsonSerializer.Serialize(configuration, _options);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, FilePath, overwrite: true);
    }
}
