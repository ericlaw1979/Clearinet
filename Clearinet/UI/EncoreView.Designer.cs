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
            this.cbxResponseAction = new System.Windows.Forms.ComboBox();
            this.cbxMatchCondition = new System.Windows.Forms.ComboBox();
            this.lnkTest = new System.Windows.Forms.LinkLabel();
            this.btnSaveRule = new System.Windows.Forms.Button();
            this.cbMatchOnce = new System.Windows.Forms.CheckBox();
            this.cbEnableEncore = new System.Windows.Forms.CheckBox();
            this.cbAcceptAllConnects = new System.Windows.Forms.CheckBox();
            this.cbUnmatchedContinue = new System.Windows.Forms.CheckBox();
            this.cbEnableLatency = new System.Windows.Forms.CheckBox();
            this.btnAddRule = new System.Windows.Forms.Button();
            this.btnImportForPlayback = new System.Windows.Forms.Button();
            this.btnGroup = new System.Windows.Forms.Button();
            this.lvRules = new Clearinet.BetterListView();
            this.colCondition = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colAction = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colLatency = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colComments = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
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
            this.gbRuleEditor.Enabled = false;
            this.gbRuleEditor.Location = new System.Drawing.Point(4, 67);
            this.gbRuleEditor.Margin = new System.Windows.Forms.Padding(4);
            this.gbRuleEditor.Name = "gbRuleEditor";
            this.gbRuleEditor.Padding = new System.Windows.Forms.Padding(4);
            this.gbRuleEditor.Size = new System.Drawing.Size(783, 94);
            this.gbRuleEditor.TabIndex = 7;
            this.gbRuleEditor.TabStop = false;
            this.gbRuleEditor.Text = "Rule Editor";
            // 
            // cbxResponseAction
            // 
            this.cbxResponseAction.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cbxResponseAction.FormattingEnabled = true;
            this.cbxResponseAction.Location = new System.Drawing.Point(7, 57);
            this.cbxResponseAction.Name = "cbxResponseAction";
            this.cbxResponseAction.Size = new System.Drawing.Size(586, 24);
            this.cbxResponseAction.TabIndex = 2;
            // 
            // cbxMatchCondition
            // 
            this.cbxMatchCondition.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cbxMatchCondition.FormattingEnabled = true;
            this.cbxMatchCondition.Location = new System.Drawing.Point(7, 27);
            this.cbxMatchCondition.Name = "cbxMatchCondition";
            this.cbxMatchCondition.Size = new System.Drawing.Size(586, 24);
            this.cbxMatchCondition.TabIndex = 0;
            // 
            // lnkTest
            // 
            this.lnkTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lnkTest.AutoSize = true;
            this.lnkTest.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
            this.lnkTest.Location = new System.Drawing.Point(610, 30);
            this.lnkTest.Name = "lnkTest";
            this.lnkTest.Size = new System.Drawing.Size(46, 17);
            this.lnkTest.TabIndex = 1;
            this.lnkTest.TabStop = true;
            this.lnkTest.Text = "Test...";
            // 
            // btnSaveRule
            // 
            this.btnSaveRule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveRule.Location = new System.Drawing.Point(701, 22);
            this.btnSaveRule.Name = "btnSaveRule";
            this.btnSaveRule.Size = new System.Drawing.Size(75, 33);
            this.btnSaveRule.TabIndex = 4;
            this.btnSaveRule.Text = "Save";
            this.btnSaveRule.UseVisualStyleBackColor = true;
            // 
            // cbMatchOnce
            // 
            this.cbMatchOnce.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cbMatchOnce.AutoSize = true;
            this.cbMatchOnce.Location = new System.Drawing.Point(613, 59);
            this.cbMatchOnce.Name = "cbMatchOnce";
            this.cbMatchOnce.Size = new System.Drawing.Size(144, 21);
            this.cbMatchOnce.TabIndex = 3;
            this.cbMatchOnce.Text = "Disable after match";
            this.cbMatchOnce.UseVisualStyleBackColor = true;
            // 
            // cbEnableEncore
            // 
            this.cbEnableEncore.AutoSize = true;
            this.cbEnableEncore.Location = new System.Drawing.Point(4, 4);
            this.cbEnableEncore.Margin = new System.Windows.Forms.Padding(4);
            this.cbEnableEncore.Name = "cbEnableEncore";
            this.cbEnableEncore.Size = new System.Drawing.Size(99, 21);
            this.cbEnableEncore.TabIndex = 0;
            this.cbEnableEncore.Text = "Enable rules";
            this.cbEnableEncore.UseVisualStyleBackColor = true;
            this.cbEnableEncore.CheckedChanged += new System.EventHandler(this.cbEnableEncore_CheckedChanged);
            // 
            // cbAcceptAllConnects
            // 
            this.cbAcceptAllConnects.AutoSize = true;
            this.cbAcceptAllConnects.Location = new System.Drawing.Point(121, 4);
            this.cbAcceptAllConnects.Margin = new System.Windows.Forms.Padding(4);
            this.cbAcceptAllConnects.Name = "cbAcceptAllConnects";
            this.cbAcceptAllConnects.Size = new System.Drawing.Size(156, 21);
            this.cbAcceptAllConnects.TabIndex = 1;
            this.cbAcceptAllConnects.Text = "Accept all CONNECTs";
            this.cbAcceptAllConnects.UseVisualStyleBackColor = true;
            // 
            // cbUnmatchedContinue
            // 
            this.cbUnmatchedContinue.AutoSize = true;
            this.cbUnmatchedContinue.Location = new System.Drawing.Point(284, 4);
            this.cbUnmatchedContinue.Margin = new System.Windows.Forms.Padding(4);
            this.cbUnmatchedContinue.Name = "cbUnmatchedContinue";
            this.cbUnmatchedContinue.Size = new System.Drawing.Size(155, 21);
            this.cbUnmatchedContinue.TabIndex = 2;
            this.cbUnmatchedContinue.Text = "Unmatched continue";
            this.cbUnmatchedContinue.UseVisualStyleBackColor = true;
            // 
            // cbEnableLatency
            // 
            this.cbEnableLatency.AutoSize = true;
            this.cbEnableLatency.Location = new System.Drawing.Point(460, 4);
            this.cbEnableLatency.Margin = new System.Windows.Forms.Padding(4);
            this.cbEnableLatency.Name = "cbEnableLatency";
            this.cbEnableLatency.Size = new System.Drawing.Size(109, 21);
            this.cbEnableLatency.TabIndex = 3;
            this.cbEnableLatency.Text = "Apply latency";
            this.cbEnableLatency.UseVisualStyleBackColor = true;
            this.cbEnableLatency.CheckedChanged += new System.EventHandler(this.cbEnableLatency_CheckedChanged);
            // 
            // btnAddRule
            // 
            this.btnAddRule.Location = new System.Drawing.Point(3, 32);
            this.btnAddRule.Name = "btnAddRule";
            this.btnAddRule.Size = new System.Drawing.Size(130, 28);
            this.btnAddRule.TabIndex = 4;
            this.btnAddRule.Text = "Add Rule";
            this.btnAddRule.UseVisualStyleBackColor = true;
            this.btnAddRule.Click += new System.EventHandler(this.btnAddRule_Click);
            // 
            // btnImportForPlayback
            // 
            this.btnImportForPlayback.Location = new System.Drawing.Point(139, 32);
            this.btnImportForPlayback.Name = "btnImportForPlayback";
            this.btnImportForPlayback.Size = new System.Drawing.Size(130, 28);
            this.btnImportForPlayback.TabIndex = 5;
            this.btnImportForPlayback.Text = "Import...";
            this.btnImportForPlayback.UseVisualStyleBackColor = true;
            this.btnImportForPlayback.Click += new System.EventHandler(this.btnImportForPlayback_Click);
            // 
            // btnGroup
            // 
            this.btnGroup.Location = new System.Drawing.Point(275, 32);
            this.btnGroup.Name = "btnGroup";
            this.btnGroup.Size = new System.Drawing.Size(130, 28);
            this.btnGroup.TabIndex = 6;
            this.btnGroup.Text = "Group";
            this.btnGroup.UseVisualStyleBackColor = true;
            this.btnGroup.Visible = false;
            this.btnGroup.Click += new System.EventHandler(this.btnGroup_Click);
            // 
            // lvRules
            // 
            this.lvRules.AllowDrop = true;
            this.lvRules.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvRules.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colCondition,
            this.colAction,
            this.colLatency,
            this.colComments});
            this.lvRules.EmptyText = null;
            this.lvRules.FullRowSelect = true;
            this.lvRules.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lvRules.HideSelection = false;
            this.lvRules.Location = new System.Drawing.Point(4, 169);
            this.lvRules.Margin = new System.Windows.Forms.Padding(4);
            this.lvRules.Name = "lvRules";
            this.lvRules.Size = new System.Drawing.Size(783, 387);
            this.lvRules.TabIndex = 8;
            this.lvRules.UseCompatibleStateImageBehavior = false;
            this.lvRules.View = System.Windows.Forms.View.Details;
            this.lvRules.SelectedIndexChanged += new System.EventHandler(this.lvRules_SelectedIndexChanged);
            this.lvRules.DragDrop += new System.Windows.Forms.DragEventHandler(this.lvRules_DragDrop);
            this.lvRules.DragEnter += new System.Windows.Forms.DragEventHandler(this.lvRules_DragEnter);
            this.lvRules.KeyDown += new System.Windows.Forms.KeyEventHandler(this.lvRules_KeyDown);
            this.lvRules.CheckBoxes = true;
            // 
            // colCondition
            // 
            this.colCondition.Text = "If request matches...";
            this.colCondition.Width = 200;
            // 
            // colAction
            // 
            this.colAction.Text = "then respond with...";
            this.colAction.Width = 200;
            // 
            // colLatency
            // 
            this.colLatency.Text = "Latency (ms)";
            this.colLatency.Width = 0;
            // 
            // colComments
            // 
            this.colComments.Text = "Comments";
            this.colComments.Width = 100;
            // 
            // EncoreView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Controls.Add(this.btnGroup);
            this.Controls.Add(this.btnImportForPlayback);
            this.Controls.Add(this.btnAddRule);
            this.Controls.Add(this.lvRules);
            this.Controls.Add(this.cbEnableLatency);
            this.Controls.Add(this.cbUnmatchedContinue);
            this.Controls.Add(this.cbAcceptAllConnects);
            this.Controls.Add(this.cbEnableEncore);
            this.Controls.Add(this.gbRuleEditor);
            this.Font = new System.Drawing.Font("Tahoma", 10F);
            this.Margin = new System.Windows.Forms.Padding(4);
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
        private Clearinet.BetterListView lvRules;
        private System.Windows.Forms.ComboBox cbxResponseAction;
        private System.Windows.Forms.ComboBox cbxMatchCondition;
        private System.Windows.Forms.LinkLabel lnkTest;
        private System.Windows.Forms.Button btnSaveRule;
        private System.Windows.Forms.Button btnAddRule;
        private System.Windows.Forms.Button btnImportForPlayback;
        private System.Windows.Forms.Button btnGroup;
        private System.Windows.Forms.ColumnHeader colCondition;
        private System.Windows.Forms.ColumnHeader colAction;
        private System.Windows.Forms.ColumnHeader colLatency;
        private System.Windows.Forms.ColumnHeader colComments;
    }
}
