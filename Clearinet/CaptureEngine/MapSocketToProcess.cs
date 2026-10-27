using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;

namespace Clearinet.CaptureEngine
{
    public sealed class MapSocketToProcess
    {
        private const int AfInet = 2;
        private const int AfInet6 = 23;
        // private const int TcpTableOwnerPidAll = 5; // Only if you need Listeners.
        private const int TcpTableOwnerPidConnections = 4;
        private const uint ErrorInsufficientBuffer = 122;
        private const int MaximumTableReadAttempts = 3;

        // The table of connections grows constantly, so we add a small amount of extra
        // headroom to avoid repeated reallocations.
        private const int BufferGrowthHeadroom = 2048;
        private const uint AmbiguousOwner = uint.MaxValue;

        private readonly object _syncRoot = new object();
        private readonly double _cacheDurationTicks;

        /// <summary>
        /// TODO: We want a smarter cache of process names here; it should have a
        /// cache duration of a different length (likely 30 seconds or something), and
        /// it should be smart enough to disambiguate owner information in cases
        /// where the target is a servicehost or UWP app process.
        /// </summary>
        private readonly Dictionary<uint, string> _processNames = new Dictionary<uint, string>();
        private readonly TcpTableSnapshot _ipv4Table = new TcpTableSnapshot();
        private readonly TcpTableSnapshot _ipv6Table = new TcpTableSnapshot();

        private sealed class TcpTableSnapshot
        {
            internal readonly Dictionary<EndpointKey, uint> Owners = new Dictionary<EndpointKey, uint>();
            internal byte[] Buffer = Array.Empty<byte>();
            internal bool HasSnapshot;
            internal long FetchedAt;
        }

        public MapSocketToProcess() : this(TimeSpan.FromSeconds(10)) { }

        public MapSocketToProcess(TimeSpan cacheDuration)
        {
            if (cacheDuration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheDuration));
            }

            _cacheDurationTicks = cacheDuration.TotalSeconds * Stopwatch.Frequency;
        }


        // BUG BUG: TODO: Windows API failures are propagated to the caller.
        /// <summary>
        /// Returns processname:PID for the local client identified by an inbound connection's RemoteEndPoint.
        /// Matches only the TCP table's local endpoint; never attributes the connection to its accepting server.
        /// Returns null for an unknown, exited, or ambiguous owner.
        /// Reuse this instance to cache TCP ownership by address family and process names for the constructor-specified duration.
        /// A missing endpoint in a cached table triggers one refresh of that address family's table and a retry.
        /// IPv4-mapped IPv6 endpoints use the IPv4 table. A zero duration disables caching.
        /// </summary>
        public string MapSocketToOriginatingProcess(IPEndPoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            lock (_syncRoot)
            {
                IPAddress address = endpoint.Address;
                if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
                bool isIpv6 = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
                TcpTableSnapshot table = isIpv6 ? _ipv6Table : _ipv4Table;
                bool refreshed = EnsureTcpTableIsCurrent(table, isIpv6);
                EndpointKey key = new EndpointKey(address.GetAddressBytes(), 0, isIpv6,
                    isIpv6 ? (uint)address.ScopeId : 0, endpoint.Port);
                uint processId;
                if (!table.Owners.TryGetValue(key, out processId))
                {
                    if (refreshed) return null;
                    EnsureTcpTableIsCurrent(table, isIpv6, true);
                    if (!table.Owners.TryGetValue(key, out processId)) return null;
                }
                if (processId == AmbiguousOwner) return null;

                string processName;
                if (!_processNames.TryGetValue(processId, out processName))
                {
                    try
                    {
                        using (Process process = Process.GetProcessById(checked((int)processId)))
                        {
                            processName = process.ProcessName + ":" + processId.ToString(CultureInfo.InvariantCulture);
                        }
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                    catch (InvalidOperationException)
                    {
                        return null;
                    }

                    _processNames.Add(processId, processName);
                }

                return processName;
            }
        }

        private bool EnsureTcpTableIsCurrent(TcpTableSnapshot table, bool isIpv6, bool forceRefresh = false)
        {
            if (!forceRefresh && table.HasSnapshot && Stopwatch.GetTimestamp() - table.FetchedAt < _cacheDurationTicks)
            {
                return false;
            }

            // TODO: Consider using GetExtendedTcpTable with the TCP_TABLE_OWNER_MODULE_ALL
            int rowCount = ReadTcpTable(ref table.Buffer, isIpv6 ? AfInet6 : AfInet, isIpv6 ? 56 : 24);

            table.HasSnapshot = false;
            table.Owners.Clear();
            IndexTcpTable(table.Owners, table.Buffer, rowCount, isIpv6);
            _processNames.Clear();
            table.FetchedAt = Stopwatch.GetTimestamp();
            table.HasSnapshot = true;
            return true;
        }

        private static int ReadTcpTable(ref byte[] buffer, int addressFamily, int rowSize)
        {
            for (int attempt = 0; attempt < MaximumTableReadAttempts; attempt++)
            {
                int bufferSize = buffer.Length;
                uint result;
                GCHandle pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    result = GetExtendedTcpTable(pinnedBuffer.AddrOfPinnedObject(), ref bufferSize, 0,
                        addressFamily, TcpTableOwnerPidConnections, 0);
                }
                finally
                {
                    pinnedBuffer.Free();
                }

                if (result == ErrorInsufficientBuffer)
                {
                    buffer = new byte[checked(bufferSize + BufferGrowthHeadroom)];
                    continue;
                }

                if (result != 0)
                {
                    throw new Win32Exception((int)result);
                }

                if (bufferSize < sizeof(uint) || bufferSize > buffer.Length)
                {
                    throw new InvalidOperationException("The Windows TCP table returned an invalid buffer size.");
                }

                uint rowCount = BitConverter.ToUInt32(buffer, 0);
                if (rowCount > (bufferSize - sizeof(uint)) / rowSize)
                {
                    throw new InvalidOperationException("The Windows TCP table returned an invalid row count.");
                }

                return (int)rowCount;
            }

            throw new Win32Exception((int)ErrorInsufficientBuffer, "The TCP table changed repeatedly while it was being read.");
        }

        private static void IndexTcpTable(Dictionary<EndpointKey, uint> owners, byte[] buffer, int rowCount, bool isIpv6)
        {
            int rowSize = isIpv6 ? 56 : 24;
            for (int index = 0, offset = sizeof(uint); index < rowCount; index++, offset += rowSize)
            {
                EndpointKey localEndpoint;
                uint processId;
                if (isIpv6)
                {
                    localEndpoint = new EndpointKey(buffer, offset, true,
                        BitConverter.ToUInt32(buffer, offset + 16), ReadPort(buffer, offset + 20));
                    processId = BitConverter.ToUInt32(buffer, offset + 52);
                }
                else
                {
                    localEndpoint = new EndpointKey(buffer, offset + 4, false, 0, ReadPort(buffer, offset + 8));
                    processId = BitConverter.ToUInt32(buffer, offset + 20);
                }

                AddOwner(owners, localEndpoint, processId);
            }
        }

        private static void AddOwner(Dictionary<EndpointKey, uint> owners, EndpointKey endpoint, uint processId)
        {
            uint existingOwner;
            if (owners.TryGetValue(endpoint, out existingOwner))
            {
                if (existingOwner != processId && existingOwner != AmbiguousOwner)
                {
                    owners[endpoint] = AmbiguousOwner;
                }
            }
            else
            {
                owners.Add(endpoint, processId);
            }
        }

        private static int ReadPort(byte[] buffer, int offset)
        {
            return (buffer[offset] << 8) | buffer[offset + 1];
        }

        [DllImport("iphlpapi.dll", SetLastError = false)]
        private static extern uint GetExtendedTcpTable(
            IntPtr tcpTable,
            ref int size,
            int order,
            int ipVersion,
            int tableClass,
            uint reserved);

        private struct EndpointKey : IEquatable<EndpointKey>
        {
            private readonly uint _address0;
            private readonly uint _address1;
            private readonly uint _address2;
            private readonly uint _address3;
            private readonly uint _scopeId;
            private readonly int _port;
            private readonly bool _isIpv6;

            public EndpointKey(byte[] buffer, int offset, bool isIpv6, uint scopeId, int port)
            {
                uint address0 = BitConverter.ToUInt32(buffer, offset);
                uint address1 = isIpv6 ? BitConverter.ToUInt32(buffer, offset + 4) : 0;
                uint address2 = isIpv6 ? BitConverter.ToUInt32(buffer, offset + 8) : 0;
                uint address3 = isIpv6 ? BitConverter.ToUInt32(buffer, offset + 12) : 0;

                // Windows is little-endian; normalize ::ffff:a.b.c.d to the IPv4 key.
                if (isIpv6 && address0 == 0 && address1 == 0 && address2 == 0xffff0000)
                {
                    address0 = address3;
                    address1 = address2 = address3 = 0;
                    scopeId = 0;
                    isIpv6 = false;
                }

                _address0 = address0;
                _address1 = address1;
                _address2 = address2;
                _address3 = address3;
                _scopeId = scopeId;
                _port = port;
                _isIpv6 = isIpv6;
            }

            public bool Equals(EndpointKey other)
            {
                return _address0 == other._address0 && _address1 == other._address1
                    && _address2 == other._address2 && _address3 == other._address3
                    && _scopeId == other._scopeId && _port == other._port && _isIpv6 == other._isIpv6;
            }

            public override bool Equals(object obj)
            {
                return obj is EndpointKey && Equals((EndpointKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)_address0;
                    hash = (hash * 397) ^ (int)_address1;
                    hash = (hash * 397) ^ (int)_address2;
                    hash = (hash * 397) ^ (int)_address3;
                    hash = (hash * 397) ^ (int)_scopeId;
                    hash = (hash * 397) ^ _port;
                    return (hash * 397) ^ (_isIpv6 ? 1 : 0);
                }
            }
        }
    }
}
