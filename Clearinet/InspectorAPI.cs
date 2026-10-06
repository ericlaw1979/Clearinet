using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Clearinet
{
    // TODO: write code that does FindBestInspector based on a given Exchange. Then call it in actInspectSession/Exchange

    [Flags]
    public enum InspectorFlags
    {
        None,

        /// <summary>
        /// Allow edits even if not Unlocked or paused for tampering.
        /// </summary>
        AlwaysCommitUpdates,

        /// <summary>
        /// Only relevant for ResponseInspectors.
        /// The AutoResponder allows editing responses, but not all Inspectors support useful editing.
        /// </summary>
        HideInAutoResponder,

        HideInNewWindow
    }

    public abstract class RequestInspectorBase : InspectorBase
    {
        public abstract void Assign(HTTPRequestHeaders headersRequest, byte[] arrBody, bool bReadOnly);

        public HTTPRequestHeaders headers { get; protected set; }

        internal override bool CommitIsAllowed(Exchange oX)
        {
            // If the Exchange isn't at a breakpoint or unlocked, bail on changes.
            if ((oX.state != ExchangeState.HandTamperRequest) &&
                    !oX.oFlags.ContainsKey("x-Unlocked") &&
                    !GetFlags().HasFlag(InspectorFlags.AlwaysCommitUpdates))
            {
                Debug.Assert(false, "Dirty but readonly??");
                return false;
            }
            return true;
        }
    }

    public abstract class ResponseInspectorBase : InspectorBase
    {
        public abstract void Assign(HTTPResponseHeaders headersResponse, byte[] arrBody, bool bReadOnly);
        public HTTPResponseHeaders headers { get; protected set; }
        internal override bool CommitIsAllowed(Exchange oX)
        {
            // If the Exchange isn't at a breakpoint or unlocked, bail on changes.
            if ((oX.state != ExchangeState.HandTamperResponse) &&
                !oX.oFlags.ContainsKey("x-Unlocked") &&
                !GetFlags().HasFlag(InspectorFlags.AlwaysCommitUpdates))
            {
                Debug.Assert(false, "Dirty but readonly??");
                return false;
            }
            return true;
        }
    }

    public abstract class InspectorBase : IAppExtension
    {
        internal InspectorBase() { }
        internal virtual string TabTitle
        {
            get
            {
                return this.GetType().FullName;
            }
        }

        /// <summary>
        /// Is the Inspector in Edit mode?
        /// </summary>
        bool bReadOnly { get; set; }

        /// <summary>
        /// Set to false when an Exchange is assigned.
        /// Set it to true if the user makes any edits.
        /// </summary>
        bool bDirty { get; set; }

        internal abstract bool CommitIsAllowed(Exchange oX);

        /// <summary>
        /// Request or Response body.
        /// </summary>
        public byte[] body { get; protected set; }

        public abstract void Clear();

        public virtual void OnLoad()
        {
            if (CApp.isTracing) Trace.WriteLine($"Loading Inspector {this}");
        }
        public virtual void OnBeforeUnload()
        {
            if (CApp.isTracing) Trace.WriteLine($"Unloading Inspector {this}");
        }

        public abstract void AddToTab(TabPage o);
        public abstract int GetOrder();

        public virtual void SetFontSize(float flSize)
        {
            if (CApp.isTracing) Trace.WriteLine($"Inspector:{this} ignored font size update.");
        }
        public virtual void ShowAboutBox()
        {
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(GetType().Assembly.Location);
            CApp.DoNotifyUser(this.ToString() + "\n\n" + fvi.ToString(), "About Inspector", MessageBoxIcon.Information);
        }

        // TODO: Should we cache a copy of the tabpage and use it for copies
        // and for default font-size adjustments?
        public virtual void CopyAsImage(TabPage tbp)
        {
            if ((0 == tbp.Width * tbp.Height))
            {
                CApp.DoNotifyUser("The Inspector must visible to copy it.", "Invalid State", MessageBoxIcon.Error);
                return;
            }
            try
            {
                var bitmap = new Bitmap(tbp.Width, tbp.Height);
                tbp.DrawToBitmap(bitmap, tbp.ClientRectangle);
                Clipboard.SetDataObject(new DataObject(bitmap), true);
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Failed to copy");
            }
        }

        /// <summary>
        /// By default, Inspectors have no special behavior.
        /// </summary>
        public virtual InspectorFlags GetFlags()
        {
            return InspectorFlags.None;
        }

        /// <summary>
        /// Return 1 to 100 for how much this Exchange "wants" a given Content Type.
        /// Return 1000 if the MIME is a custom MIME for which your Inspector is the one
        /// that the user *definitely* will want used.
        public virtual int ScoreForContentType(string sMIME)
        {
            return 0;
        }

        public virtual int ScoreForExchange(Exchange oX)
        {
            // Content-Type should be meaningless for tunnels
            if (oX.isTunnel) return 0;

            string contentType = String.Empty;
            if (this is RequestInspectorBase)
            {
                if (!oX.RequestBody.HasData()) return 0;
                contentType = oX.RequestHeaders["Content-Type"];
            }
            else
            {
                Debug.Assert(this is ResponseInspectorBase);
                if (!oX.ResponseBody.HasData()) return 0;
                contentType = oX.ResponseHeaders["Content-Type"];
            }

            // Trim any MIME type parameters (e.g. "; charset=utf-8")
            contentType = contentType.TrimAfter(';');
            return ScoreForContentType(contentType);
        }

        /// <summary>
        /// By default, assigning an Inspector to inspect an Exchange will set the
        /// extension's Headers, Body, and Readonly properties. But an inspector may
        /// override the method to do anything it wants.
        /// </summary>
        public virtual void AssignExchange(Exchange oX)
        {
            // Figure out if we're a Request Inspector or Response Inspector and then
            // call the correct assignment method.
            RequestInspectorBase reqThis = (this as RequestInspectorBase);
            if (null != reqThis)
            {
                reqThis.Assign(oX.RequestHeaders, oX.RequestBody,
                    (oX.state != ExchangeState.HandTamperRequest) &&
                            !oX.oFlags.ContainsKey("x-Unlocked"));
                return;
            }

            ResponseInspectorBase respThis = (this as ResponseInspectorBase);
            if (null != respThis)
            {
                respThis.Assign(oX.ResponseHeaders, oX.ResponseBody, (oX.state != ExchangeState.HandTamperResponse) &&
                    !oX.oFlags.ContainsKey("x-Unlocked"));
                return;
            }
            Debug.Assert(false);
        }

        /// <summary>
        /// Called when the App wants the Inspector to commit any user edits. 
        /// Override to provide your own behavior, otherwise the base class will examine the
        /// dirty property and if set, will query the Inspector for the headers and body and
        /// if non-null will update the Exchange.
        /// Overrides of this function MUST not alter the Exchange unless the Exchange's state is
        /// at .HandTamper* or the oFlags contains an "X-Unlocked" key.
        /// </summary>
        public virtual bool CommitAnyChanges(Exchange oX)
        {
            bool bMadeChanges = false;

            if (!bDirty)
            {
                // No changes? Bail.
                return false;
            }

            if (!CommitIsAllowed(oX))
            {
                Debug.Assert(false, "Dirty but readonly??");
                return false;
            }

            RequestInspectorBase ibRequest = (this as RequestInspectorBase);
            if (null != ibRequest)
            {
                if (null != ibRequest.headers)
                {
                    bMadeChanges = true;
                    oX.RequestHeaders = ibRequest.headers;
                }

                // Update the Request body. TODO We can hoist this up to a common implementation
                if (null != ibRequest.body)
                {
                    bMadeChanges = true;
                    oX.RequestBody = ibRequest.body;
                    // Set the Content-Length header if it exists. TODO: Should we add one if it didn't?
                    // TODO: ISSUE: We may need to remove Transfer-Encoding.
                    if ((null != oX.RequestHeaders) && oX.RequestHeaders.Exists("Content-Length"))
                    {
                        oX.RequestHeaders["Content-Length"] = oX.RequestBody.LongLength.ToString();
                    }
                }

                // We've committed the changes.
                ibRequest.bDirty = false;

                return bMadeChanges;
            }

            ResponseInspectorBase ibResponse = (this as ResponseInspectorBase);
            if (null != ibResponse)
            {
                // Update the Response Headers
                if (null != ibResponse)
                {
                    bMadeChanges = true;
                    oX.ResponseHeaders = ibResponse.headers;
                }

                // Update the Response Body.  TODO We can hoist this up to a common implementation!
                if (null != ibResponse.body)
                {
                    bMadeChanges = true;
                    oX.ResponseBody = ibResponse.body;

                    if ((null != oX.ResponseHeaders) && oX.ResponseHeaders.Exists("Content-Length"))
                    {
                        oX.ResponseHeaders["Content-Length"] = oX.ResponseBody.LongLength.ToString();
                    }
                }

                bDirty = false;
                return bMadeChanges;
            }

            Debug.Assert(false, "Impossible!");
            return false;
        }
    }
}