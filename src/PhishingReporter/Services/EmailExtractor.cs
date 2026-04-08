using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Outlook = Microsoft.Office.Interop.Outlook;
using PhishingReporter.Models;

namespace PhishingReporter.Services
{
    /// <summary>
    /// 邮件提取服务实现
    /// 从 Outlook MailItem 提取所有必要信息用于钓鱼上报
    /// </summary>
    public class EmailExtractor : IEmailExtractor
    {
        private readonly ILogger _logger;

        // MAPI 属性常量
        private const string PR_TRANSPORT_MESSAGE_HEADERS =
            "http://schemas.microsoft.com/mapi/proptag/0x007D001E";
        private const string PR_INTERNET_CONTENT =
            "http://schemas.microsoft.com/mapi/string/{00020386-0000-0000-C000-000000000046}/InternetContent";
        private const string PR_SMTP_ADDRESS =
            "http://schemas.microsoft.com/mapi/proptag/0x39FE001E";

        public EmailExtractor(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 从 Outlook MailItem 提取完整的钓鱼上报信息
        /// </summary>
        public PhishingReport ExtractReport(Outlook.MailItem mailItem)
        {
            if (mailItem == null)
                throw new ArgumentNullException(nameof(mailItem));

            _logger.Info($"Extracting report from mail: {mailItem.Subject}");

            var report = new PhishingReport
            {
                // 使用 EntryID 作为消息标识
                MessageId = mailItem.EntryID,
                Subject = mailItem.Subject ?? string.Empty,
                BodyPreview = GetBodyPreview(mailItem.Body),

                // 发件人信息
                SenderEmail = mailItem.SenderEmailAddress ?? string.Empty,
                SenderName = mailItem.SenderName ?? string.Empty,
                SenderSmtpAddress = GetSenderSmtpAddress(mailItem),

                // 时间信息
                SentOn = mailItem.SentOn,
                ReceivedTime = mailItem.ReceivedTime,

                // 上报信息
                ReportedBy = GetCurrentUserEmail(),
                ReportedAt = DateTime.UtcNow
            };

            // 提取收件人
            ExtractRecipients(mailItem, report);

            // 提取邮件头
            report.InternetHeaders = ExtractHeaders(mailItem);

            // 提取附件信息
            ExtractAttachments(mailItem, report);

            // 导出原始 EML
            report.RawEmlBase64 = ExportToEml(mailItem);

            _logger.Info($"Report extracted successfully. Attachments: {report.Attachments.Count}");

            return report;
        }

        /// <summary>
        /// 导出邮件为 EML 格式（Base64 编码）
        /// </summary>
        public string ExportToEml(Outlook.MailItem mailItem)
        {
            try
            {
                // 方法1: 使用 PropertyAccessor 直接获取 MIME 内容
                var propertyAccessor = mailItem.PropertyAccessor;
                byte[] contentBytes = null;

                try
                {
                    contentBytes = propertyAccessor.GetProperty(PR_INTERNET_CONTENT) as byte[];
                }
                catch
                {
                    // PR_INTERNET_CONTENT 可能不存在，尝试其他方法
                }

                if (contentBytes != null && contentBytes.Length > 0)
                {
                    _logger.Info("Exported EML using PropertyAccessor");
                    return Convert.ToBase64String(contentBytes);
                }

                // 方法2: 保存为 MSG 文件并手动构建 EML 内容
                // 注意：这不是完整的 MIME 内容，但包含足够的信息
                var emlContent = BuildEmlFromMailItem(mailItem);
                _logger.Info("Exported EML using fallback method");

                return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(emlContent));
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to export EML: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 提取 Internet 邮件头
        /// </summary>
        public Dictionary<string, string> ExtractHeaders(Outlook.MailItem mailItem)
        {
            var headers = new Dictionary<string, string>();

            try
            {
                var propertyAccessor = mailItem.PropertyAccessor;
                string headerString = propertyAccessor.GetProperty(PR_TRANSPORT_MESSAGE_HEADERS)?.ToString();

                if (!string.IsNullOrEmpty(headerString))
                {
                    // 解析邮件头字符串
                    var headerLines = headerString.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                    string currentHeaderName = null;
                    string currentHeaderValue = null;

                    foreach (var line in headerLines)
                    {
                        // 处理多行邮件头（续行以空格或制表符开头）
                        if (line.StartsWith(" ") || line.StartsWith("\t"))
                        {
                            if (currentHeaderName != null)
                            {
                                currentHeaderValue += line.Trim();
                            }
                            continue;
                        }

                        // 保存上一个邮件头
                        if (currentHeaderName != null)
                        {
                            AddHeader(headers, currentHeaderName, currentHeaderValue);
                        }

                        // 解析新邮件头
                        var colonIndex = line.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            currentHeaderName = line.Substring(0, colonIndex).Trim();
                            currentHeaderValue = line.Substring(colonIndex + 1).Trim();
                        }
                    }

                    // 保存最后一个邮件头
                    if (currentHeaderName != null)
                    {
                        AddHeader(headers, currentHeaderName, currentHeaderValue);
                    }
                }

                _logger.Info($"Extracted {headers.Count} headers");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to extract headers: {ex.Message}");
            }

            return headers;
        }

        /// <summary>
        /// 获取发件人的 SMTP 地址
        /// </summary>
        private string GetSenderSmtpAddress(Outlook.MailItem mailItem)
        {
            try
            {
                // 方法1: 使用 PropertyAccessor 获取 SMTP 地址
                var propertyAccessor = mailItem.PropertyAccessor;
                string smtpAddress = propertyAccessor.GetProperty(PR_SMTP_ADDRESS)?.ToString();

                if (!string.IsNullOrEmpty(smtpAddress))
                    return smtpAddress;

                // 方法2: 使用 Sender 属性
                var sender = mailItem.Sender;
                if (sender != null)
                {
                    // 尝试获取 SMTP 地址
                    if (sender.AddressEntryUserType == Outlook.OlAddressEntryUserType.olSmtpAddressEntry)
                    {
                        return sender.Address;
                    }

                    // Exchange 地址需要转换
                    try
                    {
                        var exchangeUser = sender.GetExchangeUser();
                        if (exchangeUser != null)
                        {
                            return exchangeUser.PrimarySmtpAddress;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to get SMTP address: {ex.Message}");
            }

            return mailItem.SenderEmailAddress ?? string.Empty;
        }

        /// <summary>
        /// 提取收件人列表
        /// </summary>
        private void ExtractRecipients(Outlook.MailItem mailItem, PhishingReport report)
        {
            try
            {
                var recipients = mailItem.Recipients;
                if (recipients == null || recipients.Count == 0)
                    return;

                for (int i = 1; i <= recipients.Count; i++)
                {
                    var recipient = recipients[i];
                    string address = GetRecipientAddress(recipient);

                    switch ((Outlook.OlMailRecipientType)recipient.Type)
                    {
                        case Outlook.OlMailRecipientType.olTo:
                            report.ToRecipients.Add(address);
                            break;
                        case Outlook.OlMailRecipientType.olCC:
                            report.CcRecipients.Add(address);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to extract recipients: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取收件人地址
        /// </summary>
        private string GetRecipientAddress(Outlook.Recipient recipient)
        {
            try
            {
                // 尝试获取 SMTP 地址
                if (recipient.AddressEntry.UserType == Outlook.OlAddressEntryUserType.olSmtpAddressEntry)
                {
                    return recipient.AddressEntry.Address;
                }

                // Exchange 地址转换
                try
                {
                    var exchangeUser = recipient.AddressEntry.GetExchangeUser();
                    if (exchangeUser != null)
                    {
                        return exchangeUser.PrimarySmtpAddress;
                    }
                }
                catch { }

                return recipient.Address ?? string.Empty;
            }
            catch
            {
                return recipient.Address ?? string.Empty;
            }
        }

        /// <summary>
        /// 提取附件信息
        /// </summary>
        private void ExtractAttachments(Outlook.MailItem mailItem, PhishingReport report)
        {
            var attachments = mailItem.Attachments;
            if (attachments == null || attachments.Count == 0)
                return;

            for (int i = 1; i <= attachments.Count; i++)
            {
                var attachment = attachments[i];
                try
                {
                    // 保存附件到临时文件
                    var tempPath = Path.Combine(Path.GetTempPath(), $"phishing_{Guid.NewGuid()}_{attachment.FileName}");
                    attachment.SaveAsFile(tempPath);

                    string sha256Hash = string.Empty;
                    byte[] contentBytes = null;

                    if (File.Exists(tempPath))
                    {
                        contentBytes = File.ReadAllBytes(tempPath);
                        using var sha256 = SHA256.Create();
                        var hashBytes = sha256.ComputeHash(contentBytes);
                        sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                        // 清理临时文件
                        File.Delete(tempPath);
                    }

                    report.Attachments.Add(new EmailAttachment
                    {
                        FileName = attachment.FileName,
                        MimeType = GetMimeType(attachment.FileName),
                        Size = attachment.Size,
                        ContentBase64 = contentBytes != null ? Convert.ToBase64String(contentBytes) : null,
                        Sha256Hash = sha256Hash
                    });

                    _logger.Info($"Extracted attachment: {attachment.FileName}, Size: {attachment.Size}");
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Failed to extract attachment {attachment.FileName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取当前用户的邮箱地址
        /// </summary>
        private string GetCurrentUserEmail()
        {
            try
            {
                // 通过 Outlook Session 获取当前用户
                var app = Globals.ThisAddIn?.Application;
                if (app != null)
                {
                    var session = app.Session;
                    var currentUser = session.CurrentUser;

                    if (currentUser != null)
                    {
                        // 尝试获取 SMTP 地址
                        try
                        {
                            var addressEntry = currentUser.AddressEntry;
                            if (addressEntry.UserType == Outlook.OlAddressEntryUserType.olSmtpAddressEntry)
                            {
                                return addressEntry.Address;
                            }

                            var exchangeUser = addressEntry.GetExchangeUser();
                            if (exchangeUser != null)
                            {
                                return exchangeUser.PrimarySmtpAddress;
                            }
                        }
                        catch { }

                        return currentUser.Address ?? currentUser.Name ?? Environment.UserName;
                    }
                }
            }
            catch { }

            return Environment.UserName;
        }

        /// <summary>
        /// 获取正文预览（截取前500字符）
        /// </summary>
        private string GetBodyPreview(object body)
        {
            if (body == null)
                return string.Empty;

            string bodyText = body.ToString();
            return bodyText.Length > 500 ? bodyText.Substring(0, 500) : bodyText;
        }

        /// <summary>
        /// 添加邮件头到字典（处理重复头）
        /// </summary>
        private void AddHeader(Dictionary<string, string> headers, string name, string value)
        {
            if (headers.ContainsKey(name))
            {
                // 重复的邮件头（如 Received）合并
                headers[name] += "\r\n" + value;
            }
            else
            {
                headers[name] = value;
            }
        }

        /// <summary>
        /// 从 MailItem 构建 EML 内容（备用方法）
        /// </summary>
        private string BuildEmlFromMailItem(Outlook.MailItem mailItem)
        {
            var sb = new System.Text.StringBuilder();

            // 添加邮件头
            var headers = ExtractHeaders(mailItem);
            foreach (var header in headers)
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }

            // 添加基本头（如果没有）
            if (!headers.ContainsKey("From"))
            {
                sb.AppendLine($"From: {mailItem.SenderName} <{mailItem.SenderEmailAddress}>");
            }

            if (!headers.ContainsKey("To"))
            {
                var toRecipients = string.Join(", ", mailItem.Recipients
                    .Cast<Outlook.Recipient>()
                    .Where(r => r.Type == (int)Outlook.OlMailRecipientType.olTo)
                    .Select(r => r.Address));
                sb.AppendLine($"To: {toRecipients}");
            }

            if (!headers.ContainsKey("Subject"))
            {
                sb.AppendLine($"Subject: {mailItem.Subject}");
            }

            if (!headers.ContainsKey("Date"))
            {
                sb.AppendLine($"Date: {mailItem.SentOn:R}");
            }

            // 添加空行分隔头和正文
            sb.AppendLine();

            // 添加正文
            if (mailItem.BodyFormat == Outlook.OlBodyFormat.olFormatHTML)
            {
                sb.AppendLine(mailItem.HTMLBody);
            }
            else
            {
                sb.AppendLine(mailItem.Body);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 根据文件扩展名获取 MIME 类型
        /// </summary>
        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".zip" => "application/zip",
                ".exe" => "application/octet-stream",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                ".html" or ".htm" => "text/html",
                _ => "application/octet-stream"
            };
        }
    }
}