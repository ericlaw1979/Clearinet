namespace Clearinet.UI
{
    partial class frmHostRemap
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
            this.components = new System.ComponentModel.Container();
            this.txtRules = new System.Windows.Forms.TextBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.cbEnableRemap = new System.Windows.Forms.CheckBox();
            this.lnkHelp = new BetterLinkLabel();
            this.lblExplain = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lnkImportHOSTS = new BetterLinkLabel();
            this.SuspendLayout();
            // 
            // txtRules
            // 
            this.txtRules.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtRules.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtRules.Enabled = false;
            this.txtRules.Location = new System.Drawing.Point(12, 39);
            this.txtRules.Multiline = true;
            this.txtRules.Name = "txtRules";
            this.txtRules.Size = new System.Drawing.Size(776, 342);
            this.txtRules.TabIndex = 1;
            this.txtRules.ModifiedChanged += new System.EventHandler(this.txtRules_ModifiedChanged);
            this.txtRules.EnabledChanged += new System.EventHandler(this.txtRules_EnabledChanged);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(61, 4);
            // 
            // cbEnableRemap
            // 
            this.cbEnableRemap.AutoSize = true;
            this.cbEnableRemap.Location = new System.Drawing.Point(12, 12);
            this.cbEnableRemap.Name = "cbEnableRemap";
            this.cbEnableRemap.Size = new System.Drawing.Size(463, 21);
            this.cbEnableRemap.TabIndex = 0;
            this.cbEnableRemap.Text = "Override DNS to remap requests from one host to a different host or IP";
            this.cbEnableRemap.UseVisualStyleBackColor = true;
            this.cbEnableRemap.CheckedChanged += new System.EventHandler(this.cbEnableRemap_CheckedChanged);
            // 
            // lnkHelp
            // 
            this.lnkHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lnkHelp.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.lnkHelp.Location = new System.Drawing.Point(709, -1);
            this.lnkHelp.Name = "lnkHelp";
            this.lnkHelp.Size = new System.Drawing.Size(79, 37);
            this.lnkHelp.TabIndex = 6;
            this.lnkHelp.TabStop = true;
            this.lnkHelp.Text = "Help...";
            this.lnkHelp.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lnkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkHelp_LinkClicked);
            // 
            // lblExplain
            // 
            this.lblExplain.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblExplain.Location = new System.Drawing.Point(12, 384);
            this.lblExplain.Name = "lblExplain";
            this.lblExplain.Size = new System.Drawing.Size(275, 57);
            this.lblExplain.TabIndex = 5;
            this.lblExplain.Text = "#Example Mapping\r\nNewIP|Host OriginalURLHost";
            this.lblExplain.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSave.Enabled = false;
            this.btnSave.Location = new System.Drawing.Point(591, 394);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(93, 37);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "&Save";
            this.btnSave.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(690, 394);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(98, 37);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "&Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // lnkImportHOSTS
            // 
            this.lnkImportHOSTS.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lnkImportHOSTS.AutoSize = true;
            this.lnkImportHOSTS.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.lnkImportHOSTS.Location = new System.Drawing.Point(406, 404);
            this.lnkImportHOSTS.Name = "lnkImportHOSTS";
            this.lnkImportHOSTS.Size = new System.Drawing.Size(179, 17);
            this.lnkImportHOSTS.TabIndex = 4;
            this.lnkImportHOSTS.TabStop = true;
            this.lnkImportHOSTS.Text = "Import Windows\' HOSTS file";
            this.lnkImportHOSTS.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkImportHOSTS_LinkClicked);
            // 
            // frmHostRemap
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.ControlBox = false;
            this.Controls.Add(this.lnkImportHOSTS);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.lblExplain);
            this.Controls.Add(this.lnkHelp);
            this.Controls.Add(this.cbEnableRemap);
            this.Controls.Add(this.txtRules);
            this.Font = new System.Drawing.Font("Tahoma", 10F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "frmHostRemap";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Host Remapper";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtRules;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.CheckBox cbEnableRemap;
        private Clearinet.BetterLinkLabel lnkHelp;
        private System.Windows.Forms.Label lblExplain;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private Clearinet.BetterLinkLabel lnkImportHOSTS;
    }
}