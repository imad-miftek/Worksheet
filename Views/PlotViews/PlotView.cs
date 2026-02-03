using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public abstract class PlotView
    {
        protected PlotView(PlotContextMenu contextMenu)
        {
            ContextMenu = contextMenu;
        }

        public PlotContextMenu ContextMenu { get; }
        public abstract PlotType PlotType { get; }
        public abstract void Configure(WpfPlot plot);

        public void AttachContextMenu(PlotItem plotItem)
        {
            ContextMenu.Attach(plotItem, this);
        }
    }
}
