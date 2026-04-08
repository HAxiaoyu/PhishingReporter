using Microsoft.Extensions.Logging;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;
using System.Text.Json;

namespace PhishingReporter.Core.Services
{
    /// <summary>
    /// 上报服务实现 - 处理钓鱼邮件上报的核心业务逻辑
    /// </summary>
    public class ReportService : IReportService
    {
        private readonly IReportRepository _reportRepository;
        private readonly IEmailArchiveService _archiveService;
        private readonly IAnalysisService _analysisService;
        private readonly INotificationService _notificationService;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<ReportService> _logger;

        public ReportService(
            IReportRepository reportRepository,
            IEmailArchiveService archiveService,
            IAnalysisService analysisService,
            INotificationService notificationService,
            IFileStorageService fileStorage,
            ILogger<ReportService> logger)
        {
            _reportRepository = reportRepository;
            _archiveService = archiveService;
            _analysisService = analysisService;
            _notificationService = notificationService;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task<ProcessReportResult> ProcessReportAsync(
            SubmitReportRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation(
                    "Processing phishing report from {ReportedBy} for email from {Sender}",
                    request.ReportedBy,
                    request.SenderEmail);

                // 1. 创建上报记录
                var report = new PhishingReport
                {
                    Id = Guid.NewGuid(),
                    MessageId = request.MessageId,
                    Subject = request.Subject,
                    SenderEmail = request.SenderEmail,
                    SenderName = request.SenderName,
                    SenderSmtpAddress = request.SenderEmail,
                    ToRecipients = request.ToRecipients ?? new List<string>(),
                    CcRecipients = request.CcRecipients ?? new List<string>(),
                    SentOn = request.SentOn,
                    ReceivedTime = request.ReceivedTime,
                    BodyPreview = request.BodyPreview,
                    ReportedBy = request.ReportedBy,
                    ReportedAt = DateTime.UtcNow,
                    UserNotes = request.UserNotes,
                    Status = "Pending"
                };

                // 2. 保存邮件头
                if (request.Headers != null)
                {
                    foreach (var header in request.Headers)
                    {
                        report.Headers.Add(new EmailHeader
                        {
                            Id = Guid.NewGuid(),
                            ReportId = report.Id,
                            HeaderName = header.Key,
                            HeaderValue = header.Value
                        });
                    }
                }

                // 3. 保存附件信息
                if (request.Attachments != null)
                {
                    foreach (var attachment in request.Attachments)
                    {
                        report.Attachments.Add(new EmailAttachment
                        {
                            Id = Guid.NewGuid(),
                            ReportId = report.Id,
                            FileName = attachment.FileName,
                            MimeType = attachment.MimeType,
                            FileSize = attachment.Size,
                            FileHash = attachment.Sha256Hash,
                            IsMalicious = false
                        });
                    }
                }

                // 4. 存储原始 EML 文件
                if (!string.IsNullOrEmpty(request.RawEmlBase64))
                {
                    try
                    {
                        var emlBytes = Convert.FromBase64String(request.RawEmlBase64);
                        report.EmlFilePath = await _fileStorage.SaveEmlAsync(
                            report.Id,
                            emlBytes,
                            cancellationToken
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save EML file for report {ReportId}", report.Id);
                    }
                }

                // 5. 保存到数据库
                await _reportRepository.AddAsync(report, cancellationToken);

                _logger.LogInformation(
                    "Created phishing report {ReportId} for message from {Sender}",
                    report.Id,
                    report.SenderEmail);

                // 6. 存档到 Exchange（异步，不阻塞）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var archiveResult = await _archiveService.ArchiveEmailAsync(report, CancellationToken.None);

                        if (archiveResult.Success)
                        {
                            report.ArchivedMessageId = archiveResult.ArchivedId;
                            await _reportRepository.UpdateAsync(report, CancellationToken.None);
                            _logger.LogInformation("Archived email for report {ReportId}", report.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to archive email for report {ReportId}", report.Id);
                    }
                }, CancellationToken.None);

                // 7. 运行分析
                try
                {
                    var analysisResult = await _analysisService.AnalyzeAsync(report, cancellationToken);
                    report.Status = "Analyzed";
                    report.RiskScore = analysisResult.RiskScore;
                    report.Category = analysisResult.Category;

                    // 保存分析结果
                    report.AnalysisResults.Add(new AnalysisResult
                    {
                        Id = Guid.NewGuid(),
                        ReportId = report.Id,
                        AnalyzerType = "Default",
                        ResultJson = JsonSerializer.Serialize(analysisResult),
                        RiskIndicatorsJson = JsonSerializer.Serialize(analysisResult.Indicators)
                    });

                    await _reportRepository.UpdateAsync(report, cancellationToken);

                    _logger.LogInformation(
                        "Analysis completed for report {ReportId}. Risk score: {RiskScore}",
                        report.Id,
                        report.RiskScore);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Analysis failed for report {ReportId}", report.Id);
                    report.Status = "AnalysisFailed";
                    await _reportRepository.UpdateAsync(report, CancellationToken.None);
                }

                // 8. 发送通知（异步，不阻塞）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _notificationService.NotifyNewReportAsync(report);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send notification for report {ReportId}", report.Id);
                    }
                }, CancellationToken.None);

                return new ProcessReportResult
                {
                    Success = true,
                    ReportId = report.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process phishing report");
                return new ProcessReportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ErrorCode = "PROCESSING_ERROR"
                };
            }
        }

        public async Task<ReportDetailResponse?> GetReportAsync(Guid id, CancellationToken cancellationToken)
        {
            var report = await _reportRepository.GetByIdAsync(id, cancellationToken);

            if (report == null)
                return null;

            return new ReportDetailResponse
            {
                Id = report.Id,
                Subject = report.Subject ?? string.Empty,
                SenderEmail = report.SenderEmail ?? string.Empty,
                SenderName = report.SenderName ?? string.Empty,
                Status = report.Status,
                RiskScore = report.RiskScore,
                Category = report.Category,
                ReportedAt = report.ReportedAt,
                ReportedBy = report.ReportedBy,
                UserNotes = report.UserNotes,
                Attachments = report.Attachments?.Select(a => new AttachmentInfo
                {
                    FileName = a.FileName,
                    MimeType = a.MimeType,
                    Size = a.FileSize,
                    Sha256Hash = a.FileHash,
                    IsMalicious = a.IsMalicious
                }).ToList(),
                Indicators = GetIndicatorsFromAnalysis(report)
            };
        }

        public async Task<PagedResponse<ReportSummaryResponse>> GetReportsAsync(
            ReportFilter filter,
            CancellationToken cancellationToken)
        {
            var (reports, totalCount) = await _reportRepository.GetPagedAsync(filter, cancellationToken);

            var items = reports.Select(r => new ReportSummaryResponse
            {
                Id = r.Id,
                Subject = r.Subject ?? string.Empty,
                SenderEmail = r.SenderEmail ?? string.Empty,
                Status = r.Status,
                RiskScore = r.RiskScore,
                ReportedAt = r.ReportedAt
            }).ToList();

            return new PagedResponse<ReportSummaryResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<bool> UpdateStatusAsync(
            Guid id,
            string status,
            string? notes,
            CancellationToken cancellationToken)
        {
            var report = await _reportRepository.GetByIdAsync(id, cancellationToken);

            if (report == null)
                return false;

            var previousStatus = report.Status;
            report.Status = status;
            report.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(notes))
            {
                report.AdminNotes = notes;
            }

            await _reportRepository.UpdateAsync(report, cancellationToken);

            // 发送状态更新通知
            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.NotifyStatusUpdateAsync(report, previousStatus);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send status update notification");
                }
            }, CancellationToken.None);

            return true;
        }

        public async Task<StatisticsResponse> GetStatisticsAsync(CancellationToken cancellationToken)
        {
            var stats = await _reportRepository.GetStatisticsAsync(cancellationToken);

            return new StatisticsResponse
            {
                TotalReports = stats.TotalReports,
                PendingReports = stats.PendingReports,
                ConfirmedPhishing = stats.ConfirmedReports,
                FalsePositives = stats.FalsePositiveReports,
                ReportsByStatus = new Dictionary<string, int>
                {
                    ["Pending"] = stats.PendingReports,
                    ["Analyzing"] = stats.AnalyzingReports,
                    ["Confirmed"] = stats.ConfirmedReports,
                    ["FalsePositive"] = stats.FalsePositiveReports,
                    ["Resolved"] = stats.ResolvedReports
                }
            };
        }

        private List<AnalysisIndicator>? GetIndicatorsFromAnalysis(PhishingReport report)
        {
            if (report.AnalysisResults == null || !report.AnalysisResults.Any())
                return null;

            var result = report.AnalysisResults.FirstOrDefault();
            if (result?.RiskIndicatorsJson == null)
                return null;

            try
            {
                return JsonSerializer.Deserialize<List<AnalysisIndicator>>(result.RiskIndicatorsJson);
            }
            catch
            {
                return null;
            }
        }
    }
}