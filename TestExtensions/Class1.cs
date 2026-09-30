using System;
using Clearinet;

[assembly: RequiredVersion("0.0.1.0")]
namespace TestExtensions
{
    public class SimpleAppExtension: IAppExtension
    {
        public void OnLoad()
        {
            CApp.Log.LogFormat("TestExtensions.SimpleAppExtension.OnLoad() called");
        }
        public void OnBeforeUnload()
        {
            CApp.Log.LogFormat("TestExtensions.SimpleAppExtension.OnBeforeUnload() called");
        }
    }
}
