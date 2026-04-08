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
        [HttpPost]
        [ProducesResponseType(typeof(SubmitReportResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SubmitReportResponse>> SubmitReport(
            [FromBody] SubmitReportRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Received phishing report from {ReportedBy} for email from {Sender}",
                request.ReportedBy ?? "Unknown",
                request.SenderEmail ?? "Unknown"
            );

            if (string.IsNullOrEmpty(request.SenderEmail))
            {
                return BadRequest(new ApiErrorResponse(
                    "Sender email is required",
                    "VALIDATION_ERROR"
                ));
            }

            try
            {
                var result = await _reportService.ProcessReportAsync(request, cancellationToken);

                if (!result.Success)
                {
                    return BadRequest(new ApiErrorResponse(
                        result.ErrorMessage ?? "Failed to process report",
                        result.ErrorCode ?? "PROCESSING_ERROR"
                    ));
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
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new ApiErrorResponse("Internal server error", "INTERNAL_ERROR"));
            }
        }

        /// <summary>
        /// 获取上报详情
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ReportDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ReportDetailResponse>> GetReport(
            Guid id,
            CancellationToken cancellationToken)
        {
            var report = await _reportService.GetReportAsync(id, cancellationToken);

            if (report == null)
            {
                return NotFound(new ApiErrorResponse("Report not found", "NOT_FOUND"));
            }

            return Ok(report);
        }

        /// <summary>
        /// 获取原始邮件内容（MIME格式）
        /// </summary>
        [HttpGet("{id:guid}/raw")]
        [ProducesResponseType(typeof(RawEmailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RawEmailResponse>> GetRawEmail(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.GetRawEmailAsync(id, cancellationToken);

            if (result == null)
            {
                return NotFound(new ApiErrorResponse("Report not found", "NOT_FOUND"));
            }

            if (!result.Success)
            {
                return NotFound(new ApiErrorResponse(
                    result.ErrorMessage ?? "Raw email not available",
                    "RAW_EMAIL_NOT_FOUND"
                ));
            }

            return Ok(new RawEmailResponse
            {
                EmlContentBase64 = result.EmlContentBase64
            });
        }

        /// <summary>
        /// 下载原始邮件文件（.eml格式）
        /// </summary>
        [HttpGet("{id:guid}/download")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadRawEmail(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _reportService.GetRawEmailAsync(id, cancellationToken);

            if (result == null || !result.Success)
            {
                return NotFound(new ApiErrorResponse(
                    result?.ErrorMessage ?? "Raw email not found",
                    "RAW_EMAIL_NOT_FOUND"
                ));
            }

            var emlBytes = Convert.FromBase64String(result.EmlContentBase64!);
            return File(emlBytes, "message/rfc822", $"report-{id:N}.eml");
        }

        /// <summary>
        /// 获取上报列表
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<ReportSummaryResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<ReportSummaryResponse>>> GetReports(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? reportedBy = null,
            CancellationToken cancellationToken = default)
        {
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
        [HttpPatch("{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateStatus(
            Guid id,
            [FromBody] UpdateStatusRequest request,
            CancellationToken cancellationToken)
        {
            var validStatuses = new[] { "Pending", "Analyzing", "Confirmed", "FalsePositive", "Resolved" };
            if (!validStatuses.Contains(request.Status))
            {
                return BadRequest(new ApiErrorResponse(
                    $"Invalid status. Valid values: {string.Join(", ", validStatuses)}",
                    "VALIDATION_ERROR"
                ));
            }

            var success = await _reportService.UpdateStatusAsync(
                id,
                request.Status,
                request.Notes,
                cancellationToken
            );

            if (!success)
            {
                return NotFound(new ApiErrorResponse("Report not found", "NOT_FOUND"));
            }

            return NoContent();
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(StatisticsResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<StatisticsResponse>> GetStatistics(
            CancellationToken cancellationToken)
        {
            var stats = await _reportService.GetStatisticsAsync(cancellationToken);
            return Ok(stats);
        }
    }

    #region Controller-specific DTOs

    /// <summary>
    /// 提交上报响应
    /// </summary>
    public record SubmitReportResponse
    {
        public Guid ReportId { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// API 错误响应
    /// </summary>
    public record ApiErrorResponse(string Error, string Code);

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
    /// 原始邮件响应
    /// </summary>
    public record RawEmailResponse
    {
        /// <summary>原始邮件内容（EML格式，Base64编码）</summary>
        public string? EmlContentBase64 { get; init; }
    }

    #endregion
}