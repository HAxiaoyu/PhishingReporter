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

        /// <summary>
        /// 获取分类统计
        /// </summary>
        Task<Dictionary<string, int>> GetCategoryStatisticsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 获取近期上报趋势
        /// </summary>
        Task<List<DailyReportCount>> GetRecentTrendAsync(int days, CancellationToken cancellationToken);
    }
}