namespace Clearinet
{
    partial class frmPrompt
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmPrompt));
            this.txtResponse = new System.Windows.Forms.TextBox();
            this.rePrompt = new System.Windows.Forms.RichTextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pbPromptIcon = new System.Windows.Forms.PictureBox();
            this.lblChecksum = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pbPromptIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // txtResponse
            // 
            this.txtResponse.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtResponse.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtResponse.Location = new System.Drawing.Point(125, 156);
            this.txtResponse.Margin = new System.Windows.Forms.Padding(2, 4, 2, 4);
            this.txtResponse.Name = "txtResponse";
            this.txtResponse.Size = new System.Drawing.Size(416, 24);
            this.txtResponse.TabIndex = 0;
            this.txtResponse.TextChanged += new System.EventHandler(this.txtResponse_TextChanged);
            // 
            // rePrompt
            // 
            this.rePrompt.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rePrompt.BackColor = System.Drawing.SystemColors.Control;
            this.rePrompt.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rePrompt.Location = new System.Drawing.Point(125, 30);
            this.rePrompt.Margin = new System.Windows.Forms.Padding(2, 4, 2, 4);
            this.rePrompt.Name = "rePrompt";
            this.rePrompt.ReadOnly = true;
            this.rePrompt.Size = new System.Drawing.Size(416, 118);
            this.rePrompt.TabIndex = 1;
            this.rePrompt.Text = "";
            // 
            // btnOk
            // 
            this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(358, 209);
            this.btnOk.Margin = new System.Windows.Forms.Padding(2, 4, 2, 4);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(84, 30);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(456, 209);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(2, 4, 2, 4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(84, 30);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // pbPromptIcon
            // 
            this.pbPromptIcon.BackColor = System.Drawing.Color.Transparent;
            this.pbPromptIcon.Image = global::Clearinet.Properties.Resources.PromptQuery;
            this.pbPromptIcon.Location = new System.Drawing.Point(28, 30);
            this.pbPromptIcon.Margin = new System.Windows.Forms.Padding(2);
            this.pbPromptIcon.Name = "pbPromptIcon";
            this.pbPromptIcon.Size = new System.Drawing.Size(80, 80);
            this.pbPromptIcon.TabIndex = 4;
            this.pbPromptIcon.TabStop = false;
            // 
            // lblChecksum
            // 
            this.lblChecksum.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblChecksum.AutoSize = true;
            this.lblChecksum.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblChecksum.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblChecksum.Location = new System.Drawing.Point(24, 209);
            this.lblChecksum.Name = "lblChecksum";
            this.lblChecksum.Size = new System.Drawing.Size(72, 17);
            this.lblChecksum.TabIndex = 5;
            this.lblChecksum.Text = "Checksum";
            this.lblChecksum.Visible = false;
            // 
            // frmPrompt
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(575, 252);
            this.Controls.Add(this.lblChecksum);
            this.Controls.Add(this.pbPromptIcon);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.rePrompt);
            this.Controls.Add(this.txtResponse);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2, 4, 2, 4);
            this.Name = "frmPrompt";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Clearinet has a question";
            ((System.ComponentModel.ISupportInitialize)(this.pbPromptIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtResponse;
        private System.Windows.Forms.RichTextBox rePrompt;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.PictureBox pbPromptIcon;
        private System.Windows.Forms.Label lblChecksum;
    }
}