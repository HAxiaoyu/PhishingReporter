using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;
using System.Text.Json;

namespace PhishingReporter.Infrastructure.Data.Repositories
{
    /// <summary>
    /// 上报仓储实现
    /// </summary>
    public class ReportRepository : IReportRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReportRepository> _logger;

        public ReportRepository(AppDbContext context, ILogger<ReportRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task AddAsync(PhishingReport report, CancellationToken cancellationToken)
        {
            // 序列化收件人列表
            if (report.ToRecipients?.Count > 0)
            {
                report.ToRecipientsJson = JsonSerializer.Serialize(report.ToRecipients);
            }
            if (report.CcRecipients?.Count > 0)
            {
                report.CcRecipientsJson = JsonSerializer.Serialize(report.CcRecipients);
            }

            await _context.PhishingReports.AddAsync(report, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Added phishing report {ReportId}", report.Id);
        }

        public async Task UpdateAsync(PhishingReport report, CancellationToken cancellationToken)
        {
            // 序列化收件人列表
            if (report.ToRecipients?.Count > 0)
            {
                report.ToRecipientsJson = JsonSerializer.Serialize(report.ToRecipients);
            }
            if (report.CcRecipients?.Count > 0)
            {
                report.CcRecipientsJson = JsonSerializer.Serialize(report.CcRecipients);
            }

            // 更新主实体 - 只更新字段，不处理子实体（它们在AddAsync中已添加）
            var existingEntry = _context.ChangeTracker.Entries<PhishingReport>()
                .FirstOrDefault(e => e.Entity.Id == report.Id);

            if (existingEntry != null)
            {
                existingEntry.CurrentValues.SetValues(report);
                existingEntry.State = EntityState.Modified;
            }
            else
            {
                _context.PhishingReports.Attach(report);
                _context.Entry(report).State = EntityState.Modified;
            }

            // 只添加新的分析结果（在初始添加时不存在）
            foreach (var analysisResult in report.AnalysisResults)
            {
                var existingAnalysis = await _context.AnalysisResults
                    .FirstOrDefaultAsync(a => a.Id == analysisResult.Id, cancellationToken);

                if (existingAnalysis == null)
                {
                    _context.AnalysisResults.Add(analysisResult);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated phishing report {ReportId}", report.Id);
        }

        public async Task<PhishingReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var report = await _context.PhishingReports
                .Include(r => r.Headers)
                .Include(r => r.Attachments)
                .Include(r => r.AnalysisResults)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (report != null)
            {
                // 反序列化收件人列表
                if (!string.IsNullOrEmpty(report.ToRecipientsJson))
                {
                    report.ToRecipients = JsonSerializer.Deserialize<List<string>>(report.ToRecipientsJson) ?? new();
                }
                if (!string.IsNullOrEmpty(report.CcRecipientsJson))
                {
                    report.CcRecipients = JsonSerializer.Deserialize<List<string>>(report.CcRecipientsJson) ?? new();
                }
            }

            return report;
        }

        public async Task<(List<PhishingReport>, int)> GetPagedAsync(ReportFilter filter, CancellationToken cancellationToken)
        {
            var query = _context.PhishingReports.AsQueryable();

            // 应用过滤器
            if (!string.IsNullOrEmpty(filter.Status))
            {
                query = query.Where(r => r.Status == filter.Status);
            }

            if (!string.IsNullOrEmpty(filter.ReportedBy))
            {
                query = query.Where(r => r.ReportedBy == filter.ReportedBy);
            }

            if (!string.IsNullOrEmpty(filter.SenderEmail))
            {
                query = query.Where(r => r.SenderEmail.Contains(filter.SenderEmail));
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(r => r.ReportedAt >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(r => r.ReportedAt <= filter.ToDate.Value);
            }

            // 获取总数
            var totalCount = await query.CountAsync(cancellationToken);

            // 分页查询
            var reports = await query
                .OrderByDescending(r => r.ReportedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            return (reports, totalCount);
        }

        public async Task<ReportStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
        {
            var stats = new ReportStatistics();

            stats.TotalReports = await _context.PhishingReports.CountAsync(cancellationToken);
            stats.PendingReports = await _context.PhishingReports.CountAsync(r => r.Status == "Pending", cancellationToken);
            stats.AnalyzingReports = await _context.PhishingReports.CountAsync(r => r.Status == "Analyzing", cancellationToken);
            stats.ConfirmedReports = await _context.PhishingReports.CountAsync(r => r.Status == "Confirmed", cancellationToken);
            stats.FalsePositiveReports = await _context.PhishingReports.CountAsync(r => r.Status == "FalsePositive", cancellationToken);
            stats.ResolvedReports = await _context.PhishingReports.CountAsync(r => r.Status == "Resolved", cancellationToken);

            return stats;
        }

        public async Task<Dictionary<string, int>> GetCategoryStatisticsAsync(CancellationToken cancellationToken)
        {
            var result = await _context.PhishingReports
                .Where(r => r.Category != null && r.Category != "")
                .GroupBy(r => r.Category!)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Category, x => x.Count, cancellationToken);

            return result;
        }

        public async Task<List<DailyReportCount>> GetRecentTrendAsync(int days, CancellationToken cancellationToken)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days + 1);

            var result = await _context.PhishingReports
                .Where(r => r.ReportedAt >= startDate)
                .GroupBy(r => r.ReportedAt.Date)
                .Select(g => new DailyReportCount { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync(cancellationToken);

            // 填充没有数据的天数
            var allDates = Enumerable.Range(0, days)
                .Select(offset => startDate.AddDays(offset))
                .ToList();

            var fullTrend = allDates
                .GroupJoin(result, d => d.Date, r => r.Date, (date, reports) => new DailyReportCount
                {
                    Date = date,
                    Count = reports.Sum(r => r.Count)
                })
                .OrderBy(x => x.Date)
                .ToList();

            return fullTrend;
        }
    }
}