using Clearinet.CaptureEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureEngine.Tests
{
    [TestClass]
    public sealed class UpgradedTunnelTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task RelayUpgradedConnectionAsync_ForwardsBinaryBytesInBothDirections(bool useTls)
        {
            using (var connection = await TunnelConnection.CreateAsync(useTls))
            {
                Task relay = TcpCaptureEngineListener.RelayUpgradedConnectionAsync(
                    connection.ClientPipe, connection.ServerPipe, CancellationToken.None);
                byte[] fromClient = Enumerable.Range(0, 20000).Select(i => (byte)i).ToArray();
                byte[] fromServer = Enumerable.Range(0, 20001).Select(i => (byte)(255 - i)).ToArray();
                await connection.ClientStream.WriteAsync(fromClient, 0, fromClient.Length);
                await connection.ServerStream.WriteAsync(fromServer, 0, fromServer.Length);

                Task<byte[]> receivedByServer = ReadExactlyAsync(connection.ServerStream, fromClient.Length);
                Task<byte[]> receivedByClient = ReadExactlyAsync(connection.ClientStream, fromServer.Length);
                await CompletesAsync(Task.WhenAll(receivedByServer, receivedByClient));
                CollectionAssert.AreEqual(fromClient, await receivedByServer);
                CollectionAssert.AreEqual(fromServer, await receivedByClient);
                Assert.IsFalse(relay.IsCompleted, "The tunnel must remain open after forwarding messages.");

                connection.Client.Dispose();
                await CompletesAsync(relay);
                Assert.IsFalse(connection.ClientPipe.IsConnected);
                Assert.IsFalse(connection.ServerPipe.IsConnected);
            }
        }

        [DataTestMethod]
        [DataRow("client", false)]
        [DataRow("server", false)]
        [DataRow("cancel", false)]
        [DataRow("client", true)]
        [DataRow("server", true)]
        [DataRow("cancel", true)]
        public async Task RelayUpgradedConnectionAsync_ClosesBothPipesAndFinishesPendingReads(string shutdown, bool useTls)
        {
            using (var connection = await TunnelConnection.CreateAsync(useTls))
            using (var cancellation = new CancellationTokenSource())
            {
                Task relay = TcpCaptureEngineListener.RelayUpgradedConnectionAsync(
                    connection.ClientPipe, connection.ServerPipe, cancellation.Token);
                if (shutdown == "client") connection.Client.Dispose();
                else if (shutdown == "server") connection.Server.Dispose();
                else cancellation.Cancel();

                await CompletesAsync(relay);
                Assert.IsFalse(connection.ClientPipe.IsConnected, "The upgraded client pipe must not be reusable.");
                Assert.IsFalse(connection.ServerPipe.IsConnected, "The upgraded server pipe must not be reusable.");
                Assert.ThrowsException<ObjectDisposedException>(() => connection.ClientPipe.Stream.ReadByte());
                Assert.ThrowsException<ObjectDisposedException>(() => connection.ServerPipe.Stream.ReadByte());
            }
        }

        private static async Task CompletesAsync(Task task)
        {
            Assert.AreSame(task, await Task.WhenAny(task, Task.Delay(5000)), "Tunnel operation timed out.");
            await task;
        }

        private static async Task<byte[]> ReadExactlyAsync(Stream stream, int count)
        {
            byte[] bytes = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(bytes, offset, count - offset);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
            return bytes;
        }

        private sealed class TunnelConnection : IDisposable
        {
            internal TcpClient Client;
            internal TcpClient Server;
            internal Stream ClientStream;
            internal Stream ServerStream;
            internal ClientPipe ClientPipe;
            internal ServerPipe ServerPipe;
            private TcpClient _acceptedClient;
            private TcpClient _acceptedServer;
            private Stream _proxyClientStream;
            private Stream _proxyServerStream;

            internal static async Task<TunnelConnection> CreateAsync(bool useTls)
            {
                var connection = new TunnelConnection();
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                try
                {
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    connection.Client = new TcpClient();
                    await connection.Client.ConnectAsync(IPAddress.Loopback, port);
                    connection._acceptedClient = await listener.AcceptTcpClientAsync();
                    connection.ClientStream = connection.Client.GetStream();
                    connection._proxyClientStream = connection._acceptedClient.GetStream();

                    connection.Server = new TcpClient();
                    await connection.Server.ConnectAsync(IPAddress.Loopback, port);
                    connection._acceptedServer = await listener.AcceptTcpClientAsync();
                    connection.ServerStream = connection.Server.GetStream();
                    connection._proxyServerStream = connection._acceptedServer.GetStream();
                    if (useTls)
                    {
                        var clientTls = new SslStream(connection.ClientStream, false, (sender, cert, chain, errors) => true);
                        var proxyClientTls = new SslStream(connection._proxyClientStream, false);
                        var serverTls = new SslStream(connection.ServerStream, false);
                        var proxyServerTls = new SslStream(connection._proxyServerStream, false, (sender, cert, chain, errors) => true);
                        connection.ClientStream = clientTls;
                        connection._proxyClientStream = proxyClientTls;
                        connection.ServerStream = serverTls;
                        connection._proxyServerStream = proxyServerTls;
                        using (RSA key = RSA.Create(2048))
                        {
                            var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            using (var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5)))
                            using (var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx)))
                            {
                                const System.Security.Authentication.SslProtocols protocols = System.Security.Authentication.SslProtocols.Tls12;
                                await CompletesAsync(Task.WhenAll(
                                    clientTls.AuthenticateAsClientAsync("localhost", null, protocols, false),
                                    proxyClientTls.AuthenticateAsServerAsync(certificate, false, protocols, false),
                                    serverTls.AuthenticateAsServerAsync(certificate, false, protocols, false),
                                    proxyServerTls.AuthenticateAsClientAsync("localhost", null, protocols, false)));
                            }
                        }
                    }
                    connection.ClientPipe = new ClientPipe(connection._acceptedClient.Client, connection._proxyClientStream, "test");
                    connection.ServerPipe = new ServerPipe(connection._acceptedServer.Client, connection._proxyServerStream,
                        ServerPipeBindingPolicy.Free, "test", connection.ClientPipe.Id);
                    return connection;
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
                finally
                {
                    listener.Stop();
                }
            }

            public void Dispose()
            {
                ClientPipe?.Dispose();
                ServerPipe?.Dispose();
                ClientStream?.Dispose();
                ServerStream?.Dispose();
                _proxyClientStream?.Dispose();
                _proxyServerStream?.Dispose();
                _acceptedClient?.Dispose();
                _acceptedServer?.Dispose();
                Client?.Dispose();
                Server?.Dispose();
            }
        }
    }
}
