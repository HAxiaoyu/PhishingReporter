using System;
using System.Windows.Forms;
using System.Threading.Tasks;
using Outlook = Microsoft.Office.Interop.Outlook;
using PhishingReporter.Services;
using PhishingReporter.Forms;

namespace PhishingReporter.Ribbon
{
    /// <summary>
    /// Outlook 功能区 - 添加钓鱼上报按钮
    /// 注意：此文件需要配合 Visual Studio Ribbon Designer 生成的代码
    /// </summary>
    public partial class ReportRibbon
    {
        private Outlook.Application _application;
        private IConfigManager _config;
        private ILogger _logger;
        private IEmailExtractor _extractor;
        private IReportService _reportService;

        /// <summary>
        /// Ribbon 加载时初始化
        /// </summary>
        private void ReportRibbon_Load(object sender, RibbonUIEventArgs e)
        {
            try
            {
                _application = Globals.ThisAddIn.Application;

                // 初始化服务
                _config = new ConfigManager();
                _logger = new Logger(_config.LogFilePath, _config.LogLevel);
                _extractor = new EmailExtractor(_logger);
                _reportService = new ReportService(_config, _logger);

                _logger.Info("PhishingReporter Ribbon loaded successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"插件初始化失败: {ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 上报钓鱼按钮点击事件
        /// </summary>
        private void btnReportPhishing_Click(object sender, RibbonControlEventArgs e)
        {
            try
            {
                var explorer = _application.ActiveExplorer();

                if (explorer == null || explorer.Selection == null || explorer.Selection.Count == 0)
                {
                    MessageBox.Show(
                        "请先选择要上报的邮件。",
                        "提示",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    return;
                }

                // 获取选中的邮件
                var selectedMail = explorer.Selection[1] as Outlook.MailItem;

                if (selectedMail == null)
                {
                    MessageBox.Show(
                        "请选择一封邮件进行上报。",
                        "提示",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                // 显示确认对话框
                using var dialog = new ReportDialog(selectedMail);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // 执行上报
                    ReportPhishingEmailAsync(selectedMail, dialog.UserNotes);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Button click error: {ex.Message}");
                MessageBox.Show(
                    $"操作失败: {ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 异步上报钓鱼邮件
        /// </summary>
        private async void ReportPhishingEmailAsync(Outlook.MailItem mailItem, string userNotes)
        {
            ProgressDialog progressDialog = null;

            try
            {
                // 显示进度
                progressDialog = ProgressDialog.ShowDialog("正在上报钓鱼邮件，请稍候...");

                // 提取邮件信息
                _logger.Info("Extracting email information...");
                var report = _extractor.ExtractReport(mailItem);
                report.UserNotes = userNotes;

                // 提交上报
                _logger.Info("Submitting report to server...");
                var result = await _reportService.SubmitReportAsync(report);

                // 关闭进度对话框
                progressDialog.CloseDialog();

                // 显示结果
                if (result.Success)
                {
                    MessageBox.Show(
                        result.Message,
                        "上报成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );

                    // 可选：标记邮件或移动到特定文件夹
                    if (_config.EnableAutoArchive)
                    {
                        MarkAsReported(mailItem);
                    }
                }
                else
                {
                    MessageBox.Show(
                        result.Message,
                        "上报失败",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Report failed: {ex.Message}");

                if (progressDialog != null)
                {
                    progressDialog.CloseDialog();
                }

                MessageBox.Show(
                    $"上报过程中发生错误: {ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 标记邮件为已上报（可选功能）
        /// </summary>
        private void MarkAsReported(Outlook.MailItem mailItem)
        {
            try
            {
                // 添加自定义分类
                if (mailItem.Categories == null || !mailItem.Categories.Contains("已上报钓鱼"))
                {
                    mailItem.Categories = "已上报钓鱼";
                    mailItem.Save();
                    _logger.Info("Email marked as reported");
                }

                // 可选：移动到指定文件夹
                // MoveToArchiveFolder(mailItem);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to mark email: {ex.Message}");
            }
        }

        /// <summary>
        /// 移动邮件到存档文件夹（可选）
        /// </summary>
        private void MoveToArchiveFolder(Outlook.MailItem mailItem)
        {
            try
            {
                var folder = FindOrCreateFolder(_config.ArchiveFolderPath);
                if (folder != null)
                {
                    mailItem.Move(folder);
                    _logger.Info($"Email moved to {folder.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to move email: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找或创建文件夹
        /// </summary>
        private Outlook.MAPIFolder FindOrCreateFolder(string folderName)
        {
            try
            {
                var inbox = _application.Session.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderInbox);
                var parentFolder = inbox.Parent as Outlook.MAPIFolder;

                // 查找现有文件夹
                foreach (Outlook.MAPIFolder folder in parentFolder.Folders)
                {
                    if (folder.Name == folderName)
                    {
                        return folder;
                    }
                }

                // 创建新文件夹
                var newFolder = parentFolder.Folders.Add(folderName, Outlook.OlFolderType.olFolderInbox);
                return newFolder;
            }
            catch (Exception ex)
            {
                _logger.Warning($"Failed to find/create folder: {ex.Message}");
                return null;
            }
        }
    }
}