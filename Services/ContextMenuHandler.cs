using System;
using System.Windows.Controls;
using Worksheet.Interfaces;
using Worksheet.Models;

namespace Worksheet.Services
{
    /// <summary>
    /// Handles attaching context menus to worksheet items.
    /// </summary>
    public class ContextMenuHandler : IContextMenuHandler
    {
        public void AttachContextMenu(PlotItem plotItem, Action<AxisScaleType> onAxisScaleChanged)
        {
            // Only attach context menu for histogram plots
            if (plotItem.PlotType != PlotType.Histogram)
                return;

            var contextMenu = new ContextMenu();

            // Linear scale option
            var linearItem = new MenuItem
            {
                Header = "Linear X-Axis"
            };
            linearItem.Click += (s, e) => onAxisScaleChanged(AxisScaleType.Linear);

            // Logarithmic scale option
            var logItem = new MenuItem
            {
                Header = "Logarithmic X-Axis"
            };
            logItem.Click += (s, e) => onAxisScaleChanged(AxisScaleType.Logarithmic);

            contextMenu.Items.Add(linearItem);
            contextMenu.Items.Add(logItem);

            // Attach to DragLayer for right-click events
            plotItem.PlotContainer.DragLayer.ContextMenu = contextMenu;
        }
    }
}
