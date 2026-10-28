using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

public class CheckedComboBox : ComboBox
{
    private readonly CheckedListBox _checkedListBox;
    private readonly ToolStripControlHost _host;
    private readonly ToolStripDropDown _dropDown;
    private bool _isUpdatingText;

    [Category("Data")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public CheckedListBox.ObjectCollection CheckItems => _checkedListBox.Items;

    [Category("Behavior")]
    public string SummarySeparator { get; set; } = ";";

    [Category("Behavior")]
    public string EmptyText { get; set; } = "-- Select Items --";

    public CheckedComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;

        _checkedListBox = new CheckedListBox
        {
            BorderStyle = BorderStyle.None,
            CheckOnClick = true,
            Dock = DockStyle.Fill
        };

        _checkedListBox.ItemCheck += (s, e) => RequestTextSummaryUpdate();

        _host = new ToolStripControlHost(_checkedListBox)
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false
        };

        _dropDown = new ToolStripDropDown
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = false
        };
        _dropDown.Items.Add(_host);
    }

    /// <summary>
    /// Safely checks or unchecks all items without throwing an Exception
    /// if called before the window handle is created.
    /// </summary>
    public void SetAllItemsChecked(bool isChecked)
    {
        _checkedListBox.BeginUpdate();
        try
        {
            for (int i = 0; i < _checkedListBox.Items.Count; i++)
            {
                _checkedListBox.SetItemChecked(i, isChecked);
            }
        }
        finally
        {
            _checkedListBox.EndUpdate();
        }

        UpdateTextSummary();
    }

    public void CheckAll()
    {
        SetAllItemsChecked(true);
    }

    public void UncheckAll()
    {
        SetAllItemsChecked(false);
    }

    private void RequestTextSummaryUpdate()
    {
        // If the window handle exists, defer execution so ItemCheck state updates first.
        // Otherwise, invoke synchronously to avoid InvalidOperationException.
        if (IsHandleCreated)
        {
            BeginInvoke(new Action(UpdateTextSummary));
        }
        else
        {
            UpdateTextSummary();
        }
    }


    protected override void WndProc(ref Message m)
    {
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONDBLCLK = 0x0203;
        const int WM_KEYDOWN = 0x0100;
        const int VK_DOWN = 0x28;
        const int VK_F4 = 0x70;

        if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_LBUTTONDBLCLK)
        {
            Focus();
            ToggleDropDown();
            return;
        }

        if (m.Msg == WM_KEYDOWN)  // TODO: Huh? Alt-f4 is handled by the form, not the combobox, right?
        {
            int key = m.WParam.ToInt32();
            if (key == VK_F4 || (key == VK_DOWN && ModifierKeys.HasFlag(Keys.Alt)))
            {
                ToggleDropDown();
                return;
            }
        }

        base.WndProc(ref m);
    }

    private void ToggleDropDown()
    {
        if (_dropDown.Visible)
        {
            _dropDown.Close();
        }
        else
        {
            ShowCustomDropDown();
        }
    }

    private void ShowCustomDropDown()
    {
        _checkedListBox.Width = Width;
        _checkedListBox.Height = Math.Max(160, DropDownHeight);
        _host.Size = _checkedListBox.Size;
        _dropDown.Size = _checkedListBox.Size;

        _dropDown.Show(this, 0, Height);
    }

    public List<string> GetCheckedItems()
    {
        List<string> list = new List<string>();
        foreach (object item in _checkedListBox.CheckedItems)
        {
            list.Add(item.ToString());
        }
        return list;
    }

    private void UpdateTextSummary()
    {
        if (_isUpdatingText) return;
        _isUpdatingText = true;

        try
        {
            List<string> checkedStrings = new List<string>();
            foreach (object item in _checkedListBox.CheckedItems)
            {
                if (item != null)
                {
                    checkedStrings.Add(item.ToString());
                }
            }

            string summary;
            if (checkedStrings.Count > 0)
            {
                summary = string.Join(SummarySeparator, checkedStrings.ToArray());
            }
            else
            {
                summary = EmptyText;
            }

            // TODO: This is too tightly-coupled to the FindPanel's SearchTargets, we need to undo that.
            // Summarize:
            summary = summary.Replace("ExchangeListbox Columns", "Columns");
            summary = summary.Replace("Request Headers;Request Body", "Requests");
            summary = summary.Replace("Response Headers;Response Body", "Responses");
            summary = summary.Replace("Requests;Responses", "Network Traffic");
            Items.Clear();
            Items.Add("in " + summary);
            SelectedIndex = 0;
        }
        finally
        {
            _isUpdatingText = false;
        }
    }
}