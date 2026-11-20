using Clearinet.CaptureEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Clearinet
{
    public delegate bool DnsLookupHandler(string host, out IPAddress[] addresses);

    public static class DnsCache
    {
        private sealed class DnsCacheEntry
        {
            internal DnsCacheEntry(IPAddress[] addresses, DateTime expiresUtc)
            {
                Addresses = addresses ?? Array.Empty<IPAddress>();
                ExpiresUtc = expiresUtc;
            }

            internal IPAddress[] Addresses { get; }
            internal DateTime ExpiresUtc { get; }
        }

        private static readonly ConcurrentDictionary<string, DnsCacheEntry> _cache =
            new ConcurrentDictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, Task<IPAddress[]>> _inFlight =
            new ConcurrentDictionary<string, Task<IPAddress[]>>(StringComparer.OrdinalIgnoreCase);

        public static event DnsLookupHandler OnLookup;

        /// <summary>
        /// Return the list of IP Addresses for the target hostname
        /// </summary>
        /// <param name="host"></param>
        /// <returns></returns>
        public static async Task<IPAddress[]> LookupAsync(string hostname)
        {
            string normalizedHost = NormalizeHost(hostname);

            if (TryLookupFromExtension(normalizedHost, out IPAddress[] overrideAddresses))
            {
                return RemoveIPv6IfDisabled(overrideAddresses);
            }

            // This is ReplaceVirtualHostnames in the App. TODO: Offer a preference to disable built-in hostnames.
            //if (hostname.OICEquals("localhost.fiddler")) return new IPAddress[] { IPAddress.IPv6Loopback, IPAddress.Loopback };
            //if (hostname.OICEquals("ipv4.fiddler")) return new IPAddress[] { IPAddress.Loopback };
            //if (hostname.OICEquals("ipv6.fiddler")) return new IPAddress[] { IPAddress.IPv6Loopback };

            if (TryGetCached(normalizedHost, out IPAddress[] cachedAddresses))
            {
                return RemoveIPv6IfDisabled(cachedAddresses);
            }

            Task<IPAddress[]> createdTask = null;
            Task<IPAddress[]> lookupTask = _inFlight.GetOrAdd(normalizedHost, key =>
            {
                createdTask = ResolveAndCacheAsync(key);
                return createdTask;
            });

            bool ownsInFlightEntry = ReferenceEquals(createdTask, lookupTask);
            return await AwaitLookupAndReleaseAsync(normalizedHost, lookupTask, ownsInFlightEntry).ConfigureAwait(false);
        }

        public static string ListCacheContents()
        {
            PurgeExpiredEntries();
            var snapshot = _cache.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase).ToArray();
            var sb = new StringBuilder();
            const int HostColumnWidth = 50;
            const int TtlColumnWidth = 6;

            sb.AppendLine($" {"Hostname".PadRight(HostColumnWidth)} | {"TTLms".PadLeft(TtlColumnWidth)} | Addresses");
            sb.AppendLine($"-{new string('-', HostColumnWidth)}-|-{new string('-', TtlColumnWidth)}-|-{new string('-', 30)}");

            DateTime nowUtc = DateTime.UtcNow;
            if (snapshot.Length == 0)
            {
                sb.Append("<empty>");
                return sb.ToString();
            }

            foreach (KeyValuePair<string, DnsCacheEntry> item in snapshot)
            {
                long ttlMs = Math.Max(0, (long)(item.Value.ExpiresUtc - nowUtc).TotalMilliseconds);
                string addressList = string.Join(", ", item.Value.Addresses.Select(ip => ip.ToString()));
                string hostDisplay = item.Key.Ellipsize(HostColumnWidth);

                sb.Append(" ").Append(hostDisplay.PadRight(HostColumnWidth))
                  .Append(" | ").Append(ttlMs.ToString().PadLeft(TtlColumnWidth))
                  .Append(" | ").AppendLine(addressList);
            }

            return sb.ToString().TrimEnd();
        }

        public static void ClearAll()
        {
            _cache.Clear();
        }

        public static bool Remove(string host)
        {
            string normalizedHost = NormalizeHost(host);
            return _cache.TryRemove(normalizedHost, out _);
        }

        private static async Task<IPAddress[]> AwaitLookupAndReleaseAsync(string host, Task<IPAddress[]> lookupTask, bool ownsInFlightEntry)
        {
            try
            {
                IPAddress[] addresses = await lookupTask.ConfigureAwait(false);
                return RemoveIPv6IfDisabled(addresses);
            }
            finally
            {
                if (ownsInFlightEntry)
                {
                    _inFlight.TryRemove(host, out _);
                }
            }
        }

        private static async Task<IPAddress[]> ResolveAndCacheAsync(string host)
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
            IPAddress[] cloned = CloneAddresses(addresses);

            int lifetimeMs = CaptureEnginePrefs.GetDnsCacheLifetimeMs();
            if (lifetimeMs > 0)
            {
                DateTime expiresUtc = DateTime.UtcNow.AddMilliseconds(lifetimeMs);
                _cache[host] = new DnsCacheEntry(cloned, expiresUtc);
            }
            else
            {
                _cache.TryRemove(host, out _);
            }

            return cloned;
        }

        private static bool TryLookupFromExtension(string host, out IPAddress[] addresses)
        {
            addresses = null;
            DnsLookupHandler handler = OnLookup;
            if (handler == null) return false;

            foreach (DnsLookupHandler lookupHandler in handler.GetInvocationList())
            {
                try
                {
                    if (!lookupHandler(host, out IPAddress[] provided) || provided == null) continue;
                    addresses = provided;
                    return true;
                }
                catch (Exception ex)
                {
                    CApp.Log.Log($"DNS extension lookup failed for '{host}': {ex.Message}");
                }
            }

            return false;
        }

        private static bool TryGetCached(string host, out IPAddress[] addresses)
        {
            addresses = null;
            if (!_cache.TryGetValue(host, out DnsCacheEntry entry)) return false;
            if (entry.ExpiresUtc <= DateTime.UtcNow)
            {
                _cache.TryRemove(host, out _);
                return false;
            }

            addresses = CloneAddresses(entry.Addresses);
            return true;
        }

        private static void PurgeExpiredEntries()
        {
            DateTime nowUtc = DateTime.UtcNow;
            foreach (KeyValuePair<string, DnsCacheEntry> item in _cache)
            {
                if (item.Value.ExpiresUtc <= nowUtc)
                {
                    _cache.TryRemove(item.Key, out _);
                }
            }
        }

        private static string NormalizeHost(string host)
        {
            string normalized = (host ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                throw new ArgumentException("Host must be provided.", nameof(host));
            }

            return normalized;
        }

        private static IPAddress[] CloneAddresses(IPAddress[] addresses)
        {
            if (addresses == null || addresses.Length == 0) return Array.Empty<IPAddress>();
            var clone = new IPAddress[addresses.Length];
            Array.Copy(addresses, clone, addresses.Length);
            return clone;
        }

        private static IPAddress[] RemoveIPv6IfDisabled(IPAddress[] addresses)
        {
            IPAddress[] cloned = CloneAddresses(addresses);
            if (CaptureEnginePrefs.IsIPv6Enabled()) return cloned;

            return cloned.Where(ip => ip?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToArray();
        }
    }
}
