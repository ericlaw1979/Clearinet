namespace Clearinet
{
    partial class frmOptions
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tcOptions = new System.Windows.Forms.TabControl();
            this.pageGeneral = new System.Windows.Forms.TabPage();
            this.gbWebServices = new System.Windows.Forms.GroupBox();
            this.cbxBigSwitch = new System.Windows.Forms.ComboBox();
            this.cbCheckForUpdates = new System.Windows.Forms.CheckBox();
            this.cbAutoStream = new System.Windows.Forms.CheckBox();
            this.cbEnableIPv6 = new System.Windows.Forms.CheckBox();
            this.cbAttachOnStartup = new System.Windows.Forms.CheckBox();
            this.pageHTTPS = new System.Windows.Forms.TabPage();
            this.txtDecryptHosts = new System.Windows.Forms.TextBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.cbxDecryptFor = new System.Windows.Forms.ComboBox();
            this.cbCaptureCONNECT = new System.Windows.Forms.CheckBox();
            this.cbDecryptHTTPS = new System.Windows.Forms.CheckBox();
            this.pageConnections = new System.Windows.Forms.TabPage();
            this.pageGateway = new System.Windows.Forms.TabPage();
            this.pageAppearance = new System.Windows.Forms.TabPage();
            this.pageExtensions = new System.Windows.Forms.TabPage();
            this.txtExtensionsList = new System.Windows.Forms.TextBox();
            this.pagePerformance = new System.Windows.Forms.TabPage();
            this.pagePaths = new System.Windows.Forms.TabPage();
            this.pnlFooter = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.lnkHelp = new System.Windows.Forms.LinkLabel();
            this.tcOptions.SuspendLayout();
            this.pageGeneral.SuspendLayout();
            this.gbWebServices.SuspendLayout();
            this.pageHTTPS.SuspendLayout();
            this.pageExtensions.SuspendLayout();
            this.pnlFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tcOptions
            // 
            this.tcOptions.Controls.Add(this.pageGeneral);
            this.tcOptions.Controls.Add(this.pageHTTPS);
            this.tcOptions.Controls.Add(this.pageConnections);
            this.tcOptions.Controls.Add(this.pageGateway);
            this.tcOptions.Controls.Add(this.pageAppearance);
            this.tcOptions.Controls.Add(this.pageExtensions);
            this.tcOptions.Controls.Add(this.pagePerformance);
            this.tcOptions.Controls.Add(this.pagePaths);
            this.tcOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tcOptions.Location = new System.Drawing.Point(0, 0);
            this.tcOptions.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tcOptions.Name = "tcOptions";
            this.tcOptions.SelectedIndex = 0;
            this.tcOptions.Size = new System.Drawing.Size(678, 346);
            this.tcOptions.TabIndex = 0;
            this.tcOptions.Selecting += new System.Windows.Forms.TabControlCancelEventHandler(this.tcOptions_Selecting);
            // 
            // pageGeneral
            // 
            this.pageGeneral.Controls.Add(this.gbWebServices);
            this.pageGeneral.Controls.Add(this.cbAutoStream);
            this.pageGeneral.Controls.Add(this.cbEnableIPv6);
            this.pageGeneral.Controls.Add(this.cbAttachOnStartup);
            this.pageGeneral.Location = new System.Drawing.Point(4, 26);
            this.pageGeneral.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageGeneral.Name = "pageGeneral";
            this.pageGeneral.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageGeneral.Size = new System.Drawing.Size(670, 316);
            this.pageGeneral.TabIndex = 0;
            this.pageGeneral.Text = "General";
            this.pageGeneral.UseVisualStyleBackColor = true;
            // 
            // gbWebServices
            // 
            this.gbWebServices.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.gbWebServices.Controls.Add(this.cbxBigSwitch);
            this.gbWebServices.Controls.Add(this.cbCheckForUpdates);
            this.gbWebServices.Location = new System.Drawing.Point(378, 23);
            this.gbWebServices.Name = "gbWebServices";
            this.gbWebServices.Size = new System.Drawing.Size(284, 154);
            this.gbWebServices.TabIndex = 7;
            this.gbWebServices.TabStop = false;
            this.gbWebServices.Text = "Clearinet Cloud Services";
            // 
            // cbxBigSwitch
            // 
            this.cbxBigSwitch.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxBigSwitch.FormattingEnabled = true;
            this.cbxBigSwitch.Items.AddRange(new object[] {
            "Allow Web Service requests",
            "Prompt on every Web Service call",
            "Disallow all Web Service calls"});
            this.cbxBigSwitch.Location = new System.Drawing.Point(17, 35);
            this.cbxBigSwitch.Name = "cbxBigSwitch";
            this.cbxBigSwitch.Size = new System.Drawing.Size(252, 25);
            this.cbxBigSwitch.TabIndex = 1;
            // 
            // cbCheckForUpdates
            // 
            this.cbCheckForUpdates.AutoSize = true;
            this.cbCheckForUpdates.Location = new System.Drawing.Point(17, 66);
            this.cbCheckForUpdates.Name = "cbCheckForUpdates";
            this.cbCheckForUpdates.Size = new System.Drawing.Size(210, 21);
            this.cbCheckForUpdates.TabIndex = 0;
            this.cbCheckForUpdates.Text = "Check for Updates on Startup";
            this.cbCheckForUpdates.UseVisualStyleBackColor = true;
            // 
            // cbAutoStream
            // 
            this.cbAutoStream.AutoSize = true;
            this.cbAutoStream.Location = new System.Drawing.Point(23, 77);
            this.cbAutoStream.Name = "cbAutoStream";
            this.cbAutoStream.Size = new System.Drawing.Size(194, 21);
            this.cbAutoStream.TabIndex = 6;
            this.cbAutoStream.Text = "Automatically &stream media";
            this.cbAutoStream.UseVisualStyleBackColor = true;
            // 
            // cbEnableIPv6
            // 
            this.cbEnableIPv6.AutoSize = true;
            this.cbEnableIPv6.Location = new System.Drawing.Point(23, 50);
            this.cbEnableIPv6.Name = "cbEnableIPv6";
            this.cbEnableIPv6.Size = new System.Drawing.Size(186, 21);
            this.cbEnableIPv6.TabIndex = 4;
            this.cbEnableIPv6.Text = "Enable IPv6 (if supported)";
            this.cbEnableIPv6.UseVisualStyleBackColor = true;
            // 
            // cbAttachOnStartup
            // 
            this.cbAttachOnStartup.AutoSize = true;
            this.cbAttachOnStartup.Location = new System.Drawing.Point(23, 23);
            this.cbAttachOnStartup.Name = "cbAttachOnStartup";
            this.cbAttachOnStartup.Size = new System.Drawing.Size(137, 21);
            this.cbAttachOnStartup.TabIndex = 3;
            this.cbAttachOnStartup.Text = "Attach on Startup";
            this.cbAttachOnStartup.UseVisualStyleBackColor = true;
            // 
            // pageHTTPS
            // 
            this.pageHTTPS.Controls.Add(this.txtDecryptHosts);
            this.pageHTTPS.Controls.Add(this.checkBox1);
            this.pageHTTPS.Controls.Add(this.cbxDecryptFor);
            this.pageHTTPS.Controls.Add(this.cbCaptureCONNECT);
            this.pageHTTPS.Controls.Add(this.cbDecryptHTTPS);
            this.pageHTTPS.Location = new System.Drawing.Point(4, 26);
            this.pageHTTPS.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageHTTPS.Name = "pageHTTPS";
            this.pageHTTPS.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageHTTPS.Size = new System.Drawing.Size(670, 316);
            this.pageHTTPS.TabIndex = 1;
            this.pageHTTPS.Text = "HTTPS";
            this.pageHTTPS.UseVisualStyleBackColor = true;
            // 
            // txtDecryptHosts
            // 
            this.txtDecryptHosts.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDecryptHosts.Location = new System.Drawing.Point(54, 209);
            this.txtDecryptHosts.Multiline = true;
            this.txtDecryptHosts.Name = "txtDecryptHosts";
            this.txtDecryptHosts.Size = new System.Drawing.Size(581, 0);
            this.txtDecryptHosts.TabIndex = 7;
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(54, 140);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(265, 21);
            this.checkBox1.TabIndex = 6;
            this.checkBox1.Text = "&Ignore server certificate errors (unsafe)";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // cbxDecryptFor
            // 
            this.cbxDecryptFor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxDecryptFor.FormattingEnabled = true;
            this.cbxDecryptFor.Items.AddRange(new object[] {
            "...from all processes",
            "...from browsers",
            "...from non-browsers",
            "...only from remote clients"});
            this.cbxDecryptFor.Location = new System.Drawing.Point(54, 91);
            this.cbxDecryptFor.Name = "cbxDecryptFor";
            this.cbxDecryptFor.Size = new System.Drawing.Size(225, 25);
            this.cbxDecryptFor.TabIndex = 5;
            // 
            // cbCaptureCONNECT
            // 
            this.cbCaptureCONNECT.AutoSize = true;
            this.cbCaptureCONNECT.Location = new System.Drawing.Point(11, 21);
            this.cbCaptureCONNECT.Name = "cbCaptureCONNECT";
            this.cbCaptureCONNECT.Size = new System.Drawing.Size(193, 21);
            this.cbCaptureCONNECT.TabIndex = 4;
            this.cbCaptureCONNECT.Text = "Capture &HTTPS CONNECTs";
            this.cbCaptureCONNECT.UseVisualStyleBackColor = true;
            // 
            // cbDecryptHTTPS
            // 
            this.cbDecryptHTTPS.AutoSize = true;
            this.cbDecryptHTTPS.Location = new System.Drawing.Point(31, 48);
            this.cbDecryptHTTPS.Name = "cbDecryptHTTPS";
            this.cbDecryptHTTPS.Size = new System.Drawing.Size(192, 21);
            this.cbDecryptHTTPS.TabIndex = 0;
            this.cbDecryptHTTPS.Text = "&Decrypt HTTPS exchanges";
            this.cbDecryptHTTPS.UseVisualStyleBackColor = true;
            // 
            // pageConnections
            // 
            this.pageConnections.Location = new System.Drawing.Point(4, 26);
            this.pageConnections.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageConnections.Name = "pageConnections";
            this.pageConnections.Size = new System.Drawing.Size(670, 316);
            this.pageConnections.TabIndex = 2;
            this.pageConnections.Text = "Connections";
            this.pageConnections.UseVisualStyleBackColor = true;
            // 
            // pageGateway
            // 
            this.pageGateway.Location = new System.Drawing.Point(4, 26);
            this.pageGateway.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageGateway.Name = "pageGateway";
            this.pageGateway.Size = new System.Drawing.Size(670, 316);
            this.pageGateway.TabIndex = 3;
            this.pageGateway.Text = "Gateway";
            this.pageGateway.UseVisualStyleBackColor = true;
            // 
            // pageAppearance
            // 
            this.pageAppearance.Location = new System.Drawing.Point(4, 26);
            this.pageAppearance.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageAppearance.Name = "pageAppearance";
            this.pageAppearance.Size = new System.Drawing.Size(670, 316);
            this.pageAppearance.TabIndex = 4;
            this.pageAppearance.Text = "Appearance";
            this.pageAppearance.UseVisualStyleBackColor = true;
            // 
            // pageExtensions
            // 
            this.pageExtensions.Controls.Add(this.txtExtensionsList);
            this.pageExtensions.Location = new System.Drawing.Point(4, 26);
            this.pageExtensions.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageExtensions.Name = "pageExtensions";
            this.pageExtensions.Size = new System.Drawing.Size(670, 316);
            this.pageExtensions.TabIndex = 5;
            this.pageExtensions.Text = "Extensions";
            this.pageExtensions.UseVisualStyleBackColor = true;
            // 
            // txtExtensionsList
            // 
            this.txtExtensionsList.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtExtensionsList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtExtensionsList.Location = new System.Drawing.Point(0, 0);
            this.txtExtensionsList.Multiline = true;
            this.txtExtensionsList.Name = "txtExtensionsList";
            this.txtExtensionsList.ReadOnly = true;
            this.txtExtensionsList.Size = new System.Drawing.Size(670, 316);
            this.txtExtensionsList.TabIndex = 0;
            // 
            // pagePerformance
            // 
            this.pagePerformance.Location = new System.Drawing.Point(4, 26);
            this.pagePerformance.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pagePerformance.Name = "pagePerformance";
            this.pagePerformance.Size = new System.Drawing.Size(670, 316);
            this.pagePerformance.TabIndex = 6;
            this.pagePerformance.Text = "Performance";
            this.pagePerformance.UseVisualStyleBackColor = true;
            // 
            // pagePaths
            // 
            this.pagePaths.Location = new System.Drawing.Point(4, 26);
            this.pagePaths.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pagePaths.Name = "pagePaths";
            this.pagePaths.Size = new System.Drawing.Size(670, 316);
            this.pagePaths.TabIndex = 7;
            this.pagePaths.Text = "Paths";
            this.pagePaths.UseVisualStyleBackColor = true;
            // 
            // pnlFooter
            // 
            this.pnlFooter.Controls.Add(this.btnCancel);
            this.pnlFooter.Controls.Add(this.btnOk);
            this.pnlFooter.Controls.Add(this.lnkHelp);
            this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlFooter.Location = new System.Drawing.Point(0, 346);
            this.pnlFooter.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pnlFooter.Name = "pnlFooter";
            this.pnlFooter.Size = new System.Drawing.Size(678, 64);
            this.pnlFooter.TabIndex = 1;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(578, 9);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(97, 43);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(475, 9);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(97, 43);
            this.btnOk.TabIndex = 1;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // lnkHelp
            // 
            this.lnkHelp.AutoSize = true;
            this.lnkHelp.Location = new System.Drawing.Point(12, 22);
            this.lnkHelp.Name = "lnkHelp";
            this.lnkHelp.Size = new System.Drawing.Size(46, 17);
            this.lnkHelp.TabIndex = 0;
            this.lnkHelp.TabStop = true;
            this.lnkHelp.Text = "Help...";
            this.lnkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkHelp_LinkClicked);
            // 
            // frmOptions
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(678, 410);
            this.Controls.Add(this.tcOptions);
            this.Controls.Add(this.pnlFooter);
            this.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "frmOptions";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Clearinet Options";
            this.Load += new System.EventHandler(this.frmOptions_Load);
            this.tcOptions.ResumeLayout(false);
            this.pageGeneral.ResumeLayout(false);
            this.pageGeneral.PerformLayout();
            this.gbWebServices.ResumeLayout(false);
            this.gbWebServices.PerformLayout();
            this.pageHTTPS.ResumeLayout(false);
            this.pageHTTPS.PerformLayout();
            this.pageExtensions.ResumeLayout(false);
            this.pageExtensions.PerformLayout();
            this.pnlFooter.ResumeLayout(false);
            this.pnlFooter.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tcOptions;
        private System.Windows.Forms.TabPage pageGeneral;
        private System.Windows.Forms.TabPage pageHTTPS;
        private System.Windows.Forms.TabPage pageConnections;
        private System.Windows.Forms.TabPage pageGateway;
        private System.Windows.Forms.TabPage pageAppearance;
        private System.Windows.Forms.TabPage pageExtensions;
        private System.Windows.Forms.TabPage pagePerformance;
        private System.Windows.Forms.TabPage pagePaths;
        private System.Windows.Forms.Panel pnlFooter;
        private System.Windows.Forms.LinkLabel lnkHelp;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.CheckBox cbAttachOnStartup;
        private System.Windows.Forms.TextBox txtExtensionsList;
        private System.Windows.Forms.CheckBox cbEnableIPv6;
        private System.Windows.Forms.CheckBox cbDecryptHTTPS;
        private System.Windows.Forms.GroupBox gbWebServices;
        private System.Windows.Forms.ComboBox cbxBigSwitch;
        private System.Windows.Forms.CheckBox cbCheckForUpdates;
        private System.Windows.Forms.CheckBox cbAutoStream;
        private System.Windows.Forms.CheckBox cbCaptureCONNECT;
        private System.Windows.Forms.TextBox txtDecryptHosts;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.ComboBox cbxDecryptFor;
    }
}