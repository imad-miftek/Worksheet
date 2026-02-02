using ScottPlot.WPF;

namespace Worksheet
{
    /// <summary>
    /// Factory for creating the container DOM hierarchy for a plot.
    /// </summary>
    public interface IPlotContainerFactory
    {
        /// <summary>
        /// Creates a PlotContainer with all necessary UI elements wired together.
        /// </summary>
        /// <param name="plot">The WpfPlot to wrap</param>
        /// <param name="childIndex">Index used for grid positioning</param>
        /// <param name="worksheetWidth">Available width for calculating columns</param>
        PlotContainer CreateContainer(WpfPlot plot, int childIndex, double worksheetWidth);
    }
}
