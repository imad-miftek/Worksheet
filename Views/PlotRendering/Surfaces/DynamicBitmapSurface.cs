using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Worksheet.Views.PlotRendering.Surfaces
{
    public sealed class DynamicBitmapSurface : Image
    {
        private WriteableBitmap? _bitmap;

        public DynamicBitmapSurface()
        {
            IsHitTestVisible = false;
            Stretch = Stretch.Fill;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Visibility = Visibility.Collapsed;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        }

        public Rect DataRect { get; private set; }

        public void SetDataRect(Rect plotDataRectPixels)
        {
            DataRect = plotDataRectPixels;
            Width = Math.Max(0, plotDataRectPixels.Width);
            Height = Math.Max(0, plotDataRectPixels.Height);
            Margin = new Thickness(plotDataRectPixels.X, plotDataRectPixels.Y, 0, 0);
        }

        public void PresentBitmap(byte[] buffer, int width, int height)
        {
            if (buffer == null || buffer.Length == 0 || width <= 0 || height <= 0)
            {
                Clear();
                return;
            }

            const int bytesPerPixel = 4;
            int stride = width * bytesPerPixel;
            if (_bitmap == null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
            {
                _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                Source = _bitmap;
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, width, height), buffer, stride, 0);
            Visibility = Visibility.Visible;
        }

        public void Clear()
        {
            Source = null;
            _bitmap = null;
            Visibility = Visibility.Collapsed;
        }
    }
}
