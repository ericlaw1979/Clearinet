using System;
using System.Windows.Forms;

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

        internal void SelectAll()
        {
            // TODO: For perf, we need to ensure we're not firing events during this process.
            BeginUpdate();
            foreach (ListViewItem lvi in Items)
            {
                lvi.Selected = true;
            }
            EndUpdate();
        }
        internal void ClearExchanges()
        {
            BeginUpdate();
            this.Items.Clear();
            EndUpdate();
        }

        internal void RemoveSelected()
        {
            int cSelected = SelectedCount;
            if (cSelected < 1) return;

            // TODO: Cache items using a "Weak" store to enable undelete

            // Perf optimization.
            if (cSelected == Items.Count)
            {
                ClearExchanges();
                return;
            }

            // TODO: For perf, we need to ensure we're not firing events during this process.
            BeginUpdate();

            // TODO PERF: If ItemCount > 1000 and cSelected > 12 or so, get the list of SelectedIndicies and delete them downward.

            // For small numbers of items, just remove.
            foreach (ListViewItem lvi in SelectedItems)
            {
                lvi.Remove();
            }


            EndUpdate();
        }


        internal void RemoveUnselected()
        {
            int cSelected = SelectedCount;
            int cToDelete = Items.Count - cSelected;
            if (cToDelete == 0) return; // nothing to do.

            // TODO: Cache items using a "Weak" store to enable undelete
            if (0 == cSelected)
            {
                ClearExchanges();
                return;
            }

            // TODO: For perf, we need to ensure we're not firing events during this process.
            BeginUpdate();

            // TODO PERF: If ItemCount > 1000 and cSelected > 12 or so, get the list of SelectedIndicies and delete them downward.

            int iX = Items.Count - 1; // Start at the end.
            while (iX >= 0 && (cToDelete > 0))
            {
                if (!Items[iX].Selected)
                {
                    Items.RemoveAt(iX);
                    --cToDelete;
                }
                --iX;
            }

            EndUpdate();
        }
    }
}
