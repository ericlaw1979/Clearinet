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

        public Action OnExchangesAdded;

        private ContextMenuStrip headerMenu;
        private Font headerMenuFont;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x007B /* WM_CONTEXTMENU */ && m.LParam != new IntPtr(-1) && View == View.Details)
            {
                long coordinates = m.LParam.ToInt64();
                Point screenPoint = new Point(unchecked((short)coordinates), unchecked((short)(coordinates >> 16)));
                IntPtr header = Win32ListViewAPI.SendMessage(Handle, Win32ListViewAPI.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                if (header != IntPtr.Zero && Win32ListViewAPI.WindowFromPoint(screenPoint) == header)
                {
                    Point headerPoint = screenPoint;
                    if (!Win32ListViewAPI.ScreenToClient(header, ref headerPoint))
                        throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                    var hit = new Win32ListViewAPI.HDHITTESTINFO { pt = headerPoint };
                    int index = Win32ListViewAPI.SendMessage(header, Win32ListViewAPI.HDM_HITTEST, IntPtr.Zero, ref hit).ToInt32();
                    ShowHeaderMenu(index >= 0 && index < Columns.Count ? Columns[index] : null, screenPoint);
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void ShowHeaderMenu(ColumnHeader column, Point screenPoint)
        {
            headerMenu?.Dispose();
            headerMenuFont?.Dispose();
            headerMenu = new ContextMenuStrip();
            headerMenuFont = new Font(headerMenu.Font, FontStyle.Bold);
            headerMenu.Items.Add(new ToolStripLabel((column?.Text ?? "Columns").Replace("&", "&&"))
            {
                Font = headerMenuFont,
                AutoToolTip = false
            });
            headerMenu.Items.Add("Search this Column...", null, (sender, e) =>
            {
                string text = frmPrompt.GetUserString(new frmPrompt.PromptOptions
                {
                    Title = $"Search {column.Text}",
                    PromptText = $"Enter the string to search for in '{column.Text}':",
                    ReturnNullOnCancel = true,
                    OwnerWindow = FindForm()
                });
                if (text != null) SearchColumn(column, text);
            }).Enabled = column != null;
            headerMenu.Items.Add("Flag duplicates", null, (sender, e) => FlagColumnDuplicates(column)).Enabled = column != null;
            headerMenu.Items.Add("Hide this column", null, (sender, e) => column.Width = 0).Enabled = column != null;
            headerMenu.Items.Add(new ToolStripSeparator());
            headerMenu.Items.Add("Ensure all columns are visible...", null, (sender, e) =>
            {
                foreach (ColumnHeader col in Columns) col.Width = Math.Max(40, col.Width);
            });
            headerMenu.Items.Add("Customize columns...", null, (sender, e) =>
                CApp.DoNotifyUser(FindForm(), "Not yet implemented.", "Customize columns", MessageBoxIcon.Information));
            headerMenu.Show(screenPoint);
        }

        private void SearchColumn(ColumnHeader column, string text)
        {
            int index = column.Index;
            if (index < 0)
            {
                CApp.UI.SetStatusText("The column is no longer available.");
                return;
            }
            CApp.UI.actSelectExchangesMatchingCriteria(x =>
                x.ViewItem.SubItems.Count > index && x.ViewItem.SubItems[index].Text.OICContains(text));
            if (SelectedCount > 0) EnsureVisible(SelectedIndices[0]);
            CApp.UI.SetStatusText($"Selected {SelectedCount} Exchanges matching '{column.Text}' containing '{text}'.");
        }

        private void FlagColumnDuplicates(ColumnHeader column)
        {
            int index = column.Index;
            if (index < 0)
            {
                CApp.UI.SetStatusText("The column is no longer available.");
                return;
            }
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (ListViewItem item in Items)
            {
                if (item.SubItems.Count <= index) continue;
                string value = item.SubItems[index].Text;
                counts.TryGetValue(value, out int count);
                counts[value] = count + 1;
            }
            var duplicates = new List<Exchange>();
            foreach (ListViewItem item in Items)
            {
                if (item.SubItems.Count > index && counts[item.SubItems[index].Text] > 1 && item.Tag is Exchange exchange)
                    duplicates.Add(exchange);
            }
            CApp.UI.MarkExchanges(duplicates.ToArray());
            CApp.UI.SetStatusText($"Flagged {duplicates.Count} Exchanges with duplicate '{column.Text}' values.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                headerMenu?.Dispose();
                headerMenuFont?.Dispose();
            }
            base.Dispose(disposing);
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

            var sColTitle = Columns[ixColumn].Text ?? $"Col#{ixColumn}";
            CApp.UI.SetStatusText($"Selected {this.SelectedCount} Exchanges matching '{sColTitle}'='{sFindText}'.");
        }

        public Exchange[] GetAllExchanges()
        {
            int iCount = ItemCount;
            Exchange[] arrExchanges = new Exchange[iCount];
            for (int ix = 0; ix < iCount; ++ix)
            {
                arrExchanges[ix] = Items[ix].Tag as Exchange;
            }
            return arrExchanges;
        }

        public Exchange[] GetSelectedExchanges()
        {
            int iSelCount = SelectedCount;
            Exchange[] arrExchanges = new Exchange[iSelCount];
            for (int ix = 0; ix < iSelCount; ++ix)
            {
                arrExchanges[ix] = SelectedItems[ix].Tag as Exchange;
            }
            return arrExchanges;
        }

        // TODO: I *think* what I really want to do is keep a simple boolean flag that tracks
        // whether the user has scrolled up at all (clearing that flag if they scroll down to the bottom).
        // That way, scrolling up becomes a simple "Don't keep autoscrolling" flag and we don't need to worry
        // about the minutia of painting, delayed additions, etc.
        public bool IsScrolledToBottom()
        {
            if (Items.Count == 0) return true;
            if (!IsHandleCreated) return false;

            /* TODO: Decide what best approach is: using TopItem+CountPerPage, or by using GetScrollInfo
             * https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getscrollinfo
             * 
             ListViewItem topItem = TopItem;
             if (null == topItem) return true;
             // Issue: We could probably cache this until the control resizes.
             int itemsPerPage = Win32ListViewAPI.SendMessage(Handle, Win32ListViewAPI.LVM_GETCOUNTPERPAGE, IntPtr.Zero, IntPtr.Zero).ToInt32();
             return topItem.Index + itemsPerPage >= Items.Count /* -2 (tolerance) */
            return Win32UI.IsScrolledToBottom(this);
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

        public int TotalCount
        {
            // TODO: After we start queueing pending additions, include that number
            get { return this.Items.Count; }
        }

        public ListViewItem[] UnselectedItems
        {
            get
            {
                var result = new List<ListViewItem>(Items.Count - SelectedCount);
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
            foreach (ListViewItem lvi in Items) lvi.Selected = true;
            EndUpdate();
        }

        // TODO: Do we need to override Clear() instead?
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

            if (SelectedCount == 1)
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

            int cToDelete = Items.Count - iToKeep;
            var itemsToDelete = new ListViewItem[cToDelete];
            for (int iX = 0; iX < cToDelete; ++iX)
            {
                itemsToDelete[iX] = Items[iX];
            }
            StoreItemsBeforeDeletion(itemsToDelete);

            BeginUpdate();
            try
            {
                for (int iX = cToDelete - 1; iX >= 0; --iX)
                {
                    Items.RemoveAt(iX);
                }
            }
            finally
            {
                EndUpdate();
            }
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
            // Trace.WriteLine($"Configured column #{ix} ({Columns[ix].Text}) with sort mode {cst}");
        }

        internal ColumnHeader AddBoundColumn(string sTitle, ColumnSortType columnSortType, Func<Exchange, string> columnProvider)
        {
            // Clearinet uses column titles as unique identifiers for layout serialization, so if we already have a column
            // of a given title, rename it.
            while (GetColumnByTitle(sTitle) != null)
            {
                sTitle = $"{sTitle}.bis";
            }

            // By default, let's right-align numeric columns.
            ColumnHeader newCol = this.Columns.Add(sTitle, 60,
                columnSortType == ColumnSortType.Numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left);

            ColumnInfo ci = new ColumnInfo
            {
                SortType = columnSortType,
                Provider = columnProvider
            };
            newCol.Tag = ci;
            foreach (ListViewItem item in Items)
            {
                while (item.SubItems.Count < Columns.Count) item.SubItems.Add(string.Empty);
                item.SubItems[newCol.Index].Text = item.Tag is Exchange exchange
                    ? PopulateColumn(newCol.Index, exchange) : string.Empty;
            }
            return newCol;
        }

        internal void RemoveBoundColumn(ColumnHeader column)
        {
            int index = Columns.IndexOf(column);
            if (index < 0) return;

            BeginUpdate();
            try
            {
                Columns.Remove(column);
                foreach (ListViewItem item in Items)
                {
                    if (index < item.SubItems.Count) item.SubItems.RemoveAt(index);
                }
                column.Tag = null;
                column.Dispose();

                if (sorter.SortColumn == index)
                {
                    sorter.SortColumn = 0;
                    sorter.SortType = Columns.Count > 0
                        ? (Columns[0].Tag as ColumnInfo?)?.SortType ?? ColumnSortType.Numeric
                        : ColumnSortType.Text;
                    sorter.Order = Columns.Count > 0 ? SortOrder.Ascending : SortOrder.None;
                }
                else if (sorter.SortColumn > index)
                {
                    --sorter.SortColumn;
                }
                SetColumnHeaderSortIcon(sorter.SortColumn, sorter.Order);
                Sort();
            }
            finally
            {
                EndUpdate();
            }
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
            // TODO: put the new menu item in the right place rather than just
            // shoving extension commands at the top.
            ContextMenuStrip.Items.Insert(0, tsmi);
        }

        internal void InvertSelection()
        {
            if (InvokeRequired)
            {
                CApp.UIInvokeAsync(() => InvertSelection());
                return;
            }
            CApp.PauseReporting = true;
            BeginUpdate();
            foreach (ListViewItem lvi in Items) lvi.Selected = !lvi.Selected;
            EndUpdate();
            CApp.PauseReporting = false;
            CApp.UI.actUpdateInspector(true, true);
        }

        /// <summary>
        /// Returns a serializable string of the columns (Title/Width) in display order.
        /// We use ~ and § as delimiters because we need characters unlikely to appear in titles.
        /// </summary>
        internal string GetColumnLayout()
        {
            string[] arrColumnsInDisplayOrder = new string[Columns.Count];
            for (int ix = 0; ix < Columns.Count; ++ix)
            {
                ColumnHeader col = Columns[ix];
                arrColumnsInDisplayOrder[col.DisplayIndex] = $"{col.Text}~{col.Width}";
            }
            return string.Join("§", arrColumnsInDisplayOrder);
        }

        internal void ApplyColumnLayout(string sLayout)
        {
            try
            {
                if (!sLayout.HasText()) return;
                string[] arrColumns = sLayout.Split(new[] { '§' }, StringSplitOptions.RemoveEmptyEntries);
                var i = 0;
                foreach (var colLayout in arrColumns)
                {
                    string[] arrParts = colLayout.Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries);
                    if (arrParts.Length != 2) continue;
                    string sTitle = arrParts[0];
                    ColumnHeader col = GetColumnByTitle(sTitle);
                    if (col == null) continue;

                    // If we accidentally end up with more columns in the serialized layout than we
                    // have in the ListView, just ignore the extra ones.
                    if (i < Columns.Count)
                    {
                        col.DisplayIndex = i++;
                    }

                    if (!int.TryParse(arrParts[1], out int iWidth)) continue;
                    col.Width = iWidth;
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Failed to apply column layout");
            }
        }

        internal ColumnHeader GetColumnByTitle(string sTitle)
        {
            foreach (ColumnHeader col in Columns)
            {
                if (String.Equals(col.Text, sTitle, StringComparison.OrdinalIgnoreCase)) return col;
            }
            return null;
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