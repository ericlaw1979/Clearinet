namespace TestExtensions
{
    partial class RawText
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
            this.pnlBottom = new System.Windows.Forms.Panel();
            this.txtFind = new System.Windows.Forms.TextBox();
            this.mnuContext = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miSendToTextWizard = new System.Windows.Forms.ToolStripMenuItem();
            this.miCopy = new System.Windows.Forms.ToolStripMenuItem();
            this.miPaste = new System.Windows.Forms.ToolStripMenuItem();
            this.miCut = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.miWordwrap = new System.Windows.Forms.ToolStripMenuItem();
            this.rtbRaw = new Clearinet.RichTextBoxV5();
            this.pnlBottom.SuspendLayout();
            this.mnuContext.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlBottom
            // 
            this.pnlBottom.Controls.Add(this.txtFind);
            this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBottom.Location = new System.Drawing.Point(0, 461);
            this.pnlBottom.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.pnlBottom.Name = "pnlBottom";
            this.pnlBottom.Size = new System.Drawing.Size(581, 32);
            this.pnlBottom.TabIndex = 0;
            // 
            // txtFind
            // 
            this.txtFind.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtFind.Font = new System.Drawing.Font("Tahoma", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtFind.Location = new System.Drawing.Point(2, 5);
            this.txtFind.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtFind.Name = "txtFind";
            this.txtFind.Size = new System.Drawing.Size(517, 24);
            this.txtFind.TabIndex = 0;
            this.txtFind.TextChanged += new System.EventHandler(this.txtFind_TextChanged);
            this.txtFind.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtFind_KeyDown);
            // 
            // mnuContext
            // 
            this.mnuContext.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.mnuContext.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miSendToTextWizard,
            this.miCopy,
            this.miPaste,
            this.miCut,
            this.toolStripMenuItem1,
            this.miWordwrap});
            this.mnuContext.Name = "mnuContext";
            this.mnuContext.Size = new System.Drawing.Size(190, 120);
            this.mnuContext.Opening += new System.ComponentModel.CancelEventHandler(this.mnuContext_Opening);
            // 
            // miSendToTextWizard
            // 
            this.miSendToTextWizard.Name = "miSendToTextWizard";
            this.miSendToTextWizard.Size = new System.Drawing.Size(189, 22);
            this.miSendToTextWizard.Text = "Send to Text&Wizard";
            this.miSendToTextWizard.Click += new System.EventHandler(this.miSendToTextWizard_Click);
            // 
            // miCopy
            // 
            this.miCopy.Name = "miCopy";
            this.miCopy.Size = new System.Drawing.Size(189, 22);
            this.miCopy.Text = "&Copy";
            this.miCopy.Click += new System.EventHandler(this.miCopy_Click);
            // 
            // miPaste
            // 
            this.miPaste.Name = "miPaste";
            this.miPaste.Size = new System.Drawing.Size(189, 22);
            this.miPaste.Text = "&Paste";
            this.miPaste.Click += new System.EventHandler(this.miPaste_Click);
            // 
            // miCut
            // 
            this.miCut.Name = "miCut";
            this.miCut.Size = new System.Drawing.Size(189, 22);
            this.miCut.Text = "Cu&t";
            this.miCut.Click += new System.EventHandler(this.miCut_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(186, 6);
            // 
            // miWordwrap
            // 
            this.miWordwrap.CheckOnClick = true;
            this.miWordwrap.Name = "miWordwrap";
            this.miWordwrap.Size = new System.Drawing.Size(189, 22);
            this.miWordwrap.Text = "&Wordwrap";
            this.miWordwrap.Click += new System.EventHandler(this.miWordwrap_Click);
            // 
            // rtbRaw
            // 
            this.rtbRaw.ContextMenuStrip = this.mnuContext;
            this.rtbRaw.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbRaw.HideSelection = false;
            this.rtbRaw.Location = new System.Drawing.Point(0, 0);
            this.rtbRaw.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.rtbRaw.Name = "rtbRaw";
            this.rtbRaw.ReadOnly = true;
            this.rtbRaw.Size = new System.Drawing.Size(581, 461);
            this.rtbRaw.TabIndex = 1;
            this.rtbRaw.Text = "";
            this.rtbRaw.LinkClicked += new System.Windows.Forms.LinkClickedEventHandler(this.rtbRaw_LinkClicked);
            this.rtbRaw.ReadOnlyChanged += new System.EventHandler(this.rtbRaw_ReadOnlyChanged);
            this.rtbRaw.KeyDown += new System.Windows.Forms.KeyEventHandler(this.rtbRaw_KeyDown);
            // 
            // RawText
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.rtbRaw);
            this.Controls.Add(this.pnlBottom);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "RawText";
            this.Size = new System.Drawing.Size(581, 493);
            this.pnlBottom.ResumeLayout(false);
            this.pnlBottom.PerformLayout();
            this.mnuContext.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlBottom;
        private System.Windows.Forms.TextBox txtFind;
        private System.Windows.Forms.ContextMenuStrip mnuContext;
        private System.Windows.Forms.ToolStripMenuItem miSendToTextWizard;
        private System.Windows.Forms.ToolStripMenuItem miCopy;
        private System.Windows.Forms.ToolStripMenuItem miPaste;
        private System.Windows.Forms.ToolStripMenuItem miCut;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem miWordwrap;
        internal Clearinet.RichTextBoxV5 rtbRaw;
    }
}
