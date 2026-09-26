using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clearinet
{
    internal partial class frmTextWizard : Form
    {
        internal frmTextWizard(string sInput)
        {
            InitializeComponent();
            if (!String.IsNullOrEmpty(sInput))
            {
                txtInput.Text = sInput.Replace("\n", "\r\n");
                this.ActiveControl = txtOutput;
            }
        }

        private void txtInput_TextChanged(object sender, EventArgs e)
        {
            txtOutput.Text = txtInput.Text;
            this.Text = $"TextWizard [ {txtInput.TextLength} => {txtOutput.TextLength} characters]";
        }
    }
}
