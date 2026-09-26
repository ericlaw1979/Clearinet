namespace Clearinet
{
    partial class frmTextWizard
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
            System.Windows.Forms.Label lblTransform;
            System.Windows.Forms.Label lblSave;
            this.pnlFill = new System.Windows.Forms.Panel();
            this.lblExplain = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.button1 = new System.Windows.Forms.Button();
            this.lnkEncodings = new System.Windows.Forms.LinkLabel();
            this.cbViewBytes = new System.Windows.Forms.CheckBox();
            this.cbxTransforms = new System.Windows.Forms.ComboBox();
            this.txtOutput = new System.Windows.Forms.TextBox();
            this.txtInput = new System.Windows.Forms.TextBox();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            lblTransform = new System.Windows.Forms.Label();
            lblSave = new System.Windows.Forms.Label();
            this.pnlFill.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlFill
            // 
            this.pnlFill.Controls.Add(this.txtOutput);
            this.pnlFill.Controls.Add(this.panel2);
            this.pnlFill.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlFill.Location = new System.Drawing.Point(5, 186);
            this.pnlFill.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pnlFill.Name = "pnlFill";
            this.pnlFill.Size = new System.Drawing.Size(969, 287);
            this.pnlFill.TabIndex = 0;
            // 
            // lblExplain
            // 
            this.lblExplain.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblExplain.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblExplain.Font = new System.Drawing.Font("Segoe UI", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblExplain.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
            this.lblExplain.Location = new System.Drawing.Point(5, 5);
            this.lblExplain.Margin = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.lblExplain.Name = "lblExplain";
            this.lblExplain.Size = new System.Drawing.Size(969, 24);
            this.lblExplain.TabIndex = 2;
            this.lblExplain.Text = "TextWizard encodes and decodes text. Enter text and then select a transform from " +
    "the dropdown.";
            // 
            // panel2
            // 
            this.panel2.Controls.Add(lblSave);
            this.panel2.Controls.Add(this.linkLabel1);
            this.panel2.Controls.Add(lblTransform);
            this.panel2.Controls.Add(this.button1);
            this.panel2.Controls.Add(this.lnkEncodings);
            this.panel2.Controls.Add(this.cbViewBytes);
            this.panel2.Controls.Add(this.cbxTransforms);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(969, 47);
            this.panel2.TabIndex = 2;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(778, 7);
            this.button1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(188, 24);
            this.button1.TabIndex = 3;
            this.button1.Text = "Se&nd output to input";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // lnkEncodings
            // 
            this.lnkEncodings.AutoSize = true;
            this.lnkEncodings.Location = new System.Drawing.Point(457, 7);
            this.lnkEncodings.Name = "lnkEncodings";
            this.lnkEncodings.Size = new System.Drawing.Size(71, 17);
            this.lnkEncodings.TabIndex = 2;
            this.lnkEncodings.TabStop = true;
            this.lnkEncodings.Text = "Encodings";
            // 
            // cbViewBytes
            // 
            this.cbViewBytes.AutoSize = true;
            this.cbViewBytes.Location = new System.Drawing.Point(354, 6);
            this.cbViewBytes.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbViewBytes.Name = "cbViewBytes";
            this.cbViewBytes.Size = new System.Drawing.Size(95, 21);
            this.cbViewBytes.TabIndex = 1;
            this.cbViewBytes.Text = "&View Bytes";
            this.cbViewBytes.UseVisualStyleBackColor = true;
            // 
            // cbxTransforms
            // 
            this.cbxTransforms.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxTransforms.FormattingEnabled = true;
            this.cbxTransforms.Items.AddRange(new object[] {
            "To Base64",
            "To Base64-url",
            "From Base64",
            "URLEncode",
            "URLDecode",
            "HexEncode",
            "HexDecode",
            "To C# byte[]",
            "To JS string",
            "From JS string",
            "HTMLEncode",
            "HTMLDecode",
            "To UTF-7",
            "From UTF-7",
            "To DeflatedSAML",
            "From DeflatedSAML",
            "Hash(AsMD5)",
            "Hash(AsSHA1)",
            "Hash(AsSHA256)",
            "Hash(AsSHA384)",
            "Hash(AsSHA512)"});
            this.cbxTransforms.Location = new System.Drawing.Point(79, 4);
            this.cbxTransforms.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbxTransforms.Name = "cbxTransforms";
            this.cbxTransforms.Size = new System.Drawing.Size(269, 25);
            this.cbxTransforms.TabIndex = 0;
            // 
            // txtOutput
            // 
            this.txtOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOutput.Location = new System.Drawing.Point(0, 47);
            this.txtOutput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtOutput.Multiline = true;
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.Size = new System.Drawing.Size(969, 240);
            this.txtOutput.TabIndex = 3;
            // 
            // txtInput
            // 
            this.txtInput.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtInput.Location = new System.Drawing.Point(5, 29);
            this.txtInput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtInput.Multiline = true;
            this.txtInput.Name = "txtInput";
            this.txtInput.Size = new System.Drawing.Size(969, 157);
            this.txtInput.TabIndex = 6;
            this.txtInput.TextChanged += new System.EventHandler(this.txtInput_TextChanged);
            // 
            // lblTransform
            // 
            lblTransform.AutoSize = true;
            lblTransform.Location = new System.Drawing.Point(3, 10);
            lblTransform.Name = "lblTransform";
            lblTransform.Size = new System.Drawing.Size(71, 17);
            lblTransform.TabIndex = 0;
            lblTransform.Text = "&Transform";
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Location = new System.Drawing.Point(657, 10);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(86, 17);
            this.linkLabel1.TabIndex = 4;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "as Exchange";
            // 
            // lblSave
            // 
            lblSave.AutoSize = true;
            lblSave.Location = new System.Drawing.Point(562, 10);
            lblSave.Name = "lblSave";
            lblSave.Size = new System.Drawing.Size(91, 17);
            lblSave.TabIndex = 5;
            lblSave.Text = "Save Output:";
            // 
            // frmTextWizard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(979, 478);
            this.Controls.Add(this.pnlFill);
            this.Controls.Add(this.txtInput);
            this.Controls.Add(this.lblExplain);
            this.Font = new System.Drawing.Font("Tahoma", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "frmTextWizard";
            this.Padding = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.Text = "TextWizard";
            this.pnlFill.ResumeLayout(false);
            this.pnlFill.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel pnlFill;
        private System.Windows.Forms.Label lblExplain;
        private System.Windows.Forms.TextBox txtOutput;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.LinkLabel lnkEncodings;
        private System.Windows.Forms.CheckBox cbViewBytes;
        private System.Windows.Forms.ComboBox cbxTransforms;
        private System.Windows.Forms.TextBox txtInput;
        private System.Windows.Forms.LinkLabel linkLabel1;
    }
}