using System;
using System.Windows.Forms;
using Clearinet;

namespace TestExtensions
{
    public class RawInspector: RequestInspectorBase
    {
        public override void OnLoad()
        {
            MessageBox.Show("RawInspector.OnLoad() called");
        }
        public override InspectorFlags GetFlags() => InspectorFlags.AlwaysCommitUpdates;

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
