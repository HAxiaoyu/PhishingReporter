using System;
using System.Windows.Forms;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace PhishingReporter.Forms
{
    /// <summary>
    /// 上报确认对话框
    /// 显示邮件摘要信息，允许用户添加备注后确认上报
    /// </summary>
    public partial class ReportDialog : Form
    {
        private readonly Outlook.MailItem _mailItem;
        public string UserNotes { get; private set; }

        public ReportDialog(Outlook.MailItem mailItem)
        {
            _mailItem = mailItem ?? throw new ArgumentNullException(nameof(mailItem));

            InitializeComponent();
            PopulateMailInfo();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form 设置
            this.Text = "上报钓鱼邮件";
            this.Size = new System.Drawing.Size(500, 450);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 标题标签
            var titleLabel = new Label();
            titleLabel.Text = "钓鱼邮件上报确认";
            titleLabel.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            titleLabel.Location = new System.Drawing.Point(20, 20);
            titleLabel.Size = new System.Drawing.Size(460, 30);
            titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.Controls.Add(titleLabel);

            // 信息面板
            var infoPanel = new Panel();
            infoPanel.Location = new System.Drawing.Point(20, 60);
            infoPanel.Size = new System.Drawing.Size(460, 200);
            infoPanel.BorderStyle = BorderStyle.FixedSingle;
            infoPanel.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
            this.Controls.Add(infoPanel);

            // 发件人
            var senderLabel = new Label();
            senderLabel.Text = "发件人:";
            senderLabel.Location = new System.Drawing.Point(10, 10);
            senderLabel.Size = new System.Drawing.Size(80, 25);
            senderLabel.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            infoPanel.Controls.Add(senderLabel);

            var senderValue = new Label();
            senderValue.Name = "senderValue";
            senderValue.Location = new System.Drawing.Point(100, 10);
            senderValue.Size = new System.Drawing.Size(350, 25);
            senderValue.Font = new System.Drawing.Font("微软雅黑", 9F);
            infoPanel.Controls.Add(senderValue);

            // 主题
            var subjectLabel = new Label();
            subjectLabel.Text = "主题:";
            subjectLabel.Location = new System.Drawing.Point(10, 40);
            subjectLabel.Size = new System.Drawing.Size(80, 25);
            subjectLabel.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            infoPanel.Controls.Add(subjectLabel);

            var subjectValue = new Label();
            subjectValue.Name = "subjectValue";
            subjectValue.Location = new System.Drawing.Point(100, 40);
            subjectValue.Size = new System.Drawing.Size(350, 25);
            subjectValue.Font = new System.Drawing.Font("微软雅黑", 9F);
            infoPanel.Controls.Add(subjectValue);

            // 时间
            var timeLabel = new Label();
            timeLabel.Text = "发送时间:";
            timeLabel.Location = new System.Drawing.Point(10, 70);
            timeLabel.Size = new System.Drawing.Size(80, 25);
            timeLabel.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            infoPanel.Controls.Add(timeLabel);

            var timeValue = new Label();
            timeValue.Name = "timeValue";
            timeValue.Location = new System.Drawing.Point(100, 70);
            timeValue.Size = new System.Drawing.Size(350, 25);
            timeValue.Font = new System.Drawing.Font("微软雅黑", 9F);
            infoPanel.Controls.Add(timeValue);

            // 附件
            var attachmentLabel = new Label();
            attachmentLabel.Text = "附件数:";
            attachmentLabel.Location = new System.Drawing.Point(10, 100);
            attachmentLabel.Size = new System.Drawing.Size(80, 25);
            attachmentLabel.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            infoPanel.Controls.Add(attachmentLabel);

            var attachmentValue = new Label();
            attachmentValue.Name = "attachmentValue";
            attachmentValue.Location = new System.Drawing.Point(100, 100);
            attachmentValue.Size = new System.Drawing.Size(350, 25);
            attachmentValue.Font = new System.Drawing.Font("微软雅黑", 9F);
            infoPanel.Controls.Add(attachmentValue);

            // 提示
            var tipLabel = new Label();
            tipLabel.Text = "此邮件将被上报至安全团队进行分析。";
            tipLabel.Location = new System.Drawing.Point(10, 130);
            tipLabel.Size = new System.Drawing.Size(440, 60);
            tipLabel.Font = new System.Drawing.Font("微软雅黑", 9F);
            tipLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
            infoPanel.Controls.Add(tipLabel);

            // 备注标签
            var notesLabel = new Label();
            notesLabel.Text = "备注 (可选):";
            notesLabel.Location = new System.Drawing.Point(20, 280);
            notesLabel.Size = new System.Drawing.Size(460, 25);
            notesLabel.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.Controls.Add(notesLabel);

            // 备注文本框
            var notesTextBox = new TextBox();
            notesTextBox.Name = "notesTextBox";
            notesTextBox.Location = new System.Drawing.Point(20, 310);
            notesTextBox.Size = new System.Drawing.Size(460, 60);
            notesTextBox.Multiline = true;
            notesTextBox.Font = new System.Drawing.Font("微软雅黑", 9F);
            notesTextBox.PlaceholderText = "请描述您认为此邮件可疑的原因...";
            this.Controls.Add(notesTextBox);

            // 确认按钮
            var confirmButton = new Button();
            confirmButton.Text = "确认上报";
            confirmButton.Location = new System.Drawing.Point(280, 380);
            confirmButton.Size = new System.Drawing.Size(100, 35);
            confirmButton.Font = new System.Drawing.Font("微软雅黑", 9F);
            confirmButton.BackColor = System.Drawing.Color.FromArgb(0, 120, 215);
            confirmButton.ForeColor = System.Drawing.Color.White;
            confirmButton.FlatStyle = FlatStyle.Flat;
            confirmButton.DialogResult = DialogResult.OK;
            confirmButton.Click += new EventHandler(ConfirmButton_Click);
            this.Controls.Add(confirmButton);

            // 取消按钮
            var cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new System.Drawing.Point(390, 380);
            cancelButton.Size = new System.Drawing.Size(90, 35);
            cancelButton.Font = new System.Drawing.Font("微软雅黑", 9F);
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.DialogResult = DialogResult.Cancel;
            this.Controls.Add(cancelButton);

            // 设置 AcceptButton 和 CancelButton
            this.AcceptButton = confirmButton;
            this.CancelButton = cancelButton;

            this.ResumeLayout(false);
        }

        private void PopulateMailInfo()
        {
            try
            {
                // 查找控件
                var senderValue = this.Controls.Find("senderValue", true)[0] as Label;
                var subjectValue = this.Controls.Find("subjectValue", true)[0] as Label;
                var timeValue = this.Controls.Find("timeValue", true)[0] as Label;
                var attachmentValue = this.Controls.Find("attachmentValue", true)[0] as Label;

                if (senderValue != null)
                {
                    senderValue.Text = $"{_mailItem.SenderName} <{_mailItem.SenderEmailAddress}>";
                }

                if (subjectValue != null)
                {
                    subjectValue.Text = _mailItem.Subject ?? "无主题";
                    // 截断过长主题
                    if (subjectValue.Text.Length > 50)
                    {
                        subjectValue.Text = subjectValue.Text.Substring(0, 50) + "...";
                    }
                }

                if (timeValue != null)
                {
                    timeValue.Text = _mailItem.SentOn.ToString("yyyy-MM-dd HH:mm:ss");
                }

                if (attachmentValue != null)
                {
                    var count = _mailItem.Attachments?.Count ?? 0;
                    attachmentValue.Text = count > 0 ? $"{count} 个附件" : "无附件";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PopulateMailInfo error: {ex.Message}");
            }
        }

        private void ConfirmButton_Click(object sender, EventArgs e)
        {
            // 获取备注文本
            var notesTextBox = this.Controls.Find("notesTextBox", true)[0] as TextBox;
            if (notesTextBox != null)
            {
                UserNotes = notesTextBox.Text?.Trim() ?? string.Empty;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}