using Microsoft.JScript;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public string this[string sHeaderName]
        {
            get { return "TODO"; }
            set { }
        }
        public HTTPHeaderItem Add(string sHeaderName, string sHeaderValue)
        {
            HTTPHeaderItem hhi = new HTTPHeaderItem(sHeaderName, sHeaderValue);
            storage.Add(hhi);
            return hhi;
        }

        protected List<HTTPHeaderItem> storage = new List<HTTPHeaderItem>();
    }
    public class HTTPRequestHeaders : HTTPHeaders
    {
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
            set {
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
    }
    public class HTTPResponseHeaders : HTTPHeaders
    {
        // Status code from HTTP Response. Call SetStatus() to update StatusText at the same time.
        public int StatusCode { get; set; }
        public string StatusText { get; set; }
        public void SetStatus(int iCode, string sText)
        {
            StatusCode = iCode;
            StatusText = sText;
        }

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
        public string HTTPResponseStatus { 
            get => $"{StatusCode} {StatusText}";
            set
            {
                var parts = value.Split(new[] {' '}, 2);
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
