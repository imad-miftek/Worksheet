using ScottPlot.WPF;

namespace Worksheet
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
    }
}
