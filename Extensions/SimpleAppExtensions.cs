using Clearinet;

namespace TestExtensions
{
    public class SimpleAppExtension : IAppExtension
    {
        public void OnLoad()
        {
            CApp.Log.LogFormat("Extensions.SimpleAppExtension.OnLoad() called");
        }
        public void OnBeforeUnload()
        {
            CApp.Log.LogFormat("Extensions.SimpleAppExtension.OnBeforeUnload() called");
        }
    }
}
