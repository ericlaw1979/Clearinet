using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Collections.Specialized.BitVector32;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace Clearinet
{
    /// <summary>
    /// The Exchange class represents a single HTTP exchange: the request, response, and metadata.
    /// </summary>
    public class Exchange
    {
        public string fullUrl { get; private set; }
        public ExchangeStates state { get;
            set; /* TODO: Full state machine goes in here!!! */ 
        } = ExchangeStates.Created;

        byte[] _requestBodyBytes = Array.Empty<byte>();
        byte[] _responseBodyBytes = Array.Empty<byte>();

        public string this[string sFlagName]
        {
            get { return "TODO"; }
            set { }
        }

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

        public string url { get; set; }

        // ISSUE: For the core engine, we won't have a ListViewItem;
        // We probably need to have a "Data" pointer that can be used
        // to store arbitrary data for the core engine, and then have
        // the UI layer use that to store a ListViewItem.
        public ListViewItem ViewItem { get; internal set; }

        /// <summary>
        /// Builds an Exchange object from the given data.
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
                (rph = new HTTPResponseHeaders {
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
            //exchBuilt._AssignID();
            //exchBuilt.SetBitFlag(ef, true);
            //exchBuilt.oResponse.headers = rph;
            exchBuilt._responseBodyBytes = arrRespBody;
            exchBuilt.state = ExchangeStates.Done;

            return exchBuilt;
        }

        public Exchange(HTTPRequestHeaders rqh, byte[] arrReqBody)
        {
            this._requestBodyBytes = arrReqBody;
        }

        public void utilSetResponseBody(string responseBody)
        {
            // TODO: update this method to set the response body
            // **USING* the current encoding from the headers.
            Encoding.UTF8.GetBytes(responseBody);
        }

    }
}
