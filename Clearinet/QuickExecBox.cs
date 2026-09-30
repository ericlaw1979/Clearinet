using System;
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
    public class QuickExecBox: TextBox
    {
        public QuickExecBox()
        {
            this.KeyPress += new KeyPressEventHandler(QuickExecBox_KeyPress);
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

        void QuickExecBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            // TODO: Autocomplete 
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
                        // TODO: examine eArgs.Handled and eArgs.AddToHistory
                    }
                    Clear();
                    return true;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }
        public event ExecuteHandler OnExecute;
    }
}
