namespace Clearinet.UI
{
    partial class frmInspect
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
            this.tabsInspect = new System.Windows.Forms.TabControl();
            this.pageProperties = new System.Windows.Forms.TabPage();
            this.rtbProperties = new System.Windows.Forms.RichTextBox();
            this.pageRequest = new System.Windows.Forms.TabPage();
            this.tabsInspectRequest = new System.Windows.Forms.TabControl();
            this.pageResponse = new System.Windows.Forms.TabPage();
            this.tabsInspectResponse = new System.Windows.Forms.TabControl();
            this.tabsInspect.SuspendLayout();
            this.pageProperties.SuspendLayout();
            this.pageRequest.SuspendLayout();
            this.pageResponse.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabsInspect
            // 
            this.tabsInspect.Appearance = System.Windows.Forms.TabAppearance.FlatButtons;
            this.tabsInspect.Controls.Add(this.pageProperties);
            this.tabsInspect.Controls.Add(this.pageRequest);
            this.tabsInspect.Controls.Add(this.pageResponse);
            this.tabsInspect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsInspect.Location = new System.Drawing.Point(0, 0);
            this.tabsInspect.Margin = new System.Windows.Forms.Padding(4);
            this.tabsInspect.Name = "tabsInspect";
            this.tabsInspect.SelectedIndex = 0;
            this.tabsInspect.Size = new System.Drawing.Size(978, 540);
            this.tabsInspect.TabIndex = 0;
            this.tabsInspect.SelectedIndexChanged += new System.EventHandler(this.tabsInspect_SelectedIndexChanged);
            // 
            // pageProperties
            // 
            this.pageProperties.Controls.Add(this.rtbProperties);
            this.pageProperties.Location = new System.Drawing.Point(4, 36);
            this.pageProperties.Name = "pageProperties";
            this.pageProperties.Size = new System.Drawing.Size(970, 500);
            this.pageProperties.TabIndex = 2;
            this.pageProperties.Text = "Properties";
            this.pageProperties.UseVisualStyleBackColor = true;
            // 
            // rtbProperties
            // 
            this.rtbProperties.BackColor = System.Drawing.SystemColors.ControlDark;
            this.rtbProperties.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbProperties.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbProperties.Font = new System.Drawing.Font("Consolas", 10F);
            this.rtbProperties.Location = new System.Drawing.Point(0, 0);
            this.rtbProperties.Name = "rtbProperties";
            this.rtbProperties.ReadOnly = true;
            this.rtbProperties.Size = new System.Drawing.Size(970, 500);
            this.rtbProperties.TabIndex = 0;
            this.rtbProperties.Text = "";
            // 
            // pageRequest
            // 
            this.pageRequest.Controls.Add(this.tabsInspectRequest);
            this.pageRequest.Location = new System.Drawing.Point(4, 36);
            this.pageRequest.Margin = new System.Windows.Forms.Padding(4);
            this.pageRequest.Name = "pageRequest";
            this.pageRequest.Padding = new System.Windows.Forms.Padding(4);
            this.pageRequest.Size = new System.Drawing.Size(970, 500);
            this.pageRequest.TabIndex = 0;
            this.pageRequest.Text = "Request";
            this.pageRequest.UseVisualStyleBackColor = true;
            // 
            // tabsInspectRequest
            // 
            this.tabsInspectRequest.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsInspectRequest.Location = new System.Drawing.Point(4, 4);
            this.tabsInspectRequest.Name = "tabsInspectRequest";
            this.tabsInspectRequest.SelectedIndex = 0;
            this.tabsInspectRequest.Size = new System.Drawing.Size(962, 492);
            this.tabsInspectRequest.TabIndex = 0;
            this.tabsInspectRequest.SelectedIndexChanged += new System.EventHandler(this.tabsInspectRequest_SelectedIndexChanged);
            // 
            // pageResponse
            // 
            this.pageResponse.Controls.Add(this.tabsInspectResponse);
            this.pageResponse.Location = new System.Drawing.Point(4, 36);
            this.pageResponse.Margin = new System.Windows.Forms.Padding(4);
            this.pageResponse.Name = "pageResponse";
            this.pageResponse.Padding = new System.Windows.Forms.Padding(4);
            this.pageResponse.Size = new System.Drawing.Size(970, 500);
            this.pageResponse.TabIndex = 1;
            this.pageResponse.Text = "Response";
            this.pageResponse.UseVisualStyleBackColor = true;
            // 
            // tabsInspectResponse
            // 
            this.tabsInspectResponse.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsInspectResponse.Location = new System.Drawing.Point(4, 4);
            this.tabsInspectResponse.Name = "tabsInspectResponse";
            this.tabsInspectResponse.SelectedIndex = 0;
            this.tabsInspectResponse.Size = new System.Drawing.Size(962, 492);
            this.tabsInspectResponse.TabIndex = 1;
            this.tabsInspectResponse.SelectedIndexChanged += new System.EventHandler(this.tabsInspectResponse_SelectedIndexChanged);
            // 
            // frmInspect
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(978, 540);
            this.Controls.Add(this.tabsInspect);
            this.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "frmInspect";
            this.Text = "Inspecting Exchange #";
            this.tabsInspect.ResumeLayout(false);
            this.pageProperties.ResumeLayout(false);
            this.pageRequest.ResumeLayout(false);
            this.pageResponse.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabsInspect;
        private System.Windows.Forms.TabPage pageRequest;
        private System.Windows.Forms.TabPage pageResponse;
        private System.Windows.Forms.TabPage pageProperties;
        private System.Windows.Forms.RichTextBox rtbProperties;
        private System.Windows.Forms.TabControl tabsInspectRequest;
        private System.Windows.Forms.TabControl tabsInspectResponse;
    }
}