using System;
using System.Windows.Forms;

namespace Clearinet
{
    public delegate void RulesBeforeCompileHandler(string sFilename);
    public delegate void RulesAfterCompileHandler();
    public delegate void RulesCompileFailedHandler(string sDescription, int iLine, int iStartColumn, int iEndColumn);

    /// <summary>
    /// Bind a toolbar button to your method.
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
    /// Bind a new tab to your method. The method should return a string, which will be displayed in the tab.
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

    /// <summary>
    /// Bind a Tools menu item to your method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ToolsAction : Attribute
    {
        internal string _sTitle;
        internal string _sSubmenu;

        public ToolsAction(string sTitle) : this(sTitle, null) { }

        public ToolsAction(string sTitle, string sSubmenu)
        {
            _sTitle = sTitle;
            _sSubmenu = sSubmenu;
        }
    }

    /// <summary>
    /// The ContextAction Attribute allows users to bind a script to an item on the Exchanges listview ContextMenu
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ContextAction : Attribute
    {
        internal string _sTitle;
        internal string _sSubmenu;

        public ContextAction(string sTitle) : this(sTitle, null) { }

        public ContextAction(string sTitle, string sSubmenu)
        {
            _sTitle = sTitle;
            _sSubmenu = sSubmenu;
        }
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


}
