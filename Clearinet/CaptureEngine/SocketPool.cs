using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Clearinet.CaptureEngine
{
    internal sealed class SocketPool : ISocketPool
    {
        private sealed class PooledSocket
        {
            internal readonly Socket Socket;
            internal readonly DateTime CreatedUtc;
            internal DateTime IdleSinceUtc;

            internal PooledSocket(Socket socket, DateTime idleSinceUtc)
            {
                Socket = socket;
                CreatedUtc = DateTime.UtcNow;
                IdleSinceUtc = idleSinceUtc;
            }
        }

        private readonly object _syncRoot = new object();
        private readonly TimeSpan _idleLimit;
        private readonly TimeSpan _maxAge;
        private readonly Dictionary<ServerConnectionKey, Stack<PooledSocket>> _pool =
            new Dictionary<ServerConnectionKey, Stack<PooledSocket>>();

        internal SocketPool(TimeSpan idleLimit, TimeSpan maxAge)
        {
            _idleLimit = idleLimit < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : idleLimit;
            _maxAge = maxAge < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : maxAge;
        }

        public bool TryRent(ServerConnectionKey key, out Socket socket)
        {
            socket = null;
            lock (_syncRoot)
            {
                if (!_pool.TryGetValue(key, out Stack<PooledSocket> stack) || stack.Count == 0)
                {
                    return false;
                }

                DateTime nowUtc = DateTime.UtcNow;
                while (stack.Count > 0)
                {
                    PooledSocket candidate = stack.Pop();
                    if (IsUsable(candidate, nowUtc))
                    {
                        socket = candidate.Socket;
                        return true;
                    }

                    CloseQuietly(candidate.Socket);
                }

                if (stack.Count == 0)
                {
                    _pool.Remove(key);
                }

                return false;
            }
        }

        public void Return(ServerConnectionKey key, Socket socket, DateTime idleSinceUtc)
        {
            if (socket == null) return;
            lock (_syncRoot)
            {
                if (!IsSocketConnected(socket))
                {
                    CloseQuietly(socket);
                    return;
                }

                if (!_pool.TryGetValue(key, out Stack<PooledSocket> stack))
                {
                    stack = new Stack<PooledSocket>();
                    _pool[key] = stack;
                }

                stack.Push(new PooledSocket(socket, idleSinceUtc));
            }
        }

        public void PurgeExpired(DateTime nowUtc)
        {
            lock (_syncRoot)
            {
                List<ServerConnectionKey> empty = new List<ServerConnectionKey>();
                foreach (var kvp in _pool)
                {
                    Stack<PooledSocket> original = kvp.Value;
                    if (original.Count == 0)
                    {
                        empty.Add(kvp.Key);
                        continue;
                    }

                    Stack<PooledSocket> retained = new Stack<PooledSocket>(original.Count);
                    while (original.Count > 0)
                    {
                        PooledSocket ps = original.Pop();
                        if (IsUsable(ps, nowUtc))
                        {
                            retained.Push(ps);
                        }
                        else
                        {
                            CloseQuietly(ps.Socket);
                        }
                    }

                    if (retained.Count == 0)
                    {
                        empty.Add(kvp.Key);
                        continue;
                    }

                    while (retained.Count > 0)
                    {
                        original.Push(retained.Pop());
                    }
                }

                for (int i = 0; i < empty.Count; i++)
                {
                    _pool.Remove(empty[i]);
                }
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                foreach (var kvp in _pool)
                {
                    foreach (PooledSocket ps in kvp.Value)
                    {
                        CloseQuietly(ps.Socket);
                    }
                }
                _pool.Clear();
            }
        }

        private bool IsUsable(PooledSocket ps, DateTime nowUtc)
        {
            if (ps == null || ps.Socket == null) return false;
            if (!IsSocketConnected(ps.Socket)) return false;
            if ((nowUtc - ps.IdleSinceUtc) > _idleLimit) return false;
            if ((nowUtc - ps.CreatedUtc) > _maxAge) return false;
            return true;
        }

        private static bool IsSocketConnected(Socket socket)
        {
            try
            {
                if (socket == null || !socket.Connected) return false;
                bool readyToRead = socket.Poll(0, SelectMode.SelectRead);
                bool noData = (socket.Available == 0);
                return !(readyToRead && noData);
            }
            catch
            {
                return false;
            }
        }

        private static void CloseQuietly(Socket socket)
        {
            if (socket == null) return;
            try { socket.Close(); } catch { }
        }
    }
}
