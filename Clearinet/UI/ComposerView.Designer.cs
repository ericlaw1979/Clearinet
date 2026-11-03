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
            this.splitCompose = new System.Windows.Forms.Splitter();
            this.txtBody = new System.Windows.Forms.TextBox();
            this.txtHeaders = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.layoutRequest = new System.Windows.Forms.TableLayoutPanel();
            this.pnlMessage.SuspendLayout();
            this.layoutRequest.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbxMethod
            // 
            this.cbxMethod.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbxMethod.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbxMethod.FormattingEnabled = true;
            this.cbxMethod.Items.AddRange(new object[] {
            "GET",
            "POST",
            "HEAD",
            "PUT",
            "PATCH",
            "DELETE",
            "QUERY",
            "OPTIONS"});
            this.cbxMethod.Location = new System.Drawing.Point(13, 15);
            this.cbxMethod.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbxMethod.Name = "cbxMethod";
            this.cbxMethod.Size = new System.Drawing.Size(119, 24);
            this.cbxMethod.TabIndex = 4;
            this.cbxMethod.TextChanged += new System.EventHandler(this.cbxMethod_TextChanged);
            // 
            // cbxHTTPVersion
            // 
            this.cbxHTTPVersion.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbxHTTPVersion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxHTTPVersion.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbxHTTPVersion.FormattingEnabled = true;
            this.cbxHTTPVersion.Items.AddRange(new object[] {
            "HTTP/1.1",
            "HTTP/1.0"});
            this.cbxHTTPVersion.Location = new System.Drawing.Point(861, 13);
            this.cbxHTTPVersion.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbxHTTPVersion.Name = "cbxHTTPVersion";
            this.cbxHTTPVersion.Size = new System.Drawing.Size(131, 24);
            this.cbxHTTPVersion.TabIndex = 5;
            // 
            // txtURL
            // 
            this.txtURL.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtURL.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtURL.Location = new System.Drawing.Point(138, 14);
            this.txtURL.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtURL.Name = "txtURL";
            this.txtURL.Size = new System.Drawing.Size(717, 24);
            this.txtURL.TabIndex = 0;
            this.txtURL.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtURL_KeyDown);
            // 
            // pnlMessage
            // 
            this.pnlMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMessage.Controls.Add(this.txtBody);
            this.pnlMessage.Controls.Add(this.splitCompose);
            this.pnlMessage.Controls.Add(this.txtHeaders);
            this.pnlMessage.Location = new System.Drawing.Point(13, 59);
            this.pnlMessage.Name = "pnlMessage";
            this.pnlMessage.Size = new System.Drawing.Size(1060, 719);
            this.pnlMessage.TabIndex = 4;
            // 
            // splitCompose
            // 
            this.splitCompose.BackColor = System.Drawing.Color.LightSlateGray;
            this.splitCompose.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitCompose.Location = new System.Drawing.Point(0, 160);
            this.splitCompose.Name = "splitCompose";
            this.splitCompose.Size = new System.Drawing.Size(1060, 6);
            this.splitCompose.TabIndex = 6;
            this.splitCompose.TabStop = false;
            // 
            // txtBody
            // 
            this.txtBody.BackColor = System.Drawing.SystemColors.ControlLight;
            this.txtBody.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtBody.Location = new System.Drawing.Point(0, 166);
            this.txtBody.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtBody.Multiline = true;
            this.txtBody.Name = "txtBody";
            this.txtBody.Size = new System.Drawing.Size(1060, 553);
            this.txtBody.TabIndex = 3;
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
            this.txtHeaders.Text = "Host: debugtheweb.com";
            // 
            // btnSend
            // 
            this.btnSend.AutoSize = true;
            this.btnSend.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSend.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnSend.Location = new System.Drawing.Point(998, 11);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(75, 30);
            this.btnSend.TabIndex = 1;
            this.btnSend.Text = "Send";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // layoutRequest
            //
            this.layoutRequest.AutoSize = true;
            this.layoutRequest.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.layoutRequest.ColumnCount = 4;
            this.layoutRequest.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 119F));
            this.layoutRequest.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutRequest.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 131F));
            this.layoutRequest.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.layoutRequest.Controls.Add(this.cbxMethod, 0, 0);
            this.layoutRequest.Controls.Add(this.txtURL, 1, 0);
            this.layoutRequest.Controls.Add(this.cbxHTTPVersion, 2, 0);
            this.layoutRequest.Controls.Add(this.btnSend, 3, 0);
            this.layoutRequest.Dock = System.Windows.Forms.DockStyle.Top;
            this.layoutRequest.Name = "layoutRequest";
            this.layoutRequest.Padding = new System.Windows.Forms.Padding(0, 0, 0, 10);
            this.layoutRequest.RowCount = 2;
            this.layoutRequest.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.layoutRequest.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.layoutRequest.TabIndex = 0;
            //
            // ComposerView
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pnlMessage);
            this.Controls.Add(this.layoutRequest);
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
            this.layoutRequest.ResumeLayout(false);
            this.layoutRequest.PerformLayout();
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
        private System.Windows.Forms.TableLayoutPanel layoutRequest;
    }
}
