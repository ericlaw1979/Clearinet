namespace Clearinet
{
    partial class frmAbout
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmAbout));
            this.lnkFeedback = new System.Windows.Forms.LinkLabel();
            this.txtAbout = new System.Windows.Forms.TextBox();
            this.pbIcon = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pbIcon)).BeginInit();
            this.SuspendLayout();
            // 
            // lnkFeedback
            // 
            this.lnkFeedback.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lnkFeedback.AutoSize = true;
            this.lnkFeedback.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.lnkFeedback.Location = new System.Drawing.Point(108, 296);
            this.lnkFeedback.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lnkFeedback.Name = "lnkFeedback";
            this.lnkFeedback.Size = new System.Drawing.Size(136, 23);
            this.lnkFeedback.TabIndex = 0;
            this.lnkFeedback.TabStop = true;
            this.lnkFeedback.Text = "remove me";
            this.lnkFeedback.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lnkFeedback_LinkClicked);
            // 
            // txtAbout
            // 
            this.txtAbout.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtAbout.BackColor = System.Drawing.SystemColors.Control;
            this.txtAbout.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtAbout.Location = new System.Drawing.Point(111, 16);
            this.txtAbout.Margin = new System.Windows.Forms.Padding(4);
            this.txtAbout.Multiline = true;
            this.txtAbout.Name = "txtAbout";
            this.txtAbout.Size = new System.Drawing.Size(391, 281);
            this.txtAbout.TabIndex = 1;
            this.txtAbout.Text = "";
            // 
            // pbIcon
            // 
            this.pbIcon.Image = ((System.Drawing.Image)(resources.GetObject("pbIcon.Image")));
            this.pbIcon.InitialImage = ((System.Drawing.Image)(resources.GetObject("pbIcon.InitialImage")));
            this.pbIcon.Location = new System.Drawing.Point(4, 16);
            this.pbIcon.Name = "pbIcon";
            this.pbIcon.Size = new System.Drawing.Size(100, 104);
            this.pbIcon.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pbIcon.TabIndex = 2;
            this.pbIcon.TabStop = false;
            // 
            // frmAbout
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(517, 322);
            this.Controls.Add(this.pbIcon);
            this.Controls.Add(this.txtAbout);
            this.Controls.Add(this.lnkFeedback);
            this.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "frmAbout";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About Clearinet";
            ((System.ComponentModel.ISupportInitialize)(this.pbIcon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.LinkLabel lnkFeedback;
        private System.Windows.Forms.TextBox txtAbout;
        private System.Windows.Forms.PictureBox pbIcon;
    }
}