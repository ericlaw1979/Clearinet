using System;
using System.Windows.Forms;

namespace Clearinet
{
    public delegate void RulesBeforeCompileHandler(string sFilename);
    public delegate void RulesAfterCompileHandler();
    public delegate void RulesCompileFailedHandler(string sDescription, int iLine, int iStartColumn, int iEndColumn);

    /// <summary>
    /// Bind a toolbar button to the method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field, Inherited = false)]
    public sealed class BindUIButton : Attribute
    {
        internal string _sText;

        public BindUIButton(string sText)
        {
            _sText = sText;
        }
    }


    /// <summary>
    /// Bind a column to a method returning a string for an Exchange. The column is removed when the script reloads.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class BindUIColumn : Attribute
    {
        internal string _sTitle;
        internal bool _sortNumerically;

        public BindUIColumn(string sTitle) : this(sTitle, false) { }

        public BindUIColumn(string sTitle, bool bSortNumerically)
        {
            // Disallow characters used as delimiters in serialization.
            _sTitle = sTitle.Replace('§', '$').Replace('~', '-');
            _sortNumerically = bSortNumerically;
        }
    }

    /// <summary>
    /// Bind a new tab to the method. The method should return a string, which will be displayed in the tab.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class BindUITab : Attribute
    {
        // TODO: In Fiddler, a bound tab goes into the View menu. We're not doing that here, but
        // we should consider whether the script can add on-demand tabs like extensions can. 
        internal bool _isHTML;
        internal string _sOptions;
        internal string _sTitle;
        internal TabPage _pageTab;
        internal CalculateReportHandler _delegate;

        internal void RunDelegate(Exchange[] arrXC)
        {
            _delegate?.Invoke(arrXC);
        }

        public BindUITab(string sTitle)
        {
            _sTitle = sTitle;
        }

        public BindUITab(string sTitle, bool bHTML)
        {
            _sTitle = sTitle;
            _isHTML = bHTML;
        }

        public BindUITab(string sTitle, string sOptions)
        {
            _sTitle = sTitle;
            if (sOptions.OICContains("<html>")) _isHTML = true;
            _sOptions = sOptions;
        }
    }

    // Bind a top-level menu. The items on it will come from QuickLinkItem attributes on the same method.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class QuickLinkMenu : Attribute
    {
        private string _menuTitle;
        public QuickLinkMenu(string menuTitle)
        {
            _menuTitle = menuTitle;
        }

        public string MenuTitle
        {
            get => _menuTitle;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class QuickLinkItem : Attribute, IComparable<QuickLinkItem>
    {
        private string _itemTitle;
        private string _itemAction;

        public QuickLinkItem(string itemTitle, string itemAction)
        {
            _itemTitle = itemTitle;
            _itemAction = itemAction;
        }

        public string ItemText
        {
            get => _itemTitle;
        }

        public string ItemAction
        {
            get => _itemAction;
        }

        // Alpha-sort the items.
        public int CompareTo(QuickLinkItem other)
        {
            return String.Compare(_itemTitle, other._itemTitle);
        }
    }

    /// <summary>
    /// Bind a Tools menu item to the method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ToolsAction : Attribute
    {
        internal string _sTitle;
        internal string _sSubmenu;

        // Should we collect the selected Exchanges and pass them to the method?
        internal bool bPassSelectedExchanges;

        public ToolsAction(string sTitle) : this(sTitle, null) { }

        public ToolsAction(string sTitle, string sSubmenu)
        {
            _sTitle = sTitle;
            _sSubmenu = sSubmenu;
        }
    }

    /// <summary>
    /// Binds a script to an item on the Exchanges listview ContextMenu
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ContextAction : Attribute
    {
        internal string _sTitle;
        //internal string _sSubmenu;

        public ContextAction(string sTitle)
        {
            _sTitle = sTitle;
        }

        // TODO: ContextAction Submenu support
        /*public ContextAction(string sTitle, string sSubmenu)
        {
            _sTitle = sTitle;
            _sSubmenu = sSubmenu;
        }*/
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class BindPref : Attribute
    {
        internal string _sPref;

        public BindPref(string sName)
        {
            _sPref = sName;
        }
    }


    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class BindTextCodec : Attribute
    {
        internal string Name { get; private set; }

        internal Func<string, string> Method { get; set; }
        public BindTextCodec(string sCodecName)
        {
            Name = sCodecName;
        }
    }

}
