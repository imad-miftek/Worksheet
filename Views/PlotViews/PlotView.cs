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

        public void AttachContextMenu(PlotItem plotItem)
        {
            ContextMenu.Attach(plotItem, this);
        }
    }
}
