using Microsoft.Extensions.Logging;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;

namespace PhishingReporter.Infrastructure.MockServices
{
    /// <summary>
    /// 模拟 Exchange 存档服务 - 用于测试环境
    /// </summary>
    public class MockEmailArchiveService : IEmailArchiveService
    {
        private readonly ILogger<MockEmailArchiveService> _logger;

        public MockEmailArchiveService(ILogger<MockEmailArchiveService> logger)
        {
            _logger = logger;
        }

        public Task<ArchiveResult> ArchiveEmailAsync(PhishingReport report, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "[MOCK] Archiving email for report {ReportId} from {Sender}",
                report.Id,
                report.SenderEmail
            );

            // 模拟成功存档
            return Task.FromResult(new ArchiveResult
            {
                Success = true,
                ArchivedId = $"mock-archive-{report.Id}"
            });
        }
    }
}