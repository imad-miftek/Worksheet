using System;
using Worksheet.Models;

namespace Worksheet.Interfaces
{
    /// <summary>
    /// Service for attaching context menus to worksheet items.
    /// </summary>
    public interface IContextMenuHandler
    {
        /// <summary>
        /// Attaches a context menu to a plot item for switching axis scales.
        /// </summary>
        /// <param name="plotItem">The plot item to attach the context menu to.</param>
        /// <param name="onAxisScaleChanged">Callback invoked when the user selects a new axis scale.</param>
        void AttachContextMenu(PlotItem plotItem, Action<AxisScaleType> onAxisScaleChanged);
    }
}
