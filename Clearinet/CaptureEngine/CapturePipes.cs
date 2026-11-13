using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Clearinet.CaptureEngine
{
    internal enum ServerPipeBindingPolicy
    {
        Free,
        Process,
        PipeBound
    }

    internal struct PipeIdentity : IEquatable<PipeIdentity>
    {
        internal PipeIdentity(Guid id, bool isTls)
        {
            Id = id;
            IsTls = isTls;
        }

        internal Guid Id { get; private set; }
        internal bool IsTls { get; private set; }

        public bool Equals(PipeIdentity other)
        {
            return Id.Equals(other.Id) && IsTls == other.IsTls;
        }

        public override bool Equals(object obj)
        {
            return obj is PipeIdentity && Equals((PipeIdentity)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return (Id.GetHashCode() * 397) ^ IsTls.GetHashCode(); }
        }

        public override string ToString()
        {
            return Id.ToString("N") + (IsTls ? "/tls" : "/tcp");
        }
    }

    internal sealed class ClientPipe : IDisposable
    {
        private bool _disposed;

        internal ClientPipe(Socket socket, Stream stream, string processIdentity)
        {
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            ProcessIdentity = processIdentity ?? string.Empty;
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            Reader = new HttpStreamReader(stream);
        }

        internal Guid Id { get; private set; }
        internal PipeIdentity Identity { get { return new PipeIdentity(Id, Stream is System.Net.Security.SslStream); } }
        internal Socket Socket { get; private set; }
        internal Stream Stream { get; private set; }
        internal HttpStreamReader Reader { get; private set; }
        internal string ProcessIdentity { get; private set; }
        internal DateTime CreatedUtc { get; private set; }
        internal bool IsConnected { get { return CapturePipeSocket.IsConnected(Socket); } }

        internal void SetStream(Stream stream)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ClientPipe));
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Reader = new HttpStreamReader(stream);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Stream.Dispose(); } finally { Socket.Dispose(); }
        }
    }

    internal sealed class ServerPipe : IDisposable
    {
        private bool _disposed;

        internal ServerPipe(Socket socket, Stream stream, ServerPipeBindingPolicy bindingPolicy,
            string processIdentity, Guid clientPipeId)
        {
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            BindingPolicy = bindingPolicy;
            ProcessIdentity = processIdentity ?? string.Empty;
            ClientPipeId = clientPipeId;
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            Reader = new HttpStreamReader(stream);
        }

        internal Guid Id { get; private set; }
        internal PipeIdentity Identity { get { return new PipeIdentity(Id, Stream is System.Net.Security.SslStream); } }
        internal Socket Socket { get; private set; }
        internal Stream Stream { get; private set; }
        internal HttpStreamReader Reader { get; private set; }
        internal ServerPipeBindingPolicy BindingPolicy { get; private set; }
        internal string ProcessIdentity { get; private set; }
        internal Guid ClientPipeId { get; private set; }
        internal DateTime CreatedUtc { get; private set; }
        internal bool IsConnected { get { return CapturePipeSocket.IsConnected(Socket); } }

        internal bool CanBindTo(string processIdentity, Guid clientPipeId)
        {
            switch (BindingPolicy)
            {
                case ServerPipeBindingPolicy.Free:
                    return true;
                case ServerPipeBindingPolicy.Process:
                    return !string.IsNullOrWhiteSpace(ProcessIdentity) &&
                        string.Equals(ProcessIdentity, processIdentity, StringComparison.Ordinal);
                case ServerPipeBindingPolicy.PipeBound:
                    return ClientPipeId == clientPipeId;
                default:
                    return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Stream.Dispose(); } finally { Socket.Dispose(); }
        }
    }

    internal static class CapturePipeSocket
    {
        internal static bool IsConnected(Socket socket)
        {
            try
            {
                return socket != null && socket.Connected && !(socket.Poll(0, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class HttpStreamReader
    {
        private const int MaxHeaderBytes = 64 * 1024;
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[8192];
        private readonly byte[] _singleByte = new byte[1];
        private int _offset;
        private int _count;

        internal HttpStreamReader(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        internal Stream DetachUnreadStream()
        {
            int remaining = _count - _offset;
            byte[] prefix = new byte[remaining];
            if (remaining > 0) Buffer.BlockCopy(_buffer, _offset, prefix, 0, remaining);
            _offset = _count;
            return new BufferedPrefixStream(prefix, _stream);
        }

        internal async Task<(Clearinet.HTTPRequestHeaders requestHeaders, byte[] requestBody)> ReadRequestAsync(CancellationToken token)
        {
            byte[] headerBytes = await ReadHeaderBlockAsync(token).ConfigureAwait(false);
            if (headerBytes == null) return (null, Array.Empty<byte>());

            string headerText = CONFIG.encodingOfHeaders.GetString(headerBytes);
            var (method, path, version, headerLines, origin) = Parser.ParseRequestHeaders(headerText);
            var headers = new Clearinet.HTTPRequestHeaders(path, headerLines)
            {
                HTTPMethod = method,
                HTTPVersion = version
            };
            if (origin.HasText() && origin.Contains(":")) headers.UriScheme = origin.TrimAfter(":");

            byte[] body = await ReadFramedBodyAsync(headers, token).ConfigureAwait(false);
            return (headers, body);
        }

        internal async Task<(Clearinet.HTTPResponseHeaders responseHeaders, byte[] responseBody, bool reusable)> ReadResponseAsync(
            string requestMethod, CancellationToken token)
        {
            while (true)
            {
                byte[] headerBytes = await ReadHeaderBlockAsync(token).ConfigureAwait(false);
                if (headerBytes == null) return (null, Array.Empty<byte>(), false);

                string headerText = CONFIG.encodingOfHeaders.GetString(headerBytes);
                var (version, statusCode, statusText, headerLines) = Parser.ParseResponseHeaders(headerText);
                var headers = new Clearinet.HTTPResponseHeaders(statusCode, statusText, headerLines)
                {
                    HTTPVersion = version
                };

                if (headers.Exists("Transfer-Encoding") && headers.Exists("Content-Length"))
                {
                    /*
                     * RFC 9112, Section 6.1: Explicitly forbids senders from combining the two headers in any HTTP message (both requests and responses):
                     * A sender MUST NOT send a Content-Length header field in any message that contains a Transfer-Encoding header field."
                    */
                    Debug.Assert(false, "A response cannot contain both Transfer-Encoding and Content-Length.");
                    headers.RenameHeaderItems("Content-Length", "PROTOCOL-VIOLATION-Content-Length");
                }

                // RFC 7230 Section 3.3.3: "All 1xx (Informational), 204 (No Content), and 304 (Not Modified) responses do not include a message body."
                if (statusCode >= 100 && statusCode < 200 && statusCode != 101)
                {
                    if (headers.Exists("Content-Length") && !headers.ExistsAndContains("Content-Length", "0"))
                    {
                        Debug.Assert(false, "Content is unexpected on a HTTP/1.xx");
                    }

                    // NYI: TODO: Squirrel away the informational header block for later storage, and read the next header block.
                    continue; // Read the next header block.
                }

                /* TODO: Track as HTTP Protocol violation.
                 * bool hasNoBody = requestMethod.OICEquals("HEAD") ||
                    (statusCode >= 100 && statusCode < 200) || statusCode == 204 || statusCode == 304;
                if (hasNoBody) return (headers, Array.Empty<byte>(), statusCode != 101);*/

                if (ContainsHeaderToken(headers, "Transfer-Encoding", "chunked"))
                {
                    byte[] chunkedBody = await ReadChunkedBodyAsync(token).ConfigureAwait(false);
                    return (headers, chunkedBody, headers.ExistsAndContains("Connection", "close")); /*hack: not quite right*/
                }

                if (headers.Exists("Transfer-Encoding"))
                {
                    Debug.Assert(false, "Unexpected T-E, but it's possible to have a compression alg here.");
                    //return (headers, await ReadToEndAsync(token).ConfigureAwait(false), false);
                }

                if (headers.Exists("Content-Length"))
                {
                    int contentLength = GetContentLength(headers);
                    return (headers, await ReadExactlyAsync(contentLength, token).ConfigureAwait(false), headers.ExistsAndContains("Connection", "close")); /*hack: not quite right*/
                }

                Debug.Assert(headers.ExistsAndContains("Connection", "close"), "Unexpected response with no framing and no Connection: close.");
                return (headers, await ReadToEndAsync(token).ConfigureAwait(false), false);
            }
        }

        private async Task<byte[]> ReadHeaderBlockAsync(CancellationToken token)
        {
            using (MemoryStream header = new MemoryStream())
            {
                while (header.Length < MaxHeaderBytes)
                {
                    int value = await ReadByteAsync(token).ConfigureAwait(false);
                    if (value < 0)
                    {
                        if (header.Length == 0) return null;
                        throw new InvalidDataException("Unexpected EOF reading HTTP headers.");
                    }

                    header.WriteByte((byte)value);
                    long length = header.Length;
                    byte[] bytes = header.GetBuffer();
                    if ((length >= 4 && bytes[length - 4] == '\r' && bytes[length - 3] == '\n' && bytes[length - 2] == '\r' && bytes[length - 1] == '\n') ||
                        (length >= 2 && bytes[length - 2] == '\n' && bytes[length - 1] == '\n') ||
                        (length >= 3 && bytes[length - 3] == '\n' && bytes[length - 2] == '\r' && bytes[length - 1] == '\n'))
                    {
                        byte[] result = new byte[header.Length];
                        Buffer.BlockCopy(bytes, 0, result, 0, result.Length);
                        return result;
                    }
                }

                throw new InvalidDataException("HTTP headers exceeded maximum allowed size.");
            }
        }

        private async Task<byte[]> ReadFramedBodyAsync(Clearinet.HTTPRequestHeaders headers, CancellationToken token)
        {
            if (headers.Exists("Transfer-Encoding") && headers.Exists("Content-Length"))
                throw new InvalidDataException("A request cannot contain both Transfer-Encoding and Content-Length.");
            if (headers.Exists("Transfer-Encoding"))
            {
                if (!ContainsHeaderToken(headers, "Transfer-Encoding", "chunked"))
                    throw new InvalidDataException("Unsupported request Transfer-Encoding.");
                return await ReadChunkedBodyAsync(token).ConfigureAwait(false);
            }
            if (!headers.Exists("Content-Length")) return Array.Empty<byte>();
            return await ReadExactlyAsync(GetContentLength(headers), token).ConfigureAwait(false);
        }

        private async Task<byte[]> ReadChunkedBodyAsync(CancellationToken token)
        {
            using (MemoryStream body = new MemoryStream())
            {
                while (true)
                {
                    string sizeLine = await ReadLineAsync(body, token).ConfigureAwait(false);
                    string sizeToken = sizeLine.Trim();
                    int extension = sizeToken.IndexOf(';');
                    if (extension >= 0) sizeToken = sizeToken.Substring(0, extension);
                    if (!int.TryParse(sizeToken, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int chunkSize) || chunkSize < 0)
                        throw new InvalidDataException("Invalid chunk size line: " + sizeLine);

                    if (chunkSize == 0)
                    {
                        while (true)
                        {
                            string trailer = await ReadLineAsync(body, token).ConfigureAwait(false);
                            if (trailer.Length == 0) return body.ToArray();
                        }
                    }

                    await CopyExactlyAsync(body, chunkSize, token).ConfigureAwait(false);
                    int terminator = await ReadByteAsync(token).ConfigureAwait(false);
                    if (terminator == '\r')
                    {
                        if (await ReadByteAsync(token).ConfigureAwait(false) != '\n') throw new InvalidDataException("Invalid chunk payload terminator.");
                    }
                    else if (terminator != '\n')
                    {
                        throw new InvalidDataException("Invalid chunk payload terminator.");
                    }
                    body.WriteByte((byte)terminator);
                    if (terminator == '\r') body.WriteByte((byte)'\n');
                }
            }
        }

        private async Task<string> ReadLineAsync(MemoryStream capture, CancellationToken token)
        {
            using (MemoryStream line = new MemoryStream())
            {
                while (true)
                {
                    int value = await ReadByteAsync(token).ConfigureAwait(false);
                    if (value < 0) throw new InvalidDataException("Unexpected EOF reading HTTP chunk framing.");
                    capture.WriteByte((byte)value);
                    if (value == '\n') break;
                    if (value != '\r') line.WriteByte((byte)value);
                }
                return Encoding.ASCII.GetString(line.ToArray());
            }
        }

        private async Task CopyExactlyAsync(Stream destination, int byteCount, CancellationToken token)
        {
            byte[] copyBuffer = new byte[8192];
            int remaining = byteCount;
            while (remaining > 0)
            {
                int take = Math.Min(remaining, copyBuffer.Length);
                int read = await ReadAsync(copyBuffer, 0, take, token).ConfigureAwait(false);
                if (read <= 0) throw new InvalidDataException("Unexpected EOF inside chunk payload.");
                destination.Write(copyBuffer, 0, read);
                remaining -= read;
            }
        }

        private async Task<byte[]> ReadExactlyAsync(int byteCount, CancellationToken token)
        {
            using (MemoryStream body = new MemoryStream(byteCount))
            {
                byte[] copyBuffer = new byte[8192];
                int remaining = byteCount;
                while (remaining > 0)
                {
                    int read = await ReadAsync(copyBuffer, 0, Math.Min(remaining, copyBuffer.Length), token).ConfigureAwait(false);
                    if (read <= 0) throw new InvalidDataException("Unexpected EOF inside HTTP message body.");
                    body.Write(copyBuffer, 0, read);
                    remaining -= read;
                }
                return body.ToArray();
            }
        }

        private async Task<byte[]> ReadToEndAsync(CancellationToken token)
        {
            using (MemoryStream body = new MemoryStream())
            {
                byte[] copyBuffer = new byte[8192];
                int read;
                while ((read = await ReadAsync(copyBuffer, 0, copyBuffer.Length, token).ConfigureAwait(false)) > 0)
                    body.Write(copyBuffer, 0, read);
                return body.ToArray();
            }
        }

        private async Task<int> ReadByteAsync(CancellationToken token)
        {
            int read = await ReadAsync(_singleByte, 0, 1, token).ConfigureAwait(false);
            return read == 0 ? -1 : _singleByte[0];
        }

        private async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (_offset == _count)
            {
                _count = await _stream.ReadAsync(_buffer, 0, _buffer.Length, token).ConfigureAwait(false);
                _offset = 0;
                if (_count == 0) return 0;
            }

            int available = Math.Min(count, _count - _offset);
            Buffer.BlockCopy(_buffer, _offset, buffer, offset, available);
            _offset += available;
            return available;
        }

        private sealed class BufferedPrefixStream : Stream
        {
            private readonly byte[] _prefix;
            private readonly Stream _inner;
            private int _offset;

            internal BufferedPrefixStream(byte[] prefix, Stream inner)
            {
                _prefix = prefix;
                _inner = inner;
            }

            public override bool CanRead { get { return _inner.CanRead; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return _inner.CanWrite; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int copied = CopyPrefix(buffer, offset, count);
                return copied > 0 ? copied : _inner.Read(buffer, offset, count);
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                int copied = CopyPrefix(buffer, offset, count);
                return copied > 0 ? copied : await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }

            private int CopyPrefix(byte[] buffer, int offset, int count)
            {
                int copied = Math.Min(count, _prefix.Length - _offset);
                if (copied > 0)
                {
                    Buffer.BlockCopy(_prefix, _offset, buffer, offset, copied);
                    _offset += copied;
                }
                return copied;
            }

            public override void Flush() { _inner.Flush(); }
            public override void Write(byte[] buffer, int offset, int count) { _inner.Write(buffer, offset, count); }
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _inner.WriteAsync(buffer, offset, count, cancellationToken);
            }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing) { base.Dispose(disposing); }
        }

        private static int ParseContentLength(string value)
        {
            if (!int.TryParse((value ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int length) || length < 0)
                throw new InvalidDataException("Invalid Content-Length header.");
            return length;
        }

        private static int GetContentLength(IEnumerable<Clearinet.HTTPHeaderItem> headers)
        {
            int? contentLength = null;
            foreach (Clearinet.HTTPHeaderItem header in headers)
            {
                if (!header.Name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                int parsed = ParseContentLength(header.Value);
                if (contentLength.HasValue && contentLength.Value != parsed)
                    throw new InvalidDataException("Conflicting Content-Length headers.");
                contentLength = parsed;
            }
            return contentLength ?? 0;
        }

        private static bool ContainsHeaderToken(IEnumerable<Clearinet.HTTPHeaderItem> headers, string headerName, string sought)
        {
            foreach (Clearinet.HTTPHeaderItem header in headers)
            {
                if (!header.Name.Equals(headerName, StringComparison.OrdinalIgnoreCase)) continue;
                string[] tokens = (header.Value ?? string.Empty).Split(',');
                for (int i = 0; i < tokens.Length; i++)
                    if (string.Equals(tokens[i].Trim(), sought, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
