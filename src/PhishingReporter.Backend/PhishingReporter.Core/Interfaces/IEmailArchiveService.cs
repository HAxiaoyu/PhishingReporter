using PhishingReporter.Core.Models;

namespace PhishingReporter.Core.Interfaces
{
    /// <summary>
    /// Exchange 邮件存档服务接口
    /// </summary>
    public interface IEmailArchiveService
    {
        /// <summary>
        /// 将钓鱼邮件存档到 Exchange 指定邮箱
        /// </summary>
        Task<ArchiveResult> ArchiveEmailAsync(PhishingReport report, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 存档结果
    /// </summary>
    public record ArchiveResult
    {
        public bool Success { get; init; }
        public string? ArchivedId { get; init; }
        public string? ErrorMessage { get; init; }
    }
}