using System;
using System.Collections.Generic;

namespace Clearinet.CaptureEngine
{
    internal sealed class SocketPool : ISocketPool
    {
        private sealed class PooledPipe
        {
            internal readonly ServerPipe Pipe;
            internal readonly DateTime IdleSinceUtc;

            internal PooledPipe(ServerPipe pipe, DateTime idleSinceUtc)
            {
                Pipe = pipe;
                IdleSinceUtc = idleSinceUtc;
            }
        }

        private readonly object _syncRoot = new object();
        private readonly TimeSpan _idleLimit;
        private readonly TimeSpan _maxAge;
        private readonly Dictionary<ServerConnectionKey, Stack<PooledPipe>> _pool =
            new Dictionary<ServerConnectionKey, Stack<PooledPipe>>();

        internal SocketPool(TimeSpan idleLimit, TimeSpan maxAge)
        {
            _idleLimit = idleLimit < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : idleLimit;
            _maxAge = maxAge < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : maxAge;
        }

        public bool TryRent(ServerConnectionKey key, string processIdentity, Guid clientPipeId, out ServerPipe pipe)
        {
            pipe = null;
            lock (_syncRoot)
            {
                if (!_pool.TryGetValue(key, out Stack<PooledPipe> stack) || stack.Count == 0) return false;

                DateTime nowUtc = DateTime.UtcNow;
                Stack<PooledPipe> retained = new Stack<PooledPipe>();
                while (stack.Count > 0)
                {
                    PooledPipe candidate = stack.Pop();
                    if (!IsUsable(candidate, nowUtc))
                    {
                        candidate.Pipe.Dispose();
                        continue;
                    }

                    if (candidate.Pipe.CanBindTo(processIdentity, clientPipeId))
                    {
                        pipe = candidate.Pipe;
                        while (retained.Count > 0) stack.Push(retained.Pop());
                        if (stack.Count == 0) _pool.Remove(key);
                        return true;
                    }
                    retained.Push(candidate);
                }

                while (retained.Count > 0) stack.Push(retained.Pop());
                if (stack.Count == 0) _pool.Remove(key);
                return false;
            }
        }

        public void Return(ServerConnectionKey key, ServerPipe pipe, DateTime idleSinceUtc)
        {
            if (pipe == null) return;
            lock (_syncRoot)
            {
                if (!pipe.IsConnected)
                {
                    pipe.Dispose();
                    return;
                }

                if (!_pool.TryGetValue(key, out Stack<PooledPipe> stack))
                {
                    stack = new Stack<PooledPipe>();
                    _pool[key] = stack;
                }
                stack.Push(new PooledPipe(pipe, idleSinceUtc));
            }
        }

        public void PurgeExpired(DateTime nowUtc)
        {
            lock (_syncRoot)
            {
                List<ServerConnectionKey> empty = new List<ServerConnectionKey>();
                foreach (var entry in _pool)
                {
                    Stack<PooledPipe> stack = entry.Value;
                    Stack<PooledPipe> retained = new Stack<PooledPipe>();
                    while (stack.Count > 0)
                    {
                        PooledPipe candidate = stack.Pop();
                        if (IsUsable(candidate, nowUtc)) retained.Push(candidate);
                        else candidate.Pipe.Dispose();
                    }
                    while (retained.Count > 0) stack.Push(retained.Pop());
                    if (stack.Count == 0) empty.Add(entry.Key);
                }
                for (int i = 0; i < empty.Count; i++) _pool.Remove(empty[i]);
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                foreach (Stack<PooledPipe> stack in _pool.Values)
                    while (stack.Count > 0) stack.Pop().Pipe.Dispose();
                _pool.Clear();
            }
        }

        private bool IsUsable(PooledPipe pooledPipe, DateTime nowUtc)
        {
            return pooledPipe != null && pooledPipe.Pipe != null && pooledPipe.Pipe.IsConnected &&
                (nowUtc - pooledPipe.IdleSinceUtc) <= _idleLimit &&
                (nowUtc - pooledPipe.Pipe.CreatedUtc) <= _maxAge;
        }
    }
}
