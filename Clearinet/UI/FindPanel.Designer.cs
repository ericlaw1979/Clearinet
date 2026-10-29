namespace Clearinet.UI
{
    partial class FindPanel
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
            this.cbSelectMatches = new System.Windows.Forms.CheckBox();
            this.txtToFind = new System.Windows.Forms.TextBox();
            this.cbDecode = new System.Windows.Forms.CheckBox();
            this.btnFindClose = new System.Windows.Forms.Button();
            this.cbSearchBinaries = new System.Windows.Forms.CheckBox();
            this.dudColor = new System.Windows.Forms.DomainUpDown();
            this.ccbxWhere = new CheckedComboBox();
            this.SuspendLayout();
            // 
            // cbSelectMatches
            // 
            this.cbSelectMatches.AutoSize = true;
            this.cbSelectMatches.Location = new System.Drawing.Point(308, 84);
            this.cbSelectMatches.Margin = new System.Windows.Forms.Padding(4);
            this.cbSelectMatches.Name = "cbSelectMatches";
            this.cbSelectMatches.Size = new System.Drawing.Size(177, 29);
            this.cbSelectMatches.TabIndex = 5;
            this.cbSelectMatches.Text = "Select Matches";
            this.cbSelectMatches.UseVisualStyleBackColor = true;
            // 
            // txtToFind
            // 
            this.txtToFind.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtToFind.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtToFind.Location = new System.Drawing.Point(6, 4);
            this.txtToFind.Margin = new System.Windows.Forms.Padding(4);
            this.txtToFind.Name = "txtToFind";
            this.txtToFind.Size = new System.Drawing.Size(442, 32);
            this.txtToFind.TabIndex = 0;
            this.txtToFind.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtToFind_KeyDown);
            // 
            // cbDecode
            // 
            this.cbDecode.AutoSize = true;
            this.cbDecode.Location = new System.Drawing.Point(126, 84);
            this.cbDecode.Margin = new System.Windows.Forms.Padding(4);
            this.cbDecode.Name = "cbDecode";
            this.cbDecode.Size = new System.Drawing.Size(174, 29);
            this.cbDecode.TabIndex = 4;
            this.cbDecode.Text = "Decode bodies";
            this.cbDecode.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.cbDecode.UseVisualStyleBackColor = true;
            // 
            // btnFindClose
            // 
            this.btnFindClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFindClose.BackColor = System.Drawing.Color.Red;
            this.btnFindClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnFindClose.ForeColor = System.Drawing.Color.White;
            this.btnFindClose.Location = new System.Drawing.Point(456, -8);
            this.btnFindClose.Margin = new System.Windows.Forms.Padding(4);
            this.btnFindClose.Name = "btnFindClose";
            this.btnFindClose.Size = new System.Drawing.Size(25, 29);
            this.btnFindClose.TabIndex = 6;
            this.btnFindClose.Text = "x";
            this.btnFindClose.UseVisualStyleBackColor = false;
            this.btnFindClose.Click += new System.EventHandler(this.btnFindClose_Click);
            // 
            // cbSearchBinaries
            // 
            this.cbSearchBinaries.AutoSize = true;
            this.cbSearchBinaries.Location = new System.Drawing.Point(6, 84);
            this.cbSearchBinaries.Margin = new System.Windows.Forms.Padding(4);
            this.cbSearchBinaries.Name = "cbSearchBinaries";
            this.cbSearchBinaries.Size = new System.Drawing.Size(112, 29);
            this.cbSearchBinaries.TabIndex = 3;
            this.cbSearchBinaries.Text = "Binaries";
            this.cbSearchBinaries.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.cbSearchBinaries.UseVisualStyleBackColor = true;
            // 
            // dudColor
            // 
            this.dudColor.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.dudColor.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dudColor.Items.Add("Yellow");
            this.dudColor.Items.Add("PaleGreen");
            this.dudColor.Items.Add("Plum");
            this.dudColor.Items.Add("Pink");
            this.dudColor.Items.Add("LightCyan");
            this.dudColor.Location = new System.Drawing.Point(361, 43);
            this.dudColor.Name = "dudColor";
            this.dudColor.ReadOnly = true;
            this.dudColor.Size = new System.Drawing.Size(120, 28);
            this.dudColor.TabIndex = 2;
            this.dudColor.SelectedItemChanged += new System.EventHandler(this.dudColor_SelectedItemChanged);
            // 
            // ccbxWhere
            // 
            this.ccbxWhere.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ccbxWhere.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ccbxWhere.EmptyText = "-- Search Where --";
            this.ccbxWhere.FormattingEnabled = true;
            this.ccbxWhere.Location = new System.Drawing.Point(6, 44);
            this.ccbxWhere.Margin = new System.Windows.Forms.Padding(4);
            this.ccbxWhere.Name = "ccbxWhere";
            this.ccbxWhere.Size = new System.Drawing.Size(348, 32);
            this.ccbxWhere.SummarySeparator = ";";
            this.ccbxWhere.TabIndex = 1;
            // 
            // FindPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.dudColor);
            this.Controls.Add(this.cbSearchBinaries);
            this.Controls.Add(this.btnFindClose);
            this.Controls.Add(this.cbDecode);
            this.Controls.Add(this.ccbxWhere);
            this.Controls.Add(this.cbSelectMatches);
            this.Controls.Add(this.txtToFind);
            this.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "FindPanel";
            this.Size = new System.Drawing.Size(485, 117);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbSelectMatches;
        internal System.Windows.Forms.TextBox txtToFind;
        private CheckedComboBox ccbxWhere;
        private System.Windows.Forms.CheckBox cbDecode;
        private System.Windows.Forms.Button btnFindClose;
        private System.Windows.Forms.CheckBox cbSearchBinaries;
        private System.Windows.Forms.DomainUpDown dudColor;
    }
}
