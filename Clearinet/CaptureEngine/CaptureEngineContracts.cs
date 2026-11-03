using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Clearinet.CaptureEngine
{
    internal interface ICaptureEngineListener : IDisposable
    {
        bool IsRunning { get; }
        void Start();
        void Stop();
    }

    internal interface ICaptureRequestProcessor
    {
        void Process(CaptureConnectionContext context);
    }

    internal interface ISocketPool : IDisposable
    {
        bool TryRent(ServerConnectionKey key, out Socket socket);
        void Return(ServerConnectionKey key, Socket socket, DateTime idleSinceUtc);
        void PurgeExpired(DateTime nowUtc);
    }

    internal interface ICertificateProvider
    {
        X509Certificate2 GetRootCertificate();
        X509Certificate2 GetCertificateForHost(string hostname);
        bool EnsureRootTrust();
        void ClearCache(bool clearRoot);
    }

    internal sealed class CaptureEngineOptions
    {
        internal CaptureEngineOptions(IPAddress listenAddress, int listenPort)
        {
            if (listenAddress == null) throw new ArgumentNullException(nameof(listenAddress));
            if (listenPort < IPEndPoint.MinPort || listenPort > IPEndPoint.MaxPort)
                throw new ArgumentOutOfRangeException(nameof(listenPort));

            ListenAddress = listenAddress;
            ListenPort = listenPort;
        }

        public IPAddress ListenAddress { get; private set; }
        public int ListenPort { get; private set; }
        public IPEndPoint ListenEndPoint { get { return new IPEndPoint(ListenAddress, ListenPort); } }
    }

    internal sealed class CaptureConnectionContext
    {
        public CaptureConnectionContext(Socket clientSocket)
        {
            ClientSocket = clientSocket ?? throw new ArgumentNullException(nameof(clientSocket));
            AcceptedAtUtc = DateTime.UtcNow;
        }

        public Socket ClientSocket { get; private set; }
        public DateTime AcceptedAtUtc { get; private set; }
        public string LocalProcessDescriptor { get; set; }
        public Exchange Exchange { get; set; }
    }

    internal struct ServerConnectionKey
    {
        public string Host;
        public int Port;
        public bool IsTls;

        public ServerConnectionKey(string host, int port, bool isTls)
        {
            Host = host ?? string.Empty;
            Port = port;
            IsTls = isTls;
        }

        public override string ToString()
        {
            return (IsTls ? "tls://" : "tcp://") + Host + ":" + Port;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ServerConnectionKey other)) return false;
            return IsTls == other.IsTls &&
                   Port == other.Port &&
                   string.Equals(Host ?? string.Empty, other.Host ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsTls ? 17 : 23;
                hash = (hash * 31) + Port;
                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(Host ?? string.Empty);
                return hash;
            }
        }
    }

    internal static class CaptureEnginePrefs
    {
        internal const string PrefCaptureConnect = "proxy.capture_connect";
        internal const string PrefDecryptHttps = "proxy.decrypt_https";
        internal const string PrefListenPort = "proxy.listen_port";
        internal const string PrefAllowRemoteConnections = "proxy.allow_remote_connections";
        internal const string PrefEnableIPv6 = "proxy.enable_ipv6";
        internal const string PrefSocketPoolIdleMs = "proxy.socketpool.idle_ms";
        internal const string PrefSocketPoolMaxAgeMs = "proxy.socketpool.max_age_ms";
        internal const string PrefCaptureTrace = "proxy.capture.trace";

        internal static bool ShouldCaptureConnect()
        {
            return CApp.Prefs.GetBoolPref(PrefCaptureConnect, true);
        }

        internal static bool ShouldDecryptHttps()
        {
            return CApp.Prefs.GetBoolPref(PrefDecryptHttps, false);
        }

        internal static bool IsCaptureTracingEnabled()
        {
            return CApp.Prefs.GetBoolPref(PrefCaptureTrace, false);
        }

        internal static int GetSocketPoolIdleMs()
        {
            return NormalizeDuration(CApp.Prefs.GetInt32Pref(PrefSocketPoolIdleMs, 5 * 60 * 1000));
        }

        internal static int GetSocketPoolMaxAgeMs()
        {
            return NormalizeDuration(CApp.Prefs.GetInt32Pref(PrefSocketPoolMaxAgeMs, 5 * 60 * 1000));
        }

        internal static IPAddress GetListenAddress()
        {
            bool allowRemote = CApp.Prefs.GetBoolPref(PrefAllowRemoteConnections, false);
            bool enableIPv6 = CApp.Prefs.GetBoolPref(PrefEnableIPv6, true);
            if (!allowRemote) return IPAddress.Loopback;
            return enableIPv6 ? IPAddress.IPv6Any : IPAddress.Any;      // This will trigger a firewall prompt if an exclusion wasn't set.
        }

        private static int NormalizeDuration(int durationMs)
        {
            if (durationMs < 5000) return 5000;
            if (durationMs > 24 * 60 * 60 * 1000) return 24 * 60 * 60 * 1000;
            return durationMs;
        }
    }
}
