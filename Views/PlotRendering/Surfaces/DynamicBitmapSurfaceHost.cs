using System;
using System.Windows;
using ScottPlot.WPF;

namespace Worksheet.Views.PlotRendering.Surfaces
{
    public sealed class DynamicBitmapSurfaceHost
    {
        private DynamicBitmapSurface? _surface;

        public Rect DataRect => _surface?.DataRect ?? Rect.Empty;

        public void Attach(WpfPlot plot, DynamicBitmapSurface surface)
        {
            _surface = surface;
            _surface.IsHitTestVisible = false;

            plot.Plot.RenderManager.RenderFinished += (_, __) => UpdateLayout(plot);
            plot.SizeChanged += (_, __) => UpdateLayout(plot);
            plot.Loaded += (_, __) =>
            {
                try
                {
                    plot.Refresh();
                }
                catch
                {
                }
            };
        }

        public void PresentBitmap(byte[] buffer, int width, int height)
        {
            _surface?.PresentBitmap(buffer, width, height);
        }

        public void Clear()
        {
            _surface?.Clear();
        }

        public void UpdateLayout(WpfPlot plot)
        {
            if (_surface == null)
                return;

            try
            {
                var dataRect = plot.Plot.RenderManager.LastRender.DataRect;
                if (dataRect.Width <= 0 || dataRect.Height <= 0)
                    return;

                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(plot);
                var rectDip = new Rect(
                    dataRect.Left / dpi.DpiScaleX,
                    dataRect.Top / dpi.DpiScaleY,
                    dataRect.Width / dpi.DpiScaleX,
                    dataRect.Height / dpi.DpiScaleY);
                _surface.SetDataRect(rectDip);
            }
            catch
            {
            }
        }

        public bool TryGetSurface(out DynamicBitmapSurface surface)
        {
            surface = _surface!;
            return surface != null;
        }
    }
}
