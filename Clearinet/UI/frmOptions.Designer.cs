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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmOptions));
            this.tcOptions = new System.Windows.Forms.TabControl();
            this.pageGeneral = new System.Windows.Forms.TabPage();
            this.gbWebServices = new System.Windows.Forms.GroupBox();
            this.cbxBigSwitch = new System.Windows.Forms.ComboBox();
            this.cbCheckForUpdates = new System.Windows.Forms.CheckBox();
            this.cbAttachOnStartup = new System.Windows.Forms.CheckBox();
            this.pageNetworking = new System.Windows.Forms.TabPage();
            this.udListenPort = new System.Windows.Forms.NumericUpDown();
            this.cbEnableIPv6 = new System.Windows.Forms.CheckBox();
            this.cbAllowRemoteConnections = new System.Windows.Forms.CheckBox();
            this.lblListenOn = new System.Windows.Forms.Label();
            this.pageGateway = new System.Windows.Forms.TabPage();
            this.cbChainToGateway = new System.Windows.Forms.CheckBox();
            this.pageHTTPS = new System.Windows.Forms.TabPage();
            this.txtDecryptHosts = new System.Windows.Forms.TextBox();
            this.cbIgnoreUpstreamCertErrors = new System.Windows.Forms.CheckBox();
            this.cbxDecryptFor = new System.Windows.Forms.ComboBox();
            this.cbCaptureCONNECT = new System.Windows.Forms.CheckBox();
            this.cbDecryptHTTPS = new System.Windows.Forms.CheckBox();
            this.pageAppearance = new System.Windows.Forms.TabPage();
            this.cbDarkMode = new System.Windows.Forms.CheckBox();
            this.btnSetColorMark = new System.Windows.Forms.Button();
            this.btnSetColorReadonly = new System.Windows.Forms.Button();
            this.lblFontSize = new System.Windows.Forms.Label();
            this.cbxFontSize = new System.Windows.Forms.ComboBox();
            this.pageExtensions = new System.Windows.Forms.TabPage();
            this.txtExtensionsList = new System.Windows.Forms.TextBox();
            this.pnlExtFooter = new System.Windows.Forms.Panel();
            this.lnkGetExtensions = new System.Windows.Forms.LinkLabel();
            this.pagePerformance = new System.Windows.Forms.TabPage();
            this.cbAutoStream = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cbxStreamBehavior = new System.Windows.Forms.ComboBox();
            this.lblMB = new System.Windows.Forms.Label();
            this.lblAutoStreamSize = new System.Windows.Forms.Label();
            this.numericUpDown2 = new System.Windows.Forms.NumericUpDown();
            this.pagePaths = new System.Windows.Forms.TabPage();
            this.btnPickDiffTool = new System.Windows.Forms.Button();
            this.btnPickScriptEditor = new System.Windows.Forms.Button();
            this.btnPickTextEditor = new System.Windows.Forms.Button();
            this.txtToolDiffArgs = new System.Windows.Forms.TextBox();
            this.txtToolDiff = new System.Windows.Forms.TextBox();
            this.txtToolScriptEditor = new System.Windows.Forms.TextBox();
            this.txtToolTextEditor = new System.Windows.Forms.TextBox();
            this.lblDiffToolArgs = new System.Windows.Forms.Label();
            this.lblToolDiffTool = new System.Windows.Forms.Label();
            this.lblToolScriptEditor = new System.Windows.Forms.Label();
            this.lblToolTextEditor = new System.Windows.Forms.Label();
            this.pnlFooter = new System.Windows.Forms.Panel();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.lblUsageNote = new System.Windows.Forms.Label();
            this.lnkHelp = new System.Windows.Forms.LinkLabel();
            this.cbMinimizeToTray = new System.Windows.Forms.CheckBox();
            this.tcOptions.SuspendLayout();
            this.pageGeneral.SuspendLayout();
            this.gbWebServices.SuspendLayout();
            this.pageNetworking.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.udListenPort)).BeginInit();
            this.pageGateway.SuspendLayout();
            this.pageHTTPS.SuspendLayout();
            this.pageAppearance.SuspendLayout();
            this.pageExtensions.SuspendLayout();
            this.pnlExtFooter.SuspendLayout();
            this.pagePerformance.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).BeginInit();
            this.pagePaths.SuspendLayout();
            this.pnlFooter.SuspendLayout();
            this.SuspendLayout();
            // 
            // tcOptions
            // 
            this.tcOptions.Controls.Add(this.pageGeneral);
            this.tcOptions.Controls.Add(this.pageNetworking);
            this.tcOptions.Controls.Add(this.pageGateway);
            this.tcOptions.Controls.Add(this.pageHTTPS);
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
            this.gbWebServices.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbWebServices.Controls.Add(this.cbxBigSwitch);
            this.gbWebServices.Controls.Add(this.cbCheckForUpdates);
            this.gbWebServices.Location = new System.Drawing.Point(23, 60);
            this.gbWebServices.Name = "gbWebServices";
            this.gbWebServices.Size = new System.Drawing.Size(620, 117);
            this.gbWebServices.TabIndex = 1;
            this.gbWebServices.TabStop = false;
            this.gbWebServices.Text = "Clearinet Cloud Services";
            // 
            // cbxBigSwitch
            // 
            this.cbxBigSwitch.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxBigSwitch.FormattingEnabled = true;
            this.cbxBigSwitch.Items.AddRange(new object[] {
            "Allow Web Service calls",
            "Prompt on every Web Service call",
            "Disallow all Web Service calls"});
            this.cbxBigSwitch.Location = new System.Drawing.Point(17, 35);
            this.cbxBigSwitch.Name = "cbxBigSwitch";
            this.cbxBigSwitch.Size = new System.Drawing.Size(313, 25);
            this.cbxBigSwitch.TabIndex = 0;
            // 
            // cbCheckForUpdates
            // 
            this.cbCheckForUpdates.AutoSize = true;
            this.cbCheckForUpdates.Location = new System.Drawing.Point(17, 66);
            this.cbCheckForUpdates.Name = "cbCheckForUpdates";
            this.cbCheckForUpdates.Size = new System.Drawing.Size(210, 21);
            this.cbCheckForUpdates.TabIndex = 1;
            this.cbCheckForUpdates.Text = "Check for Updates on Startup";
            this.cbCheckForUpdates.UseVisualStyleBackColor = true;
            // 
            // cbAttachOnStartup
            // 
            this.cbAttachOnStartup.AutoSize = true;
            this.cbAttachOnStartup.Location = new System.Drawing.Point(23, 23);
            this.cbAttachOnStartup.Name = "cbAttachOnStartup";
            this.cbAttachOnStartup.Size = new System.Drawing.Size(137, 21);
            this.cbAttachOnStartup.TabIndex = 0;
            this.cbAttachOnStartup.Text = "Attach on Startup";
            this.cbAttachOnStartup.UseVisualStyleBackColor = true;
            // 
            // pageNetworking
            // 
            this.pageNetworking.Controls.Add(this.udListenPort);
            this.pageNetworking.Controls.Add(this.cbEnableIPv6);
            this.pageNetworking.Controls.Add(this.cbAllowRemoteConnections);
            this.pageNetworking.Controls.Add(this.lblListenOn);
            this.pageNetworking.Location = new System.Drawing.Point(4, 26);
            this.pageNetworking.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageNetworking.Name = "pageNetworking";
            this.pageNetworking.Size = new System.Drawing.Size(670, 316);
            this.pageNetworking.TabIndex = 2;
            this.pageNetworking.Text = "Networking";
            this.pageNetworking.UseVisualStyleBackColor = true;
            // 
            // udListenPort
            // 
            this.udListenPort.Location = new System.Drawing.Point(101, 22);
            this.udListenPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.udListenPort.Name = "udListenPort";
            this.udListenPort.Size = new System.Drawing.Size(65, 24);
            this.udListenPort.TabIndex = 1;
            this.udListenPort.Value = new decimal(new int[] {
            8888,
            0,
            0,
            0});
            // 
            // cbEnableIPv6
            // 
            this.cbEnableIPv6.AutoSize = true;
            this.cbEnableIPv6.Location = new System.Drawing.Point(20, 102);
            this.cbEnableIPv6.Name = "cbEnableIPv6";
            this.cbEnableIPv6.Size = new System.Drawing.Size(186, 21);
            this.cbEnableIPv6.TabIndex = 3;
            this.cbEnableIPv6.Text = "Enable IPv6 (if supported)";
            this.cbEnableIPv6.UseVisualStyleBackColor = true;
            // 
            // cbAllowRemoteConnections
            // 
            this.cbAllowRemoteConnections.AutoSize = true;
            this.cbAllowRemoteConnections.Location = new System.Drawing.Point(20, 75);
            this.cbAllowRemoteConnections.Name = "cbAllowRemoteConnections";
            this.cbAllowRemoteConnections.Size = new System.Drawing.Size(189, 21);
            this.cbAllowRemoteConnections.TabIndex = 2;
            this.cbAllowRemoteConnections.Text = "Allow Remote Connections";
            this.cbAllowRemoteConnections.UseVisualStyleBackColor = true;
            // 
            // lblListenOn
            // 
            this.lblListenOn.AutoSize = true;
            this.lblListenOn.Location = new System.Drawing.Point(17, 24);
            this.lblListenOn.Name = "lblListenOn";
            this.lblListenOn.Size = new System.Drawing.Size(78, 17);
            this.lblListenOn.TabIndex = 0;
            this.lblListenOn.Text = "Listen Port:";
            // 
            // pageGateway
            // 
            this.pageGateway.Controls.Add(this.cbChainToGateway);
            this.pageGateway.Location = new System.Drawing.Point(4, 26);
            this.pageGateway.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageGateway.Name = "pageGateway";
            this.pageGateway.Size = new System.Drawing.Size(670, 316);
            this.pageGateway.TabIndex = 3;
            this.pageGateway.Text = "Gateway";
            this.pageGateway.UseVisualStyleBackColor = true;
            // 
            // cbChainToGateway
            // 
            this.cbChainToGateway.AutoSize = true;
            this.cbChainToGateway.Location = new System.Drawing.Point(11, 16);
            this.cbChainToGateway.Name = "cbChainToGateway";
            this.cbChainToGateway.Size = new System.Drawing.Size(198, 21);
            this.cbChainToGateway.TabIndex = 10;
            this.cbChainToGateway.Text = "Chain to Upstream Gateway";
            this.cbChainToGateway.UseVisualStyleBackColor = true;
            // 
            // pageHTTPS
            // 
            this.pageHTTPS.Controls.Add(this.txtDecryptHosts);
            this.pageHTTPS.Controls.Add(this.cbIgnoreUpstreamCertErrors);
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
            this.txtDecryptHosts.TabIndex = 4;
            // 
            // cbIgnoreUpstreamCertErrors
            // 
            this.cbIgnoreUpstreamCertErrors.AutoSize = true;
            this.cbIgnoreUpstreamCertErrors.Location = new System.Drawing.Point(54, 140);
            this.cbIgnoreUpstreamCertErrors.Name = "cbIgnoreUpstreamCertErrors";
            this.cbIgnoreUpstreamCertErrors.Size = new System.Drawing.Size(265, 21);
            this.cbIgnoreUpstreamCertErrors.TabIndex = 3;
            this.cbIgnoreUpstreamCertErrors.Text = "&Ignore server certificate errors (unsafe)";
            this.cbIgnoreUpstreamCertErrors.UseVisualStyleBackColor = true;
            this.cbIgnoreUpstreamCertErrors.CheckedChanged += new System.EventHandler(this.cbIgnoreUpstreamCertErrors_CheckedChanged);
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
            this.cbxDecryptFor.TabIndex = 2;
            // 
            // cbCaptureCONNECT
            // 
            this.cbCaptureCONNECT.AutoSize = true;
            this.cbCaptureCONNECT.Location = new System.Drawing.Point(11, 21);
            this.cbCaptureCONNECT.Name = "cbCaptureCONNECT";
            this.cbCaptureCONNECT.Size = new System.Drawing.Size(193, 21);
            this.cbCaptureCONNECT.TabIndex = 0;
            this.cbCaptureCONNECT.Text = "Capture &HTTPS CONNECTs";
            this.cbCaptureCONNECT.UseVisualStyleBackColor = true;
            // 
            // cbDecryptHTTPS
            // 
            this.cbDecryptHTTPS.AutoSize = true;
            this.cbDecryptHTTPS.Location = new System.Drawing.Point(31, 48);
            this.cbDecryptHTTPS.Name = "cbDecryptHTTPS";
            this.cbDecryptHTTPS.Size = new System.Drawing.Size(192, 21);
            this.cbDecryptHTTPS.TabIndex = 1;
            this.cbDecryptHTTPS.Text = "&Decrypt HTTPS exchanges";
            this.cbDecryptHTTPS.UseVisualStyleBackColor = true;
            // 
            // pageAppearance
            // 
            this.pageAppearance.Controls.Add(this.cbMinimizeToTray);
            this.pageAppearance.Controls.Add(this.cbDarkMode);
            this.pageAppearance.Controls.Add(this.btnSetColorMark);
            this.pageAppearance.Controls.Add(this.btnSetColorReadonly);
            this.pageAppearance.Controls.Add(this.lblFontSize);
            this.pageAppearance.Controls.Add(this.cbxFontSize);
            this.pageAppearance.Location = new System.Drawing.Point(4, 26);
            this.pageAppearance.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageAppearance.Name = "pageAppearance";
            this.pageAppearance.Size = new System.Drawing.Size(670, 316);
            this.pageAppearance.TabIndex = 4;
            this.pageAppearance.Text = "Appearance";
            this.pageAppearance.UseVisualStyleBackColor = true;
            // 
            // cbDarkMode
            // 
            this.cbDarkMode.AutoSize = true;
            this.cbDarkMode.Location = new System.Drawing.Point(11, 142);
            this.cbDarkMode.Name = "cbDarkMode";
            this.cbDarkMode.Size = new System.Drawing.Size(177, 21);
            this.cbDarkMode.TabIndex = 4;
            this.cbDarkMode.Text = "Dark Mode (rough draft)";
            this.cbDarkMode.UseVisualStyleBackColor = true;
            // 
            // btnSetColorMark
            // 
            this.btnSetColorMark.Location = new System.Drawing.Point(11, 94);
            this.btnSetColorMark.Name = "btnSetColorMark";
            this.btnSetColorMark.Size = new System.Drawing.Size(208, 32);
            this.btnSetColorMark.TabIndex = 3;
            this.btnSetColorMark.Text = "Set node \'mark\' color";
            this.btnSetColorMark.UseVisualStyleBackColor = true;
            this.btnSetColorMark.Click += new System.EventHandler(this.btnSetColorMark_Click);
            // 
            // btnSetColorReadonly
            // 
            this.btnSetColorReadonly.Location = new System.Drawing.Point(11, 57);
            this.btnSetColorReadonly.Name = "btnSetColorReadonly";
            this.btnSetColorReadonly.Size = new System.Drawing.Size(208, 31);
            this.btnSetColorReadonly.TabIndex = 2;
            this.btnSetColorReadonly.Text = "Set \'readonly\' color";
            this.btnSetColorReadonly.UseVisualStyleBackColor = true;
            this.btnSetColorReadonly.Click += new System.EventHandler(this.btnSetColorReadonly_Click);
            // 
            // lblFontSize
            // 
            this.lblFontSize.Location = new System.Drawing.Point(8, 16);
            this.lblFontSize.Name = "lblFontSize";
            this.lblFontSize.Size = new System.Drawing.Size(106, 22);
            this.lblFontSize.TabIndex = 0;
            this.lblFontSize.Text = "Font Size:";
            this.lblFontSize.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cbxFontSize
            // 
            this.cbxFontSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxFontSize.FormattingEnabled = true;
            this.cbxFontSize.Items.AddRange(new object[] {
            "8",
            "10",
            "12",
            "14",
            "16",
            "18",
            "20",
            "22",
            "24",
            "28",
            "32"});
            this.cbxFontSize.Location = new System.Drawing.Point(120, 16);
            this.cbxFontSize.Name = "cbxFontSize";
            this.cbxFontSize.Size = new System.Drawing.Size(99, 25);
            this.cbxFontSize.TabIndex = 1;
            this.cbxFontSize.SelectionChangeCommitted += new System.EventHandler(this.cbxFontSize_SelectionChangeCommitted);
            // 
            // pageExtensions
            // 
            this.pageExtensions.Controls.Add(this.txtExtensionsList);
            this.pageExtensions.Controls.Add(this.pnlExtFooter);
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
            this.txtExtensionsList.Size = new System.Drawing.Size(670, 291);
            this.txtExtensionsList.TabIndex = 0;
            // 
            // pnlExtFooter
            // 
            this.pnlExtFooter.Controls.Add(this.lnkGetExtensions);
            this.pnlExtFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlExtFooter.Location = new System.Drawing.Point(0, 291);
            this.pnlExtFooter.Name = "pnlExtFooter";
            this.pnlExtFooter.Size = new System.Drawing.Size(670, 25);
            this.pnlExtFooter.TabIndex = 1;
            // 
            // lnkGetExtensions
            // 
            this.lnkGetExtensions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lnkGetExtensions.AutoSize = true;
            this.lnkGetExtensions.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.lnkGetExtensions.Location = new System.Drawing.Point(555, 3);
            this.lnkGetExtensions.Name = "lnkGetExtensions";
            this.lnkGetExtensions.Size = new System.Drawing.Size(111, 17);
            this.lnkGetExtensions.TabIndex = 0;
            this.lnkGetExtensions.TabStop = true;
            this.lnkGetExtensions.Text = "Get Extensions...";
            this.lnkGetExtensions.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkGetExtensions_LinkClicked);
            // 
            // pagePerformance
            // 
            this.pagePerformance.Controls.Add(this.cbAutoStream);
            this.pagePerformance.Controls.Add(this.label1);
            this.pagePerformance.Controls.Add(this.cbxStreamBehavior);
            this.pagePerformance.Controls.Add(this.lblMB);
            this.pagePerformance.Controls.Add(this.lblAutoStreamSize);
            this.pagePerformance.Controls.Add(this.numericUpDown2);
            this.pagePerformance.Location = new System.Drawing.Point(4, 26);
            this.pagePerformance.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pagePerformance.Name = "pagePerformance";
            this.pagePerformance.Size = new System.Drawing.Size(670, 316);
            this.pagePerformance.TabIndex = 6;
            this.pagePerformance.Text = "Performance";
            this.pagePerformance.UseVisualStyleBackColor = true;
            // 
            // cbAutoStream
            // 
            this.cbAutoStream.AutoSize = true;
            this.cbAutoStream.Location = new System.Drawing.Point(17, 17);
            this.cbAutoStream.Name = "cbAutoStream";
            this.cbAutoStream.Size = new System.Drawing.Size(194, 21);
            this.cbAutoStream.TabIndex = 0;
            this.cbAutoStream.Text = "Automatically &stream media";
            this.cbAutoStream.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 93);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(207, 17);
            this.label1.TabIndex = 4;
            this.label1.Text = "If a client &aborts while streaming:";
            // 
            // cbxStreamBehavior
            // 
            this.cbxStreamBehavior.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxStreamBehavior.FormattingEnabled = true;
            this.cbxStreamBehavior.Items.AddRange(new object[] {
            "finish downloading anyway",
            "finish only if Exchange is visible",
            "close the server connection"});
            this.cbxStreamBehavior.Location = new System.Drawing.Point(227, 90);
            this.cbxStreamBehavior.Name = "cbxStreamBehavior";
            this.cbxStreamBehavior.Size = new System.Drawing.Size(240, 25);
            this.cbxStreamBehavior.TabIndex = 5;
            // 
            // lblMB
            // 
            this.lblMB.AutoSize = true;
            this.lblMB.Location = new System.Drawing.Point(288, 53);
            this.lblMB.Name = "lblMB";
            this.lblMB.Size = new System.Drawing.Size(28, 17);
            this.lblMB.TabIndex = 3;
            this.lblMB.Text = "mb";
            // 
            // lblAutoStreamSize
            // 
            this.lblAutoStreamSize.AutoSize = true;
            this.lblAutoStreamSize.Location = new System.Drawing.Point(16, 53);
            this.lblAutoStreamSize.Name = "lblAutoStreamSize";
            this.lblAutoStreamSize.Size = new System.Drawing.Size(195, 17);
            this.lblAutoStreamSize.TabIndex = 1;
            this.lblAutoStreamSize.Text = "Stream and forget bodies over";
            // 
            // numericUpDown2
            // 
            this.numericUpDown2.Location = new System.Drawing.Point(217, 51);
            this.numericUpDown2.Maximum = new decimal(new int[] {
            2147,
            0,
            0,
            0});
            this.numericUpDown2.Name = "numericUpDown2";
            this.numericUpDown2.Size = new System.Drawing.Size(65, 24);
            this.numericUpDown2.TabIndex = 2;
            this.numericUpDown2.Value = new decimal(new int[] {
            2147,
            0,
            0,
            0});
            // 
            // pagePaths
            // 
            this.pagePaths.Controls.Add(this.btnPickDiffTool);
            this.pagePaths.Controls.Add(this.btnPickScriptEditor);
            this.pagePaths.Controls.Add(this.btnPickTextEditor);
            this.pagePaths.Controls.Add(this.txtToolDiffArgs);
            this.pagePaths.Controls.Add(this.txtToolDiff);
            this.pagePaths.Controls.Add(this.txtToolScriptEditor);
            this.pagePaths.Controls.Add(this.txtToolTextEditor);
            this.pagePaths.Controls.Add(this.lblDiffToolArgs);
            this.pagePaths.Controls.Add(this.lblToolDiffTool);
            this.pagePaths.Controls.Add(this.lblToolScriptEditor);
            this.pagePaths.Controls.Add(this.lblToolTextEditor);
            this.pagePaths.Location = new System.Drawing.Point(4, 26);
            this.pagePaths.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pagePaths.Name = "pagePaths";
            this.pagePaths.Size = new System.Drawing.Size(670, 316);
            this.pagePaths.TabIndex = 7;
            this.pagePaths.Text = "Paths";
            this.pagePaths.UseVisualStyleBackColor = true;
            // 
            // btnPickDiffTool
            // 
            this.btnPickDiffTool.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPickDiffTool.Location = new System.Drawing.Point(613, 89);
            this.btnPickDiffTool.Name = "btnPickDiffTool";
            this.btnPickDiffTool.Size = new System.Drawing.Size(37, 23);
            this.btnPickDiffTool.TabIndex = 8;
            this.btnPickDiffTool.Text = "...";
            this.btnPickDiffTool.UseVisualStyleBackColor = true;
            this.btnPickDiffTool.Click += new System.EventHandler(this.btnPickDiffTool_Click);
            // 
            // btnPickScriptEditor
            // 
            this.btnPickScriptEditor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPickScriptEditor.Location = new System.Drawing.Point(613, 51);
            this.btnPickScriptEditor.Name = "btnPickScriptEditor";
            this.btnPickScriptEditor.Size = new System.Drawing.Size(37, 23);
            this.btnPickScriptEditor.TabIndex = 5;
            this.btnPickScriptEditor.Text = "...";
            this.btnPickScriptEditor.UseVisualStyleBackColor = true;
            this.btnPickScriptEditor.Click += new System.EventHandler(this.btnPickScriptEditor_Click);
            // 
            // btnPickTextEditor
            // 
            this.btnPickTextEditor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPickTextEditor.Location = new System.Drawing.Point(613, 14);
            this.btnPickTextEditor.Name = "btnPickTextEditor";
            this.btnPickTextEditor.Size = new System.Drawing.Size(37, 23);
            this.btnPickTextEditor.TabIndex = 2;
            this.btnPickTextEditor.Text = "...";
            this.btnPickTextEditor.UseVisualStyleBackColor = true;
            this.btnPickTextEditor.Click += new System.EventHandler(this.btnPickTextEditor_Click);
            // 
            // txtToolDiffArgs
            // 
            this.txtToolDiffArgs.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtToolDiffArgs.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtToolDiffArgs.Location = new System.Drawing.Point(196, 127);
            this.txtToolDiffArgs.Name = "txtToolDiffArgs";
            this.txtToolDiffArgs.Size = new System.Drawing.Size(454, 24);
            this.txtToolDiffArgs.TabIndex = 10;
            // 
            // txtToolDiff
            // 
            this.txtToolDiff.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtToolDiff.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.txtToolDiff.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.txtToolDiff.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtToolDiff.Location = new System.Drawing.Point(116, 89);
            this.txtToolDiff.Name = "txtToolDiff";
            this.txtToolDiff.Size = new System.Drawing.Size(491, 24);
            this.txtToolDiff.TabIndex = 7;
            // 
            // txtToolScriptEditor
            // 
            this.txtToolScriptEditor.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtToolScriptEditor.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.txtToolScriptEditor.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.txtToolScriptEditor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtToolScriptEditor.Location = new System.Drawing.Point(116, 51);
            this.txtToolScriptEditor.Name = "txtToolScriptEditor";
            this.txtToolScriptEditor.Size = new System.Drawing.Size(491, 24);
            this.txtToolScriptEditor.TabIndex = 4;
            // 
            // txtToolTextEditor
            // 
            this.txtToolTextEditor.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtToolTextEditor.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.txtToolTextEditor.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.txtToolTextEditor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtToolTextEditor.Location = new System.Drawing.Point(116, 15);
            this.txtToolTextEditor.Name = "txtToolTextEditor";
            this.txtToolTextEditor.Size = new System.Drawing.Size(491, 24);
            this.txtToolTextEditor.TabIndex = 1;
            // 
            // lblDiffToolArgs
            // 
            this.lblDiffToolArgs.AutoSize = true;
            this.lblDiffToolArgs.Location = new System.Drawing.Point(86, 129);
            this.lblDiffToolArgs.Name = "lblDiffToolArgs";
            this.lblDiffToolArgs.Size = new System.Drawing.Size(104, 17);
            this.lblDiffToolArgs.TabIndex = 9;
            this.lblDiffToolArgs.Text = "&Diff Arguments:";
            // 
            // lblToolDiffTool
            // 
            this.lblToolDiffTool.AutoSize = true;
            this.lblToolDiffTool.Location = new System.Drawing.Point(9, 92);
            this.lblToolDiffTool.Name = "lblToolDiffTool";
            this.lblToolDiffTool.Size = new System.Drawing.Size(63, 17);
            this.lblToolDiffTool.TabIndex = 6;
            this.lblToolDiffTool.Text = "&Diff Tool:";
            // 
            // lblToolScriptEditor
            // 
            this.lblToolScriptEditor.AutoSize = true;
            this.lblToolScriptEditor.Location = new System.Drawing.Point(9, 54);
            this.lblToolScriptEditor.Name = "lblToolScriptEditor";
            this.lblToolScriptEditor.Size = new System.Drawing.Size(88, 17);
            this.lblToolScriptEditor.TabIndex = 3;
            this.lblToolScriptEditor.Text = "&Script Editor:";
            // 
            // lblToolTextEditor
            // 
            this.lblToolTextEditor.AutoSize = true;
            this.lblToolTextEditor.Location = new System.Drawing.Point(9, 18);
            this.lblToolTextEditor.Name = "lblToolTextEditor";
            this.lblToolTextEditor.Size = new System.Drawing.Size(81, 17);
            this.lblToolTextEditor.TabIndex = 0;
            this.lblToolTextEditor.Text = "&Text Editor:";
            // 
            // pnlFooter
            // 
            this.pnlFooter.Controls.Add(this.btnCancel);
            this.pnlFooter.Controls.Add(this.btnOk);
            this.pnlFooter.Controls.Add(this.lblUsageNote);
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
            // lblUsageNote
            // 
            this.lblUsageNote.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblUsageNote.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblUsageNote.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblUsageNote.Location = new System.Drawing.Point(64, 9);
            this.lblUsageNote.Name = "lblUsageNote";
            this.lblUsageNote.Size = new System.Drawing.Size(405, 46);
            this.lblUsageNote.TabIndex = 3;
            this.lblUsageNote.Text = "Note: Many options require restart to take effect.";
            this.lblUsageNote.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lnkHelp
            // 
            this.lnkHelp.AutoSize = true;
            this.lnkHelp.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.lnkHelp.Location = new System.Drawing.Point(12, 22);
            this.lnkHelp.Name = "lnkHelp";
            this.lnkHelp.Size = new System.Drawing.Size(46, 17);
            this.lnkHelp.TabIndex = 0;
            this.lnkHelp.TabStop = true;
            this.lnkHelp.Text = "&Help...";
            this.lnkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkHelp_LinkClicked);
            // 
            // cbMinimizeToTray
            // 
            this.cbMinimizeToTray.AutoSize = true;
            this.cbMinimizeToTray.Location = new System.Drawing.Point(11, 169);
            this.cbMinimizeToTray.Name = "cbMinimizeToTray";
            this.cbMinimizeToTray.Size = new System.Drawing.Size(125, 21);
            this.cbMinimizeToTray.TabIndex = 5;
            this.cbMinimizeToTray.Text = "Minimize to Tray";
            this.cbMinimizeToTray.UseVisualStyleBackColor = true;
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
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Name = "frmOptions";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Clearinet Options";
            this.Load += new System.EventHandler(this.frmOptions_Load);
            this.tcOptions.ResumeLayout(false);
            this.pageGeneral.ResumeLayout(false);
            this.pageGeneral.PerformLayout();
            this.gbWebServices.ResumeLayout(false);
            this.gbWebServices.PerformLayout();
            this.pageNetworking.ResumeLayout(false);
            this.pageNetworking.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.udListenPort)).EndInit();
            this.pageGateway.ResumeLayout(false);
            this.pageGateway.PerformLayout();
            this.pageHTTPS.ResumeLayout(false);
            this.pageHTTPS.PerformLayout();
            this.pageAppearance.ResumeLayout(false);
            this.pageAppearance.PerformLayout();
            this.pageExtensions.ResumeLayout(false);
            this.pageExtensions.PerformLayout();
            this.pnlExtFooter.ResumeLayout(false);
            this.pnlExtFooter.PerformLayout();
            this.pagePerformance.ResumeLayout(false);
            this.pagePerformance.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).EndInit();
            this.pagePaths.ResumeLayout(false);
            this.pagePaths.PerformLayout();
            this.pnlFooter.ResumeLayout(false);
            this.pnlFooter.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tcOptions;
        private System.Windows.Forms.TabPage pageGeneral;
        private System.Windows.Forms.TabPage pageHTTPS;
        private System.Windows.Forms.TabPage pageNetworking;
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
        private System.Windows.Forms.CheckBox cbDecryptHTTPS;
        private System.Windows.Forms.GroupBox gbWebServices;
        private System.Windows.Forms.ComboBox cbxBigSwitch;
        private System.Windows.Forms.CheckBox cbCheckForUpdates;
        private System.Windows.Forms.CheckBox cbCaptureCONNECT;
        private System.Windows.Forms.TextBox txtDecryptHosts;
        private System.Windows.Forms.CheckBox cbIgnoreUpstreamCertErrors;
        private System.Windows.Forms.ComboBox cbxDecryptFor;
        private System.Windows.Forms.ComboBox cbxFontSize;
        private System.Windows.Forms.Button btnSetColorMark;
        private System.Windows.Forms.Button btnSetColorReadonly;
        private System.Windows.Forms.Label lblFontSize;
        private System.Windows.Forms.CheckBox cbDarkMode;
        private System.Windows.Forms.Label lblUsageNote;
        private System.Windows.Forms.Panel pnlExtFooter;
        private System.Windows.Forms.LinkLabel lnkGetExtensions;
        private System.Windows.Forms.CheckBox cbEnableIPv6;
        private System.Windows.Forms.CheckBox cbAllowRemoteConnections;
        private System.Windows.Forms.Label lblListenOn;
        private System.Windows.Forms.CheckBox cbChainToGateway;
        private System.Windows.Forms.NumericUpDown udListenPort;
        private System.Windows.Forms.ComboBox cbxStreamBehavior;
        private System.Windows.Forms.Label lblMB;
        private System.Windows.Forms.Label lblAutoStreamSize;
        private System.Windows.Forms.NumericUpDown numericUpDown2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtToolDiffArgs;
        private System.Windows.Forms.TextBox txtToolDiff;
        private System.Windows.Forms.TextBox txtToolScriptEditor;
        private System.Windows.Forms.TextBox txtToolTextEditor;
        private System.Windows.Forms.Label lblDiffToolArgs;
        private System.Windows.Forms.Label lblToolDiffTool;
        private System.Windows.Forms.Label lblToolScriptEditor;
        private System.Windows.Forms.Label lblToolTextEditor;
        private System.Windows.Forms.Button btnPickDiffTool;
        private System.Windows.Forms.Button btnPickScriptEditor;
        private System.Windows.Forms.Button btnPickTextEditor;
        private System.Windows.Forms.CheckBox cbAutoStream;
        private System.Windows.Forms.CheckBox cbMinimizeToTray;
    }
}