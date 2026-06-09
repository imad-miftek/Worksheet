using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Worksheet.Models;
using Worksheet.Views.PlotViews;
using Worksheet.Views.PlotViews.Dialogs;

namespace Worksheet.Views.PlotViews.ContextMenus
{
    public class OscilloscopeContextMenu : PlotContextMenu
    {
        public override void Attach(PlotItem plotItem, PlotView plotView)
        {
            if (plotView is not OscilloscopePlotView oscilloscopeView)
                return;

            var contextMenu = new ContextMenu();

            var propertiesItem = new MenuItem
            {
                Header = "Properties"
            };
            propertiesItem.Click += (s, e) => OpenProperties(plotItem, oscilloscopeView);

            var closeItem = new MenuItem
            {
                Header = "Close"
            };
            closeItem.Click += (s, e) => plotItem.OnCloseRequested?.Invoke(plotItem);

            contextMenu.Items.Add(propertiesItem);
            contextMenu.Items.Add(closeItem);

            plotItem.PlotContainer.DragLayer.ContextMenu = contextMenu;
        }

        private static void OpenProperties(PlotItem plotItem, OscilloscopePlotView oscilloscopeView)
        {
            var selected = oscilloscopeView.Settings.OscilloscopeChannelIndices;
            int maxSelectedIndex = selected.Length > 0 ? selected.Max() : 0;
            int channelCount = Math.Max(oscilloscopeView.Settings.OscilloscopeChannelCount, maxSelectedIndex + 1);

            var dialog = new OscilloscopePropertiesDialog(channelCount, selected)
            {
                Owner = Window.GetWindow(plotItem.Container)
            };

            if (dialog.ShowDialog() == true)
            {
                oscilloscopeView.Settings.OscilloscopeChannelIndices = dialog.SelectedChannelIndices;
                oscilloscopeView.Settings.OscilloscopeChannelCount = Math.Max(channelCount, dialog.SelectedChannelIndices.Max() + 1);
                oscilloscopeView.InvalidateStatic(plotItem.Plot);
            }
        }
    }
}
