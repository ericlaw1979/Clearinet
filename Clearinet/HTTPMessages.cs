using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clearinet
{
    public class HTTPHeaders
    {
        public void RenameHeaderItems(string fromHeaderName, string toHeaderName)
        {
        }
        public string this[string sHeaderName]
        {
            get { return "TODO"; }
            set { }
        }
        public void Add(string sHeaderName, string sHeaderValue)
        {
        }
    }
    public class HTTPRequestHeaders: HTTPHeaders
    {
    
    }
    public class HTTPResponseHeaders:HTTPHeaders
    {
        
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
