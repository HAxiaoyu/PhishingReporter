using PhishingReporter.Core.Models;

namespace PhishingReporter.Core.Interfaces
{
    /// <summary>
    /// 钓鱼邮件分析服务接口
    /// </summary>
    public interface IAnalysisService
    {
        /// <summary>
        /// 分析钓鱼邮件特征
        /// </summary>
        Task<AnalysisResult> AnalyzeAsync(PhishingReport report, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 分析结果
    /// </summary>
    public record AnalysisResult
    {
        /// <summary>风险评分 (0-100)</summary>
        public int RiskScore { get; init; }

        /// <summary>分类</summary>
        public string? Category { get; init; }

        /// <summary>风险指标列表</summary>
        public List<RiskIndicator> Indicators { get; init; } = new();
    }

    /// <summary>
    /// 风险指标
    /// </summary>
    public record RiskIndicator
    {
        public string Type { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int Severity { get; init; }  // 1-5
        public string? Details { get; init; }
    }
}