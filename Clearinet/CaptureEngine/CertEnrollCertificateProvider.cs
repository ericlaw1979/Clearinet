using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Clearinet.CaptureEngine
{
    internal sealed class CertEnrollCertificateProvider : ICertificateProvider
    {
        private const string RootName = "DO_NOT_TRUST_ClearinetRoot";
        private const string RootFriendlyName = "Clearinet Root CA";
        private const string LeafPrefix = "Clearinet Site Cert from: ";
        private const string LeafKeyPrefix = "Clearinet Site Cert Key: ";
        private const int UserContext = 1;
        private const int Base64Encoding = 1;

        private static readonly object RootSync = new object();
        private static readonly object RootTrustSync = new object();
        private static readonly object LeafKeySync = new object();
        // Store cleanup deletes private keys, so it must wait for all in-flight operations.
        private static readonly ReaderWriterLockSlim CertificateCleanupLock = new ReaderWriterLockSlim();
        private readonly ConcurrentDictionary<string, HostCertificate> _hosts =
            new ConcurrentDictionary<string, HostCertificate>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<X509Certificate2> _findRootCertificate;
        private readonly Func<string, X509Certificate2, X509Certificate2> _generateCertificate;
        private readonly Func<bool> _useWildcards;
        private volatile X509Certificate2 _root;

        internal CertEnrollCertificateProvider()
            : this(FindRootCertificate, GenerateCertificate,
                () => CApp.Prefs.GetBoolPref("proxy.reed.certs.wildcard", true))
        {
        }

        internal CertEnrollCertificateProvider(Func<X509Certificate2> findRootCertificate,
            Func<string, X509Certificate2, X509Certificate2> generateCertificate, Func<bool> useWildcards = null)
        {
            _findRootCertificate = findRootCertificate ?? throw new ArgumentNullException(nameof(findRootCertificate));
            _generateCertificate = generateCertificate ?? throw new ArgumentNullException(nameof(generateCertificate));
            _useWildcards = useWildcards ?? (() => false);
        }

        public X509Certificate2 GetRootCertificate()
        {
            CertificateCleanupLock.EnterReadLock();
            try
            {
                return GetRootCertificate(false);
            }
            finally
            {
                CertificateCleanupLock.ExitReadLock();
            }
        }

        private X509Certificate2 GetRootCertificate(bool createIfMissing)
        {
            X509Certificate2 root = _root;
            if (IsStillValid(root)) return root;

            lock (RootSync)
            {
                root = _root;
                if (IsStillValid(root)) return root;

                root = _findRootCertificate();
                if (root == null && createIfMissing)
                {
                    root = _generateCertificate(null, null);
                }
                _root = root;
                return root;
            }
        }

        private static X509Certificate2 FindRootCertificate()
        {
            using (var store = OpenStore(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadOnly))
            {
                return store.Certificates.Cast<X509Certificate2>()
                    .Where(c => c.FriendlyName == RootFriendlyName && IsStillValid(c) &&
                                c.Extensions.OfType<X509BasicConstraintsExtension>().Any(e => e.CertificateAuthority))
                    .OrderByDescending(c => c.NotAfter)
                    .FirstOrDefault();
            }
        }

        public X509Certificate2 GetCertificateForHost(string hostname)
        {
            string normalized = NormalizeHostname(hostname);
            if (_useWildcards() && !IPAddress.TryParse(normalized, out _))
            {
                int firstDot = normalized.IndexOf('.');
                // Do not turn two-label names into broad wildcards such as *.com.
                if (firstDot > 0 && normalized.IndexOf('.', firstDot + 1) > firstDot)
                    normalized = "*" + normalized.Substring(firstDot);
            }
            CertificateCleanupLock.EnterReadLock();
            try
            {
                HostCertificate host = _hosts.GetOrAdd(normalized, _ => new HostCertificate());
                lock (host)
                {
                    X509Certificate2 root = GetRootCertificate(true);
                    if (ReferenceEquals(host.Issuer, root) && IsStillValid(host.Certificate))
                    {
                        return host.Certificate;
                    }

                    X509Certificate2 cert = FindLeafCertificate(normalized, root) ?? _generateCertificate(normalized, root);
                    host.Certificate = cert;
                    host.Issuer = root;
                    return cert;
                }
            }
            finally
            {
                CertificateCleanupLock.ExitReadLock();
            }
        }

        private static X509Certificate2 FindLeafCertificate(string hostname, X509Certificate2 root)
        {
            using (var store = OpenStore(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadOnly))
            {
                foreach (var certificate in store.Certificates.Cast<X509Certificate2>()
                    .Where(c => c.FriendlyName == LeafPrefix + root.Thumbprint && IsStillValid(c) &&
                        string.Equals(c.GetNameInfo(X509NameType.DnsName, false), hostname, StringComparison.OrdinalIgnoreCase) &&
                        c.IssuerName.RawData.SequenceEqual(root.SubjectName.RawData))
                    .OrderByDescending(c => c.NotAfter))
                {
                    using (var chain = new X509Chain())
                    {
                        chain.ChainPolicy.ExtraStore.Add(root);
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                        if (chain.Build(certificate) && chain.ChainElements.Count == 2 &&
                            chain.ChainElements[1].Certificate.Thumbprint == root.Thumbprint)
                            return certificate;
                    }
                }
            }
            return null;
        }

        public bool EnsureRootTrust()
        {
            CertificateCleanupLock.EnterReadLock();
            try
            {
                X509Certificate2 root = GetRootCertificate(true);
                lock (RootTrustSync)
                {
                    using (var store = OpenStore(StoreName.Root, StoreLocation.CurrentUser, OpenFlags.ReadWrite))
                    {
                        if (store.Certificates.Find(X509FindType.FindByThumbprint, root.Thumbprint, false).Count == 0)
                        {
                            using (var publicRoot = new X509Certificate2(root.RawData))
                            {
                                publicRoot.FriendlyName = RootFriendlyName;
                                store.Add(publicRoot);
                            }
                        }
                    }
                }

                return true;
            }
            finally
            {
                CertificateCleanupLock.ExitReadLock();
            }
        }

        public void ClearCache(bool clearRoot)
        {
            CertificateCleanupLock.EnterWriteLock();
            try
            {
                var keyNames = new HashSet<string>(StringComparer.Ordinal);
                using (var store = OpenStore(StoreName.My, StoreLocation.CurrentUser, OpenFlags.ReadWrite))
                {
                    foreach (var certificate in store.Certificates.Cast<X509Certificate2>().ToArray())
                    {
                        bool isRoot = certificate.FriendlyName == RootFriendlyName;
                        bool isLeaf = certificate.FriendlyName.StartsWith(LeafPrefix, StringComparison.Ordinal);
                        if (isRoot)
                        {
                            string leafKeyName = LeafKeyPrefix + certificate.Thumbprint;
                            if (CngKey.Exists(leafKeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider))
                                keyNames.Add(leafKeyName);
                        }
                        if (!isLeaf && !(clearRoot && isRoot)) continue;

                        using (RSA key = certificate.GetRSAPrivateKey())
                        {
                            if (key is RSACng cng) keyNames.Add(cng.Key.KeyName);
                            store.Remove(certificate);
                        }
                    }
                }

                // Multiple certificates can refer to the same key. Remove them all before deleting it once.
                foreach (string keyName in keyNames)
                {
                    using (CngKey key = CngKey.Open(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider))
                    {
                        key.Delete();
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
            finally
            {
                CertificateCleanupLock.ExitWriteLock();
            }
        }

        private sealed class HostCertificate
        {
            internal X509Certificate2 Certificate;
            internal X509Certificate2 Issuer;
        }

        // Ensure that the certificate has a private key and will not expire for
        // at least one hour.
        private static bool IsStillValid(X509Certificate2 certificate)
        {
            return certificate != null && certificate.HasPrivateKey &&
                   certificate.NotBefore.ToUniversalTime() <= DateTime.UtcNow &&
                   certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddMinutes(60);
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

            // Strip any IPv6 literal brackets.
            if (hostname.StartsWith("[", StringComparison.Ordinal) && hostname.EndsWith("]", StringComparison.Ordinal))
            {
                hostname = hostname.Substring(1, hostname.Length - 2);
            }

            if (IPAddress.TryParse(hostname, out var address)) return address.ToString();

            // TODO: Strictly speaking, I don't think we ever need to do this? We should only see ASCII on the wire.
            string ascii = new IdnMapping().GetAscii(hostname.TrimEnd('.')).ToLowerInvariant();
            if (Uri.CheckHostName(ascii) != UriHostNameType.Dns)
                throw new ArgumentException("Target must be a DNS hostname or IP address.", nameof(hostname));
            return ascii;
        }

        private static string GetSharedLeafKeyName(X509Certificate2 issuer)
        {
            string name = LeafKeyPrefix + issuer.Thumbprint;
            if (CngKey.Exists(name, CngProvider.MicrosoftSoftwareKeyStorageProvider)) return name;

            lock (LeafKeySync)
            {
                if (!CngKey.Exists(name, CngProvider.MicrosoftSoftwareKeyStorageProvider))
                {
                    var parameters = new CngKeyCreationParameters
                    {
                        Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
                        ExportPolicy = CngExportPolicies.None,
                        KeyUsage = CngKeyUsages.Signing | CngKeyUsages.Decryption
                    };
                    parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None));
                    Stopwatch keyTimer = Stopwatch.StartNew();
                    using (CngKey key = CngKey.Create(CngAlgorithm.Rsa, name, parameters))
                    {
                        keyTimer.Stop();
                        // On my 2025 box, About 40ms for 2048 and 161ms for 3072
                        Trace.WriteLine($"Clearinet: Generate shared leaf RSA-2048 key took {keyTimer.ElapsedMilliseconds:N0}ms.");
                    }
                }
            }
            return name;
        }

        internal static X509Certificate2 GenerateCertificate(string hostname, X509Certificate2 issuer)
        {
            Stopwatch oSW = Stopwatch.StartNew();
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
                    key.KeySpec = 0;
                    key.MachineContext = false;
                    key.ExportPolicy = 0;
                    if (issuer == null)
                    {
                        key.Length = 3072;      // Root keys are larger to make them more resistant to brute force attacks.
                        Stopwatch keyTimer = Stopwatch.StartNew();
                        key.Create();
                        created = true;
                        keyTimer.Stop();
                        Trace.WriteLine($"Clearinet: Generate root RSA-3072 key took {keyTimer.ElapsedMilliseconds:N0}ms.");
                    }
                    else
                    {
                        key.ContainerName = GetSharedLeafKeyName(issuer);
                        key.Open();
                    }

                    dynamic subject = objects.Create("CX500DistinguishedName");
                    subject.Encode("CN=" + (null == issuer ? RootName : hostname.Length <= 64 ? hostname : "Clearinet HTTPS"), 0);

                    dynamic request = objects.Create("CX509CertificateRequestCertificate");
                    request.InitializeFromPrivateKey(UserContext, key, "");
                    request.Subject = subject;

                    dynamic hash = objects.Create("CObjectId");
                    hash.InitializeFromValue("2.16.840.1.101.3.4.2.1");
                    request.HashAlgorithm = hash;

                    // Account for clock skew by making the cert valid for a day in the past.
                    DateTime start = DateTime.UtcNow.AddDays(-1);

                    // Make the cert valid for a year (or as long as the issuer is valid).
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
                    enrollment.InstallResponse(2, encoded, Base64Encoding, string.Empty);

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
                    oSW.Stop();
                    Trace.WriteLine($"Clearinet: GenerateCertificate({hostname}) took {oSW.ElapsedMilliseconds:N0}ms.");

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
