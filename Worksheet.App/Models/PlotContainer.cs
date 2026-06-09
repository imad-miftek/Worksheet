using System.Windows.Controls;
using ScottPlot.WPF;
using System.Windows;
using Worksheet.Views.Surfaces;

namespace Worksheet.Models
{
    /// <summary>
    /// Contains all the UI elements that make up a draggable, resizable plot on the worksheet.
    /// </summary>
    public record PlotContainer(
        Canvas Container,      // Outer draggable element positioned on worksheet
        Canvas Overlay,        // Holds thumbs and drag layer
        Border DragLayer,      // Receives mouse events for dragging
        Grid Host,             // Holds dynamic surface + plot + overlay
        WpfPlot StaticPlot,
        Border DataRectBacking,
        DynamicBitmap DynamicSurface
    );
}
