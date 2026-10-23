using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Extensions
{
    internal sealed class FullscreenImageForm : Form
    {
        private readonly ImageCanvas canvas;

        internal FullscreenImageForm(Image image, Rectangle screenBounds)
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screenBounds;
            Text = "Image - Esc to close";
            canvas = new ImageCanvas(image) { Dock = DockStyle.Fill };
            Controls.Add(canvas);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            canvas.FitToWindow();
            canvas.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Escape:
                    Close();
                    return true;
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Shift | Keys.Oemplus:
                case Keys.Control | Keys.Add:
                    canvas.ZoomBy(1.25);
                    return true;
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract:
                    canvas.ZoomBy(1.0 / 1.25);
                    return true;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        private sealed class ImageCanvas : ScrollableControl
        {
            private readonly Image image;
            private double zoom = 1.0;
            private bool panning;
            private Point panStart;
            private Point panScrollStart;

            internal ImageCanvas(Image source)
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                AutoScroll = true;
                BackColor = Color.Black;
                TabStop = true;
                // The inspector disposes its image when the selected Exchange changes.
                image = (Image)source.Clone();
            }

            internal void FitToWindow()
            {
                zoom = Math.Min(1.0, Math.Min(
                    ClientSize.Width / (double)image.Width,
                    ClientSize.Height / (double)image.Height));
                AutoScrollMinSize = ScaledSize;
                AutoScrollPosition = Point.Empty;
                Invalidate();
            }

            private Size ScaledSize => new Size(
                Math.Max(1, (int)Math.Round(image.Width * zoom)),
                Math.Max(1, (int)Math.Round(image.Height * zoom)));

            private Rectangle ImageBounds
            {
                get
                {
                    Size size = ScaledSize;
                    return new Rectangle(
                        size.Width < ClientSize.Width ? (ClientSize.Width - size.Width) / 2 : AutoScrollPosition.X,
                        size.Height < ClientSize.Height ? (ClientSize.Height - size.Height) / 2 : AutoScrollPosition.Y,
                        size.Width, size.Height);
                }
            }

            internal void ZoomBy(double factor)
            {
                double nextZoom = Math.Max(1.0 / 64, Math.Min(16.0, zoom * factor));
                if (nextZoom == zoom) return;

                Rectangle previousBounds = ImageBounds;
                double imageCenterX = (ClientSize.Width / 2.0 - previousBounds.X) / zoom;
                double imageCenterY = (ClientSize.Height / 2.0 - previousBounds.Y) / zoom;
                zoom = nextZoom;
                AutoScrollMinSize = ScaledSize;
                AutoScrollPosition = new Point(
                    Math.Max(0, (int)Math.Round(imageCenterX * zoom - ClientSize.Width / 2.0)),
                    Math.Max(0, (int)Math.Round(imageCenterY * zoom - ClientSize.Height / 2.0)));
                Invalidate();
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                ZoomBy(Math.Pow(1.25, e.Delta / (double)SystemInformation.MouseWheelScrollDelta));
                if (e is HandledMouseEventArgs handled) handled.Handled = true;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                Focus();
                if (e.Button == MouseButtons.Left &&
                    (ScaledSize.Width > ClientSize.Width || ScaledSize.Height > ClientSize.Height))
                {
                    panStart = e.Location;
                    panScrollStart = AutoScrollPosition;
                    panning = true;
                    Capture = true;
                    Cursor = Cursors.Hand;
                }
                base.OnMouseDown(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (panning)
                {
                    AutoScrollPosition = new Point(
                        Math.Max(0, -panScrollStart.X + panStart.X - e.X),
                        Math.Max(0, -panScrollStart.Y + panStart.Y - e.Y));
                    Invalidate();
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Capture = false;
                base.OnMouseUp(e);
            }

            protected override void OnMouseCaptureChanged(EventArgs e)
            {
                if (!Capture)
                {
                    panning = false;
                    Cursor = Cursors.Default;
                }
                base.OnMouseCaptureChanged(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Rectangle bounds = ImageBounds;
                if (zoom == 1.0)
                {
                    e.Graphics.DrawImageUnscaled(image, bounds.Location);
                }
                else
                {
                    e.Graphics.InterpolationMode = zoom > 1.0 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    e.Graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) image.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
