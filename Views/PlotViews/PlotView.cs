using System;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public abstract class PlotView
    {
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

        // Clear the visual state of the plot without relying on a new ProcessedPlotData render cycle.
        // Used when memory is cleared while streaming is stopped (no new frames will arrive).
        public virtual void Clear(WpfPlot plot)
        {
        }

        public void AttachContextMenu(PlotItem plotItem)
        {
            ContextMenu.Attach(plotItem, this);
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
