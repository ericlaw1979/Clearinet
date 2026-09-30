using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clearinet
{
    /// <summary>
    /// The Exchange class represents a single HTTP exchange: the request, response, and metadata.
    /// </summary>
    public class Exchange
    {
        public string fullUrl { get; private set; }

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

        public static Exchange BuildFromData(bool b, HTTPRequestHeaders rqh, byte[] arrReq, HTTPResponseHeaders rsp, byte[] arrResp, ExchangeFlags ef)
        {
            return new Exchange();
        }
            
        public void utilSetResponseBody(string responseBody)
        {
            // TODO: Implement this method to set the response body.
        }

    }
}
