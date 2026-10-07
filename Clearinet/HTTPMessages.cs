using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Clearinet
{
    /// <summary>
    /// <summary>
    /// An immutable HTTP header name/value pair.
    /// </summary>
    public sealed class HTTPHeaderItem
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public HTTPHeaderItem(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.Assert(false, "Headers must be named.");
                name = string.Empty;
            }

            Name = name;
            Value = value ?? string.Empty;
        }

        public HTTPHeaderItem Clone() => new HTTPHeaderItem(Name, Value);

        public override string ToString() => $"{Name}: {Value}";
    }
    public abstract class HTTPHeaders
    {
        //TODO:Locking!
        public string HTTPVersion { get; set; } = "HTTP/1.1";
        protected Encoding _encodingHeaders = Encoding.UTF8;
        public void RenameHeaderItems(string fromHeaderName, string toHeaderName)
        {
        }

        // TODO: Should this be a smarter structure?
        protected List<HTTPHeaderItem> storage = new List<HTTPHeaderItem>();

        /// <summary>
        /// Legacy Getter/Setter that handles only the simple case of the *first* named header.
        /// returns an empty string if the header wasn't found. TODO: Build a smarter function which joins headers by comma
        /// </summary>
        public string this[string sHeaderName]
        {
            get => storage.Find(h => h.Name.OICEquals(sHeaderName))?.Value ?? string.Empty;
            set
            {
                var hi = storage.Find(h => h.Name.OICEquals(sHeaderName));
                if (hi != null)
                {
                    hi.Value = value;
                    return;
                }
                Add(sHeaderName, value);
            }
        }

        public HTTPHeaderItem Add(string sHeaderName, string sHeaderValue)
        {
            HTTPHeaderItem hhi = new HTTPHeaderItem(sHeaderName, sHeaderValue);
            storage.Add(hhi);
            return hhi;
        }
        public bool Exists(string sHeaderName)
        {
            return storage.Exists(h => h.Name.OICEquals(sHeaderName));
        }
        /// <summary>
        /// True if ANY instance of the named header exists with the sought string anywhere in the value
        /// </summary>
        public bool ExistsAndContains(string sHeaderName, string sPartialValue)
        {
            for (int iX = 0; iX < storage.Count; ++iX)
            {
                if (storage[iX].Name.OICEquals(sHeaderName) &&
                    storage[iX].Value.OICContains(sPartialValue))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ExistsAny(params string[] sHeaderNames)
        {
            return ExistsAny((IEnumerable<string>)sHeaderNames);
        }
        public bool ExistsAny(IEnumerable<string> sHeaderNames)
        {
            if (sHeaderNames is null) return false;
            for (int x = 0; x < storage.Count; x++)
            {
                foreach (string s in sHeaderNames)
                {
                    if (String.Equals(storage[x].Name, s, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }
    }
    public class HTTPRequestHeaders : HTTPHeaders, IEnumerable<HTTPHeaderItem>
    {
        public IEnumerator<HTTPHeaderItem> GetEnumerator() => storage.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public string HTTPMethod { get; set; } = String.Empty;
        private string _UrlScheme = "http";
        public string UriScheme
        {
            get
            {
                Debug.Assert(_UrlScheme.HasText());
                return _UrlScheme ?? string.Empty;
            }
            set
            {
                _UrlScheme = value.ToLowerInvariant();
            }
        }

        public string RequestPath
        {
            get => _RequestPath ?? String.Empty;
            set
            {
                if (null == value)
                {
                    Debug.Assert(false);
                    value = string.Empty;
                }

                Debug.Assert(!value.Contains(" "), "Whitespace illegal in path");
                _RequestPath = value;
                _arrRawPath = _encodingHeaders.GetBytes(value);
            }
        }

        private byte[] _arrRawPath = Array.Empty<byte>();
        private string _RequestPath = String.Empty;

        public HTTPRequestHeaders() { }
        public HTTPRequestHeaders(string sRequestPath, string[] arrHeaderLines)
        {
            this.HTTPMethod = "GET";
            this.RequestPath = sRequestPath.Trim();  // Trim for convenience
            if (null != arrHeaderLines)
            {
                string sErrs = String.Empty;
                Parser.ParseHeaderLines(this, arrHeaderLines, 0, ref sErrs); // TODO: Don't discard output errors?
            }
        }

        /// <summary>
        /// Constructor for HTTP Request headers object
        /// </summary>
        /// <param name="encodingForHeaders">Text encoding to be used for this set of Headers when converting to a byte array</param>
        public HTTPRequestHeaders(Encoding encodingForHeaders)
        {
            _encodingHeaders = encodingForHeaders;
        }

        public HTTPRequestHeaders Clone()
        {
            HTTPRequestHeaders oClone = (HTTPRequestHeaders)MemberwiseClone();
            try
            {
                oClone.storage = new List<HTTPHeaderItem>(storage.Count);
                foreach (HTTPHeaderItem oItem in storage)
                {
                    oClone.storage.Add(oItem.Clone());
                }
            }
            finally
            {
                Debug.Assert(false);
            }
            return oClone;
        }

        public override string ToString()
        {
            return ToString(true, true);
        }

        public string ToString(bool include_request_line, bool include_endline)
        {
            StringBuilder sbOut = new StringBuilder();
            if (include_request_line) sbOut.Append($"{HTTPMethod} {RequestPath} {HTTPVersion}\r\n");
            try
            {
                // TODO: Lock 
                for (int x = 0; x < storage.Count; x++)
                {
                    sbOut.Append($"{storage[x].Name}: {storage[x].Value}\r\n");
                }
            }
            finally
            {
                //unlock
            }

            if (include_endline) sbOut.Append("\r\n");
            return sbOut.ToString();
        }

        public byte[] ToByteArray()
        {
            return this._encodingHeaders.GetBytes(ToString(true, true));
        }
    }

    public class HTTPResponseHeaders : HTTPHeaders, IEnumerable<HTTPHeaderItem>
    {
        // Status code from HTTP Response. Call SetStatus() to update StatusText at the same time.
        public int StatusCode { get; set; }
        public string StatusText { get; set; }
        public void SetStatus(int iCode, string sText)
        {
            StatusCode = iCode;
            StatusText = sText;
        }

        public IEnumerator<HTTPHeaderItem> GetEnumerator() => storage.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        public HTTPResponseHeaders() { }

        public HTTPResponseHeaders(int iStatusCode, string sStatusText, string[] sNVP)
        {
            StatusCode = iStatusCode;
            StatusText = sStatusText;
            if (null != sNVP)
            {
                string sErrs = String.Empty;
                Parser.ParseHeaderLines(this, sNVP, 0, ref sErrs); // TODO: Don't discard output errors?
            }
        }

        public HTTPResponseHeaders(Encoding encodingForHeaders)
        {
            _encodingHeaders = encodingForHeaders;
        }

        public HTTPResponseHeaders Clone()
        {
            HTTPResponseHeaders oClone = (HTTPResponseHeaders)MemberwiseClone();
            try
            {
                oClone.storage = new List<HTTPHeaderItem>(storage.Count);
                foreach (HTTPHeaderItem oItem in storage)
                {
                    oClone.storage.Add(oItem.Clone());
                }
            }
            finally
            {
                Debug.Assert(false);
            }
            return oClone;
        }

        // Legacy API
        [Obsolete]
        public int HTTPResponseCode { get => StatusCode; set => StatusCode = value; }
        [Obsolete]
        public string HTTPResponseStatus
        {
            get => $"{StatusCode} {StatusText}";
            set
            {
                var parts = value.Split(new[] { ' ' }, 2);
                if (parts.Length > 0 && int.TryParse(parts[0], out int code))
                {
                    StatusCode = code;
                    StatusText = parts.Length > 1 ? parts[1] : string.Empty;
                }
                else
                {
                    StatusCode = 0;
                    StatusText = value;
                }
            }
        }

        public override string ToString()
        {
            return ToString(true, true);
        }
        public string ToString(bool include_status, bool include_endline)
        {
            StringBuilder sbOut = new StringBuilder();

            if (include_status) sbOut.Append($"{HTTPVersion} {StatusCode} {StatusText}\r\n");
            try
            {
                // TODO: Lock 
                for (int x = 0; x < storage.Count; x++)
                {
                    sbOut.Append($"{storage[x].Name}: {storage[x].Value}\r\n");
                }
            }
            finally
            {
                //unlock
            }

            if (include_endline) sbOut.Append("\r\n");
            return sbOut.ToString();
        }

        public byte[] ToByteArray()
        {
            return this._encodingHeaders.GetBytes(ToString(true, true));
        }
    }

    public class HTTPParser
    {
        public static HTTPRequestHeaders ParseRequest(string sRequest)
        {
            return new HTTPRequestHeaders();
        }
        public static HTTPResponseHeaders ParseResponse(string sResponse)
        {
            return new HTTPResponseHeaders();
        }
    }
}
