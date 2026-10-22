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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmTextWizard));
            this.pnlFill = new System.Windows.Forms.Panel();
            this.txtOutput = new System.Windows.Forms.TextBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.cbxOutputTo = new System.Windows.Forms.ComboBox();
            this.btnSetInputFromOutput = new System.Windows.Forms.Button();
            this.lnkEncodings = new System.Windows.Forms.LinkLabel();
            this.cbViewBytes = new System.Windows.Forms.CheckBox();
            this.cbxTransforms = new System.Windows.Forms.ComboBox();
            this.lblExplain = new System.Windows.Forms.Label();
            this.txtInput = new System.Windows.Forms.TextBox();
            this.splitInputOutput = new System.Windows.Forms.Splitter();
            lblTransform = new System.Windows.Forms.Label();
            this.pnlFill.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTransform
            // 
            lblTransform.AutoSize = true;
            lblTransform.Location = new System.Drawing.Point(3, 10);
            lblTransform.Name = "lblTransform";
            lblTransform.Size = new System.Drawing.Size(19, 13);
            lblTransform.TabIndex = 0;
            lblTransform.Text = "&To";
            // 
            // pnlFill
            // 
            this.pnlFill.Controls.Add(this.txtOutput);
            this.pnlFill.Controls.Add(this.panel2);
            this.pnlFill.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlFill.Location = new System.Drawing.Point(5, 192);
            this.pnlFill.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pnlFill.Name = "pnlFill";
            this.pnlFill.Size = new System.Drawing.Size(969, 281);
            this.pnlFill.TabIndex = 0;
            // 
            // txtOutput
            // 
            this.txtOutput.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtOutput.Font = new System.Drawing.Font("Arial", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtOutput.Location = new System.Drawing.Point(0, 39);
            this.txtOutput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtOutput.Multiline = true;
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.ReadOnly = true;
            this.txtOutput.Size = new System.Drawing.Size(969, 242);
            this.txtOutput.TabIndex = 5;
            this.txtOutput.TextChanged += new System.EventHandler(this.txtOutput_TextChanged);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.cbxOutputTo);
            this.panel2.Controls.Add(lblTransform);
            this.panel2.Controls.Add(this.btnSetInputFromOutput);
            this.panel2.Controls.Add(this.lnkEncodings);
            this.panel2.Controls.Add(this.cbViewBytes);
            this.panel2.Controls.Add(this.cbxTransforms);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(969, 39);
            this.panel2.TabIndex = 2;
            // 
            // cbxOutputTo
            // 
            this.cbxOutputTo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbxOutputTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbxOutputTo.FormattingEnabled = true;
            this.cbxOutputTo.Items.AddRange(new object[] {
            "Save output to...",
            "Clipboard",
            "Fake Exchange",
            "File..."});
            this.cbxOutputTo.Location = new System.Drawing.Point(531, 7);
            this.cbxOutputTo.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbxOutputTo.Name = "cbxOutputTo";
            this.cbxOutputTo.Size = new System.Drawing.Size(207, 21);
            this.cbxOutputTo.TabIndex = 3;
            this.cbxOutputTo.SelectedIndexChanged += new System.EventHandler(this.cbxOutputTo_SelectedIndexChanged);
            // 
            // btnSetInputFromOutput
            // 
            this.btnSetInputFromOutput.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSetInputFromOutput.Location = new System.Drawing.Point(756, 6);
            this.btnSetInputFromOutput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnSetInputFromOutput.Name = "btnSetInputFromOutput";
            this.btnSetInputFromOutput.Size = new System.Drawing.Size(210, 24);
            this.btnSetInputFromOutput.TabIndex = 4;
            this.btnSetInputFromOutput.Text = "Se&nd output to input";
            this.btnSetInputFromOutput.UseVisualStyleBackColor = true;
            this.btnSetInputFromOutput.Click += new System.EventHandler(this.btnSetInputFromOutput_Click);
            // 
            // lnkEncodings
            // 
            this.lnkEncodings.AutoSize = true;
            this.lnkEncodings.Location = new System.Drawing.Point(397, 10);
            this.lnkEncodings.Name = "lnkEncodings";
            this.lnkEncodings.Size = new System.Drawing.Size(88, 13);
            this.lnkEncodings.TabIndex = 2;
            this.lnkEncodings.TabStop = true;
            this.lnkEncodings.Text = "TODO:Encodings";
            this.lnkEncodings.Visible = false;
            // 
            // cbViewBytes
            // 
            this.cbViewBytes.AutoSize = true;
            this.cbViewBytes.Location = new System.Drawing.Point(271, 9);
            this.cbViewBytes.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbViewBytes.Name = "cbViewBytes";
            this.cbViewBytes.Size = new System.Drawing.Size(78, 17);
            this.cbViewBytes.TabIndex = 2;
            this.cbViewBytes.Text = "&View Bytes";
            this.cbViewBytes.UseVisualStyleBackColor = true;
            this.cbViewBytes.CheckedChanged += new System.EventHandler(this.cbViewBytes_CheckedChanged);
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
            "Hash(AsSHA512)",
            "CrackURL",
            "Epoch",
            "JWT Token Decoder",
            "CBOR"});
            this.cbxTransforms.Location = new System.Drawing.Point(28, 7);
            this.cbxTransforms.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cbxTransforms.Name = "cbxTransforms";
            this.cbxTransforms.Size = new System.Drawing.Size(228, 21);
            this.cbxTransforms.TabIndex = 1;
            this.cbxTransforms.SelectedIndexChanged += new System.EventHandler(this.cbxTransforms_SelectedIndexChanged);
            // 
            // lblExplain
            // 
            this.lblExplain.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.lblExplain.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblExplain.Font = new System.Drawing.Font("Segoe UI", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblExplain.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
            this.lblExplain.Location = new System.Drawing.Point(5, 5);
            this.lblExplain.Margin = new System.Windows.Forms.Padding(5);
            this.lblExplain.Name = "lblExplain";
            this.lblExplain.Size = new System.Drawing.Size(969, 24);
            this.lblExplain.TabIndex = 2;
            this.lblExplain.Text = "Textransform encodes and decodes text. Enter text and then select a transform fro" +
    "m the dropdown.";
            // 
            // txtInput
            // 
            this.txtInput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtInput.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtInput.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtInput.Location = new System.Drawing.Point(5, 29);
            this.txtInput.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtInput.Multiline = true;
            this.txtInput.Name = "txtInput";
            this.txtInput.Size = new System.Drawing.Size(969, 157);
            this.txtInput.TabIndex = 0;
            this.txtInput.TextChanged += new System.EventHandler(this.txtInput_TextChanged);
            // 
            // splitInputOutput
            // 
            this.splitInputOutput.BackColor = System.Drawing.Color.LightSteelBlue;
            this.splitInputOutput.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitInputOutput.Location = new System.Drawing.Point(5, 186);
            this.splitInputOutput.Name = "splitInputOutput";
            this.splitInputOutput.Size = new System.Drawing.Size(969, 6);
            this.splitInputOutput.TabIndex = 9;
            this.splitInputOutput.TabStop = false;
            // 
            // frmTextWizard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(979, 478);
            this.Controls.Add(this.pnlFill);
            this.Controls.Add(this.splitInputOutput);
            this.Controls.Add(this.txtInput);
            this.Controls.Add(this.lblExplain);
            this.Font = new System.Drawing.Font("Tahoma", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimumSize = new System.Drawing.Size(600, 400);
            this.Name = "frmTextWizard";
            this.Padding = new System.Windows.Forms.Padding(5);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Textransform";
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.frmTextWizard_KeyUp);
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
        private System.Windows.Forms.Button btnSetInputFromOutput;
        private System.Windows.Forms.LinkLabel lnkEncodings;
        private System.Windows.Forms.CheckBox cbViewBytes;
        private System.Windows.Forms.ComboBox cbxTransforms;
        private System.Windows.Forms.TextBox txtInput;
        private System.Windows.Forms.ComboBox cbxOutputTo;
        private System.Windows.Forms.Splitter splitInputOutput;
    }
}