using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ScottPlot;
using SkiaSharp;

namespace Worksheet.Views.PlotRendering.Plottables
{
    public sealed class BitmapHeatmap : IPlottable
    {
        private SKBitmap? _bitmap;
        private CoordinateRect _extent;

        public bool IsVisible { get; set; } = true;
        public ScottPlot.IAxes Axes { get; set; } = new ScottPlot.Axes();
        public IEnumerable<LegendItem> LegendItems => Array.Empty<LegendItem>();

        public void SetPixels(byte[] pixelBuffer, int width, int height, CoordinateRect extent)
        {
            if (_bitmap == null || _bitmap.Width != width || _bitmap.Height != height)
            {
                _bitmap?.Dispose();
                _bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            }

            Marshal.Copy(pixelBuffer, 0, _bitmap.GetPixels(), pixelBuffer.Length);
            _extent = extent;
        }

        public AxisLimits GetAxisLimits()
        {
            return new AxisLimits(_extent);
        }

        public void Render(RenderPack renderPack)
        {
            if (_bitmap is null)
                return;

            PixelRect dataRect = Axes.DataRect;
            float left = Axes.XAxis.GetPixel(_extent.Left, dataRect);
            float right = Axes.XAxis.GetPixel(_extent.Right, dataRect);
            float bottom = Axes.YAxis.GetPixel(_extent.Bottom, dataRect);
            float top = Axes.YAxis.GetPixel(_extent.Top, dataRect);

            var destination = new SKRect(left, top, right, bottom);
            renderPack.Canvas.DrawBitmap(_bitmap, destination);
        }
    }
}
