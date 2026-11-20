namespace Extensions
{
    partial class ImageView
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
            this.components = new System.ComponentModel.Container();
            this.pbImage = new System.Windows.Forms.PictureBox();
            this.mnuImage = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miCopyBitmap = new System.Windows.Forms.ToolStripMenuItem();
            this.copyAsDataURL = new System.Windows.Forms.ToolStripMenuItem();
            this.miSaveOnDesktop = new System.Windows.Forms.ToolStripMenuItem();
            this.miShowFullscreen = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.miTools = new System.Windows.Forms.ToolStripMenuItem();
            this.miToolsConvertToJPEG = new System.Windows.Forms.ToolStripMenuItem();
            this.pnlLeft = new System.Windows.Forms.Panel();
            this.txtMetadata = new System.Windows.Forms.TextBox();
            this.cbxSizing = new System.Windows.Forms.ComboBox();
            this.splitter1 = new System.Windows.Forms.Splitter();
            ((System.ComponentModel.ISupportInitialize)(this.pbImage)).BeginInit();
            this.mnuImage.SuspendLayout();
            this.pnlLeft.SuspendLayout();
            this.SuspendLayout();
            // 
            // pbImage
            // 
            this.pbImage.BackColor = System.Drawing.Color.AliceBlue;
            this.pbImage.ContextMenuStrip = this.mnuImage;
            this.pbImage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pbImage.Location = new System.Drawing.Point(121, 0);
            this.pbImage.Margin = new System.Windows.Forms.Padding(4);
            this.pbImage.Name = "pbImage";
            this.pbImage.Size = new System.Drawing.Size(364, 442);
            this.pbImage.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pbImage.TabIndex = 0;
            this.pbImage.TabStop = false;
            // 
            // mnuImage
            // 
            this.mnuImage.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCopyBitmap,
            this.copyAsDataURL,
            this.miSaveOnDesktop,
            this.miShowFullscreen,
            this.toolStripMenuItem1,
            this.miTools});
            this.mnuImage.Name = "mnuImage";
            this.mnuImage.Size = new System.Drawing.Size(181, 142);
            this.mnuImage.Opening += new System.ComponentModel.CancelEventHandler(this.mnuImage_Opening);
            // 
            // miCopyBitmap
            // 
            this.miCopyBitmap.Name = "miCopyBitmap";
            this.miCopyBitmap.Size = new System.Drawing.Size(180, 22);
            this.miCopyBitmap.Text = "&Copy Image";
            this.miCopyBitmap.Click += new System.EventHandler(this.miCopyBitmap_Click);
            // 
            // copyAsDataURL
            // 
            this.copyAsDataURL.Name = "copyAsDataURL";
            this.copyAsDataURL.Size = new System.Drawing.Size(180, 22);
            this.copyAsDataURL.Text = "Copy as &DataURL";
            this.copyAsDataURL.Click += new System.EventHandler(this.copyAsDataURL_Click);
            // 
            // miSaveOnDesktop
            // 
            this.miSaveOnDesktop.Name = "miSaveOnDesktop";
            this.miSaveOnDesktop.Size = new System.Drawing.Size(180, 22);
            this.miSaveOnDesktop.Text = "Save on Des&ktop";
            this.miSaveOnDesktop.Click += new System.EventHandler(this.miSaveOnDesktop_Click);
            // 
            // miShowFullscreen
            // 
            this.miShowFullscreen.Name = "miShowFullscreen";
            this.miShowFullscreen.Size = new System.Drawing.Size(180, 22);
            this.miShowFullscreen.Text = "Show &Fullscreen";
            this.miShowFullscreen.Click += new System.EventHandler(this.miShowFullscreen_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(177, 6);
            // 
            // miTools
            // 
            this.miTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miToolsConvertToJPEG});
            this.miTools.Name = "miTools";
            this.miTools.Size = new System.Drawing.Size(180, 22);
            this.miTools.Text = "&Tools";
            // 
            // miToolsConvertToJPEG
            // 
            this.miToolsConvertToJPEG.Name = "miToolsConvertToJPEG";
            this.miToolsConvertToJPEG.Size = new System.Drawing.Size(180, 22);
            this.miToolsConvertToJPEG.Text = "Convert to &JPEG";
            this.miToolsConvertToJPEG.Click += new System.EventHandler(this.miConvertToJPEG_Click);
            // 
            // pnlLeft
            // 
            this.pnlLeft.Controls.Add(this.txtMetadata);
            this.pnlLeft.Controls.Add(this.cbxSizing);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlLeft.Location = new System.Drawing.Point(0, 0);
            this.pnlLeft.Margin = new System.Windows.Forms.Padding(4);
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.Size = new System.Drawing.Size(117, 442);
            this.pnlLeft.TabIndex = 1;
            // 
            // txtMetadata
            // 
            this.txtMetadata.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtMetadata.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtMetadata.Location = new System.Drawing.Point(0, 0);
            this.txtMetadata.Margin = new System.Windows.Forms.Padding(4);
            this.txtMetadata.Multiline = true;
            this.txtMetadata.Name = "txtMetadata";
            this.txtMetadata.ReadOnly = true;
            this.txtMetadata.Size = new System.Drawing.Size(117, 418);
            this.txtMetadata.TabIndex = 0;
            // 
            // cbxSizing
            // 
            this.cbxSizing.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.cbxSizing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxSizing.FormattingEnabled = true;
            this.cbxSizing.Items.AddRange(new object[] {
            "Scale-to-fit",
            "No scaling"});
            this.cbxSizing.Location = new System.Drawing.Point(0, 418);
            this.cbxSizing.Margin = new System.Windows.Forms.Padding(4);
            this.cbxSizing.Name = "cbxSizing";
            this.cbxSizing.Size = new System.Drawing.Size(117, 24);
            this.cbxSizing.TabIndex = 1;
            this.cbxSizing.SelectedIndexChanged += new System.EventHandler(this.cbxSizing_SelectedIndexChanged);
            // 
            // splitter1
            // 
            this.splitter1.Location = new System.Drawing.Point(117, 0);
            this.splitter1.Margin = new System.Windows.Forms.Padding(4);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(4, 442);
            this.splitter1.TabIndex = 2;
            this.splitter1.TabStop = false;
            // 
            // ImageView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pbImage);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.pnlLeft);
            this.Font = new System.Drawing.Font("Tahoma", 10F);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "ImageView";
            this.Size = new System.Drawing.Size(485, 442);
            ((System.ComponentModel.ISupportInitialize)(this.pbImage)).EndInit();
            this.mnuImage.ResumeLayout(false);
            this.pnlLeft.ResumeLayout(false);
            this.pnlLeft.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel pnlLeft;
        private System.Windows.Forms.Splitter splitter1;
        internal System.Windows.Forms.PictureBox pbImage;
        internal System.Windows.Forms.TextBox txtMetadata;
        private System.Windows.Forms.ComboBox cbxSizing;
        private System.Windows.Forms.ContextMenuStrip mnuImage;
        private System.Windows.Forms.ToolStripMenuItem miCopyBitmap;
        private System.Windows.Forms.ToolStripMenuItem copyAsDataURL;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem miSaveOnDesktop;
        private System.Windows.Forms.ToolStripMenuItem miShowFullscreen;
        private System.Windows.Forms.ToolStripMenuItem miToolsConvertToJPEG;
        internal System.Windows.Forms.ToolStripMenuItem miTools;
    }
}
