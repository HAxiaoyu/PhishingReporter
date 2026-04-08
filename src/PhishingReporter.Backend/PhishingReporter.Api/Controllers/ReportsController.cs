using Microsoft.AspNetCore.Mvc;
using PhishingReporter.Core.Interfaces;
using PhishingReporter.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace PhishingReporter.Api.Controllers
{
    /// <summary>
    /// 钓鱼邮件上报 API 控制器
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IReportService reportService,
            ILogger<ReportsController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        /// <summary>
        /// 提交钓鱼邮件上报
        /// </summary>
        /// <param name="request">上报请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>上报结果</returns>
        [HttpPost]
        [ProducesResponseType(typeof(SubmitReportResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SubmitReportResponse>> SubmitReport(
            [FromBody] SubmitReportRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Received phishing report from {ReportedBy} for email from {Sender}",
                request.ReportedBy ?? "Unknown",
                request.SenderEmail ?? "Unknown"
            );

            // 验证请求
            if (string.IsNullOrEmpty(request.SenderEmail))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "Sender email is required",
                    Code = "VALIDATION_ERROR"
                });
            }

            try
            {
                var result = await _reportService.ProcessReportAsync(request, cancellationToken);

                if (!result.Success)
                {
                    return BadRequest(new ErrorResponse
                    {
                        Error = result.ErrorMessage ?? "Failed to process report",
                        Code = result.ErrorCode ?? "PROCESSING_ERROR"
                    });
                }

                return CreatedAtAction(
                    nameof(GetReport),
                    new { id = result.ReportId },
                    new SubmitReportResponse
                    {
                        ReportId = result.ReportId!.Value,
                        Message = "Report submitted successfully"
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing phishing report");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
                {
                    Error = "Internal server error",
                    Code = "INTERNAL_ERROR"
                });
            }
        }

        /// <summary>
        /// 获取上报详情
        /// </summary>
        /// <param name="id">上报 ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>上报详情</returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ReportDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ReportDetailResponse>> GetReport(
            Guid id,
            CancellationToken cancellationToken)
        {
            var report = await _reportService.GetReportAsync(id, cancellationToken);

            if (report == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "Report not found",
                    Code = "NOT_FOUND"
                });
            }

            return Ok(report);
        }

        /// <summary>
        /// 获取上报列表
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页数量</param>
        /// <param name="status">状态过滤</param>
        /// <param name="reportedBy">上报人过滤</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>上报列表</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<ReportSummaryResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<ReportSummaryResponse>>> GetReports(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? reportedBy = null,
            CancellationToken cancellationToken = default)
        {
            // 验证参数
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var filter = new ReportFilter
            {
                Page = page,
                PageSize = pageSize,
                Status = status,
                ReportedBy = reportedBy
            };

            var result = await _reportService.GetReportsAsync(filter, cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// 更新上报状态
        /// </summary>
        /// <param name="id">上报 ID</param>
        /// <param name="request">更新请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>无内容</returns>
        [HttpPatch("{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateStatus(
            Guid id,
            [FromBody] UpdateStatusRequest request,
            CancellationToken cancellationToken)
        {
            // 验证状态
            var validStatuses = new[] { "Pending", "Analyzing", "Confirmed", "FalsePositive", "Resolved" };
            if (!validStatuses.Contains(request.Status))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = $"Invalid status. Valid values: {string.Join(", ", validStatuses)}",
                    Code = "VALIDATION_ERROR"
                });
            }

            var success = await _reportService.UpdateStatusAsync(
                id,
                request.Status,
                request.Notes,
                cancellationToken
            );

            if (!success)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "Report not found",
                    Code = "NOT_FOUND"
                });
            }

            return NoContent();
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>统计信息</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(StatisticsResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<StatisticsResponse>> GetStatistics(
            CancellationToken cancellationToken)
        {
            var stats = await _reportService.GetStatisticsAsync(cancellationToken);
            return Ok(stats);
        }
    }

    #region DTO 模型

    /// <summary>
    /// 提交上报请求
    /// </summary>
    public record SubmitReportRequest
    {
        /// <summary>消息 ID</summary>
        public string? MessageId { get; init; }

        /// <summary>邮件主题</summary>
        public string? Subject { get; init; }

        /// <summary>发件人邮箱</summary>
        [Required]
        public string SenderEmail { get; init; } = string.Empty;

        /// <summary>发件人名称</summary>
        public string? SenderName { get; init; }

        /// <summary>收件人列表</summary>
        public List<string>? ToRecipients { get; init; }

        /// <summary>抄送列表</summary>
        public List<string>? CcRecipients { get; init; }

        /// <summary>邮件头</summary>
        public Dictionary<string, string>? Headers { get; init; }

        /// <summary>发送时间</summary>
        public DateTime? SentOn { get; init; }

        /// <summary>接收时间</summary>
        public DateTime? ReceivedTime { get; init; }

        /// <summary>正文预览</summary>
        public string? BodyPreview { get; init; }

        /// <summary>附件列表</summary>
        public List<AttachmentDto>? Attachments { get; init; }

        /// <summary>原始 EML 内容 (Base64)</summary>
        public string? RawEmlBase64 { get; init; }

        /// <summary>上报人邮箱</summary>
        public string? ReportedBy { get; init; }

        /// <summary>用户备注</summary>
        public string? UserNotes { get; init; }
    }

    /// <summary>
    /// 附件 DTO
    /// </summary>
    public record AttachmentDto
    {
        public string? FileName { get; init; }
        public string? MimeType { get; init; }
        public long Size { get; init; }
        public string? ContentBase64 { get; init; }
        public string? Sha256Hash { get; init; }
    }

    /// <summary>
    /// 提交上报响应
    /// </summary>
    public record SubmitReportResponse
    {
        public Guid ReportId { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// 错误响应
    /// </summary>
    public record ErrorResponse
    {
        public string Error { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
    }

    /// <summary>
    /// 上报详情响应
    /// </summary>
    public record ReportDetailResponse
    {
        public Guid Id { get; init; }
        public string Subject { get; init; } = string.Empty;
        public string SenderEmail { get; init; } = string.Empty;
        public string SenderName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int RiskScore { get; init; }
        public string? Category { get; init; }
        public DateTime ReportedAt { get; init; }
        public string? ReportedBy { get; init; }
        public string? UserNotes { get; init; }
        public List<AttachmentInfo>? Attachments { get; init; }
        public List<AnalysisIndicator>? Indicators { get; init; }
    }

    /// <summary>
    /// 上报摘要响应
    /// </summary>
    public record ReportSummaryResponse
    {
        public Guid Id { get; init; }
        public string Subject { get; init; } = string.Empty;
        public string SenderEmail { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int RiskScore { get; init; }
        public DateTime ReportedAt { get; init; }
    }

    /// <summary>
    /// 分页响应
    /// </summary>
    public record PagedResponse<T>
    {
        public List<T> Items { get; init; } = new();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// 更新状态请求
    /// </summary>
    public record UpdateStatusRequest
    {
        [Required]
        public string Status { get; init; } = string.Empty;
        public string? Notes { get; init; }
    }

    /// <summary>
    /// 附件信息
    /// </summary>
    public record AttachmentInfo
    {
        public string? FileName { get; init; }
        public string? MimeType { get; init; }
        public long Size { get; init; }
        public string? Sha256Hash { get; init; }
        public bool IsMalicious { get; init; }
    }

    /// <summary>
    /// 分析指标
    /// </summary>
    public record AnalysisIndicator
    {
        public string Type { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int Severity { get; init; }
    }

    /// <summary>
    /// 统计响应
    /// </summary>
    public record StatisticsResponse
    {
        public int TotalReports { get; init; }
        public int PendingReports { get; init; }
        public int ConfirmedPhishing { get; init; }
        public int FalsePositives { get; init; }
        public Dictionary<string, int> ReportsByStatus { get; init; } = new();
        public Dictionary<string, int> ReportsByCategory { get; init; } = new();
        public List<DailyReportCount> RecentTrend { get; init; } = new();
    }

    /// <summary>
    /// 每日上报数量
    /// </summary>
    public record DailyReportCount
    {
        public DateTime Date { get; init; }
        public int Count { get; init; }
    }

    #endregion
}