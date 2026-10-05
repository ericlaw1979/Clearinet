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
            this.txtFirstLine = new System.Windows.Forms.TextBox();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.tvNVP = new System.Windows.Forms.TreeView();
            this.SuspendLayout();
            // 
            // txtFirstLine
            // 
            this.txtFirstLine.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtFirstLine.Location = new System.Drawing.Point(0, 0);
            this.txtFirstLine.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtFirstLine.Name = "txtFirstLine";
            this.txtFirstLine.ReadOnly = true;
            this.txtFirstLine.Size = new System.Drawing.Size(827, 28);
            this.txtFirstLine.TabIndex = 0;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(61, 4);
            // 
            // tvNVP
            // 
            this.tvNVP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tvNVP.Location = new System.Drawing.Point(0, 28);
            this.tvNVP.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tvNVP.Name = "tvNVP";
            this.tvNVP.Size = new System.Drawing.Size(827, 480);
            this.tvNVP.TabIndex = 2;
            // 
            // HeaderView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tvNVP);
            this.Controls.Add(this.txtFirstLine);
            this.Font = new System.Drawing.Font("Tahoma", 10F);
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "HeaderView";
            this.Size = new System.Drawing.Size(827, 508);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        internal System.Windows.Forms.TextBox txtFirstLine;
        internal System.Windows.Forms.TreeView tvNVP;
    }
}
