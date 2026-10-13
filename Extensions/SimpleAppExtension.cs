using Clearinet;
using System.Reflection;

namespace Extensions
{
    public class SimpleAppExtension : IAppExtension
    {
        public void OnLoad()
        {
            CApp.Log.Log($"Extensions.SimpleAppExtension.OnLoad() called inside {Assembly.GetExecutingAssembly().GetName()}");
        }
        public void OnBeforeUnload()
        {
            CApp.Log.Log("Extensions.SimpleAppExtension.OnBeforeUnload() called");
        }
    }
}
