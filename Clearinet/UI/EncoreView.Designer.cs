namespace Clearinet.UI
{
    partial class EncoreView
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
            this.gbRuleEditor = new System.Windows.Forms.GroupBox();
            this.cbEnableEncore = new System.Windows.Forms.CheckBox();
            this.cbAcceptAllConnects = new System.Windows.Forms.CheckBox();
            this.cbUnmatchedContinue = new System.Windows.Forms.CheckBox();
            this.cbEnableLatency = new System.Windows.Forms.CheckBox();
            this.lvRules = new System.Windows.Forms.ListView();
            this.cbMatchOnce = new System.Windows.Forms.CheckBox();
            this.btnSaveRule = new System.Windows.Forms.Button();
            this.lnkTest = new System.Windows.Forms.LinkLabel();
            this.cbxMatchCondition = new System.Windows.Forms.ComboBox();
            this.cbxResponseAction = new System.Windows.Forms.ComboBox();
            this.btnAddRule = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.btnGroup = new System.Windows.Forms.Button();
            this.gbRuleEditor.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbRuleEditor
            // 
            this.gbRuleEditor.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbRuleEditor.Controls.Add(this.cbxResponseAction);
            this.gbRuleEditor.Controls.Add(this.cbxMatchCondition);
            this.gbRuleEditor.Controls.Add(this.lnkTest);
            this.gbRuleEditor.Controls.Add(this.btnSaveRule);
            this.gbRuleEditor.Controls.Add(this.cbMatchOnce);
            this.gbRuleEditor.Location = new System.Drawing.Point(4, 67);
            this.gbRuleEditor.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.gbRuleEditor.Name = "gbRuleEditor";
            this.gbRuleEditor.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.gbRuleEditor.Size = new System.Drawing.Size(783, 94);
            this.gbRuleEditor.TabIndex = 0;
            this.gbRuleEditor.TabStop = false;
            this.gbRuleEditor.Text = "Rule Editor";
            // 
            // cbEnableEncore
            // 
            this.cbEnableEncore.AutoSize = true;
            this.cbEnableEncore.Location = new System.Drawing.Point(4, 4);
            this.cbEnableEncore.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cbEnableEncore.Name = "cbEnableEncore";
            this.cbEnableEncore.Size = new System.Drawing.Size(99, 21);
            this.cbEnableEncore.TabIndex = 1;
            this.cbEnableEncore.Text = "Enable rules";
            this.cbEnableEncore.UseVisualStyleBackColor = true;
            // 
            // cbAcceptAllConnects
            // 
            this.cbAcceptAllConnects.AutoSize = true;
            this.cbAcceptAllConnects.Location = new System.Drawing.Point(121, 4);
            this.cbAcceptAllConnects.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cbAcceptAllConnects.Name = "cbAcceptAllConnects";
            this.cbAcceptAllConnects.Size = new System.Drawing.Size(156, 21);
            this.cbAcceptAllConnects.TabIndex = 2;
            this.cbAcceptAllConnects.Text = "Accept all CONNECTs";
            this.cbAcceptAllConnects.UseVisualStyleBackColor = true;
            // 
            // cbUnmatchedContinue
            // 
            this.cbUnmatchedContinue.AutoSize = true;
            this.cbUnmatchedContinue.Location = new System.Drawing.Point(284, 4);
            this.cbUnmatchedContinue.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cbUnmatchedContinue.Name = "cbUnmatchedContinue";
            this.cbUnmatchedContinue.Size = new System.Drawing.Size(155, 21);
            this.cbUnmatchedContinue.TabIndex = 3;
            this.cbUnmatchedContinue.Text = "Unmatched continue";
            this.cbUnmatchedContinue.UseVisualStyleBackColor = true;
            // 
            // cbEnableLatency
            // 
            this.cbEnableLatency.AutoSize = true;
            this.cbEnableLatency.Location = new System.Drawing.Point(460, 4);
            this.cbEnableLatency.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.cbEnableLatency.Name = "cbEnableLatency";
            this.cbEnableLatency.Size = new System.Drawing.Size(109, 21);
            this.cbEnableLatency.TabIndex = 4;
            this.cbEnableLatency.Text = "Apply latency";
            this.cbEnableLatency.UseVisualStyleBackColor = true;
            // 
            // lvRules
            // 
            this.lvRules.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvRules.HideSelection = false;
            this.lvRules.Location = new System.Drawing.Point(4, 169);
            this.lvRules.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.lvRules.Name = "lvRules";
            this.lvRules.Size = new System.Drawing.Size(783, 387);
            this.lvRules.TabIndex = 5;
            this.lvRules.UseCompatibleStateImageBehavior = false;
            // 
            // cbMatchOnce
            // 
            this.cbMatchOnce.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbMatchOnce.AutoSize = true;
            this.cbMatchOnce.Location = new System.Drawing.Point(613, 59);
            this.cbMatchOnce.Name = "cbMatchOnce";
            this.cbMatchOnce.Size = new System.Drawing.Size(144, 21);
            this.cbMatchOnce.TabIndex = 1;
            this.cbMatchOnce.Text = "Disable after match";
            this.cbMatchOnce.UseVisualStyleBackColor = true;
            // 
            // btnSaveRule
            // 
            this.btnSaveRule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveRule.Location = new System.Drawing.Point(701, 22);
            this.btnSaveRule.Name = "btnSaveRule";
            this.btnSaveRule.Size = new System.Drawing.Size(75, 33);
            this.btnSaveRule.TabIndex = 2;
            this.btnSaveRule.Text = "Save";
            this.btnSaveRule.UseVisualStyleBackColor = true;
            // 
            // lnkTest
            // 
            this.lnkTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lnkTest.AutoSize = true;
            this.lnkTest.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.lnkTest.Location = new System.Drawing.Point(610, 30);
            this.lnkTest.Name = "lnkTest";
            this.lnkTest.Size = new System.Drawing.Size(46, 17);
            this.lnkTest.TabIndex = 3;
            this.lnkTest.TabStop = true;
            this.lnkTest.Text = "Test...";
            // 
            // cbxMatchCondition
            // 
            this.cbxMatchCondition.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cbxMatchCondition.FormattingEnabled = true;
            this.cbxMatchCondition.Location = new System.Drawing.Point(7, 27);
            this.cbxMatchCondition.Name = "cbxMatchCondition";
            this.cbxMatchCondition.Size = new System.Drawing.Size(586, 24);
            this.cbxMatchCondition.TabIndex = 4;
            // 
            // cbxResponseAction
            // 
            this.cbxResponseAction.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cbxResponseAction.FormattingEnabled = true;
            this.cbxResponseAction.Location = new System.Drawing.Point(7, 57);
            this.cbxResponseAction.Name = "cbxResponseAction";
            this.cbxResponseAction.Size = new System.Drawing.Size(586, 24);
            this.cbxResponseAction.TabIndex = 5;
            // 
            // btnAddRule
            // 
            this.btnAddRule.Location = new System.Drawing.Point(3, 32);
            this.btnAddRule.Name = "btnAddRule";
            this.btnAddRule.Size = new System.Drawing.Size(130, 28);
            this.btnAddRule.TabIndex = 6;
            this.btnAddRule.Text = "Add Rule";
            this.btnAddRule.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(139, 32);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(130, 28);
            this.button2.TabIndex = 7;
            this.button2.Text = "Import...";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // btnGroup
            // 
            this.btnGroup.Location = new System.Drawing.Point(275, 32);
            this.btnGroup.Name = "btnGroup";
            this.btnGroup.Size = new System.Drawing.Size(130, 28);
            this.btnGroup.TabIndex = 8;
            this.btnGroup.Text = "Group";
            this.btnGroup.UseVisualStyleBackColor = true;
            // 
            // EncoreView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Controls.Add(this.btnGroup);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.btnAddRule);
            this.Controls.Add(this.lvRules);
            this.Controls.Add(this.cbEnableLatency);
            this.Controls.Add(this.cbUnmatchedContinue);
            this.Controls.Add(this.cbAcceptAllConnects);
            this.Controls.Add(this.cbEnableEncore);
            this.Controls.Add(this.gbRuleEditor);
            this.Font = new System.Drawing.Font("Tahoma", 10F);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "EncoreView";
            this.Size = new System.Drawing.Size(791, 561);
            this.gbRuleEditor.ResumeLayout(false);
            this.gbRuleEditor.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox gbRuleEditor;
        private System.Windows.Forms.CheckBox cbMatchOnce;
        private System.Windows.Forms.CheckBox cbEnableEncore;
        private System.Windows.Forms.CheckBox cbAcceptAllConnects;
        private System.Windows.Forms.CheckBox cbUnmatchedContinue;
        private System.Windows.Forms.CheckBox cbEnableLatency;
        private System.Windows.Forms.ListView lvRules;
        private System.Windows.Forms.ComboBox cbxResponseAction;
        private System.Windows.Forms.ComboBox cbxMatchCondition;
        private System.Windows.Forms.LinkLabel lnkTest;
        private System.Windows.Forms.Button btnSaveRule;
        private System.Windows.Forms.Button btnAddRule;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button btnGroup;
    }
}
