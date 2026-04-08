using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace PhishingReporter.Infrastructure.Notification
{
    /// <summary>
    /// 通知配置设置
    /// </summary>
    public class NotificationSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 25;
        public bool SmtpUseSSL { get; set; } = false;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string SecurityTeamEmail { get; set; } = string.Empty;
        public string TeamsWebhookUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// 通知服务实现
    /// 通过邮件和 Teams Webhook 发送通知
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly NotificationSettings _settings;
        private readonly ILogger<NotificationService> _logger;
        private readonly HttpClient _httpClient;

        public NotificationService(
            IOptions<NotificationSettings> settings,
            ILogger<NotificationService> logger,
            HttpClient httpClient)
        {
            _settings = settings.Value;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task NotifyNewReportAsync(PhishingReport report)
        {
            if (string.IsNullOrEmpty(_settings.SecurityTeamEmail))
            {
                _logger.LogWarning("Security team email not configured, skipping email notification");
                return;
            }

            try
            {
                // 发送邮件通知
                await SendEmailNotificationAsync(report);

                // 发送 Teams 通知（如果配置了）
                if (!string.IsNullOrEmpty(_settings.TeamsWebhookUrl))
                {
                    await SendTeamsNotificationAsync(report);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for report {ReportId}", report.Id);
            }
        }

        public async Task NotifyStatusUpdateAsync(PhishingReport report, string previousStatus)
        {
            if (string.IsNullOrEmpty(report.ReportedBy))
                return;

            try
            {
                // XSS 防护：对所有用户输入进行 HTML 转义
                var escapedSubject = WebUtility.HtmlEncode(report.Subject ?? "");
                var escapedPreviousStatus = WebUtility.HtmlEncode(previousStatus);
                var escapedStatus = WebUtility.HtmlEncode(report.Status);

                var subject = $"钓鱼邮件上报状态更新 - {escapedSubject}";
                var body = $@"
<h2>上报状态已更新</h2>
<p>您上报的钓鱼邮件状态已更新：</p>
<table>
    <tr><td><strong>报告 ID:</strong></td><td>{report.Id}</td></tr>
    <tr><td><strong>邮件主题:</strong></td><td>{escapedSubject}</td></tr>
    <tr><td><strong>原状态:</strong></td><td>{escapedPreviousStatus}</td></tr>
    <tr><td><strong>新状态:</strong></td><td>{escapedStatus}</td></tr>
</table>
<p>如有疑问，请联系安全团队。</p>";

                await SendEmailAsync(report.ReportedBy, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status update notification");
            }
        }

        private async Task SendEmailNotificationAsync(PhishingReport report)
        {
            // XSS 防护：对所有用户输入进行 HTML 转义
            var escapedSubject = WebUtility.HtmlEncode(report.Subject ?? "");
            var escapedReportedBy = WebUtility.HtmlEncode(report.ReportedBy ?? "Unknown");
            var escapedSenderName = WebUtility.HtmlEncode(report.SenderName ?? "");
            var escapedSenderEmail = WebUtility.HtmlEncode(report.SenderEmail ?? "");
            var escapedUserNotes = WebUtility.HtmlEncode(report.UserNotes ?? "无");

            var subject = $"[钓鱼上报] 新的钓鱼邮件上报 - {escapedSubject}";
            var riskLevel = GetRiskLevel(report.RiskScore);

            var body = $@"
<h2>🎣 新的钓鱼邮件上报</h2>
<table style='border-collapse: collapse; width: 100%;'>
    <tr style='background-color: #f5f5f5;'>
        <td style='padding: 10px; border: 1px solid #ddd;'><strong>报告 ID</strong></td>
        <td style='padding: 10px; border: 1px solid #ddd;'>{report.Id}</td>
    </tr>
    <tr>
        <td style='padding: 10px; border: 1px solid #ddd;'><strong>上报时间</strong></td>
        <td style='padding: 10px; border: 1px solid #ddd;'>{report.ReportedAt:yyyy-MM-dd HH:mm:ss}</td>
    </tr>
    <tr style='background-color: #f5f5f5;'>
        <td style='padding: 10px; border: 1px solid #ddd;'><strong>上报人</strong></td>
        <td style='padding: 10px; border: 1px solid #ddd;'>{escapedReportedBy}</td>
    </tr>
    <tr>
        <td style='padding: 10px; border: 1px solid #ddd;'><strong>发件人</strong></td>
        <td style='padding: 10px; border: 1px solid #ddd;'>{escapedSenderName} &lt;{escapedSenderEmail}&gt;</td>
    </tr>
    <tr style='background-color: #f5f5f5;'>
        <td style='padding: 10px; border: 1px solid #ddd;'><strong>邮件主题</strong></td>
        <td style='padding: 10px; border: 1px solid #ddd;'>{escapedSubject}</td>
    </tr>
    <tr>
        <td style='padding: 10px; border: 1px solid #ddd;'><strong>风险评分</strong></td>
        <td style='padding: 10px; border: 1px solid #ddd; color: {GetRiskColor(report.RiskScore)};'>{report.RiskScore}/100 ({riskLevel})</td>
    </tr>
    <tr style='background-color: #f5f5f5;'>
        <td style='padding: 10px; border: 1px solid #ddd;'><strong>附件数</strong></td>
        <td style='padding: 10px; border: 1px solid #ddd;'>{report.Attachments?.Count ?? 0}</td>
    </tr>
    <tr>
        <td style='padding: 10px; border: 1px solid #ddd;'><strong>用户备注</strong></td>
        <td style='padding: 10px; border: 1px solid #ddd;'>{escapedUserNotes}</td>
    </tr>
</table>
<p><a href='#'>点击查看详情</a></p>";

            await SendEmailAsync(_settings.SecurityTeamEmail, subject, body);
            _logger.LogInformation("Sent email notification for report {ReportId}", report.Id);
        }

        private async Task SendTeamsNotificationAsync(PhishingReport report)
        {
            // XSS 防护：转义用户输入
            var escapedSubject = WebUtility.HtmlEncode(report.Subject ?? "");
            var escapedReportedBy = WebUtility.HtmlEncode(report.ReportedBy ?? "Unknown");
            var escapedSenderName = WebUtility.HtmlEncode(report.SenderName ?? "");
            var escapedSenderEmail = WebUtility.HtmlEncode(report.SenderEmail ?? "");
            var riskLevel = GetRiskLevel(report.RiskScore);

            var card = new
            {
                type = "MessageCard",
                context = "http://schema.org/extensions",
                themeColor = GetRiskHexColor(report.RiskScore),
                summary = $"新的钓鱼邮件上报 - {escapedSubject}",
                sections = new[]
                {
                    new
                    {
                        activityTitle = "🎣 新的钓鱼邮件上报",
                        activitySubtitle = escapedSubject,
                        facts = new[]
                        {
                            new { name = "报告 ID", value = report.Id.ToString() },
                            new { name = "上报时间", value = report.ReportedAt.ToString("yyyy-MM-dd HH:mm:ss") },
                            new { name = "上报人", value = escapedReportedBy },
                            new { name = "发件人", value = $"{escapedSenderName} <{escapedSenderEmail}>" },
                            new { name = "风险评分", value = $"{report.RiskScore}/100 ({riskLevel})" },
                            new { name = "附件数", value = (report.Attachments?.Count ?? 0).ToString() }
                        },
                        markdown = true
                    }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(card);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_settings.TeamsWebhookUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Sent Teams notification for report {ReportId}", report.Id);
            }
            else
            {
                _logger.LogWarning("Failed to send Teams notification: {StatusCode}", response.StatusCode);
            }
        }

        private async Task SendEmailAsync(string to, string subject, string body)
        {
            if (string.IsNullOrEmpty(_settings.SmtpHost))
            {
                _logger.LogWarning("SMTP host not configured, skipping email");
                return;
            }

            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.SmtpUseSSL,
                Credentials = string.IsNullOrEmpty(_settings.SmtpUsername)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword)
            };

            using var message = new MailMessage(_settings.FromAddress, to)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await client.SendMailAsync(message);
        }

        private static string GetRiskLevel(int riskScore)
        {
            return riskScore switch
            {
                >= 70 => "高风险",
                >= 40 => "中风险",
                >= 20 => "低风险",
                _ => "可忽略"
            };
        }

        private static string GetRiskColor(int riskScore)
        {
            return riskScore switch
            {
                >= 70 => "#d32f2f",
                >= 40 => "#f57c00",
                _ => "#388e3c"
            };
        }

        private static string GetRiskHexColor(int riskScore)
        {
            return riskScore switch
            {
                >= 70 => "d32f2f",
                >= 40 => "f57c00",
                _ => "388e3c"
            };
        }
    }
}