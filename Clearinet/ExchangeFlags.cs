using System;

namespace Clearinet
{
    [Flags]
    public enum ExchangeFlags
    {
        ImportedFromOtherTool,
        ResponseStreamed,
        RequestBodyDropped,
        ResponseBodyDropped
    }
}
