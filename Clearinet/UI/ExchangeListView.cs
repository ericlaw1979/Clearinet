using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Clearinet
{
    internal enum ColumnSortType
    {
        Text,           // case-insensitive string sort
        Numeric,        // numeric sort
        DateTime        // date/time sort
    }

    internal struct ColumnInfo
    {
        public ColumnSortType SortType;
        public Func<Exchange, string> Provider;
    }

    /// <summary>
    /// This is a specialization of the ListView for the primary list of Exchanges at the left of the main UI.
    /// </summary>
    public class ExchangeListView : BetterListView
    {
        // public event EventHandler ItemShowProperties;
        // public EventHandler OnExchangesAdded;

        private LVCSorter sorter;

        public ExchangeListView()
        {
            sorter = new LVCSorter()
            {
                SortType = ColumnSortType.Numeric,
                Order = SortOrder.Ascending,
            };
            this.ListViewItemSorter = sorter;
            this.ItemDrag += ExchangeListView_ItemDrag;
            this.MouseClick += ExchangeListView_MouseClick;
        }

        // lvi may be null, ixColumn may be -1 if no column was hit.
        public (ListViewItem lvi, int ixColumn) GetItemAndColumnAt(Point pt)
        {
            ListViewItem lvi = this.GetItemAt(pt.X, pt.Y);
            int ixColumn = -1;
            var hitInfo = HitTest(pt);
            if (hitInfo.Item != null && hitInfo.SubItem != null)
            {
                ixColumn = hitInfo.Item.SubItems.IndexOf(hitInfo.SubItem);
            }
            return (lvi, ixColumn);
        }

        private void ExchangeListView_MouseClick(object sender, MouseEventArgs e)
        {
            // When a user Alt+Clicks a column value, select all Exchanges that have the same value in that column.
            // If user Ctrl+Alt+Clicks, then add to the selection instead of replacing it.
            if (Keys.Alt == (Control.ModifierKeys & Keys.Alt) && e.Button == MouseButtons.Left)
            {
                var (lvi, ixColumn) = GetItemAndColumnAt(e.Location);
                if (lvi == null || ixColumn < 0) return;

                // "Picker mode"
                bool bGrowSelection = (Keys.Control == (Control.ModifierKeys & Keys.Control));

                selectMatchingColumn(lvi, ixColumn, bGrowSelection);
            }
        }

        internal void selectMatchingColumn(ListViewItem lvi, int ixColumn, bool bGrowSelection = false)
        {
            if (lvi == null || ixColumn < 0) return;
            string sFindText = lvi.SubItems[ixColumn].Text ?? string.Empty;
            if (!bGrowSelection)
            {
                this.SelectedItems.Clear();
            }

            // TODO: Fiddler supported a "Toggle" mode where if you clicked a value that was already selected,
            // it would unselect all of the matches. That seems both obscure and complicated (since the a given
            // item could match multiple criteria). For now, we will just support "Grow" mode (Ctrl+Alt) and "Replace" mode (Alt).

            CApp.UI.actSelectExchangesMatchingCriteria((oS) =>
            {
                ListViewItem lviToTest = oS.ViewItem;
                if (lviToTest == null) return false;
                if (bGrowSelection & lviToTest.Selected) return true;
                if (lviToTest.SubItems.Count <= ixColumn) return false;
                return String.Equals(lviToTest.SubItems[ixColumn].Text ?? string.Empty, sFindText);
            }, bClearSelectionFirst: !bGrowSelection);

            CApp.UI.SetStatusText($"Selected {this.SelectedItems.Count} Exchanges matching column {ixColumn} value '{sFindText}'.");
        }

        public Exchange[] GetAllExchanges()
        {
            int iCount = Items.Count;
            Exchange[] arrExchanges = new Exchange[iCount];
            for (int ix = 0; ix < iCount; ++ix)
            {
                arrExchanges[ix] = Items[ix].Tag as Exchange;
            }
            return arrExchanges;
        }

        public Exchange[] GetSelectedExchanges()
        {
            int iSelCount = SelectedItems.Count;
            Exchange[] arrExchanges = new Exchange[iSelCount];
            for (int ix = 0; ix < iSelCount; ++ix)
            {
                arrExchanges[ix] = SelectedItems[ix].Tag as Exchange;
            }
            return arrExchanges;
        }

        public bool IsScrolledToBottom()
        {
            if (Items.Count == 0) return true;
            if (!IsHandleCreated) return false;

            ListViewItem topItem = TopItem;
            if (topItem == null) return false;

            int itemsPerPage = Win32ListViewAPI.SendMessage(Handle, Win32ListViewAPI.LVM_GETCOUNTPERPAGE, IntPtr.Zero, IntPtr.Zero).ToInt32();
            return topItem.Index + itemsPerPage >= Items.Count;
        }

        private void ExchangeListView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var XC = GetSelectedExchanges();
            DataObject dragData = new DataObject("Clearinet.Exchange[]", XC);

            // Initiate drag
            DoDragDrop(dragData, DragDropEffects.Copy);
        }

        protected override void OnColumnClick(ColumnClickEventArgs e)
        {
            base.OnColumnClick(e);
            // Toggle sort direction if clicking the same column, otherwise default to Ascending
            if (e.Column == sorter.SortColumn)
            {
                sorter.Order = (sorter.Order == SortOrder.Ascending)
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                sorter.SortColumn = e.Column;
                sorter.Order = SortOrder.Ascending;
            }

            sorter.SortType = (Columns[e.Column].Tag as ColumnInfo?)?.SortType ?? ColumnSortType.Text;
            SetColumnHeaderSortIcon(sorter.SortColumn, sorter.Order);
            Trace.WriteLine($"Sorting column {sorter.SortColumn} in {sorter.Order} order with SortType {sorter.SortType}");
            this.Sort();
        }

        private void SetColumnHeaderSortIcon(int columnIndex, SortOrder order)
        {
            // Obtain handle to the internal Header control
            IntPtr hHeader = Win32ListViewAPI.SendMessage(this.Handle, Win32ListViewAPI.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (hHeader == IntPtr.Zero) return;

            int columnCount = this.Columns.Count;

            for (int i = 0; i < columnCount; i++)
            {
                Win32ListViewAPI.HDITEM item = new Win32ListViewAPI.HDITEM();
                item.mask = Win32ListViewAPI.HDI_FORMAT;

                // Fetch current format flags for the header item
                Win32ListViewAPI.SendMessage(hHeader, Win32ListViewAPI.HDM_GETITEM, new IntPtr(i), ref item);

                if (i == columnIndex)
                {
                    // Clear existing sort flags
                    item.fmt &= ~(Win32ListViewAPI.HDF_SORTUP | Win32ListViewAPI.HDF_SORTDOWN);

                    // Set requested sort flag
                    if (order == SortOrder.Ascending)
                        item.fmt |= Win32ListViewAPI.HDF_SORTUP;
                    else if (order == SortOrder.Descending)
                        item.fmt |= Win32ListViewAPI.HDF_SORTDOWN;
                }
                else
                {
                    // Remove sort arrows from all non-sorted columns
                    item.fmt &= ~(Win32ListViewAPI.HDF_SORTUP | Win32ListViewAPI.HDF_SORTDOWN);
                }

                // Apply updated format flags
                Win32ListViewAPI.SendMessage(hHeader, Win32ListViewAPI.HDM_SETITEM, new IntPtr(i), ref item);
            }
        }

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
            Exchange.ResetExchangeCounter();
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
        /// Call this just before the currently selected Exchanges are deleted
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
                Exchange.ResetExchangeCounter();
            }
            EndUpdate();
        }

        internal void Tail(int iToKeep)
        {
            if (iToKeep >= Items.Count) return;
            if (iToKeep < 1)
            {
                ClearExchanges();
                return;
            }

            // TODO: For perf, we need to ensure we're not firing events during this process.
            BeginUpdate();

            // TODO PERF: If ItemCount > 1000 and cSelected > 12 or so, get the
            // list of SelectedIndicies and delete all but them downward.

            SelectedItems.Clear();
            int iX = Items.Count - 1; // Start at the end.
            while (iX >= 0 && (iToKeep > 0))
            {
                Items[iX].Selected = true;
                --iToKeep;
                --iX;
            }

            RemoveUnselected();
            SelectedItems.Clear();
            EndUpdate();
        }

        internal void RemoveUnselected()
        {
            int cSelected = SelectedCount;
            int cToDelete = Items.Count - cSelected;
            if (cToDelete == 0) return; // nothing to do.

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

        internal void ConfigureColumn(int ix, ColumnSortType cst)
        {
            if (ix > Columns.Count - 1) { Debug.Assert(false); return; }
            ColumnInfo ci = Columns[ix].Tag as ColumnInfo? ?? new ColumnInfo();
            ci.SortType = cst;
            Columns[ix].Tag = ci;
            Trace.WriteLine($"Configured column #{ix} ({Columns[ix].Text}) with sort mode {cst}");
        }

        internal void AddBoundColumn(string sTitle, ColumnSortType columnSortType, Func<Exchange, string> columnProvider)
        {
            // By default, let's right-align numeric columns.
            ColumnHeader newCol = this.Columns.Add(sTitle, 60,
                columnSortType == ColumnSortType.Numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left);

            ColumnInfo ci = new ColumnInfo
            {
                SortType = columnSortType,
                Provider = columnProvider
            };
            this.Columns[this.Columns.Count - 1].Tag = ci;
        }

        internal string PopulateColumn(int i, Exchange x)
        {
            try
            {
                if (Columns[i].Tag is ColumnInfo ci && ci.Provider != null)
                {
                    return ci.Provider(x);
                }
            }
            catch (Exception eX)
            {
                CApp.Log.LogFormat($"Failed to populate column {i} for Exchange {x.id} due to {eX.Message}");
            }
            return string.Empty;
        }

        public void AddExtensionMenuItem(ToolStripMenuItem tsmi)
        {
            // TODO: put this in the right place rather than just shoving it at the top.
            ContextMenuStrip.Items.Insert(0, tsmi);
        }
    }

    /// <summary>
    /// Custom IComparer to handle column-based sorting (Numeric vs String).
    /// </summary>
    internal sealed class LVCSorter : IComparer
    {
        internal int SortColumn { get; set; } = 0;
        internal SortOrder Order { get; set; } = SortOrder.None;

        private ColumnSortType _sortType = ColumnSortType.Text;

        internal ColumnSortType SortType { get => _sortType; set => _sortType = value; }

        public int Compare(object x, object y)
        {
            if (Order == SortOrder.None)
                return 0;

            var itemX = (ListViewItem)x;
            var itemY = (ListViewItem)y;

            // Retrieve subitem text defensively
            string textX = itemX.SubItems.Count > SortColumn ? itemX.SubItems[SortColumn].Text : string.Empty;
            string textY = itemY.SubItems.Count > SortColumn ? itemY.SubItems[SortColumn].Text : string.Empty;

            int compareResult;

            switch (_sortType)
            {
                // ISSUE: Does this support NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign?
                case ColumnSortType.Numeric:
                    if (decimal.TryParse(textX, out decimal numX) && decimal.TryParse(textY, out decimal numY))
                    {
                        compareResult = numX.CompareTo(numY);
                    }
                    else
                    {
                        compareResult = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
                    }
                    break;
                case ColumnSortType.DateTime:
                    if (DateTime.TryParse(textX, out DateTime dtX) && DateTime.TryParse(textY, out DateTime dtY))
                    {
                        compareResult = dtX.CompareTo(dtY);
                    }
                    else
                    {
                        compareResult = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
                    }
                    break;
                case ColumnSortType.Text:
                default:
                    compareResult = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
                    break;
            }

            // Reverse result for Descending order
            return (Order == SortOrder.Ascending) ? compareResult : -compareResult;
        }
    }
}