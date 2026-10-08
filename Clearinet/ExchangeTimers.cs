using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Clearinet
{
    public class ExchangeTimers
    {
        /// <summary>
        /// Time of client's TCP/IP connection to the proxy.
        /// </summary>
        public DateTime ClientConnected;

        /// <summary>
        /// Time at which first read from the client finished.
        /// </summary>
        public DateTime ClientBeginRequest;

        /// <summary>
        /// Time at which App got the request Headers.
        /// </summary>
        public DateTime ProxyGotRequestHeaders;

        /// <summary>
        /// Time at which the App got the complete client request.
        /// </summary>
        public DateTime ClientDoneRequest;

        /// <summary>
        /// Time of TCP/IP connection establishment to the server.
        /// </summary>
        public DateTime ServerConnected;

        /// <summary>
        /// Time of start of first Send() call to the server.
        /// </summary>
        public DateTime ProxyBeginRequest;

        /// <summary>
        /// Time of end of last Send() call to the server.
        /// </summary>
        public DateTime ProxyDoneRequest;

        /// <summary>
        /// Time of first Read() call from the server returning (aka ServerResponseFirstRead)
        /// </summary>
        public DateTime ServerBeginResponse;

        /// <summary>
        /// Time at which App got the response Headers.
        /// </summary>
        public DateTime ProxyGotResponseHeaders;

        /// <summary>
        /// Time at which the App got the complete server response (aka ServerResponseLastRead)
        /// </summary>
        public DateTime ServerDoneResponse;

        /// <summary>
        /// Time at which the app called Send() to return a Response to the client (aka ClientResponseFirstSend)
        /// </summary>
        public DateTime ClientBeginResponse;

        /// <summary>
        /// Time at which the app's final Send() to the client completed (aka ClientResponseLastSend)
        /// </summary>
        public DateTime ClientDoneResponse;

        /// <summary>
        /// # milliseconds spent determining whether to use an upstream proxy.
        /// If non-zero, DNSTime ought to be zero, since we don't need to do a DNS lookup if we're using an upstream proxy.
        /// </summary>
        public int GatewayDeterminationTime;

        /// <summary>
        /// # milliseconds spent resolving the request host in DNS.
        /// </summary>
        public int DNSTime;

        /// <summary>
        /// # milliseconds spent in the TCP/IP connect() to the server.
        /// </summary>
        public int TCPConnectTime;

        /// <summary>
        /// # milliseconds in TLS handshaking.
        /// </summary>
        public int HTTPSHandshakeTime;

        private NetTimestamps ntsClientReads;
        private NetTimestamps ntsServerReads;

        public NetTimestamps ClientReads
        {
            get
            {
                if (null == ntsClientReads) ntsClientReads = new NetTimestamps();
                return ntsClientReads;
            }
            internal set
            {
                ntsClientReads = value;
            }
        }

        public NetTimestamps ServerReads
        {
            get
            {
                if (null == ntsServerReads) ntsServerReads = new NetTimestamps();
                return ntsServerReads;
            }
            internal set
            {
                ntsServerReads = value;
            }
        }

        internal ExchangeTimers Clone()
        {
            // BUG: TODO: Do we need to deep clone the ClientReads and ServerReads?
            return (ExchangeTimers)this.MemberwiseClone();
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool bMultiLine)
        {
            string sSep = bMultiLine ? Environment.NewLine : ", ";
            StringBuilder sb = new StringBuilder(512);

            AppendTimer(sb, "ClientConnected", ClientConnected, sSep);
            AppendTimer(sb, "ClientBeginRequest", ClientBeginRequest, sSep);
            AppendTimer(sb, "ProxyGotRequestHeaders", ProxyGotRequestHeaders, sSep);
            AppendTimer(sb, "ClientDoneRequest", ClientDoneRequest, sSep);
            AppendTimer(sb, "ServerConnected", ServerConnected, sSep);
            AppendTimer(sb, "ProxyBeginRequest", ProxyBeginRequest, sSep);
            AppendTimer(sb, "ProxyDoneRequest", ProxyDoneRequest, sSep);
            AppendTimer(sb, "ServerBeginResponse", ServerBeginResponse, sSep);
            AppendTimer(sb, "ProxyGotResponseHeaders", ProxyGotResponseHeaders, sSep);
            AppendTimer(sb, "ServerDoneResponse", ServerDoneResponse, sSep);
            AppendTimer(sb, "ClientBeginResponse", ClientBeginResponse, sSep);
            AppendTimer(sb, "ClientDoneResponse", ClientDoneResponse, sSep);

            AppendDuration(sb, "DNSTime", DNSTime, sSep);
            AppendDuration(sb, "GatewayDeterminationTime", GatewayDeterminationTime, sSep);
            AppendDuration(sb, "TCPConnectTime", TCPConnectTime, sSep);
            AppendDuration(sb, "HTTPSHandshakeTime", HTTPSHandshakeTime, sSep);

            if (bMultiLine)
            {
                if (TimeSpan.Zero < (ClientDoneResponse - ClientBeginRequest))
                    sb.AppendFormat("\tOverall Elapsed:\t{0:h\\:mm\\:ss\\.fff}\r\n", ClientDoneResponse - ClientBeginRequest);
            }
            else
            {
                // Trim trailing comma.
                if (sb.Length >= sSep.Length) sb.Length -= sSep.Length;
            }
            if (sb.Length < 1) sb.Append("(No timers recorded)");
            return sb.ToString();
        }

        private static void AppendTimer(StringBuilder sb, string name, DateTime value, string sSep)
        {
            if (value == DateTime.MinValue) return;
            sb.Append($"{name}:\t{value.ToString("HH:mm:ss.fff")}{sSep}");
        }

        private static void AppendDuration(StringBuilder sb, string name, int value, string sSep)
        {
            if (value <= 0) return;
            sb.Append($"{name}:{value}ms{sSep}");
        }

        internal void WriteToSAZMetadata(XmlTextWriter oXML)
        {
            // Write ExchangeTimers
            oXML.WriteStartElement("SessionTimers");

            // Local function to format DateTime in RoundtripKind
            string FormatDT(DateTime dt) => XmlConvert.ToString(dt, XmlDateTimeSerializationMode.RoundtripKind);

            oXML.WriteAttributeString("ClientConnected", FormatDT(this.ClientConnected));
            oXML.WriteAttributeString("ClientBeginRequest", FormatDT(this.ClientBeginRequest));
            oXML.WriteAttributeString("GotRequestHeaders", FormatDT(this.ProxyGotRequestHeaders));
            oXML.WriteAttributeString("ClientDoneRequest", FormatDT(this.ClientDoneRequest));
            oXML.WriteAttributeString("GatewayTime", XmlConvert.ToString(this.GatewayDeterminationTime));
            oXML.WriteAttributeString("DNSTime", XmlConvert.ToString(this.DNSTime));
            oXML.WriteAttributeString("TCPConnectTime", XmlConvert.ToString(this.TCPConnectTime));
            oXML.WriteAttributeString("HTTPSHandshakeTime", XmlConvert.ToString(this.HTTPSHandshakeTime));
            oXML.WriteAttributeString("ServerConnected", FormatDT(this.ServerConnected));
            oXML.WriteAttributeString("FiddlerBeginRequest", FormatDT(this.ProxyBeginRequest));
            oXML.WriteAttributeString("ServerGotRequest", FormatDT(this.ProxyDoneRequest));
            oXML.WriteAttributeString("ServerBeginResponse", FormatDT(this.ServerBeginResponse));
            oXML.WriteAttributeString("GotResponseHeaders", FormatDT(this.ProxyGotResponseHeaders));
            oXML.WriteAttributeString("ServerDoneResponse", FormatDT(this.ServerDoneResponse));
            oXML.WriteAttributeString("ClientBeginResponse", FormatDT(this.ClientBeginResponse));
            oXML.WriteAttributeString("ClientDoneResponse", FormatDT(this.ClientDoneResponse));

            oXML.WriteEndElement(); // </SessionTimers>
        }
    }

    /// <summary>
    /// List containing all network reads for one direction in an Exchange.
    /// </summary>
    public class NetTimestamps
    {
        List<NetTimestamp> listTS = new List<NetTimestamp>();
        /// <summary>
        /// A NetTimestamp represents a single read from the network, including the timestamp and the number of bytes read.
        /// </summary>
        public readonly struct NetTimestamp
        {
            public readonly Int64 tsRead;
            public readonly Int32 cbRead;
            internal NetTimestamp(Int64 ts, Int32 cb)
            {
                tsRead = ts;
                cbRead = cb;
            }
        }

        /// <summary>
        /// Record results of a read.
        /// </summary>
        /// <param name="tsRead">#ms since the start of the initial Read()</param>
        /// <param name="bytesRead"># bytes read</param>
        public void AddRead(Int64 tsRead, Int32 bytesRead)
        {
            listTS.Add(new NetTimestamp(tsRead, bytesRead));
        }
        public int Count
        {
            get { return listTS.Count; }
        }

        public NetTimestamp[] ToArray()
        {
            return listTS.ToArray();
        }

        // Part of the public API, but not sure it's used. Intent was to "fold" reads that occur within a
        // certain number of milliseconds of each other into a single read.
        public NetTimestamp[] ToFoldedArray(int iMSFold)
        {
            if (iMSFold <= 0 || listTS.Count < 2)
            {
                return listTS.ToArray();
            }

            List<NetTimestamp> listFolded = new List<NetTimestamp>(listTS.Count);

            Int64 tsGroupStart = listTS[0].tsRead;
            Int64 tsLastRead = listTS[0].tsRead;
            Int32 cbGroup = listTS[0].cbRead;

            for (int i = 1; i < listTS.Count; i++)
            {
                NetTimestamp nts = listTS[i];

                if ((nts.tsRead - tsLastRead) <= iMSFold)
                {
                    cbGroup += nts.cbRead;
                    tsLastRead = nts.tsRead;
                    continue;
                }

                listFolded.Add(new NetTimestamp(tsGroupStart, cbGroup));

                tsGroupStart = nts.tsRead;
                tsLastRead = nts.tsRead;
                cbGroup = nts.cbRead;
            }

            listFolded.Add(new NetTimestamp(tsGroupStart, cbGroup));

            return listFolded.ToArray();
        }

        public override string ToString()
        {
            StringBuilder sbTable = new StringBuilder();
            sbTable.AppendFormat("{0} reads.\n<br /><table>", listTS.Count);
            foreach (NetTimestamp nts in listTS)
            {
                sbTable.AppendLine($"<tr><td>{nts.tsRead}<td>{nts.cbRead:N0}</td><tr>\n");
            }
            sbTable.AppendLine("</table>");
            return sbTable.ToString();
        }
    }

}