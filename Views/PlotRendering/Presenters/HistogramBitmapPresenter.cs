using System;
using Worksheet.Models.Data;
using Worksheet.Views.PlotRendering.Surfaces;

namespace Worksheet.Views.PlotRendering.Presenters
{
    public sealed class HistogramBitmapPresenter
    {
        private byte[] _pixelBuffer = Array.Empty<byte>();
        private int _pixelWidth;
        private int _pixelHeight;

        public void Render(HistogramProcessedData histogram, DynamicBitmapSurfaceHost surfaceHost, double upperBound)
        {
            var dataRect = surfaceHost.DataRect;
            int width = Math.Max(1, (int)Math.Ceiling(dataRect.Width));
            int height = Math.Max(1, (int)Math.Ceiling(dataRect.Height));

            if (width <= 0 || height <= 0)
            {
                surfaceHost.Clear();
                return;
            }

            if (_pixelBuffer.Length != width * height * 4)
            {
                _pixelBuffer = new byte[width * height * 4];
                _pixelWidth = width;
                _pixelHeight = height;
            }
            else
            {
                Array.Clear(_pixelBuffer, 0, _pixelBuffer.Length);
            }

            double maxCount = upperBound <= 0 ? 1 : upperBound;
            bool hasAnyData = false;
            int binCount = histogram.Counts.Length;

            for (int i = 0; i < binCount; i++)
            {
                double value = histogram.Counts[i];
                if (value <= 0)
                    continue;

                hasAnyData = true;
                int x0 = (int)Math.Floor((double)i / binCount * width);
                int x1 = (int)Math.Ceiling((double)(i + 1) / binCount * width);
                x0 = Math.Clamp(x0, 0, width - 1);
                x1 = Math.Clamp(Math.Max(x0 + 1, x1), 1, width);

                double heightFraction = Math.Clamp(value / maxCount, 0, 1);
                int filledHeight = Math.Clamp((int)Math.Round(heightFraction * height), 0, height);
                int yStart = Math.Max(0, height - filledHeight);

                for (int y = yStart; y < height; y++)
                {
                    int rowOffset = y * width * 4;
                    for (int x = x0; x < x1; x++)
                    {
                        int pixelIndex = rowOffset + (x * 4);
                        _pixelBuffer[pixelIndex + 0] = 80;
                        _pixelBuffer[pixelIndex + 1] = 175;
                        _pixelBuffer[pixelIndex + 2] = 76;
                        _pixelBuffer[pixelIndex + 3] = 255;
                    }
                }
            }

            if (!hasAnyData)
            {
                surfaceHost.Clear();
                return;
            }

            surfaceHost.PresentBitmap(_pixelBuffer, _pixelWidth, _pixelHeight);
        }
    }
}
