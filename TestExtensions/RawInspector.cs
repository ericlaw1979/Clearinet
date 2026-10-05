using System;
using System.Windows.Forms;
using Clearinet;

namespace TestExtensions
{
    public class RawRequestInspector: RequestInspectorBase
    {
        public override void OnLoad()
        {
            MessageBox.Show("RawRequestInspector.OnLoad() called");
        }

        public override void Clear()
        {
            /* */
        }

        public override void AddToTab(TabPage o)
        {
            //Control = new RawView(this);
            o.Text = "Raw";
            //o.Controls.Add(myControl);
            //o.Controls[0].Dock = DockStyle.Fill;
        }

        public override int GetOrder()
        {
            return 0;
        }
    }

    public class RawResponseInspector : ResponseInspectorBase
    {
        public override void OnLoad()
        {
            MessageBox.Show("RawResponseInspector.OnLoad() called");
        }
     
        public override void Clear()
        {
            /* */
        }

        public override void AddToTab(TabPage o)
        {
            //Control = new RawView(this);
            o.Text = "Raw";
            //o.Controls.Add(myControl);
            //o.Controls[0].Dock = DockStyle.Fill;
        }

        public override int GetOrder()
        {
            return 0;
        }
    }
}
