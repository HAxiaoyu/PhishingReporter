using Microsoft.Extensions.Logging;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;

namespace PhishingReporter.Core.Services
{
    /// <summary>
    /// 钓鱼邮件分析服务实现
    /// MVP 版本实现基础规则分析
    /// </summary>
    public class AnalysisService : IAnalysisService
    {
        private readonly ILogger<AnalysisService> _logger;

        // 可疑域名关键词
        private static readonly string[] SuspiciousKeywords = new[]
        {
            "urgent", "immediate", "verify", "suspended", "unusual activity",
            "confirm your", "click here", "update your", "security alert",
            "account locked", "password expired", "verify your identity"
        };

        // 可疑发件人域名
        private static readonly string[] SuspiciousDomains = new[]
        {
            "temp-mail", "guerrillamail", "10minutemail", "throwaway",
            "mailinator", "fakeinbox", "sharklasers"
        };

        public AnalysisService(ILogger<AnalysisService> logger)
        {
            _logger = logger;
        }

        public async Task<AnalysisResult> AnalyzeAsync(PhishingReport report, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Analyzing phishing report {ReportId}", report.Id);

            var indicators = new List<RiskIndicator>();
            int riskScore = 0;

            // 1. 检查发件人域名
            var senderDomain = GetDomainFromEmail(report.SenderEmail);
            if (!string.IsNullOrEmpty(senderDomain))
            {
                foreach (var suspiciousDomain in SuspiciousDomains)
                {
                    if (senderDomain.Contains(suspiciousDomain, StringComparison.OrdinalIgnoreCase))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Type = "SuspiciousDomain",
                            Description = $"发件人使用可疑临时邮箱域名: {senderDomain}",
                            Severity = 5,
                            Details = senderDomain
                        });
                        riskScore += 30;
                        break;
                    }
                }
            }

            // 2. 检查邮件主题
            if (!string.IsNullOrEmpty(report.Subject))
            {
                foreach (var keyword in SuspiciousKeywords)
                {
                    if (report.Subject.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Type = "SuspiciousSubject",
                            Description = $"邮件主题包含可疑关键词: {keyword}",
                            Severity = 3,
                            Details = keyword
                        });
                        riskScore += 10;
                    }
                }
            }

            // 3. 检查邮件头中的 SPF/DKIM/DMARC
            var headers = report.Headers?.ToDictionary(h => h.HeaderName?.ToLowerInvariant() ?? "", h => h.HeaderValue);
            if (headers != null)
            {
                // SPF 检查
                if (headers.TryGetValue("received-spf", out var spf))
                {
                    if (spf?.Contains("fail", StringComparison.OrdinalIgnoreCase) == true ||
                        spf?.Contains("softfail", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Type = "SPFCheck",
                            Description = "SPF 验证失败",
                            Severity = 4,
                            Details = spf
                        });
                        riskScore += 25;
                    }
                }

                // DMARC 检查
                if (headers.TryGetValue("authentication-results", out var authResults))
                {
                    if (authResults?.Contains("dmarc=fail", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Type = "DMARCCheck",
                            Description = "DMARC 验证失败",
                            Severity = 4,
                            Details = authResults
                        });
                        riskScore += 25;
                    }
                }
            }

            // 4. 检查附件
            if (report.Attachments?.Count > 0)
            {
                foreach (var attachment in report.Attachments)
                {
                    // 检查危险文件扩展名
                    var dangerousExtensions = new[] { ".exe", ".scr", ".bat", ".cmd", ".ps1", ".vbs", ".js" };
                    var extension = Path.GetExtension(attachment.FileName)?.ToLowerInvariant();

                    if (extension != null && dangerousExtensions.Contains(extension))
                    {
                        indicators.Add(new RiskIndicator
                        {
                            Type = "DangerousAttachment",
                            Description = $"危险类型的附件: {attachment.FileName}",
                            Severity = 5,
                            Details = $"Extension: {extension}"
                        });
                        riskScore += 40;
                    }
                }
            }

            // 5. 检查 URL（在正文或邮件头中）
            var bodyPreview = report.BodyPreview ?? "";
            var urlCount = CountUrls(bodyPreview);
            if (urlCount > 3)
            {
                indicators.Add(new RiskIndicator
                {
                    Type = "MultipleUrls",
                    Description = $"邮件包含多个链接 ({urlCount} 个)",
                    Severity = 2,
                    Details = $"URL count: {urlCount}"
                });
                riskScore += urlCount * 3;
            }

            // 限制风险评分在 0-100 范围内
            riskScore = Math.Min(100, riskScore);

            // 确定分类
            string? category = riskScore switch
            {
                >= 70 => "HighRiskPhishing",
                >= 40 => "SuspiciousPhishing",
                >= 20 => "PotentialSpam",
                _ => "LowRisk"
            };

            _logger.LogInformation(
                "Analysis completed for report {ReportId}. Risk score: {RiskScore}, Category: {Category}",
                report.Id,
                riskScore,
                category);

            return new AnalysisResult
            {
                RiskScore = riskScore,
                Category = category,
                Indicators = indicators
            };
        }

        private static string? GetDomainFromEmail(string? email)
        {
            if (string.IsNullOrEmpty(email))
                return null;

            var atIndex = email.IndexOf('@');
            if (atIndex < 0 || atIndex >= email.Length - 1)
                return null;

            return email.Substring(atIndex + 1).ToLowerInvariant();
        }

        private static int CountUrls(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // 简单的 URL 计数（http:// 或 https://）
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf("http", index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                index++;
            }
            return count;
        }
    }
}