using Worksheet.Models;
using Worksheet.Views.PlotViews;

namespace Worksheet.Views.PlotViews.ContextMenus
{
    public abstract class PlotContextMenu
    {
        public abstract void Attach(PlotItem plotItem, PlotView plotView);
    }
}
