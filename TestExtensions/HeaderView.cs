using Clearinet;
using System.Windows.Forms;

namespace TestExtensions
{
    public partial class HeaderView : UserControl
    {
        InspectorBase _owner;
        public HeaderView(InspectorBase owner)
        {
            _owner = owner;
            InitializeComponent();
            tvNVP.BackColor = txtFirstLine.BackColor = CONFIG.colorDisabledEdit;
        }

        public void Clear()
        {
            txtFirstLine.Clear();
            tvNVP.Nodes.Clear();
        }
    }
}
