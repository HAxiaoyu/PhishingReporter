using Microsoft.Extensions.Logging;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;

namespace PhishingReporter.Infrastructure.MockServices
{
    /// <summary>
    /// 模拟通知服务 - 用于测试环境
    /// </summary>
    public class MockNotificationService : INotificationService
    {
        private readonly ILogger<MockNotificationService> _logger;

        public MockNotificationService(ILogger<MockNotificationService> logger)
        {
            _logger = logger;
        }

        public Task NotifyNewReportAsync(PhishingReport report)
        {
            _logger.LogInformation(
                "[MOCK] New report notification for {ReportId} - Subject: {Subject}, Sender: {Sender}",
                report.Id,
                report.Subject,
                report.SenderEmail
            );

            // 模拟成功发送通知
            return Task.CompletedTask;
        }

        public Task NotifyStatusUpdateAsync(PhishingReport report, string previousStatus)
        {
            _logger.LogInformation(
                "[MOCK] Status update notification for {ReportId} - {OldStatus} -> {NewStatus}",
                report.Id,
                previousStatus,
                report.Status
            );

            return Task.CompletedTask;
        }
    }
}