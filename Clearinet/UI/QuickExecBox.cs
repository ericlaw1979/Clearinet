using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace Clearinet
{
    public class ExecuteEventArgs : EventArgs
    {
        public string Command { get; private set; }
        public bool AddToHistory { get; set; }
        public bool Handled { get; set; }

        public ExecuteEventArgs(string command)
        {
            Command = command;
            AddToHistory = true;
            Handled = false;
        }
    }

    public delegate void ExecuteHandler(ExecuteEventArgs ea);
    public class QuickExecBox : TextBox
    {
        private readonly List<string> _history = new List<string>();
        private string _typedText = string.Empty;
        private int _historyIndex = -1;
        private bool _updatingText;
        private bool _historyLoaded;

        internal void LoadHistory()
        {
            _historyLoaded = false;
            try
            {
                string[] commands = File.ReadAllLines(CONFIG.GetPath("CommandHistory"));
                _history.Clear();
                foreach (string command in commands)
                {
                    if (!string.IsNullOrWhiteSpace(command)) AddHistory(command.Trim());
                }
                _historyLoaded = true;
            }
            catch (FileNotFoundException) { _historyLoaded = true; }
            catch (DirectoryNotFoundException) { _historyLoaded = true; }
            catch (Exception ex) when (IsHistoryFileError(ex))
            {
                CApp.Log.Log("Unable to load QuickExec history: " + ex.Message);
            }
        }

        internal void SaveHistory()
        {
            if (CONFIG.isViewerMode || !_historyLoaded) return;
            try
            {
                string path = Path.GetFullPath(CONFIG.GetPath("CommandHistory"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, _history);
            }
            catch (Exception ex) when (IsHistoryFileError(ex))
            {
                CApp.Log.Log("Unable to save QuickExec history: " + ex.Message);
            }
        }

        private static bool IsHistoryFileError(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException
                || ex is ArgumentException || ex is NotSupportedException;
        }

        private void AddHistory(string command)
        {
            _history.Remove(command);
            _history.Add(command);
        }

        private string _sCueText;
        public string CueText
        {
            get
            {
                return _sCueText;
            }
            set
            {
                _sCueText = value;
                SetCueText();
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            if (!_updatingText)
            {
                _typedText = Text;
                _historyIndex = -1;
            }
            base.OnTextChanged(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.Handled || char.IsControl(e.KeyChar) || ReadOnly) return;

            SelectedText = e.KeyChar.ToString();
            e.Handled = true;
            if (SelectionStart != TextLength || TextLength == 0) return;

            for (int i = _history.Count - 1; i >= 0; --i)
            {
                if (_history[i].StartsWith(_typedText, StringComparison.OrdinalIgnoreCase))
                {
                    _historyIndex = i;
                    string completion = _typedText + _history[i].Substring(_typedText.Length);
                    ShowHistoryText(completion, _typedText.Length);
                    break;
                }
            }
        }

        private void ShowHistoryText(string text, int selectionStart)
        {
            _updatingText = true;
            try
            {
                Text = text;
                Select(selectionStart, TextLength - selectionStart);
            }
            finally { _updatingText = false; }
        }

        private void BrowseHistory(bool older)
        {
            int start = _historyIndex < 0 ? _history.Count : _historyIndex;
            int step = older ? -1 : 1;
            if (!older && _historyIndex < 0) return;
            for (int i = start + step; i >= 0 && i < _history.Count; i += step)
            {
                if (_history[i].StartsWith(_typedText, StringComparison.OrdinalIgnoreCase))
                {
                    _historyIndex = i;
                    string completion = _typedText + _history[i].Substring(_typedText.Length);
                    ShowHistoryText(completion, _typedText.Length);
                    return;
                }
            }
            if (!older)
            {
                _historyIndex = -1;
                ShowHistoryText(_typedText, _typedText.Length);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetCueText();
        }
        private void SetCueText()
        {
            if (this.IsHandleCreated) Win32UI.SetCueText(this, CueText);
        }

        /// <summary>
        /// Override of CmdKey to handle low-level keystrokes
        /// </summary>
        /// <param name="msg">The Key message</param>
        /// <param name="keyData">The data about the keypress</param>
        /// <returns>True, if we've handled the key</returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.A | Keys.Control:
                    if (TextLength == 0)
                    {
                        CApp.UI.lvExchanges.SelectAll(); // Convenience: If empty, Ctrl+A should select all Exchanges. 
                        return true;
                    }
                    break;
                case Keys.Control | Keys.Shift | Keys.Delete:
                    // If user confirms, wipe all history entries.
                    if (DialogResult.Yes == MessageBox.Show("Are you sure you want to wipe all QuickExec history?", "Confirm Wipe",
                                                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2))
                        _history.Clear();
                    return true;
                case Keys.Control | Keys.Delete:
                    var sHistoryToWipe = Text;
                    // Wipe any history entries matching the current text.
                    _history.RemoveAll(s => s.OICEquals(sHistoryToWipe));
                    Clear();
                    return true;
                case Keys.Escape:
                    if (TextLength > 0)
                    {
                        Clear();
                    }
                    else
                    {
                        CApp.UI.lvExchanges.Focus();
                    }
                    return true;
                case Keys.Up:
                case Keys.Down:
                    BrowseHistory(keyData == Keys.Up);
                    return true;
                case Keys.Enter:
                    string sCommand = Text.Trim();
                    if (sCommand.Length < 1)
                    {
                        Clear();
                        return true;
                    }
                    if (null != OnExecute)
                    {
                        ExecuteEventArgs eArgs = new ExecuteEventArgs(sCommand);
                        OnExecute(eArgs);
                        if (eArgs.AddToHistory) AddHistory(sCommand);
                    }
                    Clear();
                    _typedText = string.Empty;
                    _historyIndex = -1;
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        public event ExecuteHandler OnExecute;
    }
}
