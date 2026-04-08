using System;
using System.Collections.Generic;

namespace PhishingReporter.Models
{
    /// <summary>
    /// 钓鱼邮件上报数据模型
    /// </summary>
    public class PhishingReport
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // 基本信息
        public string MessageId { get; set; }
        public string Subject { get; set; }
        public string BodyPreview { get; set; }

        // 发件人信息
        public string SenderEmail { get; set; }
        public string SenderName { get; set; }
        public string SenderSmtpAddress { get; set; }

        // 收件人信息
        public List<string> ToRecipients { get; set; } = new List<string>();
        public List<string> CcRecipients { get; set; } = new List<string>();

        // 邮件头
        public Dictionary<string, string> InternetHeaders { get; set; } = new Dictionary<string, string>();

        // 时间信息
        public DateTime SentOn { get; set; }
        public DateTime ReceivedTime { get; set; }

        // 附件
        public List<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();

        // 原始内容
        public string RawEmlBase64 { get; set; }

        // 上报信息
        public string ReportedBy { get; set; }
        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
        public string UserNotes { get; set; }
    }

    /// <summary>
    /// 邮件附件模型
    /// </summary>
    public class EmailAttachment
    {
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public long Size { get; set; }
        public string ContentBase64 { get; set; }
        public string Sha256Hash { get; set; }
    }

    /// <summary>
    /// 应用配置模型
    /// </summary>
    public class AppSettings
    {
        public string ApiBaseUrl { get; set; } = "https://phishing-report.internal/api";
        public string ApiKey { get; set; }
        public int RequestTimeoutSeconds { get; set; } = 30;
        public string LogFilePath { get; set; }
        public bool EnableAutoArchive { get; set; } = true;
        public string ArchiveFolderPath { get; set; } = "已上报钓鱼邮件";
        public string LogLevel { get; set; } = "Info";
    }
}