namespace Clearinet
{
    partial class ComposerView
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
            this.cbxMethod = new System.Windows.Forms.ComboBox();
            this.cbxHTTPVersion = new System.Windows.Forms.ComboBox();
            this.txtURL = new System.Windows.Forms.TextBox();
            this.pnlMessage = new System.Windows.Forms.Panel();
            this.txtHeaders = new System.Windows.Forms.TextBox();
            this.txtBody = new System.Windows.Forms.TextBox();
            this.splitCompose = new System.Windows.Forms.Splitter();
            this.btnSend = new System.Windows.Forms.Button();
            this.pnlMessage.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbxMethod
            // 
            this.cbxMethod.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbxMethod.FormattingEnabled = true;
            this.cbxMethod.Items.AddRange(new object[] {
            "GET",
            "PUT",
            "POST",
            "HEAD",
            "DELETE"});
            this.cbxMethod.Location = new System.Drawing.Point(13, 14);
            this.cbxMethod.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbxMethod.Name = "cbxMethod";
            this.cbxMethod.Size = new System.Drawing.Size(119, 29);
            this.cbxMethod.TabIndex = 4;
            // 
            // cbxHTTPVersion
            // 
            this.cbxHTTPVersion.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbxHTTPVersion.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.cbxHTTPVersion.FormattingEnabled = true;
            this.cbxHTTPVersion.Items.AddRange(new object[] {
            "HTTP/1.1",
            "HTTP/1.0"});
            this.cbxHTTPVersion.Location = new System.Drawing.Point(861, 12);
            this.cbxHTTPVersion.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbxHTTPVersion.Name = "cbxHTTPVersion";
            this.cbxHTTPVersion.Size = new System.Drawing.Size(131, 29);
            this.cbxHTTPVersion.TabIndex = 5;
            // 
            // txtURL
            // 
            this.txtURL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtURL.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtURL.Location = new System.Drawing.Point(138, 14);
            this.txtURL.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtURL.Name = "txtURL";
            this.txtURL.Size = new System.Drawing.Size(717, 28);
            this.txtURL.TabIndex = 0;
            // 
            // pnlMessage
            // 
            this.pnlMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlMessage.Controls.Add(this.splitCompose);
            this.pnlMessage.Controls.Add(this.txtBody);
            this.pnlMessage.Controls.Add(this.txtHeaders);
            this.pnlMessage.Location = new System.Drawing.Point(13, 59);
            this.pnlMessage.Name = "pnlMessage";
            this.pnlMessage.Size = new System.Drawing.Size(1060, 719);
            this.pnlMessage.TabIndex = 4;
            // 
            // txtHeaders
            // 
            this.txtHeaders.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtHeaders.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtHeaders.Location = new System.Drawing.Point(0, 0);
            this.txtHeaders.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtHeaders.Multiline = true;
            this.txtHeaders.Name = "txtHeaders";
            this.txtHeaders.Size = new System.Drawing.Size(1060, 160);
            this.txtHeaders.TabIndex = 2;
            // 
            // txtBody
            // 
            this.txtBody.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtBody.Location = new System.Drawing.Point(0, 160);
            this.txtBody.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtBody.Multiline = true;
            this.txtBody.Name = "txtBody";
            this.txtBody.Size = new System.Drawing.Size(1060, 559);
            this.txtBody.TabIndex = 3;
            // 
            // splitCompose
            // 
            this.splitCompose.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitCompose.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitCompose.Location = new System.Drawing.Point(0, 160);
            this.splitCompose.Name = "splitCompose";
            this.splitCompose.Size = new System.Drawing.Size(1060, 3);
            this.splitCompose.TabIndex = 6;
            this.splitCompose.TabStop = false;
            // 
            // btnSend
            // 
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.Location = new System.Drawing.Point(998, 11);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(75, 30);
            this.btnSend.TabIndex = 1;
            this.btnSend.Text = "Send";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // ComposerView
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.pnlMessage);
            this.Controls.Add(this.txtURL);
            this.Controls.Add(this.cbxHTTPVersion);
            this.Controls.Add(this.cbxMethod);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Tahoma", 10F);
            this.Margin = new System.Windows.Forms.Padding(10);
            this.Name = "ComposerView";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.Size = new System.Drawing.Size(1086, 791);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.ComposerView_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.ComposerView_DragEnter);
            this.pnlMessage.ResumeLayout(false);
            this.pnlMessage.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.ComboBox cbxMethod;
        internal System.Windows.Forms.ComboBox cbxHTTPVersion;
        internal System.Windows.Forms.TextBox txtURL;
        internal System.Windows.Forms.Panel pnlMessage;
        internal System.Windows.Forms.Splitter splitCompose;
        internal System.Windows.Forms.TextBox txtBody;
        internal System.Windows.Forms.TextBox txtHeaders;
        internal System.Windows.Forms.Button btnSend;
    }
}
