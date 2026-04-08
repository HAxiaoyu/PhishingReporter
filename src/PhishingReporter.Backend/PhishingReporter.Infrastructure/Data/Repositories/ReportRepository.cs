using Microsoft.EntityFrameworkCore;
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

            _context.PhishingReports.Update(report);
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
    }
}