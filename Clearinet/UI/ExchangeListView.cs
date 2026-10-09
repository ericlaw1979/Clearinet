using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Clearinet
{
    /// <summary>
    /// This is a specialization of the ListView for the primary list of Exchanges at the left of the main UI.
    /// </summary>
    public class ExchangeListView : BetterListView
    {
        public event EventHandler ItemShowProperties;
        public EventHandler OnExchangesAdded;

        WeakReference wrActiveItem;
        WeakReference wrPreviousActiveItem;

        public int SelectedCount
        {
            // TODO: is LVM_GETSELECTEDCOUNT cheaper?
            get { return this.SelectedItems.Count; }
        }

        public int TotalCount
        {
            // TODO: After we start queueing updates, include that number
            get { return this.Items.Count; }
        }

        public ListViewItem[] UnselectedItems
        {
            get
            {
                var result = new List<ListViewItem>(Items.Count); // TODO: Should be TotalCount-SelectedCount ?
                foreach (ListViewItem item in Items)
                {
                    if (!item.Selected) result.Add(item);
                }
                return result.ToArray();
            }
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
            StoreItemsBeforeDeletion(Items);
            this.Items.Clear();
            Exchange.ResetSessionCounter();
            EndUpdate();
        }

        internal void UpdateActiveItem()
        {
            wrPreviousActiveItem = wrActiveItem;

            if (SelectedItems.Count == 1)
            {
                wrActiveItem = new WeakReference(SelectedItems[0]);
            }
        }

        /// <summary>
        /// Call this just before the currently selected Sessions is deleted
        /// </summary>
        internal void RestorePriorActiveItem()
        {
            wrActiveItem = wrPreviousActiveItem;
        }

        /// <summary>
        /// Activate the previously focused item, akin to a browser's back button.
        /// </summary>
        internal void ActivatePreviousItem()
        {
            if (null == wrPreviousActiveItem) return;
            ListViewItem oLVI = wrPreviousActiveItem.Target as ListViewItem;
            if (oLVI == null) return;

            SelectedItems.Clear();
            oLVI.Selected = oLVI.Focused = true;
        }

        internal bool CanUndelete()
        {
            return ((null != _wrDeletedItems) && _wrDeletedItems.IsAlive);
        }
        private WeakReference _wrDeletedItems = null;
        // Store items to be deleted into a weak reference so that we can undelete them if the user asks.
        private void StoreItemsBeforeDeletion(object items)
        {
            ListViewItem[] arrLVIs = null;

            if (items is ListViewItemCollection lvic)
            {
                arrLVIs = new ListViewItem[lvic.Count];
                lvic.CopyTo(arrLVIs, 0);
            }
            else if (items is SelectedListViewItemCollection slvic)
            {
                arrLVIs = new ListViewItem[slvic.Count];
                slvic.CopyTo(arrLVIs, 0);
            }
            else if (items is ListViewItem[] array)
            {
                arrLVIs = array;
            }

            if (0 == (arrLVIs?.Length ?? 0)) return;
            _wrDeletedItems = new WeakReference(arrLVIs);
        }

        public void UndeleteItems()
        {
            if (null == _wrDeletedItems) { CApp.UI.SetStatusText("No Exchanges could be restored."); return; }

            ListViewItem[] arrRestoredLVIs = _wrDeletedItems.Target as ListViewItem[];
            if (null == arrRestoredLVIs) { CApp.UI.SetStatusText("No Exchanges could be restored."); return; }
            _wrDeletedItems = null;  // Only undelete once.

            try
            {
                foreach (ListViewItem oLVI in arrRestoredLVIs) oLVI.Selected = false;
                Items.AddRange(arrRestoredLVIs);
                CApp.UI.SetStatusText($"Restored {arrRestoredLVIs.Length} deleted Exchanges");
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Failed to undelete Exchanges");
            }
        }

        internal void RemoveSelected()
        {
            int cSelected = SelectedCount;
            if (cSelected < 1) return;

            RestorePriorActiveItem();

            // Perf optimization.
            if (cSelected == Items.Count)
            {
                ClearExchanges();
                return;
            }
            StoreItemsBeforeDeletion(SelectedItems);

            // TODO: For perf, we need to ensure we're not firing events during this process.
            BeginUpdate();

            // TODO PERF: If ItemCount > 1000 and cSelected > 12 or so, get the list of
            // SelectedIndicies and delete them downward.

            // For small numbers of items, just remove.
            foreach (ListViewItem lvi in SelectedItems)
            {
                lvi.Remove();
            }

            if (this.TotalCount == 0)
            {
                Exchange.ResetSessionCounter();
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

            // TODO PERF: If ItemCount > 1000 and cSelected > 12 or so, get the
            // list of SelectedIndicies and delete all but them downward.

            StoreItemsBeforeDeletion(UnselectedItems);

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