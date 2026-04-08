using Outlook = Microsoft.Office.Interop.Outlook;
using PhishingReporter.Models;

namespace PhishingReporter.Services
{
    /// <summary>
    /// 邮件提取服务接口
    /// </summary>
    public interface IEmailExtractor
    {
        /// <summary>
        /// 从 Outlook MailItem 提取完整信息
        /// </summary>
        PhishingReport ExtractReport(Outlook.MailItem mailItem);

        /// <summary>
        /// 导出邮件为 EML 格式（Base64）
        /// </summary>
        string ExportToEml(Outlook.MailItem mailItem);

        /// <summary>
        /// 提取 Internet 邮件头
        /// </summary>
        System.Collections.Generic.Dictionary<string, string> ExtractHeaders(Outlook.MailItem mailItem);
    }
}