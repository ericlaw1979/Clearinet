using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace Clearinet
{
    /// <summary>
    /// The Exchange class represents a single HTTP exchange: the request, response, and metadata.
    /// </summary>
    [DebuggerDisplay("Exchange #{_id}: {_state} {fullUrl} [{BitFlags}]")]
    public class Exchange
    {
        #region 'id' property
        // Monotonically increasing counter. Note that this can be reset e.g. when the user
        // clears the list of exchanges. Fiddler used a 32bit signed integer, so if you want
        // more than 2 billion exchanges, Clearinet wins! ;-)
        // (It's not a UInt because .NET doesn't have an Interlocked.Increment UInt.)
        private static Int64 __cTotalExchanges;

        private ExchangeState _state;

        // Having an "ID" is important to the user and simply indicate ordering, but they cause
        // lots of problems because they are basically unstable-- loading a SAZ file or importing
        // exchanges from another tool will change the displayed ID.
        private Int64 _id;
        public Int64 id { get => _id; }
        // TODO: Can't we just assign the ID in the constructor? Are there any codepaths
        // where we don't want to, e.g. if we have temporaries that are never put into
        // the Web Exchange list?
        internal void EnsureID()
        {
            if (_id == 0) _id = Interlocked.Increment(ref __cTotalExchanges);
        }
        #endregion 'id' property

        // Reset the Exchange counter to 0. In a perfect world, we'd never do this,
        // but users expect the counter to be at 0 when they clear the Exchange list.
        internal static void ResetSessionCounter()
        {
            Interlocked.Exchange(ref __cTotalExchanges, 0);
        }

        // Technically, UInt32 would be the right type, but
        // .NET doesn't do that.
        private int _LocalProcessID;
        /// <summary>
        /// LEGACY API: Returns the process ID of the client that initiated this
        /// exchange. If the client is remote, this will be 0.
        /// </summary>
        public int LocalProcessID
        {
            get
            {
                return this._LocalProcessID;
            }
        }
        public string LocalProcess
        {
            get
            {
                return oFlags.TryGetValue("X-ProcessInfo", out var s) ? s : string.Empty;
            }
        }

        public bool bypassGateway
        {
            get => oFlags.ContainsKey("net-bypassgateway");
            set => oFlags["net-bypassgateway"] = "1";
        }

        // LEGACY API: SyntacticSugar, was "uriContains" in Fiddler.
        public bool urlContains(string s) => fullUrl.OICContains(s);

        public int utilFindInRequest(string sFind, bool bIgnoreCase) { return -1; }
        public int utilFindInResponse(string sFind, bool bIgnoreCase) { return -1; }

        public bool utilReplaceInRequest(string sFrom, string sTo) { return false; }
        public bool utilReplaceInResponse(string sFrom, string sTo) { return false; }
        public bool utilReplaceOnceInResponse(string sFrom, string sTo, bool bIgnoreCase) { return false; }

        public bool utilReplaceRegexInResponse(string sFromRegEx, string sToExpression) { return false; }

        public void utilCreateResponseAndBypassServer() { /*TODO*/ }

        public void utilSetRequestBody(string sBody) { RequestBody = System.Text.Encoding.UTF8.GetBytes(sBody); /*TODO*/ }
        public void utilSetResponseBody(string sBody) { ResponseBody = System.Text.Encoding.UTF8.GetBytes(sBody); /*TODO do it for real*/ }
        public void utilPrependToResponseBody(string sPrefix) { ResponseBody = System.Text.Encoding.UTF8.GetBytes(sPrefix).Append(ResponseBody); }

        public bool utilDecodeRequest(bool bQuiet = true) { /*TODO*/ return false; }
        public bool utilDecodeResponse(bool bQuiet = true) { /*TODO*/ return false; }

        // TODO: Add a convenience handler that tries to get a .NET URL from the string?

        // For silly historical reasons, this is the schemeless-url.
        public string url
        {
            get
            {
                if (null == RequestHeaders) return string.Empty;
                if (RequestHeaders.UriScheme.OICEquals("data")) return "data:" + RequestHeaders.RequestPath;
                return $"{RequestHeaders["Host"]}{RequestHeaders.RequestPath}";
            }
            set
            {
                // TODO: We probably don't want to use the actual .NET URL parser here.
                Uri oUri;
                if (Uri.TryCreate(value, UriKind.Absolute, out oUri))
                {
                    RequestHeaders["Host"] = oUri.Host;
                    if (!oUri.IsDefaultPort)
                    {
                        RequestHeaders["Host"] += $":{oUri.Port}";
                        RequestHeaders.RequestPath = oUri.PathAndQuery;
                    }
                }
                else
                {
                    RequestHeaders.RequestPath = value;
                }
            }
        }
        public string fullUrl
        {
            get
            {
                if (null == RequestHeaders) return string.Empty;
                if (RequestHeaders.UriScheme.OICEquals("data")) return "data:" + RequestHeaders.RequestPath;
                return $"{RequestHeaders.UriScheme}://{RequestHeaders["Host"]}{RequestHeaders.RequestPath}";
            }
            set
            {
                if (null == RequestHeaders) RequestHeaders = new HTTPRequestHeaders();

                // TODO: We probably don't want to use the actual .NET URL parser here.
                Uri oUri;
                if (!Uri.TryCreate(value, UriKind.Absolute, out oUri))
                {
                    throw new ArgumentException($"Invalid URL: {value}");
                }
                RequestHeaders.UriScheme = oUri.Scheme;
                RequestHeaders["Host"] = oUri.Host;
                if (!oUri.IsDefaultPort)
                {
                    RequestHeaders["Host"] += $":{oUri.Port}";
                }
                RequestHeaders.RequestPath = oUri.PathAndQuery;
            }
        }
        public string host
        {
            get => RequestHeaders?["Host"] ?? string.Empty;
            set
            {
                if (null == RequestHeaders) RequestHeaders = new HTTPRequestHeaders();
                RequestHeaders["Host"] = value;
            }
        }

        public string hostname { get; set; }
        public int port { get; set; }
        public string clientIP { get; set; }
        public int responseCode
        {
            get => ResponseHeaders?.StatusCode ?? 0;
            set
            {
                if (null == ResponseHeaders) ResponseHeaders = new HTTPResponseHeaders();
                ResponseHeaders.StatusCode = value;
            }
        }

        public void PoisonServerPipe() { } // TODO: Prevent server connection reuse
        public void PoisonClientPipe() { } // TODO: Prevent client connection reuse

        internal void Abort() { } // Move state to Aborted and stop processing

        // LEGACY API: SyntacticSugar
        public bool bHasResponse { get => false; } // TODO: implement

        public ExchangeState state
        {
            get => _state;
            set
            {
                ExchangeState statePrior = _state;
                _state = value;

                /* TODO: Full state machine goes in here!!! */
                OnStateChanged?.Invoke(this, new StateChangeEventArgs(statePrior, _state));

                if (_state >= ExchangeState.Done)
                {
                    OnStateChanged = null;
                }
            }
        }

        // 
        public event EventHandler<StateChangeEventArgs> OnStateChanged;

        byte[] _arrRequestBody = Array.Empty<byte>();
        byte[] _arrResponseBody = Array.Empty<byte>();

        // TODO: Figure out cloning
        public byte[] RequestBody { get => _arrRequestBody; set => _arrRequestBody = value; }
        public byte[] ResponseBody { get => _arrResponseBody; set => _arrResponseBody = value; }

        // TODO: These are supposed to point into the underlying object...
        public HTTPRequestHeaders RequestHeaders { get; set; }
        public HTTPResponseHeaders ResponseHeaders { get; set; }

        #region FlagHandling
        public ConcurrentDictionary<string, string> oFlags =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string this[string sFlagName]
        {
            get => oFlags.TryGetValue(sFlagName, out var value) ? value : null;
            set => oFlags[sFlagName] = value;
        }
        private ExchangeFlags _bitFlags;
        public ExchangeFlags BitFlags
        {
            get => _bitFlags;
            internal set
            {
                _bitFlags = value;
            }
        }
        internal void SetBitFlag(ExchangeFlags flagsToAdjust)
        {
            BitFlags = (_bitFlags | flagsToAdjust);
        }
        // TODO? ClearBitFlag
        internal void AdjustBitFlag(ExchangeFlags flagsToAdjust, bool state)
        {
            BitFlags = state ? (_bitFlags | flagsToAdjust) : (_bitFlags & ~flagsToAdjust);
        }
        public bool isFlagSet(ExchangeFlags f) => (f == (_bitFlags & f));
        public bool isAnyFlagSet(ExchangeFlags f) => (0 != (_bitFlags & f));
        #endregion

        /// <summary>
        /// Indicates if the client is local. If not, beware the SECURITY IMPLICATIONS of
        /// allowing a remote client to proxy through us.
        /// </summary>
        /// <returns>True if the client is local.</returns>
        public bool ClientIsLoopback()
        {
            // TODO: If the ClientPipe isn't connected to loopback, return false.
            return true;
        }

        public string GetRequestBodyAsString()
        {
            return string.Empty;
        }
        public string GetResponseBodyAsString()
        {
            return string.Empty;
        }
        public ExchangeTimers Timers = new ExchangeTimers();

        // ISSUE: For the core engine, we won't have a ListViewItem;
        // We probably need to have a "Tag" pointer that can be used
        // to hold arbitrary data for the core engine, and then have
        // the UI layer use that to store a ListViewItem.
        public ListViewItem ViewItem { get; internal set; }

        internal bool ShouldHide()
        {
            if (CApp.isClosing) return true;
            if (isFlagSet(ExchangeFlags.Ignored)) return true;
            if (oFlags.ContainsKey("ui-hide")) return true;
            return false;
        }


        /// <summary>
        /// Builds an Exchange object from the given data. 
        /// Note that this method does not assign an ID to the Exchange; call EnsureID() if you want to assign an ID.
        /// </summary>
        /// <param name="bDeepCopy">Should objects (e.g. headers) be deepcopied?</param>
        /// <param name="rqh">Request headers</param>
        /// <param name="arrReqBody">Request Body bytes</param>
        /// <param name="rsp">Response headers</param>
        /// <param name="arrRespBody">Response Body bytes</param>
        /// <param name="ef">Exchange Flags</param>
        /// <returns>The new Exchange</returns>
        public static Exchange BuildFromData(bool bDeepCopy, HTTPRequestHeaders rqh, byte[] arrReqBody, HTTPResponseHeaders rph, byte[] arrRespBody, ExchangeFlags ef)
        {
            if (null == rqh)
            {
                (rqh = new HTTPRequestHeaders
                {
                    HTTPMethod = "GET",
                    HTTPVersion = "HTTP/1.1",
                    UriScheme = "http",
                    RequestPath = $"/{DateTime.Now.Ticks}"
                }).Add("Host", "localhost");
            }
            else
            {
                if (bDeepCopy) rqh = (HTTPRequestHeaders)rqh.Clone();
            }

            if (null == rph)
            {
                (rph = new HTTPResponseHeaders
                {
                    StatusCode = 200,
                    StatusText = "OK",
                    HTTPVersion = "HTTP/1.1"
                }).Add("Connection", "close");
            }
            else
            {
                if (bDeepCopy) rph = (HTTPResponseHeaders)rph.Clone();
            }

            if (null == arrReqBody)
            {
                arrReqBody = Array.Empty<byte>();
            }
            else
            {
                if (bDeepCopy) arrReqBody = arrReqBody.FastClone();
            }

            if (null == arrRespBody)
            {
                arrRespBody = Array.Empty<byte>();
            }
            else
            {
                if (bDeepCopy) arrRespBody = arrRespBody.FastClone();
            }

            Exchange exchBuilt = new Exchange(rqh, arrReqBody);
            exchBuilt.EnsureID();
            exchBuilt.SetBitFlag(ef);
            exchBuilt.ResponseHeaders = rph;
            exchBuilt._arrResponseBody = arrRespBody;
            exchBuilt.state = ExchangeState.Done;

            return exchBuilt;
        }

        public Exchange(HTTPRequestHeaders rqh, byte[] arrReqBody)
        {
            this.RequestHeaders = rqh; // CLONE?
            this._arrRequestBody = arrReqBody; // CLONE?
        }

        public string RequestMethod
        {
            get
            {
                return this.RequestHeaders.HTTPMethod;
            }
            set
            {
                // todo: if headers not present...
                this.RequestHeaders.HTTPMethod = value;
            }
        }

        /// <summary>
        /// Public (Legacy) API. Instruct Clearinet to ignore this exchange
        /// to the extent possible.
        /// </summary>
        public void Ignore()
        {
            SetBitFlag(ExchangeFlags.Ignored);
            if (HTTPMethodIs("CONNECT"))
            {
                oFlags["x-no-decrypt"] = "CalledIgnore";
                oFlags["x-no-parse"] = "CalledIgnore";
            }
            else
            {
                oFlags["log-drop-response-body"] = "CalledIgnore";
                oFlags["log-drop-request-body"] = "CalledIgnore";
            }
            // TODO: configure both request and respones to stream
        }


        public bool isTunnel { get => HTTPMethodIs("CONNECT"); }

        public bool HTTPMethodIs(string sTestFor)
        {
            return this.RequestMethod.OICEquals(sTestFor);
        }


        public bool WriteMetadataToStream(Stream strmMetadata)
        {
            try
            {
                using (XmlTextWriter oXML = new XmlTextWriter(strmMetadata, Encoding.UTF8))
                {
                    oXML.Formatting = Formatting.Indented;
                    oXML.WriteStartDocument();
                    oXML.WriteStartElement("Session");  // Legacy name for Exchange
                    oXML.WriteAttributeString("SID", this.id.ToString());
                    oXML.WriteAttributeString("BitFlags", $"{(uint)this.BitFlags:X}");

                    if (this.state == ExchangeState.Aborted)
                    {
                        oXML.WriteAttributeString("Aborted", "true");
                    }

                    oXML.WriteStartElement("SessionFlags");
                    foreach (var kvp in this.oFlags)
                    {
                        oXML.WriteStartElement("SessionFlag");
                        oXML.WriteAttributeString("N", kvp.Key);
                        oXML.WriteAttributeString("V", kvp.Value);
                        oXML.WriteEndElement();
                    }
                    oXML.WriteEndElement();     // End Flags

                    if (null != Timers) Timers.WriteToSAZMetadata(oXML);

                    // TODO: Write CONNECT info after we support it.
                    /*if (__oTunnel != null)
                    {
                        oXML.WriteStartElement("TunnelInfo");
                        oXML.WriteAttributeString("BytesEgress", __oTunnel.IngressByteCount.ToString());
                        oXML.WriteAttributeString("BytesIngress", __oTunnel.EgressByteCount.ToString());
                        oXML.WriteEndElement();
                    }*/

                    // Write PipeInfo // TODO: This is pretty redundant with the BitFlags. Is there
                    // really an handler old enough that it wants it?
                    oXML.WriteStartElement("PipeInfo");

                    if (this.BitFlags.HasFlag(ExchangeFlags.ResponseStreamed))
                        oXML.WriteAttributeString("Streamed", "true");

                    if (this.BitFlags.HasFlag(ExchangeFlags.ClientPipeReused))
                        oXML.WriteAttributeString("CltReuse", "true");

                    if (this.BitFlags.HasFlag(ExchangeFlags.ServerPipeReused))
                        oXML.WriteAttributeString("Reused", "true");

                    if (this.BitFlags.HasFlag(ExchangeFlags.SentToGateway))
                        oXML.WriteAttributeString("Forwarded", "true");

                    oXML.WriteEndElement(); // </PipeInfo>

                    oXML.WriteEndElement(); // </Session>
                    oXML.WriteEndDocument();
                    oXML.Flush();
                    return true;
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Saving Exchange metadata failed");
                return false;
            }
        }

        internal byte[] FullResponseBytes()
        {
            MemoryStream ms = new MemoryStream();
            if (null != ResponseHeaders)
            {
                byte[] arrHeaders = ResponseHeaders.ToByteArray();
                ms.Write(arrHeaders, 0, arrHeaders.Length);
            }
            if (null != ResponseBody)
            {
                ms.Write(ResponseBody, 0, ResponseBody.Length);
            }
            return ms.ToArray();
        }

        internal byte[] FullRequestBytes()
        {
            MemoryStream ms = new MemoryStream();
            if (null != RequestHeaders)
            {
                byte[] arrHeaders = RequestHeaders.ToByteArray();
                ms.Write(arrHeaders, 0, arrHeaders.Length);
            }
            if (null != RequestBody)
            {
                ms.Write(RequestBody, 0, RequestBody.Length);
            }
            return ms.ToArray();
        }
    }


}
