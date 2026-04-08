using PhishingReporter.Core.Models;

namespace PhishingReporter.Core.Interfaces
{
    /// <summary>
    /// 上报仓储接口
    /// </summary>
    public interface IReportRepository
    {
        /// <summary>
        /// 添加上报记录
        /// </summary>
        Task AddAsync(PhishingReport report, CancellationToken cancellationToken);

        /// <summary>
        /// 更新上报记录
        /// </summary>
        Task UpdateAsync(PhishingReport report, CancellationToken cancellationToken);

        /// <summary>
        /// 获取上报记录
        /// </summary>
        Task<PhishingReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// 分页查询
        /// </summary>
        Task<(List<PhishingReport>, int)> GetPagedAsync(ReportFilter filter, CancellationToken cancellationToken);

        /// <summary>
        /// 获取统计信息
        /// </summary>
        Task<ReportStatistics> GetStatisticsAsync(CancellationToken cancellationToken);
    }
}