using System;
using System.Net;
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
        public void SocketPool_ReturnThenRent_RentsSameSocket()
        {
            Socket listener = null;
            Socket client = null;
            Socket server = null;
            var pool = new SocketPool(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            try
            {
                CreateConnectedSockets(out listener, out client, out server);
                var key = new ServerConnectionKey("localhost", ((IPEndPoint)listener.LocalEndPoint).Port, false);

                pool.Return(key, client, DateTime.UtcNow);
                client = null;

                Assert.IsTrue(pool.TryRent(key, out Socket rented));
                Assert.IsNotNull(rented);
                client = rented;
                Assert.IsFalse(pool.TryRent(key, out _));
            }
            finally
            {
                pool.Dispose();
                client?.Dispose();
                server?.Dispose();
                listener?.Dispose();
            }
        }

        [TestMethod]
        public void SocketPool_PurgeExpired_RemovesExpiredSocket()
        {
            Socket listener = null;
            Socket client = null;
            Socket server = null;
            var pool = new SocketPool(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));
            try
            {
                CreateConnectedSockets(out listener, out client, out server);
                var key = new ServerConnectionKey("localhost", ((IPEndPoint)listener.LocalEndPoint).Port, false);

                pool.Return(key, client, DateTime.UtcNow.AddSeconds(-6));
                client = null;
                pool.PurgeExpired(DateTime.UtcNow);

                Assert.IsFalse(pool.TryRent(key, out _));
            }
            finally
            {
                pool.Dispose();
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
