using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Clearinet.CaptureEngine
{
    internal sealed class CertEnrollCertificateProvider : ICertificateProvider
    {
        private const string RootName = "DO_NOT_TRUST_ClearinetRoot";
        private const string RootFriendlyName = "Clearinet CertEnroll Root";
        private const string LeafPrefix = "Clearinet CertEnroll Host: ";
        private const int UserContext = 1;
        private const int Base64Encoding = 1;

        private static readonly object Sync = new object();
        private readonly Dictionary<string, X509Certificate2> _hosts =
            new Dictionary<string, X509Certificate2>(StringComparer.OrdinalIgnoreCase);
        private X509Certificate2 _root;

        public X509Certificate2 GetRootCertificate()
        {
            lock (Sync)
            {
                if (IsCurrent(_root)) return _root;
                _root = null;
                using (var store = OpenStore(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadOnly))
                {
                    _root = store.Certificates.Cast<X509Certificate2>()
                        .Where(c => c.FriendlyName == RootFriendlyName && IsCurrent(c) &&
                                    c.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority))
                        .OrderByDescending(c => c.NotAfter)
                        .FirstOrDefault();
                }
                return _root;
            }
        }

        public X509Certificate2 GetCertificateForHost(string hostname)
        {
            string normalized = NormalizeHostname(hostname);
            lock (Sync)
            {
                if (GetRootCertificate() == null)
                {
                    _root = GenerateCertificate(null, null);
                    _hosts.Clear();
                }

                if (_hosts.TryGetValue(normalized, out var cached) && IsCurrent(cached) &&
                    cached.IssuerName.RawData.SequenceEqual(_root.SubjectName.RawData))
                {
                    return cached;
                }

                X509Certificate2 cert = GenerateCertificate(normalized, _root);
                _hosts[normalized] = cert;
                return cert;
            }
        }

        public bool EnsureRootTrust()
        {
            lock (Sync)
            {
                if (GetRootCertificate() == null)
                {
                    _root = GenerateCertificate(null, null);
                }

                using (var store = OpenStore(StoreName.Root, StoreLocation.CurrentUser, OpenFlags.ReadWrite))
                using (var publicRoot = new X509Certificate2(_root.RawData))
                {
                    publicRoot.FriendlyName = RootFriendlyName;
                    store.Add(publicRoot);
                }

                return true;
            }
        }

        public void ClearCache(bool clearRoot)
        {
            lock (Sync)
            {
                using (var store = OpenStore(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadWrite))
                {
                    foreach (var certificate in store.Certificates.Cast<X509Certificate2>().ToArray())
                    {
                        bool isRoot = certificate.FriendlyName == RootFriendlyName;
                        bool isLeaf = certificate.FriendlyName.StartsWith(LeafPrefix, StringComparison.Ordinal);
                        if (!isLeaf && !(clearRoot && isRoot)) continue;

                        using (RSA key = certificate.GetRSAPrivateKey())
                        {
                            store.Remove(certificate);
                            if (key is RSACng cng) cng.Key.Delete();
                        }
                    }
                }

                _hosts.Clear();
                if (clearRoot)
                {
                    _root = null;
                    using (var trusted = OpenStore(StoreName.Root, StoreLocation.CurrentUser, OpenFlags.ReadWrite))
                    {
                        foreach (var certificate in trusted.Certificates.Cast<X509Certificate2>().ToArray())
                        {
                            if (certificate.FriendlyName == RootFriendlyName)
                            {
                                trusted.Remove(certificate);
                            }
                        }
                    }
                }
            }
        }

        private static bool IsCurrent(X509Certificate2 certificate)
        {
            return certificate != null && certificate.HasPrivateKey &&
                   certificate.NotBefore.ToUniversalTime() <= DateTime.UtcNow &&
                   certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5);
        }

        private static X509Store OpenStore(StoreName name, StoreLocation location, OpenFlags flags)
        {
            var store = new X509Store(name, location);
            store.Open(flags | OpenFlags.OpenExistingOnly);
            return store;
        }

        private static string NormalizeHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname)) throw new ArgumentException("A target hostname is required.", nameof(hostname));
            if (hostname.StartsWith("[", StringComparison.Ordinal) && hostname.EndsWith("]", StringComparison.Ordinal))
            {
                hostname = hostname.Substring(1, hostname.Length - 2);
            }

            if (IPAddress.TryParse(hostname, out var address)) return address.ToString();

            string ascii = new IdnMapping().GetAscii(hostname.TrimEnd('.')).ToLowerInvariant();
            if (Uri.CheckHostName(ascii) != UriHostNameType.Dns)
                throw new ArgumentException("Target must be a DNS hostname or IP address.", nameof(hostname));
            return ascii;
        }

        private static X509Certificate2 GenerateCertificate(string hostname, X509Certificate2 issuer)
        {
            using (var objects = new EnrollmentObjects())
            {
                dynamic key = null;
                bool created = false;
                bool installed = false;

                try
                {
                    key = objects.Create("CX509PrivateKey");
                    key.ProviderName = "Microsoft Software Key Storage Provider";
                    key.AlgorithmName = "RSA";
                    key.Length = issuer == null ? 3072 : 2048;
                    key.KeySpec = 0;
                    key.MachineContext = false;
                    key.ExportPolicy = 0;
                    key.Create();
                    created = true;

                    dynamic subject = objects.Create("CX500DistinguishedName");
                    subject.Encode("CN=" + (issuer == null ? RootName : hostname.Length <= 64 ? hostname : "Clearinet HTTPS"), 0);

                    dynamic request = objects.Create("CX509CertificateRequestCertificate");
                    request.InitializeFromPrivateKey(UserContext, key, "");
                    request.Subject = subject;

                    dynamic hash = objects.Create("CObjectId");
                    hash.InitializeFromValue("2.16.840.1.101.3.4.2.1");
                    request.HashAlgorithm = hash;

                    DateTime start = DateTime.UtcNow.AddMinutes(-5);
                    DateTime end = start.AddYears(1);
                    if (issuer != null && end > issuer.NotAfter.ToUniversalTime()) end = issuer.NotAfter.ToUniversalTime();
                    request.NotBefore = start;
                    request.NotAfter = end;

                    dynamic extensions = objects.Track(request.X509Extensions);
                    dynamic constraints = objects.Create("CX509ExtensionBasicConstraints");
                    constraints.InitializeEncode(issuer == null, issuer == null ? 0 : -1);
                    constraints.Critical = true;
                    extensions.Add(constraints);

                    dynamic usage = objects.Create("CX509ExtensionKeyUsage");
                    usage.InitializeEncode(issuer == null ? 6 : 160);
                    usage.Critical = true;
                    extensions.Add(usage);

                    if (issuer == null)
                    {
                        request.Issuer = subject;
                    }
                    else
                    {
                        dynamic issuerName = objects.Create("CX500DistinguishedName");
                        issuerName.Decode(Convert.ToBase64String(issuer.SubjectName.RawData), Base64Encoding);
                        request.Issuer = issuerName;

                        dynamic signer = objects.Create("CSignerCertificate");
                        signer.Initialize(false, 0, Base64Encoding, Convert.ToBase64String(issuer.RawData));
                        request.SignerCertificate = signer;

                        dynamic names = objects.Create("CAlternativeNames");
                        dynamic name = objects.Create("CAlternativeName");
                        if (IPAddress.TryParse(hostname, out var ip))
                        {
                            name.InitializeFromRawData(8, Base64Encoding, Convert.ToBase64String(ip.GetAddressBytes()));
                        }
                        else
                        {
                            name.InitializeFromString(3, hostname);
                        }
                        names.Add(name);

                        dynamic san = objects.Create("CX509ExtensionAlternativeNames");
                        san.InitializeEncode(names);
                        extensions.Add(san);

                        dynamic purposes = objects.Create("CObjectIds");
                        dynamic serverAuth = objects.Create("CObjectId");
                        serverAuth.InitializeFromValue("1.3.6.1.5.5.7.3.1");
                        purposes.Add(serverAuth);
                        dynamic eku = objects.Create("CX509ExtensionEnhancedKeyUsage");
                        eku.InitializeEncode(purposes);
                        extensions.Add(eku);
                    }

                    request.Encode();
                    dynamic enrollment = objects.Create("CX509Enrollment");
                    enrollment.InitializeFromRequest(request);
                    enrollment.CertificateFriendlyName = issuer == null ? RootFriendlyName : LeafPrefix + issuer.Thumbprint;
                    string encoded = enrollment.CreateRequest(Base64Encoding);
                    enrollment.InstallResponse(2, encoded, Base64Encoding, "");

                    using (var publicCertificate = new X509Certificate2(Convert.FromBase64String((string)enrollment.Certificate[Base64Encoding])))
                    using (var store = OpenStore(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadOnly))
                    {
                        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, publicCertificate.Thumbprint, false);
                        if (matches.Count == 0 || !matches[0].HasPrivateKey)
                            throw new CryptographicException("CertEnroll did not install a certificate with private key.");
                        installed = true;
                        return matches[0];
                    }
                }
                finally
                {
                    if (created && !installed)
                    {
                        key.Delete();
                    }
                }
            }
        }

        private sealed class EnrollmentObjects : IDisposable
        {
            private readonly List<object> _objects = new List<object>();

            internal dynamic Create(string name)
            {
                return Track(Activator.CreateInstance(Type.GetTypeFromProgID("X509Enrollment." + name, true)));
            }

            internal dynamic Track(object value)
            {
                _objects.Add(value);
                return value;
            }

            public void Dispose()
            {
                for (int i = _objects.Count - 1; i >= 0; --i)
                {
                    Marshal.FinalReleaseComObject(_objects[i]);
                }
            }
        }
    }
}
