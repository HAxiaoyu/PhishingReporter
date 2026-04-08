using System;
using System.Windows.Forms;
using Outlook = Microsoft.Office.Interop.Outlook;
using PhishingReporter.Services;

namespace PhishingReporter
{
    /// <summary>
    /// VSTO Outlook 插件入口点
    /// 此文件需要配合 Visual Studio 生成的 ThisAddIn.Designer.cs
    /// </summary>
    public partial class ThisAddIn
    {
        private IConfigManager _config;
        private ILogger _logger;

        /// <summary>
        /// 插件启动时执行
        /// </summary>
        private void ThisAddIn_Startup(object sender, EventArgs e)
        {
            try
            {
                // 初始化配置和日志
                _config = new ConfigManager();
                _logger = new Logger(_config.LogFilePath, _config.LogLevel);

                _logger.Info("PhishingReporter Add-in starting up...");

                // 注册事件处理
                Application.ItemContextMenuDisplay += Application_ItemContextMenuDisplay;

                _logger.Info("PhishingReporter Add-in started successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"钓鱼上报插件启动失败: {ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 插件关闭时执行
        /// </summary>
        private void ThisAddIn_Shutdown(object sender, EventArgs e)
        {
            try
            {
                _logger?.Info("PhishingReporter Add-in shutting down...");

                // 清理资源
                if (Application != null)
                {
                    Application.ItemContextMenuDisplay -= Application_ItemContextMenuDisplay;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Shutdown error: {ex.Message}");
            }
        }

        /// <summary>
        /// 右键菜单显示事件 - 添加钓鱼上报选项
        /// </summary>
        private void Application_ItemContextMenuDisplay(Outlook.CommandBar commandBar, Outlook.Selection selection)
        {
            try
            {
                // 检查是否选择了邮件
                if (selection == null || selection.Count == 0)
                    return;

                // 检查选中项是否为邮件
                var item = selection[1];
                if (!(item is Outlook.MailItem))
                    return;

                // 添加右键菜单项
                var menuItem = (Outlook.CommandBarButton)commandBar.Controls.Add(
                    Outlook.MsoControlType.msoControlButton,
                    Type.Missing,
                    Type.Missing,
                    Type.Missing,
                    true
                );

                menuItem.Caption = "上报钓鱼邮件";
                menuItem.FaceId = 184;  // 使用警告图标
                menuItem.Tag = "PhishingReporterMenuItem";

                menuItem.Click += MenuItem_Click;

                _logger.Debug("Added context menu item for phishing report");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Context menu error: {ex.Message}");
            }
        }

        /// <summary>
        /// 右键菜单项点击事件
        /// </summary>
        private void MenuItem_Click(Outlook.CommandBarButton ctrl, ref bool cancelDefault)
        {
            try
            {
                var explorer = Application.ActiveExplorer();
                var selection = explorer?.Selection;

                if (selection == null || selection.Count == 0)
                    return;

                var selectedMail = selection[1] as Outlook.MailItem;
                if (selectedMail == null)
                    return;

                // 显示确认对话框
                using var dialog = new Forms.ReportDialog(selectedMail);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    // 执行上报（这里需要调用 Ribbon 中的上报逻辑）
                    // 可以创建一个共享的上报服务来处理
                    ReportPhishingFromContextMenu(selectedMail, dialog.UserNotes);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Context menu click error: {ex.Message}");
                MessageBox.Show(
                    $"上报失败: {ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        /// <summary>
        /// 从右键菜单上报钓鱼邮件
        /// </summary>
        private async void ReportPhishingFromContextMenu(Outlook.MailItem mailItem, string userNotes)
        {
            var progressDialog = Forms.ProgressDialog.ShowDialog("正在上报钓鱼邮件...");

            try
            {
                var extractor = new EmailExtractor(_logger);
                var reportService = new ReportService(_config, _logger);

                var report = extractor.ExtractReport(mailItem);
                report.UserNotes = userNotes;

                var result = await reportService.SubmitReportAsync(report);

                progressDialog.CloseDialog();

                if (result.Success)
                {
                    MessageBox.Show(
                        result.Message,
                        "上报成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
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
                progressDialog.CloseDialog();
                _logger.Error($"Report from context menu failed: {ex.Message}");
                MessageBox.Show(
                    $"上报错误: {ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        #region VSTO 生成的代码

        /// <summary>
        /// 必需的设计器变量 - 不要修改
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理资源
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}