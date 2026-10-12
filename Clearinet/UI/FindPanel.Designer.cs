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
            this.btnFind = new System.Windows.Forms.Button();
            this.ccbxWhere = new CheckedComboBox();
            this.lblLookIn = new System.Windows.Forms.Label();
            this.cbDecode = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // cbSelectMatches
            // 
            this.cbSelectMatches.AutoSize = true;
            this.cbSelectMatches.Location = new System.Drawing.Point(28, 114);
            this.cbSelectMatches.Margin = new System.Windows.Forms.Padding(4);
            this.cbSelectMatches.Name = "cbSelectMatches";
            this.cbSelectMatches.Size = new System.Drawing.Size(144, 25);
            this.cbSelectMatches.TabIndex = 4;
            this.cbSelectMatches.Text = "Select Matches";
            this.cbSelectMatches.UseVisualStyleBackColor = true;
            // 
            // txtToFind
            // 
            this.txtToFind.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtToFind.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtToFind.Location = new System.Drawing.Point(28, 18);
            this.txtToFind.Margin = new System.Windows.Forms.Padding(4);
            this.txtToFind.Name = "txtToFind";
            this.txtToFind.Size = new System.Drawing.Size(528, 28);
            this.txtToFind.TabIndex = 0;
            this.txtToFind.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtToFind_KeyDown);
            // 
            // btnFind
            // 
            this.btnFind.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFind.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnFind.Image = global::Clearinet.Properties.Resources.FindIcon;
            this.btnFind.Location = new System.Drawing.Point(564, 9);
            this.btnFind.Margin = new System.Windows.Forms.Padding(4);
            this.btnFind.Name = "btnFind";
            this.btnFind.Size = new System.Drawing.Size(64, 42);
            this.btnFind.TabIndex = 1;
            this.btnFind.UseVisualStyleBackColor = true;
            this.btnFind.Click += new System.EventHandler(this.btnFind_Click);
            // 
            // ccbxWhere
            // 
            this.ccbxWhere.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ccbxWhere.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ccbxWhere.EmptyText = "-- Search Where --";
            this.ccbxWhere.FormattingEnabled = true;
            this.ccbxWhere.Location = new System.Drawing.Point(161, 59);
            this.ccbxWhere.Margin = new System.Windows.Forms.Padding(4);
            this.ccbxWhere.Name = "ccbxWhere";
            this.ccbxWhere.Size = new System.Drawing.Size(459, 29);
            this.ccbxWhere.SummarySeparator = ";";
            this.ccbxWhere.TabIndex = 3;
            // 
            // lblLookIn
            // 
            this.lblLookIn.AutoSize = true;
            this.lblLookIn.Location = new System.Drawing.Point(86, 62);
            this.lblLookIn.Name = "lblLookIn";
            this.lblLookIn.Size = new System.Drawing.Size(68, 21);
            this.lblLookIn.TabIndex = 2;
            this.lblLookIn.Text = "Look in:";
            // 
            // cbDecode
            // 
            this.cbDecode.AutoSize = true;
            this.cbDecode.Location = new System.Drawing.Point(196, 114);
            this.cbDecode.Margin = new System.Windows.Forms.Padding(4);
            this.cbDecode.Name = "cbDecode";
            this.cbDecode.Size = new System.Drawing.Size(141, 25);
            this.cbDecode.TabIndex = 5;
            this.cbDecode.Text = "Decode bodies";
            this.cbDecode.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.cbDecode.UseVisualStyleBackColor = true;
            // 
            // FindPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.cbDecode);
            this.Controls.Add(this.lblLookIn);
            this.Controls.Add(this.ccbxWhere);
            this.Controls.Add(this.cbSelectMatches);
            this.Controls.Add(this.txtToFind);
            this.Controls.Add(this.btnFind);
            this.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "FindPanel";
            this.Size = new System.Drawing.Size(648, 164);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbSelectMatches;
        internal System.Windows.Forms.TextBox txtToFind;
        private System.Windows.Forms.Button btnFind;
        private CheckedComboBox ccbxWhere;
        private System.Windows.Forms.Label lblLookIn;
        private System.Windows.Forms.CheckBox cbDecode;
    }
}
