using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace Be.Windows.Forms
{
    /// <summary>
    /// Defines a build-in ContextMenuStrip manager for HexBox control to show Copy, Cut, Paste menu in contextmenu of the control.
    /// </summary>
    [TypeConverterAttribute(typeof(ExpandableObjectConverter))]
    public sealed class BuiltInContextMenu : Component
    {
        /// <summary>
        /// Contains the HexBox control.
        /// </summary>
        HttpHexBox _hexBox;
        /// <summary>
        /// Contains the ContextMenuStrip control.
        /// </summary>
        ContextMenuStrip _contextMenuStrip;
        /// <summary>
        /// Contains the "Cut"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _cutToolStripMenuItem;
        /// <summary>
        /// Contains the "Copy"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _copyToolStripMenuItem;
        /// <summary>
        /// Contains the "Paste"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _pasteToolStripMenuItem;
        /// <summary>
        /// Contains the "Select All"-ToolStripMenuItem object.
        /// </summary>
        ToolStripMenuItem _selectAllToolStripMenuItem;
        /// <summary>
        /// Initializes a new instance of BuildInContextMenu class.
        /// </summary>
        /// <param name="hexBox">the HexBox control</param>
        public BuiltInContextMenu(HttpHexBox hexBox)
        {
            _hexBox = hexBox;
            _hexBox.ByteProviderChanged += new EventHandler(HexBox_ByteProviderChanged);
        }

        /// <summary>
        /// If ByteProvider
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void HexBox_ByteProviderChanged(object sender, EventArgs e)
        {
            CheckBuiltInContextMenu();
        }
        /// <summary>
        /// Assigns the ContextMenuStrip control to the HexBox control.
        /// </summary>
        void CheckBuiltInContextMenu()
        {
            if (this.DesignMode)
                return;

            if (this._contextMenuStrip == null)
            {
                _contextMenuStrip = new ContextMenuStrip();

                _cutToolStripMenuItem = AddMenuItem(
                    CutMenuItemText, CutMenuItemImage, 
                    CutMenuItem_Click);
                _copyToolStripMenuItem = AddMenuItem(
                    CopyMenuItemTextInternal, CopyMenuItemImage
                    ,CopyMenuItem_Click);
                _pasteToolStripMenuItem = AddMenuItem(
                    PasteMenuItemTextInternal, PasteMenuItemImage
                    , PasteMenuItem_Click);

                _contextMenuStrip.Items.Add(new ToolStripSeparator());

                _selectAllToolStripMenuItem = AddMenuItem(
                    SelectAllMenuItemTextInternal, SelectAllMenuItemImage
                    , SelectAllMenuItem_Click);

                _contextMenuStrip.Opening += new CancelEventHandler(BuildInContextMenuStrip_Opening);
            }

            if (this._hexBox.ByteProvider == null && this._hexBox.ContextMenuStrip == this._contextMenuStrip)
                this._hexBox.ContextMenuStrip = null;
            else if (this._hexBox.ByteProvider != null && this._hexBox.ContextMenuStrip == null)
                this._hexBox.ContextMenuStrip = _contextMenuStrip;
        }

        ToolStripMenuItem AddMenuItem(string text, Image image, EventHandler onClick)
        {
            var item = new ToolStripMenuItem();
            item.Text = text;
            item.Image = image;
            item.Click += onClick;
            _contextMenuStrip.Items.Add(item);
            return item;
        }
        /// <summary>
        /// Before opening the ContextMenuStrip, we manage the availability of the items.
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void BuildInContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            _cutToolStripMenuItem.Enabled = this._hexBox.CanCut();
            _copyToolStripMenuItem.Enabled = this._hexBox.CanCopy();
            _pasteToolStripMenuItem.Enabled = this._hexBox.CanPaste();
            _selectAllToolStripMenuItem.Enabled = this._hexBox.CanSelectAll();
        }
        /// <summary>
        /// The handler for the "Cut"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void CutMenuItem_Click(object sender, EventArgs e) { this._hexBox.Cut(); }
        /// <summary>
        /// The handler for the "Copy"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void CopyMenuItem_Click(object sender, EventArgs e) { this._hexBox.Copy(); }
        /// <summary>
        /// The handler for the "Paste"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void PasteMenuItem_Click(object sender, EventArgs e) { this._hexBox.Paste(); }
        /// <summary>
        /// The handler for the "Select All"-Click event
        /// </summary>
        /// <param name="sender">the sender object</param>
        /// <param name="e">the event data</param>
        void SelectAllMenuItem_Click(object sender, EventArgs e) { this._hexBox.SelectAll(); }
        /// <summary>
        /// Gets or sets the custom text of the "Copy" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string CopyMenuItemText { get; set; }

        /// <summary>
        /// Gets or sets the custom text of the "Cut" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string CutMenuItemText { get; set; }

        /// <summary>
        /// Gets or sets the custom text of the "Paste" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string PasteMenuItemText { get; set; }

        /// <summary>
        /// Gets or sets the custom text of the "Select All" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null), Localizable(true)]
        public string SelectAllMenuItemText { get; set; } = null;

        /// <summary>
        /// Gets the text of the "Cut" ContextMenuStrip item.
        /// </summary>
        internal string CutMenuItemTextInternal { get { return !string.IsNullOrEmpty(CutMenuItemText) ? CutMenuItemText : "Cut"; } }
        /// <summary>
        /// Gets the text of the "Copy" ContextMenuStrip item.
        /// </summary>
        internal string CopyMenuItemTextInternal { get { return !string.IsNullOrEmpty(CopyMenuItemText) ? CopyMenuItemText : "Copy"; } }
        /// <summary>
        /// Gets the text of the "Paste" ContextMenuStrip item.
        /// </summary>
        internal string PasteMenuItemTextInternal { get { return !string.IsNullOrEmpty(PasteMenuItemText) ? PasteMenuItemText : "Paste"; } }
        /// <summary>
        /// Gets the text of the "Select All" ContextMenuStrip item.
        /// </summary>
        internal string SelectAllMenuItemTextInternal { get { return !string.IsNullOrEmpty(SelectAllMenuItemText) ? SelectAllMenuItemText : "SelectAll"; } }

        /// <summary>
        /// Gets or sets the image of the "Cut" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public Image CutMenuItemImage { get; set; }
        /// <summary>
        /// Gets or sets the scaling image resource name of the "Cut" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public string CutMenuItemScalingImageName { get; set; }

        /// <summary>
        /// Gets or sets the image of the "Copy" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public Image CopyMenuItemImage { get; set; }
        /// <summary>
        /// Gets or sets the scaling image resource name of the "Copy" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public string CopyMenuItemScalingImageName { get; set; }

        /// <summary>
        /// Gets or sets the image of the "Paste" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public Image PasteMenuItemImage { get; set; }
        /// <summary>
        /// Gets or sets the scaling image resource name of the "Paste" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public string PasteMenuItemScalingImageName { get; set; }

        /// <summary>
        /// Gets or sets the image of the "Select All" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public Image SelectAllMenuItemImage { get; set; }
        /// <summary>
        /// Gets or sets the scaling image resource name of the "Select All" ContextMenuStrip item.
        /// </summary>
        [Category("BuiltIn-ContextMenu"), DefaultValue(null)]
        public string SelectAllMenuItemScalingImageName { get; set; }
    }
}
