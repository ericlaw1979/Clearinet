using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Clearinet.CaptureEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CaptureEngine.Tests
{
    [TestClass]
    public sealed class SocketPoolTests
    {
        [TestMethod]
        public void ServerConnectionKey_CaseInsensitiveEquality_Works()
        {
            var first = new ServerConnectionKey("Example.COM", 443, true);
            var second = new ServerConnectionKey("example.com", 443, true);

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [TestMethod]
        public void SocketPool_ReturnThenRent_RentsSameServerPipe()
        {
            Socket listener = null;
            Socket client = null;
            Socket server = null;
            ServerPipe pipe = null;
            var pool = new SocketPool(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            try
            {
                CreateConnectedSockets(out listener, out client, out server);
                var key = new ServerConnectionKey("localhost", ((IPEndPoint)listener.LocalEndPoint).Port, false);
                Guid clientPipeId = Guid.NewGuid();
                pipe = new ServerPipe(client, new NetworkStream(client, false),
                    ServerPipeBindingPolicy.Process, "browser:123", clientPipeId);
                client = null;

                pool.Return(key, pipe, DateTime.UtcNow);
                pipe = null;

                Assert.IsTrue(pool.TryRent(key, "browser:123", Guid.NewGuid(), out ServerPipe rented));
                Assert.IsNotNull(rented);
                Assert.AreEqual(ServerPipeBindingPolicy.Process, rented.BindingPolicy);
                pipe = rented;
                Assert.IsFalse(pool.TryRent(key, "browser:123", Guid.NewGuid(), out _));
            }
            finally
            {
                pool.Dispose();
                pipe?.Dispose();
                client?.Dispose();
                server?.Dispose();
                listener?.Dispose();
            }
        }

        [TestMethod]
        public void SocketPool_RespectsBindingPolicy()
        {
            AssertCanRent(ServerPipeBindingPolicy.Free, "process-a", Guid.NewGuid(), "process-b", Guid.NewGuid(), true);
            AssertCanRent(ServerPipeBindingPolicy.Process, "process-a", Guid.NewGuid(), "process-b", Guid.NewGuid(), false);
            AssertCanRent(ServerPipeBindingPolicy.Process, "process-a", Guid.NewGuid(), "process-a", Guid.NewGuid(), true);
            AssertCanRent(ServerPipeBindingPolicy.Process, string.Empty, Guid.NewGuid(), string.Empty, Guid.NewGuid(), false);
            Guid boundClientId = Guid.NewGuid();
            AssertCanRent(ServerPipeBindingPolicy.PipeBound, "process-a", boundClientId, "process-a", Guid.NewGuid(), false);
            AssertCanRent(ServerPipeBindingPolicy.PipeBound, "process-a", boundClientId, "process-b", boundClientId, true);
        }

        [TestMethod]
        public void ClientPipe_IdentityReflectsTlsWrapping()
        {
            Socket listener = null;
            Socket client = null;
            Socket server = null;
            ClientPipe pipe = null;
            try
            {
                CreateConnectedSockets(out listener, out client, out server);
                NetworkStream networkStream = new NetworkStream(client, false);
                pipe = new ClientPipe(client, networkStream, "browser:123");
                client = null;
                PipeIdentity tcpIdentity = pipe.Identity;

                pipe.SetStream(new SslStream(networkStream, false));

                Assert.AreEqual(tcpIdentity.Id, pipe.Identity.Id);
                Assert.IsFalse(tcpIdentity.IsTls);
                Assert.IsTrue(pipe.Identity.IsTls);
                Assert.AreNotEqual(tcpIdentity, pipe.Identity);
            }
            finally
            {
                pipe?.Dispose();
                client?.Dispose();
                server?.Dispose();
                listener?.Dispose();
            }
        }

        [TestMethod]
        public void SocketPool_PurgeExpired_RemovesExpiredPipe()
        {
            Socket listener = null;
            Socket client = null;
            Socket server = null;
            ServerPipe pipe = null;
            var pool = new SocketPool(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));
            try
            {
                CreateConnectedSockets(out listener, out client, out server);
                var key = new ServerConnectionKey("localhost", ((IPEndPoint)listener.LocalEndPoint).Port, false);
                pipe = new ServerPipe(client, new NetworkStream(client, false),
                    ServerPipeBindingPolicy.Free, string.Empty, Guid.NewGuid());
                client = null;
                pool.Return(key, pipe, DateTime.UtcNow.AddSeconds(-6));
                pipe = null;
                pool.PurgeExpired(DateTime.UtcNow);

                Assert.IsFalse(pool.TryRent(key, string.Empty, Guid.NewGuid(), out _));
            }
            finally
            {
                pool.Dispose();
                pipe?.Dispose();
                client?.Dispose();
                server?.Dispose();
                listener?.Dispose();
            }
        }

        private static void AssertCanRent(ServerPipeBindingPolicy policy, string ownerProcess, Guid ownerClient,
            string requestProcess, Guid requestClient, bool expected)
        {
            Socket listener = null;
            Socket client = null;
            Socket server = null;
            ServerPipe pipe = null;
            var pool = new SocketPool(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            try
            {
                CreateConnectedSockets(out listener, out client, out server);
                var key = new ServerConnectionKey("localhost", ((IPEndPoint)listener.LocalEndPoint).Port, false);
                pipe = new ServerPipe(client, new NetworkStream(client, false), policy, ownerProcess, ownerClient);
                client = null;
                pool.Return(key, pipe, DateTime.UtcNow);
                pipe = null;

                Assert.AreEqual(expected, pool.TryRent(key, requestProcess, requestClient, out pipe));
                if (expected) Assert.IsNotNull(pipe);
            }
            finally
            {
                pool.Dispose();
                pipe?.Dispose();
                client?.Dispose();
                server?.Dispose();
                listener?.Dispose();
            }
        }

        private static void CreateConnectedSockets(out Socket listener, out Socket client, out Socket server)
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(listener.LocalEndPoint);
            server = listener.Accept();
        }
    }
}
