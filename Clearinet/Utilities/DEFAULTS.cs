namespace Clearinet
{
    public static class DEFAULTS
    {
        // "lists.common_request_headers"
        public const string sPopularRequestHeaders = "Accept,Accept-Encoding,Accept-Language,Authorization,Cache-Control,Connection,Content-Length,Content-Type,Cookie,Host,Origin,Referer,User-Agent,X-Forwarded-For";

        // "lists.common_response_headers"  
        public const string sPopularResponseHeaders = "Access-Control-Allow-Origin,Age,Allow,Cache-Control,Connection,Content-Encoding,Content-Length,Content-Type,ETag,Expires,Last-Modified,Location,Server,Set-Cookie,Strict-Transport-Security,Vary,WWW-Authenticate";

        // "lists.common_exchange_flags"  // TODO: Flesh this out.
        public const string sPopularExchangeFlags = "X-BreakRequest,X-BreakResponse,X-ClientIP,ui-comments";
    }
}
