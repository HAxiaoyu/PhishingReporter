using Microsoft.Exchange.WebServices.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;

namespace PhishingReporter.Infrastructure.Exchange
{
    /// <summary>
    /// Exchange 配置设置
    /// </summary>
    public class ExchangeSettings
    {
        public string EwsUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string ArchiveFolderName { get; set; } = "Phishing Reports";
        public string ArchiveMailbox { get; set; } = string.Empty;
    }

    /// <summary>
    /// Exchange 邮件存档服务实现
    /// 使用 EWS Managed API 与 Exchange Server 集成
    /// </summary>
    public class EmailArchiveService : IEmailArchiveService
    {
        private readonly ExchangeSettings _settings;
        private readonly ILogger<EmailArchiveService> _logger;
        private ExchangeService? _exchangeService;

        public EmailArchiveService(
            IOptions<ExchangeSettings> settings,
            ILogger<EmailArchiveService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<ArchiveResult> ArchiveEmailAsync(PhishingReport report, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Archiving email for report {ReportId}", report.Id);

                // 检查配置
                if (string.IsNullOrEmpty(_settings.Username) || string.IsNullOrEmpty(_settings.Password))
                {
                    _logger.LogWarning("Exchange credentials not configured, skipping archive");
                    return new ArchiveResult
                    {
                        Success = false,
                        ErrorMessage = "Exchange credentials not configured"
                    };
                }

                var service = GetExchangeService();

                // 查找或创建存档文件夹
                var archiveFolder = await FindOrCreateFolderAsync(
                    service,
                    _settings.ArchiveFolderName,
                    cancellationToken
                );

                // 创建存档邮件
                var emailMessage = new EmailMessage(service);
                emailMessage.Subject = $"[钓鱼上报] {report.Subject}";
                emailMessage.Body = new MessageBody(
                    BodyType.HTML,
                    FormatArchiveBody(report)
                );

                // 设置发件人信息（显示原始发件人）
                if (!string.IsNullOrEmpty(report.SenderName) || !string.IsNullOrEmpty(report.SenderEmail))
                {
                    emailMessage.Sender = new EmailAddress(
                        report.SenderName ?? report.SenderEmail,
                        report.SenderEmail ?? "unknown@unknown.com"
                    );
                }

                // 添加分类标记
                emailMessage.Categories = new[] { "Phishing Report", $"Risk-{report.RiskScore}" };

                // 设置重要性
                emailMessage.Importance = report.RiskScore >= 70 ? Importance.High : Importance.Normal;

                // 添加自定义属性
                var reportIdProperty = new ExtendedPropertyDefinition(
                    DefaultExtendedPropertySet.PublicStrings,
                    "PhishingReportId",
                    MapiPropertyType.String
                );
                emailMessage.SetExtendedProperty(reportIdProperty, report.Id.ToString());

                // 保存到存档文件夹
                emailMessage.Save(archiveFolder.Id);

                _logger.LogInformation(
                    "Archived phishing report {ReportId} to Exchange folder {Folder}",
                    report.Id,
                    _settings.ArchiveFolderName
                );

                return new ArchiveResult
                {
                    Success = true,
                    ArchivedId = emailMessage.Id.UniqueId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive email to Exchange for report {ReportId}", report.Id);
                return new ArchiveResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private ExchangeService GetExchangeService()
        {
            if (_exchangeService != null)
                return _exchangeService;

            _exchangeService = new ExchangeService(ExchangeVersion.Exchange2016)
            {
                Credentials = new WebCredentials(_settings.Username, _settings.Password, _settings.Domain)
            };

            // 配置 EWS URL
            if (!string.IsNullOrEmpty(_settings.EwsUrl))
            {
                _exchangeService.Url = new Uri(_settings.EwsUrl);
            }
            else
            {
                // 自动发现 URL
                _exchangeService.AutodiscoverUrl(_settings.Username, (url) => true);
            }

            // 启用跟踪（调试用）
            _exchangeService.TraceEnabled = true;
            _exchangeService.TraceFlags = TraceFlags.All;
            _exchangeService.TraceListener = new EwsTraceListener(_logger);

            return _exchangeService;
        }

        private async Task<Folder> FindOrCreateFolderAsync(
            ExchangeService service,
            string folderName,
            CancellationToken cancellationToken)
        {
            // 查找现有文件夹
            var view = new FolderView(100);
            var filter = new SearchFilter.IsEqualTo(FolderSchema.DisplayName, folderName);

            var searchResult = await service.FindFoldersAsync(
                WellKnownFolderName.MsgFolderRoot,
                filter,
                view
            );

            if (searchResult.Folders.Count > 0)
            {
                return searchResult.Folders[0];
            }

            // 创建新文件夹
            var newFolder = new Folder(service);
            newFolder.DisplayName = folderName;
            newFolder.FolderClass = "IPF.Note";
            await newFolder.SaveAsync(WellKnownFolderName.MsgFolderRoot);

            _logger.LogInformation("Created Exchange folder: {FolderName}", folderName);

            return newFolder;
        }

        private string FormatArchiveBody(PhishingReport report)
        {
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background-color: #f5f5f5; padding: 15px; margin-bottom: 20px; border-radius: 5px; }}
        .field {{ margin-bottom: 10px; }}
        .label {{ font-weight: bold; color: #333; display: inline-block; width: 120px; }}
        .value {{ color: #666; }}
        .headers {{ background-color: #fafafa; padding: 15px; font-family: monospace; font-size: 12px; overflow-x: auto; border-radius: 5px; }}
        .risk-high {{ color: #d32f2f; }}
        .risk-medium {{ color: #f57c00; }}
        .risk-low {{ color: #388e3c; }}
    </style>
</head>
<body>
    <div class='header'>
        <h2>🎣 钓鱼邮件上报</h2>
        <p><strong>上报时间:</strong> {report.ReportedAt:yyyy-MM-dd HH:mm:ss} UTC</p>
        <p><strong>上报人:</strong> {EscapeHtml(report.ReportedBy)}</p>
        <p><strong>报告 ID:</strong> {report.Id}</p>
        <p><strong>风险评分:</strong> <span class='{GetRiskClass(report.RiskScore)}'>{report.RiskScore}/100</span></p>
        <p><strong>分类:</strong> {report.Category ?? "未分类"}</p>
    </div>

    <h3>📧 邮件信息</h3>
    <div class='field'>
        <span class='label'>主题:</span>
        <span class='value'>{EscapeHtml(report.Subject)}</span>
    </div>

    <div class='field'>
        <span class='label'>发件人:</span>
        <span class='value'>{EscapeHtml(report.SenderName)} &lt;{EscapeHtml(report.SenderEmail)}&gt;</span>
    </div>

    <div class='field'>
        <span class='label'>发送时间:</span>
        <span class='value'>{report.SentOn:yyyy-MM-dd HH:mm:ss}</span>
    </div>

    <div class='field'>
        <span class='label'>收件人:</span>
        <span class='value'>{EscapeHtml(string.Join(", ", report.ToRecipients ?? new List<string>()))}</span>
    </div>

    <div class='field'>
        <span class='label'>附件数:</span>
        <span class='value'>{report.Attachments?.Count ?? 0}</span>
    </div>

    <div class='field'>
        <span class='label'>用户备注:</span>
        <span class='value'>{EscapeHtml(report.UserNotes ?? "无")}</span>
    </div>

    <h3>📋 邮件头</h3>
    <div class='headers'>
        <pre>{FormatHeaders(report.Headers)}</pre>
    </div>
</body>
</html>";

            return html;
        }

        private static string EscapeHtml(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return System.Net.WebUtility.HtmlEncode(text);
        }

        private static string GetRiskClass(int riskScore)
        {
            return riskScore switch
            {
                >= 70 => "risk-high",
                >= 40 => "risk-medium",
                _ => "risk-low"
            };
        }

        private static string FormatHeaders(ICollection<EmailHeader>? headers)
        {
            if (headers == null || headers.Count == 0)
                return "无邮件头信息";

            return string.Join("\n", headers.Select(h => $"{h.HeaderName}: {h.HeaderValue}"));
        }
    }

    /// <summary>
    /// EWS 跟踪监听器
    /// </summary>
    internal class EwsTraceListener : ITraceListener
    {
        private readonly ILogger _logger;

        public EwsTraceListener(ILogger logger)
        {
            _logger = logger;
        }

        public void Trace(string traceType, string traceMessage)
        {
            _logger.LogDebug("EWS {TraceType}: {Message}", traceType, traceMessage);
        }
    }
}