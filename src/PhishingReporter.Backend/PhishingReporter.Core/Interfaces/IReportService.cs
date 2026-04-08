using PhishingReporter.Core.Models;

namespace PhishingReporter.Core.Interfaces
{
    /// <summary>
    /// 上报服务接口 - 处理钓鱼邮件上报的主要业务逻辑
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// 处理上报请求
        /// </summary>
        Task<ProcessReportResult> ProcessReportAsync(SubmitReportRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// 获取上报详情
        /// </summary>
        Task<ReportDetailResponse?> GetReportAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// 获取上报列表
        /// </summary>
        Task<PagedResponse<ReportSummaryResponse>> GetReportsAsync(ReportFilter filter, CancellationToken cancellationToken);

        /// <summary>
        /// 更新上报状态
        /// </summary>
        Task<bool> UpdateStatusAsync(Guid id, string status, string? notes, CancellationToken cancellationToken);

        /// <summary>
        /// 获取统计信息
        /// </summary>
        Task<StatisticsResponse> GetStatisticsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 获取原始邮件内容
        /// </summary>
        Task<RawEmailResult?> GetRawEmailAsync(Guid id, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 上报处理结果
    /// </summary>
    public record ProcessReportResult
    {
        public bool Success { get; init; }
        public Guid? ReportId { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }
    }

    /// <summary>
    /// 原始邮件获取结果
    /// </summary>
    public record RawEmailResult
    {
        public bool Success { get; init; }
        public string? EmlContentBase64 { get; init; }
        public string? ErrorMessage { get; init; }
    }
}