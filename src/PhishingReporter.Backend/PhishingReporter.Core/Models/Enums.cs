namespace PhishingReporter.Core.Models
{
    /// <summary>
    /// 上报状态枚举
    /// </summary>
    public enum ReportStatus
    {
        /// <summary>待处理</summary>
        Pending = 0,

        /// <summary>分析中</summary>
        Analyzing = 1,

        /// <summary>已确认钓鱼</summary>
        Confirmed = 2,

        /// <summary>误报</summary>
        FalsePositive = 3,

        /// <summary>已解决</summary>
        Resolved = 4,

        /// <summary>分析失败</summary>
        AnalysisFailed = 5
    }

    /// <summary>
    /// 风险类别枚举
    /// </summary>
    public enum RiskCategory
    {
        /// <summary>高风险钓鱼</summary>
        HighRiskPhishing = 0,

        /// <summary>可疑钓鱼</summary>
        SuspiciousPhishing = 1,

        /// <summary>潜在垃圾邮件</summary>
        PotentialSpam = 2,

        /// <summary>低风险</summary>
        LowRisk = 3,

        /// <summary>正常邮件</summary>
        Legitimate = 4
    }

    /// <summary>
    /// 状态扩展方法
    /// </summary>
    public static class ReportStatusExtensions
    {
        public static string ToDisplayString(this ReportStatus status)
        {
            return status switch
            {
                ReportStatus.Pending => "待处理",
                ReportStatus.Analyzing => "分析中",
                ReportStatus.Confirmed => "已确认钓鱼",
                ReportStatus.FalsePositive => "误报",
                ReportStatus.Resolved => "已解决",
                ReportStatus.AnalysisFailed => "分析失败",
                _ => "未知"
            };
        }

        public static ReportStatus FromString(string status)
        {
            return status?.ToLowerInvariant() switch
            {
                "pending" => ReportStatus.Pending,
                "analyzing" => ReportStatus.Analyzing,
                "confirmed" => ReportStatus.Confirmed,
                "falsepositive" => ReportStatus.FalsePositive,
                "resolved" => ReportStatus.Resolved,
                "analysisfailed" => ReportStatus.AnalysisFailed,
                _ => ReportStatus.Pending
            };
        }

        public static string ToApiString(this ReportStatus status)
        {
            return status switch
            {
                ReportStatus.Pending => "Pending",
                ReportStatus.Analyzing => "Analyzing",
                ReportStatus.Confirmed => "Confirmed",
                ReportStatus.FalsePositive => "FalsePositive",
                ReportStatus.Resolved => "Resolved",
                ReportStatus.AnalysisFailed => "AnalysisFailed",
                _ => "Pending"
            };
        }
    }
}