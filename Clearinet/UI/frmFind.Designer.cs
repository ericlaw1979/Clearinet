namespace Clearinet
{
    partial class frmFind
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
            this.btnFind = new System.Windows.Forms.Button();
            this.txtToFind = new System.Windows.Forms.TextBox();
            this.cbxFindIn = new System.Windows.Forms.ComboBox();
            this.cbxWhichComponent = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // btnFind
            // 
            this.btnFind.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnFind.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnFind.Location = new System.Drawing.Point(358, 249);
            this.btnFind.Name = "btnFind";
            this.btnFind.Size = new System.Drawing.Size(108, 32);
            this.btnFind.TabIndex = 3;
            this.btnFind.Text = "&Find";
            this.btnFind.UseVisualStyleBackColor = true;
            this.btnFind.Click += new System.EventHandler(this.btnFind_Click);
            // 
            // txtToFind
            // 
            this.txtToFind.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtToFind.Location = new System.Drawing.Point(12, 12);
            this.txtToFind.Name = "txtToFind";
            this.txtToFind.Size = new System.Drawing.Size(454, 26);
            this.txtToFind.TabIndex = 0;
            // 
            // cbxFindIn
            // 
            this.cbxFindIn.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxFindIn.FormattingEnabled = true;
            this.cbxFindIn.Items.AddRange(new object[] {
            "Requests & Responses",
            "Requests",
            "Responses",
            "URLs"});
            this.cbxFindIn.Location = new System.Drawing.Point(65, 72);
            this.cbxFindIn.Name = "cbxFindIn";
            this.cbxFindIn.Size = new System.Drawing.Size(401, 26);
            this.cbxFindIn.TabIndex = 1;
            // 
            // cbxWhichComponent
            // 
            this.cbxWhichComponent.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxWhichComponent.FormattingEnabled = true;
            this.cbxWhichComponent.Items.AddRange(new object[] {
            "Headers & bodies",
            "Headers",
            "Bodies"});
            this.cbxWhichComponent.Location = new System.Drawing.Point(65, 104);
            this.cbxWhichComponent.Name = "cbxWhichComponent";
            this.cbxWhichComponent.Size = new System.Drawing.Size(401, 26);
            this.cbxWhichComponent.TabIndex = 2;
            // 
            // frmFind
            // 
            this.AcceptButton = this.btnFind;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(490, 303);
            this.Controls.Add(this.cbxWhichComponent);
            this.Controls.Add(this.cbxFindIn);
            this.Controls.Add(this.txtToFind);
            this.Controls.Add(this.btnFind);
            this.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.KeyPreview = true;
            this.Name = "frmFind";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Find Exchanges";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnFind;
        internal System.Windows.Forms.TextBox txtToFind;
        internal System.Windows.Forms.ComboBox cbxFindIn;
        internal System.Windows.Forms.ComboBox cbxWhichComponent;
    }
}