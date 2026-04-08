using PhishingReporter.Core.Models;

namespace PhishingReporter.Core.Interfaces
{
    /// <summary>
    /// 通知服务接口
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// 发送新上报通知
        /// </summary>
        Task NotifyNewReportAsync(PhishingReport report);

        /// <summary>
        /// 发送状态更新通知
        /// </summary>
        Task NotifyStatusUpdateAsync(PhishingReport report, string previousStatus);
    }
}