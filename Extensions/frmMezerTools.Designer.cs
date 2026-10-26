namespace Extensions
{
    partial class frmMezerTools
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
            this.gbClipboard = new System.Windows.Forms.GroupBox();
            this.lblCurrentClipboard = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.lblAStringOf = new System.Windows.Forms.Label();
            this.udStringLength = new System.Windows.Forms.NumericUpDown();
            this.btnCopyText = new System.Windows.Forms.Button();
            this.txtPattern = new System.Windows.Forms.TextBox();
            this.gbColor = new System.Windows.Forms.GroupBox();
            this.lblProcessInfo = new System.Windows.Forms.Label();
            this.lblColors = new System.Windows.Forms.Label();
            this.txtColorHex = new System.Windows.Forms.TextBox();
            this.pbColorPick = new System.Windows.Forms.PictureBox();
            this.btnColorPicker = new System.Windows.Forms.Button();
            this.txtColorRGB = new System.Windows.Forms.TextBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.txtHex = new System.Windows.Forms.TextBox();
            this.btnUnicode = new System.Windows.Forms.Button();
            this.txtDecimal = new System.Windows.Forms.TextBox();
            this.gbClipboard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.udStringLength)).BeginInit();
            this.gbColor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbColorPick)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbClipboard
            // 
            this.gbClipboard.Controls.Add(this.lblCurrentClipboard);
            this.gbClipboard.Controls.Add(this.label1);
            this.gbClipboard.Controls.Add(this.label2);
            this.gbClipboard.Controls.Add(this.lblAStringOf);
            this.gbClipboard.Controls.Add(this.udStringLength);
            this.gbClipboard.Controls.Add(this.btnCopyText);
            this.gbClipboard.Controls.Add(this.txtPattern);
            this.gbClipboard.Font = new System.Drawing.Font("Tahoma", 10F);
            this.gbClipboard.Location = new System.Drawing.Point(12, 13);
            this.gbClipboard.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.gbClipboard.Name = "gbClipboard";
            this.gbClipboard.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.gbClipboard.Size = new System.Drawing.Size(616, 189);
            this.gbClipboard.TabIndex = 1;
            this.gbClipboard.TabStop = false;
            this.gbClipboard.Text = "Clipboard";
            // 
            // lblCurrentClipboard
            // 
            this.lblCurrentClipboard.AutoSize = true;
            this.lblCurrentClipboard.Location = new System.Drawing.Point(21, 147);
            this.lblCurrentClipboard.Name = "lblCurrentClipboard";
            this.lblCurrentClipboard.Size = new System.Drawing.Size(253, 21);
            this.lblCurrentClipboard.TabIndex = 7;
            this.lblCurrentClipboard.Text = "The clipboard is currently empty.";
            this.lblCurrentClipboard.MouseMove += new System.Windows.Forms.MouseEventHandler(this.lblCurrentClipboard_MouseMove);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(403, 55);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(145, 21);
            this.label1.TabIndex = 6;
            this.label1.Text = "filled with the text";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(417, 102);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(131, 21);
            this.label2.TabIndex = 5;
            this.label2.Text = "to the clipboard.";
            // 
            // lblAStringOf
            // 
            this.lblAStringOf.AutoSize = true;
            this.lblAStringOf.Location = new System.Drawing.Point(158, 55);
            this.lblAStringOf.Name = "lblAStringOf";
            this.lblAStringOf.Size = new System.Drawing.Size(137, 21);
            this.lblAStringOf.TabIndex = 4;
            this.lblAStringOf.Text = "a String of length";
            // 
            // udStringLength
            // 
            this.udStringLength.Location = new System.Drawing.Point(301, 53);
            this.udStringLength.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.udStringLength.Maximum = new decimal(new int[] {
            10000000,
            0,
            0,
            0});
            this.udStringLength.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.udStringLength.Name = "udStringLength";
            this.udStringLength.Size = new System.Drawing.Size(86, 28);
            this.udStringLength.TabIndex = 3;
            this.udStringLength.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.udStringLength.ThousandsSeparator = true;
            this.udStringLength.Value = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            // 
            // btnCopyText
            // 
            this.btnCopyText.Location = new System.Drawing.Point(17, 38);
            this.btnCopyText.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnCopyText.Name = "btnCopyText";
            this.btnCopyText.Size = new System.Drawing.Size(135, 54);
            this.btnCopyText.TabIndex = 2;
            this.btnCopyText.Text = "Copy";
            this.btnCopyText.UseVisualStyleBackColor = true;
            this.btnCopyText.Click += new System.EventHandler(this.btnCopyText_Click);
            // 
            // txtPattern
            // 
            this.txtPattern.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtPattern.Location = new System.Drawing.Point(17, 102);
            this.txtPattern.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtPattern.Name = "txtPattern";
            this.txtPattern.Size = new System.Drawing.Size(394, 28);
            this.txtPattern.TabIndex = 1;
            this.txtPattern.Text = "Codito Ergo Sum";
            this.txtPattern.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtPattern_KeyPress);
            this.txtPattern.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtPattern_KeyUp);
            // 
            // gbColor
            // 
            this.gbColor.Controls.Add(this.lblProcessInfo);
            this.gbColor.Controls.Add(this.lblColors);
            this.gbColor.Controls.Add(this.txtColorHex);
            this.gbColor.Controls.Add(this.pbColorPick);
            this.gbColor.Controls.Add(this.btnColorPicker);
            this.gbColor.Controls.Add(this.txtColorRGB);
            this.gbColor.Location = new System.Drawing.Point(12, 210);
            this.gbColor.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.gbColor.Name = "gbColor";
            this.gbColor.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.gbColor.Size = new System.Drawing.Size(616, 160);
            this.gbColor.TabIndex = 4;
            this.gbColor.TabStop = false;
            this.gbColor.Text = "Colors";
            // 
            // lblProcessInfo
            // 
            this.lblProcessInfo.AutoEllipsis = true;
            this.lblProcessInfo.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Italic);
            this.lblProcessInfo.ForeColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblProcessInfo.Location = new System.Drawing.Point(417, 86);
            this.lblProcessInfo.Name = "lblProcessInfo";
            this.lblProcessInfo.Size = new System.Drawing.Size(164, 28);
            this.lblProcessInfo.TabIndex = 8;
            this.lblProcessInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblColors
            // 
            this.lblColors.Location = new System.Drawing.Point(13, 30);
            this.lblColors.Name = "lblColors";
            this.lblColors.Size = new System.Drawing.Size(567, 47);
            this.lblColors.TabIndex = 7;
            this.lblColors.Text = "Drag the dropper to sample a screen color and its window\'s process, edit the HTML" +
    " color, or click the button to choose a color.";
            // 
            // txtColorHex
            // 
            this.txtColorHex.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtColorHex.Location = new System.Drawing.Point(57, 87);
            this.txtColorHex.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtColorHex.Name = "txtColorHex";
            this.txtColorHex.Size = new System.Drawing.Size(148, 28);
            this.txtColorHex.TabIndex = 6;
            // 
            // pbColorPick
            // 
            this.pbColorPick.Cursor = System.Windows.Forms.Cursors.Hand;
            this.pbColorPick.Image = global::Extensions.Properties.Resources.FindIcon;
            this.pbColorPick.Location = new System.Drawing.Point(17, 80);
            this.pbColorPick.Name = "pbColorPick";
            this.pbColorPick.Size = new System.Drawing.Size(34, 35);
            this.pbColorPick.TabIndex = 5;
            this.pbColorPick.TabStop = false;
            this.pbColorPick.MouseCaptureChanged += new System.EventHandler(this.pbColorPick_MouseCaptureChanged);
            this.pbColorPick.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pbColorPick_MouseDown);
            this.pbColorPick.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pbColorPick_MouseMove);
            this.pbColorPick.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pbColorPick_MouseUp);
            // 
            // btnColorPicker
            // 
            this.btnColorPicker.Location = new System.Drawing.Point(365, 85);
            this.btnColorPicker.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnColorPicker.Name = "btnColorPicker";
            this.btnColorPicker.Size = new System.Drawing.Size(46, 30);
            this.btnColorPicker.TabIndex = 2;
            this.btnColorPicker.Text = "...";
            this.btnColorPicker.UseVisualStyleBackColor = true;
            this.btnColorPicker.Click += new System.EventHandler(this.btnColorPicker_Click);
            // 
            // txtColorRGB
            // 
            this.txtColorRGB.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtColorRGB.Location = new System.Drawing.Point(211, 87);
            this.txtColorRGB.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtColorRGB.Name = "txtColorRGB";
            this.txtColorRGB.ReadOnly = true;
            this.txtColorRGB.Size = new System.Drawing.Size(148, 28);
            this.txtColorRGB.TabIndex = 1;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.txtHex);
            this.groupBox1.Controls.Add(this.btnUnicode);
            this.groupBox1.Controls.Add(this.txtDecimal);
            this.groupBox1.Location = new System.Drawing.Point(12, 378);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.groupBox1.Size = new System.Drawing.Size(616, 125);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Hexer";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Bold);
            this.label4.Location = new System.Drawing.Point(200, 48);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(21, 23);
            this.label4.TabIndex = 9;
            this.label4.Text = "=";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Consolas", 12F, System.Drawing.FontStyle.Bold);
            this.label3.Location = new System.Drawing.Point(13, 46);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(32, 23);
            this.label3.TabIndex = 8;
            this.label3.Text = "0x";
            // 
            // txtHex
            // 
            this.txtHex.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtHex.Location = new System.Drawing.Point(46, 46);
            this.txtHex.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtHex.Name = "txtHex";
            this.txtHex.Size = new System.Drawing.Size(148, 28);
            this.txtHex.TabIndex = 6;
            // 
            // btnUnicode
            // 
            this.btnUnicode.Location = new System.Drawing.Point(381, 43);
            this.btnUnicode.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnUnicode.Name = "btnUnicode";
            this.btnUnicode.Size = new System.Drawing.Size(46, 30);
            this.btnUnicode.TabIndex = 2;
            this.btnUnicode.Text = "...";
            this.btnUnicode.UseVisualStyleBackColor = true;
            this.btnUnicode.Click += new System.EventHandler(this.btnUnicode_Click);
            // 
            // txtDecimal
            // 
            this.txtDecimal.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtDecimal.Location = new System.Drawing.Point(227, 45);
            this.txtDecimal.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtDecimal.Name = "txtDecimal";
            this.txtDecimal.Size = new System.Drawing.Size(148, 28);
            this.txtDecimal.TabIndex = 1;
            // 
            // frmMezerTools
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 506);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.gbColor);
            this.Controls.Add(this.gbClipboard);
            this.Font = new System.Drawing.Font("Tahoma", 10F);
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "frmMezerTools";
            this.Text = "MezerTools";
            this.gbClipboard.ResumeLayout(false);
            this.gbClipboard.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.udStringLength)).EndInit();
            this.gbColor.ResumeLayout(false);
            this.gbColor.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbColorPick)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gbClipboard;
        private System.Windows.Forms.NumericUpDown udStringLength;
        private System.Windows.Forms.Button btnCopyText;
        private System.Windows.Forms.TextBox txtPattern;
        private System.Windows.Forms.GroupBox gbColor;
        private System.Windows.Forms.Button btnColorPicker;
        private System.Windows.Forms.TextBox txtColorRGB;
        private System.Windows.Forms.Label lblCurrentClipboard;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lblAStringOf;
        private System.Windows.Forms.TextBox txtColorHex;
        private System.Windows.Forms.PictureBox pbColorPick;
        private System.Windows.Forms.Label lblColors;
        private System.Windows.Forms.Label lblProcessInfo;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtHex;
        private System.Windows.Forms.Button btnUnicode;
        private System.Windows.Forms.TextBox txtDecimal;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
    }
}