using ScottPlot.WPF;
using Worksheet.Models;

namespace Worksheet.Interfaces
{
    /// <summary>
    /// Factory for creating and configuring WpfPlot instances.
    /// </summary>
    public interface IPlotFactory
    {
        /// <summary>
        /// Creates a configured WpfPlot with disabled pan/zoom and standard styling.
        /// </summary>
        WpfPlot CreatePlot(double width, double height);

        /// <summary>
        /// Creates a configured WpfPlot of a specific type with sample data.
        /// </summary>
        WpfPlot CreatePlot(double width, double height, PlotType plotType);

        /// <summary>
        /// Creates a configured WpfPlot of a specific type with sample data and custom axis scale.
        /// </summary>
        WpfPlot CreatePlot(double width, double height, PlotType plotType, AxisScaleType axisScale);

        /// <summary>
        /// Updates the axis scale of an existing histogram plot.
        /// </summary>
        void UpdateHistogramAxisScale(WpfPlot plot, AxisScaleType newScale);
    }
}
