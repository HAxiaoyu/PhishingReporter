using System;
using System.Windows.Forms;

namespace PhishingReporter.Forms
{
    /// <summary>
    /// 进度对话框 - 显示上报进度
    /// </summary>
    public partial class ProgressDialog : Form
    {
        private readonly string _message;
        private Timer _closeTimer;

        public ProgressDialog(string message)
        {
            _message = message;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form 设置
            this.Text = "正在处理";
            this.Size = new System.Drawing.Size(350, 120);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 进度图标/动画
            var progressLabel = new Label();
            progressLabel.Text = _message;
            progressLabel.Location = new System.Drawing.Point(20, 30);
            progressLabel.Size = new System.Drawing.Size(310, 40);
            progressLabel.Font = new System.Drawing.Font("微软雅黑", 10F);
            progressLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.Controls.Add(progressLabel);

            this.ResumeLayout(false);
        }

        /// <summary>
        /// 显示进度对话框（非阻塞）
        /// </summary>
        public static ProgressDialog ShowDialog(string message)
        {
            var dialog = new ProgressDialog(message);
            dialog.Show();
            return dialog;
        }

        /// <summary>
        /// 关闭对话框
        /// </summary>
        public void CloseDialog()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => this.Close()));
            }
            else
            {
                this.Close();
            }
        }
    }
}