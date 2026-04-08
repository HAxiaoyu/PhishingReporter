namespace PhishingReporter.Core.Models
{
    /// <summary>
    /// 提交上报请求 DTO
    /// </summary>
    public record SubmitReportRequest
    {
        public string? MessageId { get; init; }
        public string? Subject { get; init; }
        public string SenderEmail { get; init; } = string.Empty;
        public string? SenderName { get; init; }
        public List<string>? ToRecipients { get; init; }
        public List<string>? CcRecipients { get; init; }
        public Dictionary<string, string>? Headers { get; init; }
        public DateTime? SentOn { get; init; }
        public DateTime? ReceivedTime { get; init; }
        public string? BodyPreview { get; init; }
        public List<AttachmentDto>? Attachments { get; init; }
        public string? RawEmlBase64 { get; init; }
        public string? ReportedBy { get; init; }
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
    /// 上报详情响应 DTO
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
        /// <summary>邮件头信息</summary>
        public List<EmailHeaderInfo>? Headers { get; init; }
        /// <summary>原始邮件内容（EML格式，Base64编码）</summary>
        public string? RawEmlBase64 { get; init; }
        /// <summary>是否有原始邮件文件</summary>
        public bool HasRawEmail { get; init; }
    }

    /// <summary>
    /// 邮件头信息
    /// </summary>
    public record EmailHeaderInfo
    {
        public string? Name { get; init; }
        public string? Value { get; init; }
    }

    /// <summary>
    /// 上报摘要响应 DTO
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
    /// 分页响应 DTO
    /// </summary>
    public record PagedResponse<T>
    {
        public List<T> Items { get; init; } = new();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
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
        public string? Details { get; init; }
    }

    /// <summary>
    /// 统计响应 DTO
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
}