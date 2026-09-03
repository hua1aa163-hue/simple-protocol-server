using AutoTestClient.DataProcessing;

namespace AutoTestClient;

/// <summary>主界面串扰异常阈值的小型编辑器。</summary>
public partial class DashboardForm
{
    /// <summary>
    /// 应用主界面上的异常阈值。已有原始矩阵时只在内存中重新计算并刷新预览；
    /// 没有矩阵时仍立即保存，下一次串扰处理会使用该值。
    /// </summary>
    private void ButtonApplyCrosstalkThreshold_Click(object? sender, EventArgs e)
    {
        if (_closing || _planRunning) return;

        double thresholdPercent = (double)numericCrosstalkThreshold.Value;
        if (!double.IsFinite(thresholdPercent) || thresholdPercent < 0d || thresholdPercent > 10000d)
        {
            ShowCrosstalkError(new ArgumentOutOfRangeException(
                nameof(thresholdPercent), "异常阈值必须是 0 到 10000% 之间的有限数值。"));
            return;
        }

        try
        {
            // 保留当前颜色轴、掩膜和备注，只替换异常阈值。
            CrosstalkAnalysisOptions current =
                _crosstalkAnalysisOptions ?? BuildCrosstalkAnalysisOptions();
            CrosstalkAnalysisOptions options = current with
            {
                AbnormalThresholdRatio = thresholdPercent / 100d
            };

            heatmapPreview.AnomalyThresholdPercent = thresholdPercent;
            SetCrosstalkThresholdControlValue(thresholdPercent);

            if (_crosstalkCalculation is not null)
            {
                CrosstalkCalculationResult updated = CrosstalkDataProcessor.Recalculate(
                    _crosstalkCalculation, options);
                ApplyCrosstalkAnalysisToDashboard(updated, options);
                AppendLog($"已应用串扰异常阈值 {thresholdPercent:0.###}%，并刷新主界面热图与统计。");
            }
            else
            {
                _crosstalkAnalysisOptions = options;
                labelCrosstalkPreviewHint.Text =
                    $"阈值已保存 {thresholdPercent:0.###}%（下次分析生效）";
                AppendLog($"已保存串扰异常阈值 {thresholdPercent:0.###}%，当前尚无可重算矩阵。");
            }

            ApplyCrosstalkOptionsToConfiguration(options);
            _settingsStore.Save(_configuration);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidDataException)
        {
            ShowCrosstalkError(ex);
        }
        catch (IOException ex)
        {
            AppendLog($"保存串扰异常阈值失败：{ex.Message}");
            if (!_closing)
                MessageBox.Show(this, ex.Message, "保存串扰参数", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            // 设置文件可能位于只读目录或被其他进程占用；阈值重算本身已经完成，
            // 因此只提示持久化失败，不让 WinForms 事件线程冒泡退出。
            AppendLog($"保存串扰异常阈值失败：{ex.Message}");
            if (!_closing)
                MessageBox.Show(this, ex.Message, "保存串扰参数", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
        }
    }

    /// <summary>把配置中的比例安全地映射到设计器可编辑的百分比控件。</summary>
    private void SetCrosstalkThresholdControlValue(double thresholdPercent)
    {
        if (numericCrosstalkThreshold is null || !double.IsFinite(thresholdPercent)) return;
        decimal value = Math.Round((decimal)Math.Clamp(thresholdPercent, 0d, 10000d), 3,
            MidpointRounding.AwayFromZero);
        if (numericCrosstalkThreshold.Value != value)
            numericCrosstalkThreshold.Value = value;
    }
}
