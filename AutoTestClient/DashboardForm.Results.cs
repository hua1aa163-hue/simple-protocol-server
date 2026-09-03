using AutoTestClient.DataProcessing;
using AutoTestClient.Models;

namespace AutoTestClient;

/// <summary>
/// 主界面结果表的显示层。数据处理器每完成一个项目迭代就会发出一次结果；
/// 本文件把同一项目/指标的后续迭代横向展开，避免重复添加同名行。
/// </summary>
public partial class DashboardForm
{
    private readonly List<DisplayResultSeries> _displayResultSeries = new();
    private readonly Dictionary<string, DisplayResultSeries> _displayResultSeriesByKey =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _displayProjectOrdinals =
        new(StringComparer.OrdinalIgnoreCase);
    private int _displayResultMaximumOrdinal;

    private sealed class DisplayResultSeries
    {
        public required string Key { get; init; }
        public required string ProjectName { get; init; }
        public required string MetricKey { get; init; }
        public required string MetricName { get; init; }
        public List<string> Values { get; } = new();
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OutputDirectory { get; set; } = string.Empty;
    }

    /// <summary>开始一套新计划时清空上一套计划的指标横向序列。</summary>
    private void ResetResultDisplayHistory()
    {
        _displayResultSeries.Clear();
        _displayResultSeriesByKey.Clear();
        _displayProjectOrdinals.Clear();
        _displayResultMaximumOrdinal = 0;
        RenderResultGrid();
    }

    /// <summary>
    /// 接收一个项目迭代的指标。串扰没有表格展示规则，只由原有热图历史处理，
    /// 因此这里明确跳过串扰，避免把“暂无数据项”伪装成普通指标。
    /// </summary>
    private void AddDisplayResult(TestDataProcessingResult result)
    {
        if (result.Kind == TestProjectKind.Crosstalk || result.Metrics.Count == 0)
            return;

        string projectKey = BuildProjectSeriesKey(result);
        int ordinal = _displayProjectOrdinals.TryGetValue(projectKey, out int previous)
            ? previous + 1 : 1;
        _displayProjectOrdinals[projectKey] = ordinal;
        _displayResultMaximumOrdinal = Math.Max(_displayResultMaximumOrdinal, ordinal);

        foreach (TestMetric metric in result.Metrics)
        {
            string metricKey = string.IsNullOrWhiteSpace(metric.Key)
                ? metric.DisplayName : metric.Key.Trim();
            string seriesKey = $"{projectKey}\u001f{metricKey}";
            if (!_displayResultSeriesByKey.TryGetValue(seriesKey, out DisplayResultSeries? series))
            {
                series = new DisplayResultSeries
                {
                    Key = seriesKey,
                    ProjectName = result.ProjectName,
                    MetricKey = metricKey,
                    MetricName = string.IsNullOrWhiteSpace(metric.DisplayName)
                        ? metricKey : metric.DisplayName
                };
                _displayResultSeriesByKey.Add(seriesKey, series);
                _displayResultSeries.Add(series);
            }

            while (series.Values.Count < ordinal) series.Values.Add(string.Empty);
            // A processor may emit the same metric twice in one iteration. Keep
            // both values visible rather than silently overwriting the first one.
            if (string.IsNullOrEmpty(series.Values[ordinal - 1]))
                series.Values[ordinal - 1] = metric.Value ?? string.Empty;
            else if (!string.IsNullOrWhiteSpace(metric.Value))
                series.Values[ordinal - 1] += "；" + metric.Value;

            if (!string.IsNullOrWhiteSpace(metric.Source)) series.Source = metric.Source;
            if (!string.IsNullOrWhiteSpace(metric.Status)) series.Status = metric.Status;
            if (!string.IsNullOrWhiteSpace(result.OutputDirectory))
                series.OutputDirectory = result.OutputDirectory!;
        }

        RenderResultGrid();
    }

    private static string BuildProjectSeriesKey(TestDataProcessingResult result)
    {
        string project = string.IsNullOrWhiteSpace(result.ProjectName)
            ? "未命名项目" : result.ProjectName.Trim();
        return $"{(int)result.Kind}:{project}";
    }

    /// <summary>按当前序列重建表格，动态增加/移除第 N 次列。</summary>
    private void RenderResultGrid()
    {
        if (gridResults is null || IsDisposed || Disposing) return;

        EnsureResultValueColumns();
        gridResults.Rows.Clear();
        foreach (DisplayResultSeries series in _displayResultSeries)
        {
            // Two identity columns + N value columns + source/status/output.
            var values = new object?[_displayResultMaximumOrdinal + 5];
            values[0] = series.ProjectName;
            values[1] = series.MetricName;
            for (int ordinal = 1; ordinal <= _displayResultMaximumOrdinal; ordinal++)
                values[ordinal + 1] = ordinal <= series.Values.Count
                    ? series.Values[ordinal - 1] : string.Empty;
            values[_displayResultMaximumOrdinal + 2] = series.Source;
            values[_displayResultMaximumOrdinal + 3] = series.Status;
            values[_displayResultMaximumOrdinal + 4] = series.OutputDirectory;
            gridResults.Rows.Add(values);
        }
    }

    private void EnsureResultValueColumns()
    {
        if (gridResults is null) return;

        DataGridViewColumn? baseValue = gridResults.Columns["resultValueColumn"];
        if (baseValue is null) return;

        // Remove columns generated by an earlier render, preserving the
        // designer-owned first value column and the source/status/output columns.
        for (int index = gridResults.Columns.Count - 1; index >= 0; index--)
        {
            DataGridViewColumn column = gridResults.Columns[index];
            if (column.Name.StartsWith("resultValue", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(column.Name, "resultValueColumn", StringComparison.OrdinalIgnoreCase))
            {
                gridResults.Columns.RemoveAt(index);
            }
        }

        int sourceIndex = gridResults.Columns["resultSourceColumn"]?.Index
            ?? gridResults.Columns.Count;
        int wanted = Math.Max(1, _displayResultMaximumOrdinal);
        baseValue.HeaderText = wanted <= 1 ? "值" : "第1次";
        baseValue.Name = "resultValueColumn";

        // The first value column may have moved when the designer was edited;
        // keep it immediately after the project/metric columns.
        int desiredBaseIndex = Math.Min(2, sourceIndex);
        if (baseValue.Index != desiredBaseIndex)
        {
            gridResults.Columns.Remove(baseValue);
            gridResults.Columns.Insert(desiredBaseIndex, baseValue);
        }

        sourceIndex = gridResults.Columns["resultSourceColumn"]?.Index
            ?? gridResults.Columns.Count;
        for (int ordinal = 2; ordinal <= wanted; ordinal++)
        {
            var column = new DataGridViewTextBoxColumn
            {
                Name = $"resultValue{ordinal}Column",
                HeaderText = $"第{ordinal}次",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 85F,
                MinimumWidth = 70
            };
            gridResults.Columns.Insert(sourceIndex++, column);
        }

        // Keep metadata columns after all dynamic value columns.
        MoveColumnAfter("resultSourceColumn", sourceIndex);
        MoveColumnAfter("resultStatusColumn", sourceIndex + 1);
        MoveColumnAfter("resultOutputColumn", sourceIndex + 2);
    }

    private void MoveColumnAfter(string columnName, int targetIndex)
    {
        if (gridResults is null) return;
        DataGridViewColumn? column = gridResults.Columns[columnName];
        if (column is null) return;
        int bounded = Math.Clamp(targetIndex, 0, gridResults.Columns.Count - 1);
        if (column.Index == bounded) return;
        gridResults.Columns.Remove(column);
        gridResults.Columns.Insert(Math.Min(bounded, gridResults.Columns.Count), column);
    }
}
