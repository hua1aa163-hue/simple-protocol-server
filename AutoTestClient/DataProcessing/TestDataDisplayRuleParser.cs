using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTestClient.Models;

namespace AutoTestClient.DataProcessing;

/// <summary>
/// 纯 C# 的测试数据展示规则解析器。它接受规则数组，也接受包含
/// <c>DisplayRules</c>/<c>Rules</c> 数组的配置对象，并兼容早期只保存
/// <c>CellOrRange</c> 的 JSON。解析器不访问文件系统，适合设计器和服务端
/// 启动阶段复用。
/// </summary>
public static class TestDataDisplayRuleParser
{
    private static JsonSerializerOptions CreateOptions(bool indented = false) => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = indented,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 解析 JSON 规则。空白文本表示“使用内置默认规则”；显式空数组仍
    /// 表示用户有意关闭所有展示规则。
    /// </summary>
    public static IReadOnlyList<TestDataDisplayRule> Parse(
        string? json,
        bool useDefaultsWhenEmptyText = true)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return useDefaultsWhenEmptyText
                ? CloneRules(TestDataDisplayRule.CreateDefaultRules())
                : Array.Empty<TestDataDisplayRule>();
        }

        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
            MaxDepth = 128
        });
        JsonElement root = document.RootElement;
        JsonElement array = root;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(root, "DisplayRules", out JsonElement displayRules))
                array = displayRules;
            else if (TryGetProperty(root, "Rules", out JsonElement rules))
                array = rules;
        }

        if (array.ValueKind != JsonValueKind.Array)
            throw new JsonException("展示规则 JSON 必须是数组，或包含 DisplayRules/Rules 数组。");

        var parsed = new List<TestDataDisplayRule>();
        int fallbackOrder = 1;
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                continue;
            if (element.ValueKind != JsonValueKind.Object)
                throw new JsonException("展示规则数组中的每一项必须是对象。");

            TestDataDisplayRule? rule = element.Deserialize<TestDataDisplayRule>(CreateOptions());
            if (rule is null) continue;
            // Older editors used a few intuitive aliases.  Keep them in the
            // parser even though the canonical model fields remain stable.
            if (string.IsNullOrWhiteSpace(rule.DataCellOrRange) &&
                TryGetString(element, "ValueCellOrRange", out string valueCell))
                rule.DataCellOrRange = valueCell;
            if (string.IsNullOrWhiteSpace(rule.NameCellOrLabel) &&
                TryGetString(element, "NameCell", out string nameCell))
                rule.NameCellOrLabel = nameCell;
            if (rule.ProjectKind is null &&
                TryGetProperty(element, "ProjectType", out JsonElement projectType))
                rule.ProjectKind = TestDataDisplayRule.ParseProjectKind(
                    projectType.ValueKind == JsonValueKind.String
                        ? projectType.GetString()
                        : projectType.ToString());
            rule.Normalize(fallbackOrder++);
            parsed.Add(rule);
        }

        return parsed
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    /// <summary>尝试解析规则；失败时返回 false 和可供日志显示的错误。</summary>
    public static bool TryParse(
        string? json,
        out IReadOnlyList<TestDataDisplayRule> rules,
        out string error,
        bool useDefaultsWhenEmptyText = true)
    {
        try
        {
            rules = Parse(json, useDefaultsWhenEmptyText);
            error = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            rules = Array.Empty<TestDataDisplayRule>();
            error = ex.Message;
            return false;
        }
    }

    /// <summary>将规则写成可持久化 JSON；输出前会复制并规范化顺序。</summary>
    public static string Serialize(
        IEnumerable<TestDataDisplayRule>? rules,
        bool indented = true)
    {
        var normalized = (rules ?? Array.Empty<TestDataDisplayRule>())
            .Where(rule => rule is not null)
            .Select(CloneRule)
            .ToList();
        for (int index = 0; index < normalized.Count; index++)
            normalized[index].Normalize(index + 1);
        normalized = normalized
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToList();
        return JsonSerializer.Serialize(normalized, CreateOptions(indented));
    }

    /// <summary>
    /// 选出适用于一个项目类型的规则。显式传入 null 使用内置默认规则；
    /// 显式空集合保持为空，方便用户关闭某类展示。
    /// </summary>
    public static IReadOnlyList<TestDataDisplayRule> ForProject(
        TestProjectKind projectKind,
        IEnumerable<TestDataDisplayRule>? rules = null)
    {
        IEnumerable<TestDataDisplayRule> source = rules ??
            TestDataDisplayRule.CreateDefaultRules();
        return source
            .Where(rule => rule is not null && rule.Enabled &&
                           (rule.ProjectKind is null || rule.ProjectKind == projectKind))
            .Select(CloneRule)
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToArray();
    }

    /// <summary>深复制规则，避免服务规范化时修改配置编辑器正在使用的对象。</summary>
    public static TestDataDisplayRule CloneRule(TestDataDisplayRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return JsonSerializer.Deserialize<TestDataDisplayRule>(
                   JsonSerializer.Serialize(rule, CreateOptions()), CreateOptions())
               ?? new TestDataDisplayRule();
    }

    public static IReadOnlyList<TestDataDisplayRule> CloneRules(
        IEnumerable<TestDataDisplayRule> rules) =>
        (rules ?? throw new ArgumentNullException(nameof(rules)))
            .Where(rule => rule is not null)
            .Select(CloneRule)
            .ToArray();

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool TryGetString(
        JsonElement element,
        string name,
        out string value)
    {
        if (TryGetProperty(element, name, out JsonElement property))
        {
            value = property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : property.ToString();
            return true;
        }
        value = string.Empty;
        return false;
    }
}

