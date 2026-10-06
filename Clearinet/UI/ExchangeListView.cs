using System;

namespace Clearinet
{
    /// <summary>
    /// This is a specialization of the ListView for the primary list of Exchanges at the left of the main UI.
    /// </summary>
    internal class ExchangeListView : BetterListView
    {
        public event EventHandler ItemShowProperties;
        public EventHandler OnExchangesAdded;

        public int SelectedCount
        {
            // TODO: is LVM_GETSELECTEDCOUNT cheaper?
            get { return this.SelectedItems.Count; }
        }
    }
}
