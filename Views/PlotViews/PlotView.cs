using System;
using System.Windows;
using System.Windows.Controls;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public abstract class PlotView
    {
        private DynamicBitmapSurface? _dynamicSurface;
        private Canvas? _overlayCanvas;

        protected PlotView(PlotContextMenu contextMenu, PlotSettings settings)
        {
            ContextMenu = contextMenu;
            Settings = settings;
        }

        public PlotContextMenu ContextMenu { get; }
        public PlotSettings Settings { get; }
        public abstract PlotType PlotType { get; }
        public abstract void Configure(WpfPlot plot);
        public abstract void Render(WpfPlot plot, ProcessedPlotData data);
        public virtual void InvalidateStatic(WpfPlot plot)
        {
            ExecuteStaticRefresh(plot);
        }

        // Clear the visual state of the plot without relying on a new ProcessedPlotData render cycle.
        // Used when memory is cleared while streaming is stopped (no new frames will arrive).
        public virtual void Clear(WpfPlot plot)
        {
            _dynamicSurface?.Clear();
        }

        public void AttachContextMenu(PlotItem plotItem)
        {
            ContextMenu.Attach(plotItem, this);
        }

        public void AttachOverlay(Canvas overlay)
        {
            _overlayCanvas = overlay;
        }

        public void AttachDynamicSurface(WpfPlot plot, FrameworkElement dynamicSurface)
        {
            if (dynamicSurface is not DynamicBitmapSurface surface)
                return;

            _dynamicSurface = surface;
            _dynamicSurface.IsHitTestVisible = false;

            plot.Plot.RenderManager.RenderFinished += (_, __) => UpdateDynamicSurfaceLayout(plot);
            plot.SizeChanged += (_, __) => UpdateDynamicSurfaceLayout(plot);
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

        protected bool TryGetDynamicSurface(out DynamicBitmapSurface surface)
        {
            surface = _dynamicSurface!;
            return surface != null;
        }

        protected bool TryGetOverlay(out Canvas overlay)
        {
            overlay = _overlayCanvas!;
            return overlay != null;
        }

        protected void ClearDynamicSurface()
        {
            _dynamicSurface?.Clear();
        }

        protected void ExecuteStaticRefresh(WpfPlot plot, Action? configureAction = null)
        {
            if (plot == null)
                return;

            try
            {
                configureAction?.Invoke();
                plot.Refresh();
                UpdateDynamicSurfaceLayout(plot);
            }
            catch
            {
            }
        }

        protected void UpdateDynamicSurfaceLayout(WpfPlot plot)
        {
            if (_dynamicSurface == null || plot == null)
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
                _dynamicSurface.SetDataRect(rectDip);
            }
            catch
            {
            }
        }

        protected void RenderOnce(WpfPlot plot, Action renderAction)
        {
            if (plot == null || renderAction == null)
                return;

            plot.Plot.RenderManager.EnableRendering = false;
            try
            {
                try
                {
                    renderAction();
                }
                catch
                {
                    // Prevent a bad render path from crashing the UI thread.
                    // Do not return here: plot.Refresh() must still run so the control can repaint.
                }
            }
            finally
            {
                plot.Plot.RenderManager.EnableRendering = true;
            }

            plot.Refresh();
        }
    }
}
