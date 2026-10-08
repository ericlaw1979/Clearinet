/*using Clearinet;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TestExtensions
{
    public sealed class HexRequestInspector : RequestInspectorBase
    {
        HexViewer hexViewer;
        private byte[] _body;
        private HTTPRequestHeaders _headers;

        private bool _bShowHeaders = CApp.Prefs.GetBoolPref("inspectors.HexRequestInspector.ShowHeaders", true);

        public override void AddToTab(TabPage o)
        {
            o.Text = "HexView";
        }

        public override void Clear()
        {
            hexViewer.Clear();
        }

        public override int GetOrder()
        {
            return -100;
        }

        public override void Assign(HTTPRequestHeaders headersRequest, byte[] arrBody, bool bReadOnly)
        {
            if (headersRequest == null && !arrBody.HasData())
            {
                Clear();
                return;
            }

            // TODO: Changing Readonly results in refilling the entire UI and losing scroll position. Think of a way to fix that.

            byte[] arrHeaderBytes;
            if (_bShowHeaders && (headersRequest != null))
            {
                arrHeaderBytes = headersRequest.ToByteArray();
                //hexViewer.hexBox.BodyOffset = arrHeaderBytes.Length;
            }
            else
            {
                //myControl.hexViewer.BodyOffset = 0;
                arrHeaderBytes = Array.Empty<byte>();
            }

            arrBody = arrBody.FastClone() ?? Array.Empty<byte>();

            byte[] arrCombined = arrHeaderBytes.Append(arrBody);

            freeOldByteProvider();
            if (bReadOnly)
            {
                // System.Diagnostics.Trace.WriteLine("HexResponseViewer: Using StaticByteProvider (fastest) for read-only viewing.");
                StaticByteProvider staticByteProvider = new StaticByteProvider(arrCombined);
                hexViewer.hexBox.ByteProvider = staticByteProvider;
            }
            else
            {
                // System.Diagnostics.Trace.WriteLine("HexRequestViewer: Using DynamicByteProvider (fast) to enable editing.");
                DynamicByteProvider dynamicByteProvider = new DynamicByteProvider(arrCombined);
                hexViewer.hexBox.ByteProvider = dynamicByteProvider;
                dynamicByteProvider.Changed += new EventHandler(dynamicByteProvider_Changed);
            }

            hexViewer.hexBox.ByteProvider.ApplyChanges(); // Reset dirty flag
        }

        void freeOldByteProvider()
        {
            if (null == hexViewer) return;
            if (null == hexViewer.hexBox.ByteProvider) return;

            DynamicByteProvider dbp = hexViewer.hexBox.ByteProvider as DynamicByteProvider;
            if (null != dbp)
            {
                hexViewer.hexBox.ByteProvider.Changed -= new EventHandler(dynamicByteProvider_Changed);
            }

            IDisposable byteProvider = hexViewer.hexBox.ByteProvider as IDisposable;
            if (null != byteProvider)
            {
                byteProvider.Dispose();
            }

            hexViewer.hexBox.ByteProvider = null;
        }

        void dynamicByteProvider_Changed(object sender, EventArgs e)
        {
            if (hexViewer.hexBox.ReadOnly) return;
            if (!_bShowHeaders) return;
            byte[] arrData = ((hexViewer.hexBox.ByteProvider as DynamicByteProvider).Bytes.ToArray());
            HTTPHeaderParseWarnings hpw;
            int iZero = 0;
            int iBodyOffset = 0;
            Parser.FindEntityBodyOffsetFromArray(arrData, out iZero, out iBodyOffset, out hpw);
            //hexViewer.hexBox.BodyOffset = iBodyOffset;
        }

        public bool bReadOnly
        {
            get
            {
                if (null == hexViewer) return true;
                return hexViewer.hexBox.ReadOnly;
            }
            set
            {
                EnsureReady();
                hexViewer.hexBox.ReadOnly = value;
                if (value)
                {
                    hexViewer.hexViewer.BackColor = CONFIG.colorDisabledEdit;
                    hexViewer.tsmiShowHeaders.Enabled = true;
                    hexViewer.sbpInsertMode.Text = "Readonly";
                }
                else
                {
                    // TODO: allow user to update visibility of the headers during editing
                    hexViewer.tsmiShowHeaders.Enabled = false;

                    hexViewer.hexViewer.BackColor = Color.FromKnownColor(KnownColor.Window);
                    hexViewer.sbpInsertMode.Text = (hexViewer.hexViewer.InsertActive) ? "Insert" : "Overwrite";

                    // If we're entering Read/WriteMode we may need to upgrade our byte provider.
                    if (!bBulkUpdating && (hexViewer.ByteProvider is StaticByteProvider))
                    {
                        byte[] arrCurrent = hexViewer.ByteProvider.GetAllBytes();
                        freeOldByteProvider();
                        hexViewer.ByteProvider = new DynamicByteProvider(arrCurrent);
                    }
                }
            }
        }

        public override void Clear()
        {
            _headers = null;
            _body = null;

            EnsureReady();
            freeOldByteProvider();
        }

        public override void ShowAboutBox()
        {
            EnsureReady();
            string sHexBoxVer = hexViewer.hexBox.GetType().Assembly.ToString();
            CApp.DoNotifyUser(
                 $"HexRequestInspector\n\nBe.HexEditor Control\nhttps://github.com/bernharde/hexbox/\n{sHexBoxVer}",
                "About Inspector");
        }

        public override void SetFontSize(float flSizeInPoints)
        {
            hexViewer?.SetFontSize(flSizeInPoints);
        }

        private void _UpdateFieldsIfDirty()
        {
            if (!bDirty) return;
            Debug.Assert((null != hexViewer.hexBox.ByteProvider), "Byte provider MUST be non-null here");

            byte[] arrContent = hexViewer.hexBox.GetAllBytes();

            if (_bShowHeaders)
            {
                int iHeaderLen = 0;
                int iBodyOffset = 0;
                HTTPHeaderParseWarnings hhpw = HTTPHeaderParseWarnings.None;

                bool bHeadersFound = Parser.FindEntityBodyOffsetFromArray(arrContent, out iHeaderLen, out iBodyOffset, out hhpw);

                if (!bHeadersFound)
                {
                    iBodyOffset = 0;
                }

                int iBodyLength = (arrContent.Length - iBodyOffset);
                _body = new byte[iBodyLength];
                Buffer.BlockCopy(arrContent, iBodyOffset, _body, 0, iBodyLength);
                try
                {
                    HTTPRequestHeaders oRH = Parser.ParseRequest(CONFIG.encodingHeaders.GetString(arrContent, 0, iBodyOffset));
                    _headers = oRH;
                }
                catch (Exception eX)
                {
                    MessageBox.Show(eX.Message + "\n\n" + eX.StackTrace, "Unable to parse headers");
                }
            }
            else
            {
                _body = arrContent;
            }

            UnsetDirtyFlag();
        }

        public byte[] body
        {
            get
            {
                EnsureReady();
                _UpdateFieldsIfDirty();
                return _body;
            }
            set
            {
                EnsureReady();
                body = value;

                UpdateDisplay();
            }
        }

        public HTTPRequestHeaders headers
        {
            get
            {
                EnsureReady();
                _UpdateFieldsIfDirty();
                return _headers;
            }
            set
            {
                _headers = value;
            }
        }

        public bool bDirty
        {
            get
            {
                if (null == hexViewer) return false;
                return (null != hexViewer.hexBox.ByteProvider) ? hexViewer.hexBox.ByteProvider.HasChanges() : false;
            }
        }

        public override bool UnsetDirtyFlag()
        {
            if (null != hexViewer.hexBox.ByteProvider)
            {
                hexViewer.hexBox.ByteProvider.ApplyChanges();
                return true;
            }
            return false;
        }

        private void EnsureReady()
        {
            if (null != myControl) return;


            //myControl.hexViewer.Font = new Font(myControl.hexViewer.Font.FontFamily, myTabPage.Font.Size);
            hexViewer.hexBox.HeaderColor = CONFIG.GetColor("inspectors.HexRequestInspector.HeaderColor", Color.Blue);
            myTabPage.Controls.Add(hexViewer);
            hexViewer.hexBox.Dock = DockStyle.Fill;

            hexViewer.hexBox.UpdateBytesPerLine();
        }


    }
}
*/