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
            this.pageHTTPS = new System.Windows.Forms.TabPage();
            this.pnlFooter = new System.Windows.Forms.Panel();
            this.pageConnections = new System.Windows.Forms.TabPage();
            this.pageGateway = new System.Windows.Forms.TabPage();
            this.pageAppearance = new System.Windows.Forms.TabPage();
            this.pageExtensions = new System.Windows.Forms.TabPage();
            this.pagePerformance = new System.Windows.Forms.TabPage();
            this.pagePaths = new System.Windows.Forms.TabPage();
            this.lnkHelp = new System.Windows.Forms.LinkLabel();
            this.tcOptions.SuspendLayout();
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
            this.tcOptions.Size = new System.Drawing.Size(900, 527);
            this.tcOptions.TabIndex = 0;
            // 
            // pageGeneral
            // 
            this.pageGeneral.Location = new System.Drawing.Point(4, 30);
            this.pageGeneral.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageGeneral.Name = "pageGeneral";
            this.pageGeneral.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageGeneral.Size = new System.Drawing.Size(892, 493);
            this.pageGeneral.TabIndex = 0;
            this.pageGeneral.Text = "General";
            this.pageGeneral.UseVisualStyleBackColor = true;
            // 
            // pageHTTPS
            // 
            this.pageHTTPS.Location = new System.Drawing.Point(4, 25);
            this.pageHTTPS.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageHTTPS.Name = "pageHTTPS";
            this.pageHTTPS.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageHTTPS.Size = new System.Drawing.Size(892, 562);
            this.pageHTTPS.TabIndex = 1;
            this.pageHTTPS.Text = "HTTPS";
            this.pageHTTPS.UseVisualStyleBackColor = true;
            // 
            // pnlFooter
            // 
            this.pnlFooter.Controls.Add(this.lnkHelp);
            this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlFooter.Location = new System.Drawing.Point(0, 527);
            this.pnlFooter.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pnlFooter.Name = "pnlFooter";
            this.pnlFooter.Size = new System.Drawing.Size(900, 64);
            this.pnlFooter.TabIndex = 1;
            // 
            // pageConnections
            // 
            this.pageConnections.Location = new System.Drawing.Point(4, 25);
            this.pageConnections.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageConnections.Name = "pageConnections";
            this.pageConnections.Size = new System.Drawing.Size(892, 562);
            this.pageConnections.TabIndex = 2;
            this.pageConnections.Text = "Connections";
            this.pageConnections.UseVisualStyleBackColor = true;
            // 
            // pageGateway
            // 
            this.pageGateway.Location = new System.Drawing.Point(4, 25);
            this.pageGateway.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageGateway.Name = "pageGateway";
            this.pageGateway.Size = new System.Drawing.Size(892, 562);
            this.pageGateway.TabIndex = 3;
            this.pageGateway.Text = "Gateway";
            this.pageGateway.UseVisualStyleBackColor = true;
            // 
            // pageAppearance
            // 
            this.pageAppearance.Location = new System.Drawing.Point(4, 25);
            this.pageAppearance.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageAppearance.Name = "pageAppearance";
            this.pageAppearance.Size = new System.Drawing.Size(892, 562);
            this.pageAppearance.TabIndex = 4;
            this.pageAppearance.Text = "Appearance";
            this.pageAppearance.UseVisualStyleBackColor = true;
            // 
            // pageExtensions
            // 
            this.pageExtensions.Location = new System.Drawing.Point(4, 25);
            this.pageExtensions.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pageExtensions.Name = "pageExtensions";
            this.pageExtensions.Size = new System.Drawing.Size(892, 562);
            this.pageExtensions.TabIndex = 5;
            this.pageExtensions.Text = "Extensions";
            this.pageExtensions.UseVisualStyleBackColor = true;
            // 
            // pagePerformance
            // 
            this.pagePerformance.Location = new System.Drawing.Point(4, 25);
            this.pagePerformance.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pagePerformance.Name = "pagePerformance";
            this.pagePerformance.Size = new System.Drawing.Size(892, 562);
            this.pagePerformance.TabIndex = 6;
            this.pagePerformance.Text = "Performance";
            this.pagePerformance.UseVisualStyleBackColor = true;
            // 
            // pagePaths
            // 
            this.pagePaths.Location = new System.Drawing.Point(4, 25);
            this.pagePaths.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pagePaths.Name = "pagePaths";
            this.pagePaths.Size = new System.Drawing.Size(892, 562);
            this.pagePaths.TabIndex = 7;
            this.pagePaths.Text = "Paths";
            this.pagePaths.UseVisualStyleBackColor = true;
            // 
            // lnkHelp
            // 
            this.lnkHelp.AutoSize = true;
            this.lnkHelp.Location = new System.Drawing.Point(12, 16);
            this.lnkHelp.Name = "lnkHelp";
            this.lnkHelp.Size = new System.Drawing.Size(58, 21);
            this.lnkHelp.TabIndex = 0;
            this.lnkHelp.TabStop = true;
            this.lnkHelp.Text = "Help...";
            this.lnkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkHelp_LinkClicked);
            // 
            // frmOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 591);
            this.Controls.Add(this.tcOptions);
            this.Controls.Add(this.pnlFooter);
            this.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "frmOptions";
            this.Text = "Clearinet Options";
            this.tcOptions.ResumeLayout(false);
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
    }
}