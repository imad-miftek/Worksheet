using System;
using System.Windows.Controls;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Views.PlotRendering.Surfaces;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public abstract class PlotView
    {
        private readonly DynamicBitmapSurfaceHost _bitmapSurfaceHost = new();
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
            _bitmapSurfaceHost.Clear();
        }

        public void AttachContextMenu(PlotItem plotItem)
        {
            ContextMenu.Attach(plotItem, this);
        }

        public void AttachOverlay(Canvas overlay)
        {
            _overlayCanvas = overlay;
        }

        public void AttachBitmapSurface(WpfPlot plot, DynamicBitmapSurface dynamicSurface)
        {
            _bitmapSurfaceHost.Attach(plot, dynamicSurface);
        }

        protected bool TryGetDynamicSurfaceHost(out DynamicBitmapSurfaceHost surfaceHost)
        {
            surfaceHost = _bitmapSurfaceHost;
            return true;
        }

        protected bool TryGetOverlay(out Canvas overlay)
        {
            overlay = _overlayCanvas!;
            return overlay != null;
        }

        protected void ClearDynamicSurface()
        {
            _bitmapSurfaceHost.Clear();
        }

        protected void ExecuteStaticRefresh(WpfPlot plot, Action? configureAction = null)
        {
            if (plot == null)
                return;

            try
            {
                configureAction?.Invoke();
                plot.Refresh();
                _bitmapSurfaceHost.UpdateLayout(plot);
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
