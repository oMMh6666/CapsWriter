namespace CapsWriterClient
{
    partial class frmCapsWriterClient
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.txtResult = new System.Windows.Forms.TextBox();
            this.lblMicVolumeLevel = new System.Windows.Forms.Label();
            this.btnConnectToServer = new System.Windows.Forms.Button();
            this.txtServerPort = new System.Windows.Forms.TextBox();
            this.txtServerAddress = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 37);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(131, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "CapsWriter服务端端口:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(131, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "CapsWriter服务端地址:";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(12, 84);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(59, 12);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "识别状态:";
            // 
            // txtResult
            // 
            this.txtResult.Location = new System.Drawing.Point(12, 105);
            this.txtResult.MaxLength = 0;
            this.txtResult.Multiline = true;
            this.txtResult.Name = "txtResult";
            this.txtResult.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtResult.Size = new System.Drawing.Size(399, 161);
            this.txtResult.TabIndex = 4;
            // 
            // lblMicVolumeLevel
            // 
            this.lblMicVolumeLevel.AutoSize = true;
            this.lblMicVolumeLevel.Location = new System.Drawing.Point(12, 61);
            this.lblMicVolumeLevel.Name = "lblMicVolumeLevel";
            this.lblMicVolumeLevel.Size = new System.Drawing.Size(83, 12);
            this.lblMicVolumeLevel.TabIndex = 0;
            this.lblMicVolumeLevel.Text = "当前话筒音量:";
            // 
            // btnConnectToServer
            // 
            this.btnConnectToServer.Location = new System.Drawing.Point(253, 8);
            this.btnConnectToServer.Name = "btnConnectToServer";
            this.btnConnectToServer.Size = new System.Drawing.Size(75, 23);
            this.btnConnectToServer.TabIndex = 3;
            this.btnConnectToServer.Text = "连接";
            this.btnConnectToServer.UseVisualStyleBackColor = true;
            this.btnConnectToServer.Click += new System.EventHandler(this.btnConnectToServer_Click);
            // 
            // txtServerPort
            // 
            this.txtServerPort.Location = new System.Drawing.Point(147, 32);
            this.txtServerPort.Name = "txtServerPort";
            this.txtServerPort.Size = new System.Drawing.Size(37, 21);
            this.txtServerPort.TabIndex = 2;
            this.txtServerPort.Text = "6016";
            // 
            // txtServerAddress
            // 
            this.txtServerAddress.Location = new System.Drawing.Point(147, 9);
            this.txtServerAddress.Name = "txtServerAddress";
            this.txtServerAddress.Size = new System.Drawing.Size(100, 21);
            this.txtServerAddress.TabIndex = 1;
            this.txtServerAddress.Text = "192.168.1.254";
            // 
            // frmCapsWriterClient
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(421, 274);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.txtResult);
            this.Controls.Add(this.lblMicVolumeLevel);
            this.Controls.Add(this.btnConnectToServer);
            this.Controls.Add(this.txtServerPort);
            this.Controls.Add(this.txtServerAddress);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "frmCapsWriterClient";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CapsWriter-Client";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmCapsWriterClient_FormClosing);
            this.Load += new System.EventHandler(this.frmCapsWriterClient_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtResult;
        private System.Windows.Forms.Label lblMicVolumeLevel;
        private System.Windows.Forms.Button btnConnectToServer;
        private System.Windows.Forms.TextBox txtServerPort;
        private System.Windows.Forms.TextBox txtServerAddress;
    }
}

