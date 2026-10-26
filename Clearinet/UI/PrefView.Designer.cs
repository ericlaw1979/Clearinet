namespace Clearinet.UI
{
    partial class PrefView
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.dgvPrefs = new System.Windows.Forms.DataGridView();
            this.colPrefName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPrefValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.pnlTop = new System.Windows.Forms.Panel();
            this.lblInstructions = new System.Windows.Forms.Label();
            this.lnkPrefHelp = new System.Windows.Forms.LinkLabel();
            this.lnkTearoff = new System.Windows.Forms.LinkLabel();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrefs)).BeginInit();
            this.pnlTop.SuspendLayout();
            this.SuspendLayout();
            // 
            // dgvPrefs
            // 
            this.dgvPrefs.AllowUserToResizeRows = false;
            this.dgvPrefs.BackgroundColor = System.Drawing.Color.LightSteelBlue;
            this.dgvPrefs.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dgvPrefs.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvPrefs.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPrefName,
            this.colPrefValue});
            this.dgvPrefs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPrefs.Location = new System.Drawing.Point(0, 43);
            this.dgvPrefs.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.dgvPrefs.MultiSelect = false;
            this.dgvPrefs.Name = "dgvPrefs";
            this.dgvPrefs.RowHeadersWidth = 40;
            this.dgvPrefs.RowTemplate.Height = 24;
            this.dgvPrefs.Size = new System.Drawing.Size(744, 637);
            this.dgvPrefs.TabIndex = 0;
            this.dgvPrefs.CellBeginEdit += new System.Windows.Forms.DataGridViewCellCancelEventHandler(this.dgvPrefs_CellBeginEdit);
            this.dgvPrefs.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvPrefs_CellEndEdit);
            this.dgvPrefs.CellParsing += new System.Windows.Forms.DataGridViewCellParsingEventHandler(this.dgvPrefs_CellParsing);
            this.dgvPrefs.UserDeletingRow += new System.Windows.Forms.DataGridViewRowCancelEventHandler(this.dgvPrefs_UserDeletingRow);
            // 
            // colPrefName
            // 
            this.colPrefName.HeaderText = "Name";
            this.colPrefName.MinimumWidth = 6;
            this.colPrefName.Name = "colPrefName";
            this.colPrefName.Width = 125;
            // 
            // colPrefValue
            // 
            this.colPrefValue.HeaderText = "Value";
            this.colPrefValue.MinimumWidth = 6;
            this.colPrefValue.Name = "colPrefValue";
            this.colPrefValue.Width = 125;
            // 
            // pnlTop
            // 
            this.pnlTop.Controls.Add(this.lnkTearoff);
            this.pnlTop.Controls.Add(this.lblInstructions);
            this.pnlTop.Controls.Add(this.lnkPrefHelp);
            this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTop.Location = new System.Drawing.Point(0, 0);
            this.pnlTop.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pnlTop.Name = "pnlTop";
            this.pnlTop.Size = new System.Drawing.Size(744, 43);
            this.pnlTop.TabIndex = 1;
            // 
            // lblInstructions
            // 
            this.lblInstructions.AutoSize = true;
            this.lblInstructions.Location = new System.Drawing.Point(3, 9);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(304, 17);
            this.lblInstructions.TabIndex = 1;
            this.lblInstructions.Text = "This page allows viewing and editing Preferences.";
            // 
            // lnkPrefHelp
            // 
            this.lnkPrefHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lnkPrefHelp.AutoSize = true;
            this.lnkPrefHelp.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.lnkPrefHelp.Location = new System.Drawing.Point(555, 9);
            this.lnkPrefHelp.Name = "lnkPrefHelp";
            this.lnkPrefHelp.Size = new System.Drawing.Size(141, 17);
            this.lnkPrefHelp.TabIndex = 0;
            this.lnkPrefHelp.TabStop = true;
            this.lnkPrefHelp.Text = "Help on Preferences...";
            this.lnkPrefHelp.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lnkPrefHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkPrefHelp_LinkClicked);
            // 
            // lnkTearoff
            // 
            this.lnkTearoff.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lnkTearoff.AutoSize = true;
            this.lnkTearoff.LinkBehavior = System.Windows.Forms.LinkBehavior.NeverUnderline;
            this.lnkTearoff.Location = new System.Drawing.Point(717, 9);
            this.lnkTearoff.Name = "lnkTearoff";
            this.lnkTearoff.Size = new System.Drawing.Size(24, 17);
            this.lnkTearoff.TabIndex = 2;
            this.lnkTearoff.TabStop = true;
            this.lnkTearoff.Text = "🪟";
            this.lnkTearoff.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lnkTearoff.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkTearoff_LinkClicked);
            // 
            // PrefView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.dgvPrefs);
            this.Controls.Add(this.pnlTop);
            this.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "PrefView";
            this.Size = new System.Drawing.Size(744, 680);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPrefs)).EndInit();
            this.pnlTop.ResumeLayout(false);
            this.pnlTop.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvPrefs;
        private System.Windows.Forms.Panel pnlTop;
        private System.Windows.Forms.LinkLabel lnkPrefHelp;
        private System.Windows.Forms.Label lblInstructions;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrefName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPrefValue;
        private System.Windows.Forms.LinkLabel lnkTearoff;
    }
}
