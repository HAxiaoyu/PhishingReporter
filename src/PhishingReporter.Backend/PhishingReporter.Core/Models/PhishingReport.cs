using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhishingReporter.Core.Models
{
    /// <summary>
    /// 钓鱼邮件上报实体
    /// </summary>
    [Table("PhishingReports")]
    public class PhishingReport
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>消息 ID (Exchange EntryID)</summary>
        [MaxLength(500)]
        public string? MessageId { get; set; }

        /// <summary>邮件主题</summary>
        [MaxLength(500)]
        public string? Subject { get; set; }

        /// <summary>发件人邮箱</summary>
        [Required]
        [MaxLength(500)]
        public string SenderEmail { get; set; } = string.Empty;

        /// <summary>发件人名称</summary>
        [MaxLength(500)]
        public string? SenderName { get; set; }

        /// <summary>发件人 SMTP 地址</summary>
        [MaxLength(500)]
        public string? SenderSmtpAddress { get; set; }

        /// <summary>收件人列表 (JSON)</summary>
        public string? ToRecipientsJson { get; set; }

        /// <summary>抄送列表 (JSON)</summary>
        public string? CcRecipientsJson { get; set; }

        /// <summary>发送时间</summary>
        public DateTime? SentOn { get; set; }

        /// <summary>接收时间</summary>
        public DateTime? ReceivedTime { get; set; }

        /// <summary>正文预览</summary>
        [MaxLength(2000)]
        public string? BodyPreview { get; set; }

        /// <summary>上报人邮箱</summary>
        [MaxLength(500)]
        public string? ReportedBy { get; set; }

        /// <summary>上报时间</summary>
        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

        /// <summary>用户备注</summary>
        [MaxLength(2000)]
        public string? UserNotes { get; set; }

        /// <summary>状态</summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        /// <summary>风险评分 (0-100)</summary>
        public int RiskScore { get; set; } = 0;

        /// <summary>分类</summary>
        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>EML 文件存储路径</summary>
        [MaxLength(1000)]
        public string? EmlFilePath { get; set; }

        /// <summary>存档邮箱中的消息 ID</summary>
        public string? ArchivedMessageId { get; set; }

        /// <summary>管理员备注</summary>
        [MaxLength(2000)]
        public string? AdminNotes { get; set; }

        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>更新时间</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性
        public ICollection<EmailHeader> Headers { get; set; } = new List<EmailHeader>();
        public ICollection<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
        public ICollection<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();

        // 不映射到数据库的辅助属性
        [NotMapped]
        public List<string> ToRecipients { get; set; } = new();
        [NotMapped]
        public List<string> CcRecipients { get; set; } = new();
    }

    /// <summary>
    /// 邮件头实体
    /// </summary>
    [Table("EmailHeaders")]
    public class EmailHeader
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ReportId { get; set; }

        [MaxLength(200)]
        public string? HeaderName { get; set; }

        public string? HeaderValue { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ReportId")]
        public PhishingReport? Report { get; set; }
    }

    /// <summary>
    /// 邮件附件实体
    /// </summary>
    [Table("EmailAttachments")]
    public class EmailAttachment
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ReportId { get; set; }

        [MaxLength(500)]
        public string? FileName { get; set; }

        [MaxLength(200)]
        public string? MimeType { get; set; }

        public long FileSize { get; set; }

        [MaxLength(128)]
        public string? FileHash { get; set; }

        public string? StoragePath { get; set; }

        public bool IsMalicious { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ReportId")]
        public PhishingReport? Report { get; set; }
    }

    /// <summary>
    /// 分析结果实体
    /// </summary>
    [Table("AnalysisResults")]
    public class AnalysisResult
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid ReportId { get; set; }

        [MaxLength(100)]
        public string? AnalyzerType { get; set; }

        public string? ResultJson { get; set; }

        public string? RiskIndicatorsJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ReportId")]
        public PhishingReport? Report { get; set; }
    }

    /// <summary>
    /// 上报查询过滤器
    /// </summary>
    public record ReportFilter
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 20;
        public string? Status { get; init; }
        public string? ReportedBy { get; init; }
        public string? SenderEmail { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
    }

    /// <summary>
    /// 上报统计
    /// </summary>
    public class ReportStatistics
    {
        public int TotalReports { get; set; }
        public int PendingReports { get; set; }
        public int AnalyzingReports { get; set; }
        public int ConfirmedReports { get; set; }
        public int FalsePositiveReports { get; set; }
        public int ResolvedReports { get; set; }
    }
}