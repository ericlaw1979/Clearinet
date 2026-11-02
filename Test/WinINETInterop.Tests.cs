using System;
using System.Reflection;

namespace TestExtensions
{
    public static class WinINETInteropTests
    {
        private static int assertions;

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception(message);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        }

        public static void Run()
        {
            Type interopType = typeof(Clearinet.CONFIG).Assembly.GetType("Clearinet.CaptureEngine.WinINETInterop", true);
            Check(interopType != null, "WinINETInterop type exists");

            MethodInfo setProxy = interopType.GetMethod("SetProxyForClearinet", BindingFlags.Static | BindingFlags.NonPublic);
            Check(setProxy != null, "SetProxyForClearinet method exists");
            ParameterInfo[] setProxyParameters = setProxy.GetParameters();
            Check(setProxyParameters.Length == 2, "SetProxyForClearinet accepts two parameters");
            Check(setProxyParameters[0].ParameterType == typeof(string) && setProxyParameters[1].ParameterType == typeof(string),
                "SetProxyForClearinet parameters are strings");

            Type snapshotType = interopType.GetNestedType("ProxySettingsSnapshot", BindingFlags.NonPublic);
            Check(snapshotType != null, "ProxySettingsSnapshot type exists");
            object snapshot = Activator.CreateInstance(snapshotType);

            SetField(snapshot, "FlagsUi", 11);
            SetField(snapshot, "AutoDiscoveryFlags", 37);
            SetField(snapshot, "ProxyServer", "127.0.0.1:8888");
            SetField(snapshot, "ProxyBypass", "<-loopback>;*.contoso.com");
            SetField(snapshot, "AutoConfigUrl", "http://wpad.contoso.example/proxy.pac");

            MethodInfo serialize = interopType.GetMethod("SerializeSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo deserialize = interopType.GetMethod("DeserializeSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
            Check(serialize != null && deserialize != null, "Snapshot serializer methods exist");

            string encoded = (string)serialize.Invoke(null, new object[] { snapshot });
            object decoded = deserialize.Invoke(null, new object[] { encoded });

            Check((int)GetField(decoded, "FlagsUi") == 11, "FlagsUi round-trips");
            Check((int)GetField(decoded, "AutoDiscoveryFlags") == 37, "AutoDiscoveryFlags round-trips");
            Check((string)GetField(decoded, "ProxyServer") == "127.0.0.1:8888", "ProxyServer round-trips");
            Check((string)GetField(decoded, "ProxyBypass") == "<-loopback>;*.contoso.com", "ProxyBypass round-trips");
            Check((string)GetField(decoded, "AutoConfigUrl") == "http://wpad.contoso.example/proxy.pac", "AutoConfigUrl round-trips");

            bool threw = false;
            try
            {
                deserialize.Invoke(null, new object[] { "invalid" });
            }
            catch (TargetInvocationException ex)
            {
                threw = ex.InnerException is FormatException;
            }
            Check(threw, "Invalid serialized payload is rejected");

            Console.WriteLine("WinINET interop assertions passed: " + assertions);
        }
    }
}