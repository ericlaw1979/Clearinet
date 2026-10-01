using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clearinet
{
    internal static class Parser
    {
        internal static bool ParseHeaderLines(HTTPHeaders headers, string[] arrHeaderLines, int iStartFrom, ref string sErrors)
        {
            bool bResult = true;
            int ixToken, ixHeader;
            ixHeader = iStartFrom;
            HTTPHeaderItem oNewHeader = null;

            while (ixHeader < arrHeaderLines.Length)
            {
                ixToken = arrHeaderLines[ixHeader].IndexOf(':');

                // Per RFC2616, name must be at least one character, but value can be empty.
                if (ixToken > 0)
                {
                    oNewHeader = headers.Add(
                        arrHeaderLines[ixHeader].Substring(0, ixToken),
                        arrHeaderLines[ixHeader].Substring(ixToken + 1).TrimStart(new[] { ' ', '\t' }));
                }
                else
                {
                    if (0 == ixToken)
                    {
                        bResult = false;
                        oNewHeader = null;
                        sErrors += String.Format("Header missing name #{0}, {1}\n", 1 + ixHeader - iStartFrom, arrHeaderLines[ixHeader]);
                    }
                    else
                    {
                        bResult = false;
                        oNewHeader = headers.Add(arrHeaderLines[ixHeader], string.Empty);
                        sErrors += String.Format("Header missing colon #{0}, {1}\n", 1 + ixHeader - iStartFrom, arrHeaderLines[ixHeader]);
                    }
                }

                ixHeader++;

                // Lines starting with whitespace are a continuation of the value of the previous header.
                bool bIsContinuation = ((null != oNewHeader) && (ixHeader < arrHeaderLines.Length) && 
                                        arrHeaderLines[ixHeader].HasLeadingWhitespace());
                while (bIsContinuation)
                {
                    CApp.Log.Log("[Warning] HTTPHeader value was folded. Not all clients handle folded headers.");
                    oNewHeader.Value = (oNewHeader.Value + " " + arrHeaderLines[ixHeader].TrimStart(' ', '\t'));
                    ixHeader++;
                    bIsContinuation = ((ixHeader < arrHeaderLines.Length) && arrHeaderLines[ixHeader].HasLeadingWhitespace());
                }

            }
            return bResult;
        }

    }
}
