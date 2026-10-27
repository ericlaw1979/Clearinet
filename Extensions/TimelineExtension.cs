using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class TimelineExtension : IAppExtension
    {
        private TabPage _timelineTab;
        private TimelineWaterfallControl _timelineView;

        public void OnLoad()
        {
            if (CApp.UI == null || CApp.UI.tabsViews == null)
            {
                return;
            }

            if (_timelineTab != null)
            {
                return;
            }

            _timelineView = new TimelineWaterfallControl();
            _timelineTab = new TabPage("Timeline")
            {
                Padding = new Padding(4),
                AutoScroll = true
            };
            _timelineView.Dock = DockStyle.Fill;
            _timelineTab.Controls.Add(_timelineView);
            CApp.UI.tabsViews.TabPages.Add(_timelineTab);
            CApp.CalculateReport += OnCalculateReport;
        }

        public void OnBeforeUnload()
        {
            CApp.CalculateReport -= OnCalculateReport;

            if (_timelineTab != null && CApp.UI != null && CApp.UI.tabsViews != null)
            {
                CApp.UI.tabsViews.TabPages.Remove(_timelineTab);
                _timelineTab.Dispose();
                _timelineTab = null;
            }

            if (_timelineView != null)
            {
                _timelineView.Dispose();
                _timelineView = null;
            }
        }

        private void OnCalculateReport(Exchange[] arrExchanges)
        {
            if (_timelineView == null)
            {
                return;
            }

            if (CApp.UI != null && CApp.UI.InvokeRequired)
            {
                CApp.UI.BeginInvoke(new Action(() => OnCalculateReport(arrExchanges)));
                return;
            }

            _timelineView.Render(arrExchanges);
        }
    }

    internal sealed class TimelineWaterfallControl : UserControl
    {
        private readonly List<TimelineRow> _rows = new List<TimelineRow>();
        private readonly ContextMenuStrip _contextMenu;
        private readonly ToolTip _tooltip;
        private TimelineRow _contextRow;
        private TimelineRow _lastTooltipRow;
        private Point _lastTooltipPoint = Point.Empty;
        private DateTime _minTime = DateTime.MinValue;
        private DateTime _maxTime = DateTime.MinValue;
        private const int TooltipMoveThreshold = 5;

        private const int LeftLabelWidth = 160;
        private const int RightLabelWidth = 110;
        private const int TopPadding = 24;
        private const int RowHeight = 20;
        private const int ChartPadding = 12;

        public TimelineWaterfallControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            AutoScroll = true;
            ContextMenuStrip = _contextMenu = new ContextMenuStrip();
            _tooltip = new ToolTip
            {
                AutomaticDelay = 200,
                AutoPopDelay = 5000,
                InitialDelay = 100,
                ReshowDelay = 50,
                ShowAlways = false,
                ToolTipIcon = ToolTipIcon.Info
            };
            var copyItem = new ToolStripMenuItem("Copy to Clipboard");
            copyItem.Click += (s, e) => CopyCurrentImageToClipboard();
            _contextMenu.Items.Add(copyItem);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            MouseMove += OnTimelineMouseMove;
            MouseLeave += (s, e) =>
            {
                _lastTooltipRow = null;
                _lastTooltipPoint = Point.Empty;
                _tooltip.Hide(this);
            };
        }

        private TimelineRow GetRowAtPoint(Point point)
        {
            if (_rows.Count == 0)
            {
                return null;
            }

            int rowY = point.Y - AutoScrollPosition.Y - TopPadding;
            if (rowY < 0)
            {
                return null;
            }

            int rowIndex = rowY / RowHeight;
            if (rowIndex >= _rows.Count)
            {
                return null;
            }

            return _rows[rowIndex];
        }

        private void InspectExchange(Exchange exchange)
        {
            if (exchange == null || CApp.UI == null || CApp.UI.lvExchanges == null)
            {
                return;
            }

            var list = CApp.UI.lvExchanges;
            list.SelectedItems.Clear();
            foreach (ListViewItem item in list.Items)
            {
                if (ReferenceEquals(item.Tag, exchange))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    list.Focus();
                    break;
                }
            }
        }

        private void OnTimelineMouseMove(object sender, MouseEventArgs e)
        {
            var row = GetRowAtPoint(e.Location);
            if (row == null)
            {
                _lastTooltipRow = null;
                _lastTooltipPoint = Point.Empty;
                _tooltip.Hide(this);
                return;
            }

            if (_lastTooltipRow == row)
            {
                if (_lastTooltipPoint != Point.Empty && Distance(_lastTooltipPoint, e.Location) < TooltipMoveThreshold)
                {
                    return;
                }

                return;
            }

            if (_lastTooltipPoint != Point.Empty && Distance(_lastTooltipPoint, e.Location) < TooltipMoveThreshold && _lastTooltipRow != null)
            {
                return;
            }

            _lastTooltipRow = row;
            _lastTooltipPoint = e.Location;
            _tooltip.SetToolTip(this, GetTooltipText(row));
        }

        private static int Distance(Point p1, Point p2)
        {
            int dx = p2.X - p1.X;
            int dy = p2.Y - p1.Y;
            return (int)Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static string GetTooltipText(TimelineRow row)
        {
            if (row == null || row.Exchange == null)
            {
                return string.Empty;
            }

            var exchange = row.Exchange;
            var timers = exchange.Timers;
            var url = GetExchangeUrl(exchange);
            if (url.Length > 128)
            {
                url = url.Substring(0, 128);
            }

            return string.Join(Environment.NewLine,
                "ID: id:" + exchange.id,
                "URL: " + url,
                "ClientBeginRequest: " + FormatTimerValue(timers == null ? DateTime.MinValue : timers.ClientBeginRequest),
                "ServerBeginResponse: " + FormatTimerValue(timers == null ? DateTime.MinValue : timers.ServerBeginResponse),
                "ServerDoneResponse: " + FormatTimerValue(timers == null ? DateTime.MinValue : timers.ServerDoneResponse),
                "ClientDoneResponse: " + FormatTimerValue(timers == null ? DateTime.MinValue : timers.ClientDoneResponse));
        }

        private static string FormatTimerValue(DateTime value)
        {
            return value == DateTime.MinValue ? "n/a" : value.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        private static string GetExchangeUrl(Exchange exchange)
        {
            if (exchange == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(exchange.fullUrl))
            {
                return exchange.fullUrl;
            }

            if (exchange.RequestHeaders != null && !string.IsNullOrEmpty(exchange.RequestHeaders.RequestPath))
            {
                return exchange.RequestHeaders.RequestPath;
            }

            return string.Empty;
        }

        private void DrawResponseBar(Graphics g, TimelineRow row, float barLeft, float barRight, float responseX, float barY, float barHeight, Font font)
        {
            float barWidth = Math.Max(2f, barRight - barLeft);
            float actualBarRight = barLeft + barWidth;
            Color barColor = CONFIG.GetExchangeColor(row.Exchange);

            using (var barBrush = new SolidBrush(barColor))
            using (var barPen = new Pen(barColor, 1f))
            using (var responsePen = new Pen(Color.Orange, 1f))
            {
                if (row.Exchange.BitFlags.HasFlag(ExchangeFlags.ResponseStreamed))
                {
                    g.FillRectangle(barBrush, barLeft, barY, barWidth, barHeight);
                }
                else
                {
                    for (float lineY = barY + 1; lineY < barY + barHeight; lineY += 2)
                    {
                        g.DrawLine(barPen, barLeft, lineY, actualBarRight, lineY);
                    }
                }

                g.DrawLine(responsePen, responseX, barY - 2f, responseX, barY + barHeight + 2f);
            }

            Color clientReuseColor = row.Exchange.BitFlags.HasFlag(ExchangeFlags.ClientPipeReused) ? Color.Green : Color.Red;
            Color serverReuseColor = row.Exchange.BitFlags.HasFlag(ExchangeFlags.ServerPipeReused) ? Color.Green : Color.Red;
            using (var clientReusePen = new Pen(clientReuseColor, 1f))
            using (var serverReusePen = new Pen(serverReuseColor, 1f))
            {
                const float dotSize = 4f;
                float dotX = (float)Math.Floor(barLeft - dotSize - 2f);
                var graphicsState = g.Save();
                try
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawEllipse(clientReusePen, dotX, (float)Math.Round(barY), dotSize, dotSize);
                    g.DrawEllipse(serverReusePen, dotX, (float)Math.Round(barY + barHeight - dotSize), dotSize, dotSize);
                }
                finally
                {
                    g.Restore(graphicsState);
                }
            }

            int statusCode = row.Exchange.responseCode;
            string statusSymbol = (statusCode >= 300 && statusCode < 400) ? (statusCode == 304 ? "\uD83D\uDCBE" : "\u21A9") :
                (statusCode >= 400 && statusCode < 600 ? "\u26A0" : null);
            if (statusSymbol != null)
            {
                Color symbolColor = statusCode >= 400 ? Color.DarkRed : Color.DimGray;
                using (var symbolFont = new Font("Segoe UI Emoji", font.SizeInPoints, FontStyle.Regular))
                using (var symbolBrush = new SolidBrush(symbolColor))
                using (var symbolFormat = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                })
                {
                    var symbolBounds = new RectangleF((float)Math.Ceiling(actualBarRight) + 3f, barY,
                        g.MeasureString(statusSymbol, symbolFont).Width + 2f, barHeight);
                    g.DrawString(statusSymbol, symbolFont, symbolBrush, symbolBounds, symbolFormat);
                }
            }
        }

        private static void DrawRowLabel(Graphics g, TimelineRow row, int y, float barLeft, Font font)
        {
            var labelBounds = new RectangleF(8, y, LeftLabelWidth - ChartPadding - 8, RowHeight);
            if (barLeft - LeftLabelWidth > 50f)
            {
                using (var connectorPen = new Pen(Color.LightGray, 1f))
                {
                    connectorPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    float centerY = labelBounds.Top + labelBounds.Height / 2f;
                    g.DrawLine(connectorPen, labelBounds.Right + 2f, centerY, barLeft - 8f, centerY);
                }
            }

            using (var labelFormat = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                g.DrawString(row.Label, font, Brushes.DimGray, labelBounds, labelFormat);
            }
        }

        private void CopyCurrentImageToClipboard()
        {
            if (_contextRow == null || _contextRow.Exchange == null)
            {
                return;
            }

            int bitmapWidth = Math.Max(640, LeftLabelWidth + 480);
            int bitmapHeight = TopPadding + (_rows.Count * RowHeight) + 2;
            Bitmap bitmap = new Bitmap(bitmapWidth, bitmapHeight);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                double totalRangeMs = Math.Max(1d, (_maxTime - _minTime).TotalMilliseconds);
                int axisTicks = 6;
                using (var tinyFont = new Font(Font.FontFamily, 8f, FontStyle.Regular))
                {
                    for (int i = 0; i <= axisTicks; i++)
                    {
                        int x = LeftLabelWidth + (int)(i * (double)(bitmapWidth - LeftLabelWidth - 20) / axisTicks);
                        DateTime tick = _minTime.AddMilliseconds((double)i / axisTicks * totalRangeMs);
                        string text = FormatRelativeOffset(tick - _minTime);
                        var size = TextRenderer.MeasureText(text, tinyFont);
                        g.DrawString(text, tinyFont, Brushes.DimGray, x - (size.Width / 2f), 4f);
                    }

                    for (int i = 0; i < _rows.Count; i++)
                    {
                        var row = _rows[i];
                        int y = TopPadding + (i * RowHeight);

                        float requestX = LeftLabelWidth + (float)((row.Start - _minTime).TotalMilliseconds / totalRangeMs * (bitmapWidth - LeftLabelWidth - 20));
                        float responseX = LeftLabelWidth + (float)((row.ResponseStart - _minTime).TotalMilliseconds / totalRangeMs * (bitmapWidth - LeftLabelWidth - 20));
                        float endX = LeftLabelWidth + (float)((row.End - _minTime).TotalMilliseconds / totalRangeMs * (bitmapWidth - LeftLabelWidth - 20));

                        float barLeft = Math.Min(requestX, endX);
                        float barRight = Math.Max(requestX, endX);
                        float barY = y + 3;
                        float barHeight = RowHeight - 6;

                        DrawRowLabel(g, row, y, barLeft, tinyFont);
                        DrawResponseBar(g, row, barLeft, barRight, responseX, barY, barHeight, tinyFont);
                    }
                }
            }

            Clipboard.SetImage(bitmap);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            var row = GetRowAtPoint(e.Location);
            if (row != null)
            {
                InspectExchange(row.Exchange);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                var row = GetRowAtPoint(e.Location);
                if (row != null)
                {
                    _contextRow = row;
                    ContextMenuStrip.Show(this, e.Location);
                }
            }
        }

        public void Render(Exchange[] arrExchanges)
        {
            _rows.Clear();

            if (arrExchanges == null || arrExchanges.Length == 0)
            {
                _minTime = DateTime.MinValue;
                _maxTime = DateTime.MinValue;
                AutoScrollMinSize = new Size(640, 160);
                Invalidate();
                return;
            }

            foreach (Exchange exchange in arrExchanges)
            {
                if (exchange == null || exchange.Timers == null)
                {
                    continue;
                }

                var timers = exchange.Timers;
                DateTime start = timers.ClientBeginRequest;
                DateTime responseStart = timers.ServerBeginResponse == DateTime.MinValue ? start : timers.ServerBeginResponse;
                DateTime end = timers.ServerDoneResponse;
                if (end == DateTime.MinValue)
                {
                    end = timers.ClientDoneResponse == DateTime.MinValue ? start : timers.ClientDoneResponse;
                }

                if (start == DateTime.MinValue || end == DateTime.MinValue)
                {
                    continue;
                }

                if (end < start)
                {
                    end = start;
                }

                if (responseStart == DateTime.MinValue)
                {
                    responseStart = start;
                }

                _rows.Add(new TimelineRow
                {
                    Exchange = exchange,
                    Start = start,
                    ResponseStart = responseStart,
                    End = end,
                    Label = GetRowLabel(exchange)
                });
            }

            if (_rows.Count == 0)
            {
                _minTime = DateTime.MinValue;
                _maxTime = DateTime.MinValue;
                AutoScrollMinSize = new Size(640, 160);
                Invalidate();
                return;
            }

            _minTime = _rows.Min(r => r.Start);
            _maxTime = _rows.Max(r => r.End);
            if (_maxTime <= _minTime)
            {
                _maxTime = _minTime.AddMilliseconds(1);
            }

            AutoScrollMinSize = new Size(640, Math.Max(160, TopPadding + (_rows.Count * RowHeight) + 32));
            Invalidate();
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            _lastTooltipRow = null;
            _lastTooltipPoint = Point.Empty;
            _tooltip.Hide(this);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.Clear(BackColor);

            if (_rows.Count == 0)
            {
                TextRenderer.DrawText(g, "No selected Exchanges", Font, new Point(12, 8), Color.Gray);
                return;
            }

            var availableWidth = Math.Max(240, ClientSize.Width - LeftLabelWidth - RightLabelWidth - ChartPadding - 12);
            var totalRangeMs = Math.Max(1d, (_maxTime - _minTime).TotalMilliseconds);
            var axisTicks = 6;
            var graphicsState = g.Save();
            try
            {
                g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
                using (var tinyFont = new Font(Font.FontFamily, 8f, FontStyle.Regular))
                {
                    for (int i = 0; i <= axisTicks; i++)
                    {
                        int x = LeftLabelWidth + (int)(i * (double)availableWidth / axisTicks);
                        DateTime tick = _minTime.AddMilliseconds((double)i / axisTicks * totalRangeMs);
                        g.DrawLine(Pens.LightGray, x, TopPadding, x, TopPadding + (_rows.Count * RowHeight));
                        string text = FormatRelativeOffset(tick - _minTime);
                        var size = TextRenderer.MeasureText(text, tinyFont);
                        g.DrawString(text, tinyFont, Brushes.DimGray, x - (size.Width / 2f), 4f);
                    }

                    for (int i = 0; i < _rows.Count; i++)
                    {
                        var row = _rows[i];
                        int y = TopPadding + (i * RowHeight);

                        float requestX = LeftLabelWidth + (float)((row.Start - _minTime).TotalMilliseconds / totalRangeMs * availableWidth);
                        float responseX = LeftLabelWidth + (float)((row.ResponseStart - _minTime).TotalMilliseconds / totalRangeMs * availableWidth);
                        float endX = LeftLabelWidth + (float)((row.End - _minTime).TotalMilliseconds / totalRangeMs * availableWidth);

                        var barLeft = Math.Min(requestX, endX);
                        var barRight = Math.Max(requestX, endX);
                        var barY = y + 3;
                        var barHeight = RowHeight - 6;

                        DrawRowLabel(g, row, y, barLeft, tinyFont);
                        DrawResponseBar(g, row, barLeft, barRight, responseX, barY, barHeight, tinyFont);
                    }
                }
            }
            finally
            {
                g.Restore(graphicsState);
            }
        }

        private static string FormatRelativeOffset(TimeSpan offset)
        {
            double totalMilliseconds = Math.Max(0, offset.TotalMilliseconds);
            if (totalMilliseconds < 1000)
            {
                return $"{(int)Math.Round(totalMilliseconds)}ms";
            }

            return $"{totalMilliseconds / 1000d:0.###}s";
        }

        private static string GetRowLabel(Exchange exchange)
        {
            if (exchange == null) return string.Empty;

            string candidate = string.Empty;
            if (!string.IsNullOrEmpty(exchange.fullUrl))
            {
                candidate = exchange.fullUrl;
            }
            else if (exchange.RequestHeaders != null)
            {
                candidate = exchange.RequestHeaders.RequestPath ?? string.Empty;
            }

            if (string.IsNullOrEmpty(candidate))
            {
                return "id:" + exchange.id;
            }

            string url = candidate.Trim();
            if (url.IndexOf("://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                try
                {
                    Uri uri = new Uri(url);
                    string path = uri.AbsolutePath ?? string.Empty;
                    if (!string.IsNullOrEmpty(path))
                    {
                        candidate = path;
                    }
                    else
                    {
                        candidate = uri.PathAndQuery ?? string.Empty;
                    }
                }
                catch (UriFormatException)
                {
                }
            }

            if (!string.IsNullOrEmpty(candidate))
            {
                candidate = candidate.Replace('\\', '/');
                int lastSlash = candidate.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < candidate.Length - 1)
                {
                    candidate = candidate.Substring(lastSlash + 1);
                }
                else if (lastSlash == candidate.Length - 1)
                {
                    candidate = string.Empty;
                }
            }

            if (string.IsNullOrEmpty(candidate))
            {
                if (!string.IsNullOrEmpty(exchange.fullUrl))
                {
                    candidate = exchange.fullUrl;
                }
                else candidate = "?";
            }

            if (candidate.Length > 20)
            {
                candidate = candidate.Substring(0, 20);
            }

            return candidate;
        }

        private sealed class TimelineRow
        {
            public Exchange Exchange { get; set; }
            public DateTime Start { get; set; }
            public DateTime ResponseStart { get; set; }
            public DateTime End { get; set; }
            public string Label { get; set; }
        }
    }
}
