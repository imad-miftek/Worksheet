using System.Windows.Controls;
using System.Windows;
using Worksheet.Models;
using Worksheet.Views.PlotViews;
using Worksheet.Views.PlotViews.Dialogs;

namespace Worksheet.Views.PlotViews.ContextMenus
{
    public class HistogramPlotContextMenu : PlotContextMenu
    {
        public override void Attach(PlotItem plotItem, PlotView plotView)
        {
            if (plotView is not HistogramPlotView histogramView)
                return;

            var contextMenu = new ContextMenu();

            var propertiesItem = new MenuItem
            {
                Header = "Properties"
            };
            propertiesItem.Click += (s, e) => OpenProperties(plotItem, histogramView);

            var closeItem = new MenuItem
            {
                Header = "Close"
            };
            closeItem.Click += (s, e) => plotItem.OnCloseRequested?.Invoke(plotItem);

            contextMenu.Items.Add(propertiesItem);
            contextMenu.Items.Add(closeItem);

            plotItem.PlotContainer.DragLayer.ContextMenu = contextMenu;
        }

        private static void OpenProperties(PlotItem plotItem, HistogramPlotView histogramView)
        {
            var owner = Window.GetWindow(plotItem.Container);
            var dialog = new HistogramPropertiesDialog(histogramView.CurrentAxisScale)
            {
                Owner = owner
            };

            if (dialog.ShowDialog() == true)
            {
                histogramView.UpdateAxisScale(plotItem, dialog.SelectedAxisScale);
            }
        }
    }
}
