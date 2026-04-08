namespace PhishingReporter.Core.Models
{
    /// <summary>
    /// 分析结果（用于服务层返回）
    /// </summary>
    public record AnalysisOutcome
    {
        public int RiskScore { get; init; }
        public string? Category { get; init; }
        public List<AnalysisIndicator> Indicators { get; init; } = new();
    }
}