using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Clearinet.CaptureEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CaptureEngine.Tests
{
    [TestClass]
    public sealed class CertEnrollCertificateProviderTests
    {
        [TestMethod]
        public void GetRootCertificate_MissingRoot_DoesNotCreateCertificate()
        {
            int creations = 0;
            var provider = new CertEnrollCertificateProvider(() => null, (hostname, issuer) =>
            {
                Interlocked.Increment(ref creations);
                throw new InvalidOperationException("Root lookup must not create a certificate.");
            });

            Assert.IsNull(provider.GetRootCertificate());
            Assert.AreEqual(0, creations);
        }

        [TestMethod]
        public void GetCertificateForHost_ConcurrentRequests_CreateRootOnlyOnce()
        {
            using (var root = CreateCertificate())
            using (var leaf = CreateCertificate())
            using (var start = new Barrier(8))
            {
                int rootCreations = 0;
                var provider = new CertEnrollCertificateProvider(() => null, (hostname, issuer) =>
                {
                    if (issuer == null)
                    {
                        Interlocked.Increment(ref rootCreations);
                        return root;
                    }
                    Assert.AreSame(root, issuer);
                    return leaf;
                });
                var tasks = new Task<X509Certificate2>[8];
                for (int i = 0; i < tasks.Length; i++)
                {
                    string hostname = "host" + i + ".example";
                    tasks[i] = Task.Factory.StartNew(() =>
                    {
                        Assert.IsTrue(start.SignalAndWait(TimeSpan.FromSeconds(10)));
                        return provider.GetCertificateForHost(hostname);
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }

                Assert.IsTrue(Task.WaitAll(tasks, TimeSpan.FromSeconds(20)));
                Assert.AreEqual(1, rootCreations);
                Assert.AreSame(root, provider.GetRootCertificate());
                foreach (var task in tasks) Assert.AreSame(leaf, task.Result);
            }
        }

        [DataTestMethod]
        [DataRow("EXAMPLE.COM.", "example.com")]
        [DataRow("[2001:db8::1]", "2001:db8::1")]
        public void GetCertificateForHost_SameNormalizedHost_WaitsAndReturnsSameCertificate(string firstHost, string secondHost)
        {
            using (var root = CreateCertificate())
            using (var leaf = CreateCertificate())
            using (var creating = new ManualResetEventSlim())
            using (var secondStarted = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                int leafCreations = 0;
                var provider = new CertEnrollCertificateProvider(() => root, (hostname, issuer) =>
                {
                    Interlocked.Increment(ref leafCreations);
                    creating.Set();
                    Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(10)));
                    return leaf;
                });
                Task<X509Certificate2> first = Task.Run(() => provider.GetCertificateForHost(firstHost));
                Task<X509Certificate2> second = null;
                try
                {
                    Assert.IsTrue(creating.Wait(TimeSpan.FromSeconds(10)));
                    second = Task.Run(() =>
                    {
                        secondStarted.Set();
                        return provider.GetCertificateForHost(secondHost);
                    });
                    Assert.IsTrue(secondStarted.Wait(TimeSpan.FromSeconds(10)));
                    Assert.IsFalse(second.IsCompleted);
                }
                finally
                {
                    release.Set();
                    if (second == null) first.GetAwaiter().GetResult();
                    else Task.WaitAll(first, second);
                }

                Assert.AreEqual(1, leafCreations);
                Assert.AreSame(leaf, first.Result);
                Assert.AreSame(first.Result, second.Result);
                Assert.AreSame(leaf, provider.GetCertificateForHost(secondHost));
                Assert.AreEqual(1, leafCreations);
            }
        }

        [TestMethod]
        public void GetCertificateForHost_DifferentHosts_CreateCertificatesInParallel()
        {
            using (var root = CreateCertificate())
            using (var firstLeaf = CreateCertificate())
            using (var secondLeaf = CreateCertificate())
            using (var creating = new Barrier(2))
            {
                var provider = new CertEnrollCertificateProvider(() => root, (hostname, issuer) =>
                {
                    Assert.IsTrue(creating.SignalAndWait(TimeSpan.FromSeconds(10)),
                        "Different hosts must be able to enter certificate generation together.");
                    return hostname == "first.example" ? firstLeaf : secondLeaf;
                });
                Task<X509Certificate2> first = Task.Factory.StartNew(
                    () => provider.GetCertificateForHost("first.example"), CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task<X509Certificate2> second = Task.Factory.StartNew(
                    () => provider.GetCertificateForHost("second.example"), CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);

                Assert.IsTrue(Task.WaitAll(new Task[] { first, second }, TimeSpan.FromSeconds(20)));
                Assert.AreSame(firstLeaf, first.Result);
                Assert.AreSame(secondLeaf, second.Result);
            }
        }

        [TestMethod]
        public void GetCertificateForHost_UnrelatedCreation_DoesNotBlockCachedCertificates()
        {
            using (var root = CreateCertificate())
            using (var leaf = CreateCertificate())
            using (var creating = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var provider = new CertEnrollCertificateProvider(() => root, (hostname, issuer) =>
                {
                    if (hostname == "blocked.example")
                    {
                        creating.Set();
                        Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(20)));
                    }
                    return leaf;
                });
                Assert.AreSame(leaf, provider.GetCertificateForHost("cached.example"));
                Task<X509Certificate2> blocked = Task.Run(() => provider.GetCertificateForHost("blocked.example"));
                Task cached = null;
                try
                {
                    Assert.IsTrue(creating.Wait(TimeSpan.FromSeconds(10)));
                    cached = Task.Run(() =>
                    {
                        Assert.AreSame(leaf, provider.GetCertificateForHost("cached.example"));
                        Assert.AreSame(root, provider.GetRootCertificate());
                    });
                    Assert.IsTrue(cached.Wait(TimeSpan.FromSeconds(10)),
                        "Cached leaf and root lookups must not wait for an unrelated host's creation.");
                }
                finally
                {
                    release.Set();
                    if (cached == null) blocked.GetAwaiter().GetResult();
                    else Task.WaitAll(blocked, cached);
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GetCertificateForHost_FailedCreation_CanRetry(bool failRoot)
        {
            using (var root = CreateCertificate())
            using (var leaf = CreateCertificate())
            {
                int attempts = 0;
                var provider = new CertEnrollCertificateProvider(() => null, (hostname, issuer) =>
                {
                    bool failThisStage = (issuer == null) == failRoot;
                    if (failThisStage && Interlocked.Increment(ref attempts) == 1)
                        throw new CryptographicException("Creation failed.");
                    return issuer == null ? root : leaf;
                });

                Assert.ThrowsException<CryptographicException>(() => provider.GetCertificateForHost("example.com"));
                Assert.AreSame(leaf, provider.GetCertificateForHost("example.com"));
                Assert.AreEqual(2, attempts);
            }
        }

        [TestMethod]
        public void GetCertificateForHost_LeafExpiresWithinOneHour_RenewsCertificate()
        {
            using (var root = CreateCertificate())
            using (var expiringLeaf = CreateCertificate(30))
            using (var renewedLeaf = CreateCertificate())
            {
                int creations = 0;
                var provider = new CertEnrollCertificateProvider(() => root,
                    (hostname, issuer) => ++creations == 1 ? expiringLeaf : renewedLeaf);

                Assert.AreSame(expiringLeaf, provider.GetCertificateForHost("example.com"));
                Assert.AreSame(renewedLeaf, provider.GetCertificateForHost("example.com"));
                Assert.AreSame(renewedLeaf, provider.GetCertificateForHost("example.com"));
                Assert.AreEqual(2, creations);
            }
        }

        [DataTestMethod]
        [DataRow("mail.google.com", true, "*.google.com")]
        [DataRow("WWW.GOOGLE.COM.", true, "*.google.com")]
        [DataRow("one.mail.google.com", true, "*.mail.google.com")]
        [DataRow("mail.google.com", false, "mail.google.com")]
        [DataRow("google.com", true, "google.com")]
        [DataRow("localhost", true, "localhost")]
        [DataRow("127.0.0.1", true, "127.0.0.1")]
        [DataRow("[2001:db8::1]", true, "2001:db8::1")]
        public void GetCertificateForHost_WildcardPreference_SelectsCertificateIdentity(string hostname, bool wildcard, string expected)
        {
            using (var root = CreateCertificate())
            using (var leaf = CreateCertificate())
            {
                string generatedName = null;
                var provider = new CertEnrollCertificateProvider(() => root, (name, issuer) =>
                {
                    generatedName = name;
                    return leaf;
                }, () => wildcard);

                Assert.AreSame(leaf, provider.GetCertificateForHost(hostname));
                Assert.AreEqual(expected, generatedName);
            }
        }

        [TestMethod]
        public void GetCertificateForHost_WildcardSiblings_ShareSingleCreation()
        {
            using (var root = CreateCertificate())
            using (var leaf = CreateCertificate())
            {
                int creations = 0;
                var provider = new CertEnrollCertificateProvider(() => root, (name, issuer) =>
                {
                    Assert.AreEqual("*.google.com", name);
                    Interlocked.Increment(ref creations);
                    return leaf;
                }, () => true);
                Task<X509Certificate2> first = Task.Run(() => provider.GetCertificateForHost("mail.google.com"));
                Task<X509Certificate2> second = Task.Run(() => provider.GetCertificateForHost("www.google.com"));
                Task.WaitAll(first, second);
                Assert.AreSame(first.Result, second.Result);
                Assert.AreEqual(1, creations);
            }
        }

        [DataTestMethod]
        [DataRow("mail.google.com", "www.google.com", true, "*.google.com")]
        [DataRow("mail.google.com", "mail.google.com", false, "mail.google.com")]
        [DataRow("127.0.0.1", "127.0.0.1", true, "127.0.0.1")]
        [DataRow("[2001:db8::1]", "2001:db8::1", true, "2001:db8::1")]
        [TestCategory("Integration")]
        public void GetCertificateForHost_NewProvider_ReusesStoredCertificate(string firstHost, string nextHost,
            bool wildcard, string expected)
        {
            using (X509Certificate2 root = CertEnrollCertificateProvider.GenerateCertificate(null, null))
            {
                string sharedKeyName = "Clearinet CertEnroll Leaf Key: " + root.Thumbprint;
                try
                {
                    var firstProvider = new CertEnrollCertificateProvider(() => root,
                        CertEnrollCertificateProvider.GenerateCertificate, () => wildcard);
                    using (X509Certificate2 first = firstProvider.GetCertificateForHost(firstHost))
                    {
                        Assert.AreEqual(expected, first.GetNameInfo(X509NameType.DnsName, false));
                        var nextProvider = new CertEnrollCertificateProvider(() => root,
                            (name, issuer) => { throw new AssertFailedException("A stored certificate should have been reused."); },
                            () => wildcard);
                        using (X509Certificate2 reused = nextProvider.GetCertificateForHost(nextHost))
                        {
                            Assert.AreEqual(first.Thumbprint, reused.Thumbprint);
                            Assert.IsTrue(reused.HasPrivateKey);
                        }
                    }
                }
                finally
                {
                    RemoveTestCertificates(root, sharedKeyName);
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void GetCertificateForHost_StoredLeafSignedByDifferentRoot_IsNotReused()
        {
            using (X509Certificate2 root = CertEnrollCertificateProvider.GenerateCertificate(null, null))
            {
                try
                {
                    using (X509Certificate2 otherRoot = CertEnrollCertificateProvider.GenerateCertificate(null, null))
                    {
                        try
                        {
                            using (X509Certificate2 wrongLeaf = CertEnrollCertificateProvider.GenerateCertificate("mail.google.com", otherRoot))
                            using (X509Certificate2 replacement = CreateCertificate())
                            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                            {
                                store.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);
                                using (X509Certificate2 writableLeaf = store.Certificates.Find(
                                    X509FindType.FindByThumbprint, wrongLeaf.Thumbprint, false)[0])
                                {
                                    writableLeaf.FriendlyName = "Clearinet CertEnroll Host: " + root.Thumbprint;
                                    int creations = 0;
                                    var provider = new CertEnrollCertificateProvider(() => root, (name, issuer) =>
                                    {
                                        Interlocked.Increment(ref creations);
                                        Assert.AreSame(root, issuer);
                                        return replacement;
                                    });
                                    Assert.AreSame(replacement, provider.GetCertificateForHost("mail.google.com"));
                                    Assert.AreEqual(1, creations);
                                    writableLeaf.FriendlyName = "Clearinet CertEnroll Host: " + otherRoot.Thumbprint;
                                }
                            }
                        }
                        finally
                        {
                            // Also recover the test leaf if an assertion fails before its friendly name is restored.
                            RemoveTestCertificates(root, "Clearinet CertEnroll Leaf Key: " + root.Thumbprint);
                            RemoveTestCertificates(otherRoot, "Clearinet CertEnroll Leaf Key: " + otherRoot.Thumbprint);
                        }
                    }
                }
                finally
                {
                    RemoveTestCertificates(root, "Clearinet CertEnroll Leaf Key: " + root.Thumbprint);
                }
            }
        }

        [TestMethod]
        [TestCategory("Integration")]
        public void GenerateCertificate_ParallelLeaves_ShareKeyButNotRootKey()
        {
            using (X509Certificate2 root = CertEnrollCertificateProvider.GenerateCertificate(null, null))
            {
                string sharedKeyName = "Clearinet CertEnroll Leaf Key: " + root.Thumbprint;
                Task<X509Certificate2>[] tasks = null;
                try
                {
                    var provider = new CertEnrollCertificateProvider(() => root,
                        CertEnrollCertificateProvider.GenerateCertificate);
                    tasks = new[]
                    {
                        Task.Run(() => provider.GetCertificateForHost("first.example")),
                        Task.Run(() => provider.GetCertificateForHost("second.example"))
                    };
                    Task.WaitAll(tasks);

                    X509Certificate2 first = tasks[0].Result;
                    X509Certificate2 second = tasks[1].Result;
                    Assert.AreNotEqual(first.Thumbprint, second.Thumbprint);
                    CollectionAssert.AreEqual(first.GetPublicKey(), second.GetPublicKey());
                    CollectionAssert.AreNotEqual(root.GetPublicKey(), first.GetPublicKey());
                    Assert.AreEqual("first.example", first.GetNameInfo(X509NameType.DnsName, false));
                    Assert.AreEqual("second.example", second.GetNameInfo(X509NameType.DnsName, false));
                    using (RSA rootKey = root.GetRSAPrivateKey())
                    using (RSA firstKey = first.GetRSAPrivateKey())
                    using (RSA secondKey = second.GetRSAPrivateKey())
                    {
                        Assert.AreEqual(3072, rootKey.KeySize);
                        Assert.AreEqual(2048, firstKey.KeySize);
                        byte[] data = { 1, 2, 3 };
                        byte[] signature = firstKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        Assert.IsTrue(secondKey.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
                        Assert.AreEqual(CngExportPolicies.None, ((RSACng)firstKey).Key.ExportPolicy);
                    }

                    Assert.ThrowsException<NullReferenceException>(() =>
                        CertEnrollCertificateProvider.GenerateCertificate(null, root));
                    Assert.IsTrue(CngKey.Exists(sharedKeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider));
                    using (var another = CertEnrollCertificateProvider.GenerateCertificate("third.example", root))
                    {
                        CollectionAssert.AreEqual(first.GetPublicKey(), another.GetPublicKey());
                    }
                }
                finally
                {
                    if (tasks != null)
                    {
                        foreach (var task in tasks)
                        {
                            if (task.Status == TaskStatus.RanToCompletion) task.Result.Dispose();
                        }
                    }
                    RemoveTestCertificates(root, sharedKeyName);
                }
            }
        }

        private static void RemoveTestCertificates(X509Certificate2 root, string sharedKeyName)
        {
            var keyNames = new HashSet<string>(StringComparer.Ordinal);
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);
                foreach (var certificate in store.Certificates.Cast<X509Certificate2>())
                {
                    using (certificate)
                    {
                        if (certificate.Thumbprint != root.Thumbprint &&
                            certificate.FriendlyName != "Clearinet CertEnroll Host: " + root.Thumbprint) continue;
                        using (RSA key = certificate.GetRSAPrivateKey())
                        {
                            if (key is RSACng cng) keyNames.Add(cng.Key.KeyName);
                        }
                        store.Remove(certificate);
                    }
                }
            }
            if (CngKey.Exists(sharedKeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider))
                keyNames.Add(sharedKeyName);
            foreach (string name in keyNames)
            {
                using (CngKey key = CngKey.Open(name, CngProvider.MicrosoftSoftwareKeyStorageProvider))
                {
                    key.Delete();
                }
            }
        }

        private static X509Certificate2 CreateCertificate(int validMinutes = 1440)
        {
            using (RSA key = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=Clearinet concurrency test", key,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),
                    DateTimeOffset.UtcNow.AddMinutes(validMinutes));
            }
        }
    }
}
