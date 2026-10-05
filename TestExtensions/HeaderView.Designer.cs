namespace TestExtensions
{
    partial class HeaderView
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
            System.Windows.Forms.ToolStripSeparator miSplitter;
            this.txtFirstLine = new System.Windows.Forms.TextBox();
            this.mnuNodes = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.tvNVP = new System.Windows.Forms.TreeView();
            this.miCopyHeader = new System.Windows.Forms.ToolStripMenuItem();
            this.miCopyHeaderValue = new System.Windows.Forms.ToolStripMenuItem();
            this.miCopyAll = new System.Windows.Forms.ToolStripMenuItem();
            this.miSendToTextWizard = new System.Windows.Forms.ToolStripMenuItem();
            this.miAddHeader = new System.Windows.Forms.ToolStripMenuItem();
            this.miRemoveHeader = new System.Windows.Forms.ToolStripMenuItem();
            this.miPasteHeaders = new System.Windows.Forms.ToolStripMenuItem();
            this.miLookupHeader = new System.Windows.Forms.ToolStripMenuItem();
            this.miEditHeader = new System.Windows.Forms.ToolStripMenuItem();
            this.miHighlightHeader = new System.Windows.Forms.ToolStripMenuItem();
            miSplitter = new System.Windows.Forms.ToolStripSeparator();
            this.mnuNodes.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtFirstLine
            // 
            this.txtFirstLine.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtFirstLine.Location = new System.Drawing.Point(0, 0);
            this.txtFirstLine.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtFirstLine.Name = "txtFirstLine";
            this.txtFirstLine.ReadOnly = true;
            this.txtFirstLine.Size = new System.Drawing.Size(827, 24);
            this.txtFirstLine.TabIndex = 0;
            // 
            // mnuNodes
            // 
            this.mnuNodes.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.mnuNodes.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCopyAll,
            this.miCopyHeader,
            this.miCopyHeaderValue,
            this.miHighlightHeader,
            this.miLookupHeader,
            this.miSendToTextWizard,
            miSplitter,
            this.miEditHeader,
            this.miAddHeader,
            this.miRemoveHeader,
            this.miPasteHeaders});
            this.mnuNodes.Name = "contextMenuStrip1";
            this.mnuNodes.Size = new System.Drawing.Size(190, 252);
            this.mnuNodes.Opening += new System.ComponentModel.CancelEventHandler(this.mnuNodes_Opening);
            // 
            // tvNVP
            // 
            this.tvNVP.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tvNVP.ContextMenuStrip = this.mnuNodes;
            this.tvNVP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvNVP.Location = new System.Drawing.Point(0, 24);
            this.tvNVP.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tvNVP.Name = "tvNVP";
            this.tvNVP.Size = new System.Drawing.Size(827, 484);
            this.tvNVP.TabIndex = 2;
            // 
            // miCopyHeader
            // 
            this.miCopyHeader.Name = "miCopyHeader";
            this.miCopyHeader.Size = new System.Drawing.Size(189, 22);
            this.miCopyHeader.Text = "&Copy Header";
            // 
            // miCopyHeaderValue
            // 
            this.miCopyHeaderValue.Name = "miCopyHeaderValue";
            this.miCopyHeaderValue.Size = new System.Drawing.Size(189, 22);
            this.miCopyHeaderValue.Text = "Copy &Value";
            // 
            // miCopyAll
            // 
            this.miCopyAll.Name = "miCopyAll";
            this.miCopyAll.Size = new System.Drawing.Size(189, 22);
            this.miCopyAll.Text = "C&opy All";
            // 
            // miSendToTextWizard
            // 
            this.miSendToTextWizard.Name = "miSendToTextWizard";
            this.miSendToTextWizard.Size = new System.Drawing.Size(189, 22);
            this.miSendToTextWizard.Text = "Send to Te&xtWizard";
            // 
            // miAddHeader
            // 
            this.miAddHeader.Name = "miAddHeader";
            this.miAddHeader.Size = new System.Drawing.Size(189, 22);
            this.miAddHeader.Text = "&Add Header...";
            // 
            // miRemoveHeader
            // 
            this.miRemoveHeader.Name = "miRemoveHeader";
            this.miRemoveHeader.Size = new System.Drawing.Size(189, 22);
            this.miRemoveHeader.Text = "&Remove Header";
            // 
            // miPasteHeaders
            // 
            this.miPasteHeaders.Name = "miPasteHeaders";
            this.miPasteHeaders.Size = new System.Drawing.Size(189, 22);
            this.miPasteHeaders.Text = "&Paste Headers";
            // 
            // miLookupHeader
            // 
            this.miLookupHeader.Name = "miLookupHeader";
            this.miLookupHeader.Size = new System.Drawing.Size(189, 22);
            this.miLookupHeader.Text = "&Lookup Header...";
            // 
            // miSplitter
            // 
            miSplitter.Name = "miSplitter";
            miSplitter.Size = new System.Drawing.Size(186, 6);
            // 
            // miEditHeader
            // 
            this.miEditHeader.Name = "miEditHeader";
            this.miEditHeader.Size = new System.Drawing.Size(189, 22);
            this.miEditHeader.Text = "&Edit Header...";
            // 
            // miHighlightHeader
            // 
            this.miHighlightHeader.Name = "miHighlightHeader";
            this.miHighlightHeader.Size = new System.Drawing.Size(189, 22);
            this.miHighlightHeader.Text = "&Highlight";
            this.miHighlightHeader.Click += new System.EventHandler(this.miHighlightHeader_Click);
            // 
            // HeaderView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tvNVP);
            this.Controls.Add(this.txtFirstLine);
            this.Font = new System.Drawing.Font("Tahoma", 10F);
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "HeaderView";
            this.Size = new System.Drawing.Size(827, 508);
            this.mnuNodes.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ContextMenuStrip mnuNodes;
        internal System.Windows.Forms.TextBox txtFirstLine;
        internal System.Windows.Forms.TreeView tvNVP;
        private System.Windows.Forms.ToolStripMenuItem miCopyAll;
        private System.Windows.Forms.ToolStripMenuItem miCopyHeader;
        private System.Windows.Forms.ToolStripMenuItem miCopyHeaderValue;
        private System.Windows.Forms.ToolStripMenuItem miLookupHeader;
        private System.Windows.Forms.ToolStripMenuItem miSendToTextWizard;
        private System.Windows.Forms.ToolStripMenuItem miEditHeader;
        private System.Windows.Forms.ToolStripMenuItem miAddHeader;
        private System.Windows.Forms.ToolStripMenuItem miRemoveHeader;
        private System.Windows.Forms.ToolStripMenuItem miPasteHeaders;
        private System.Windows.Forms.ToolStripMenuItem miHighlightHeader;
    }
}
