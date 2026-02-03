using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.Histogram;

namespace Worksheet.Views.PlotViews.Axes
{
    public abstract class AxisItem
    {
        public abstract AxisScaleType ScaleType { get; }
        public abstract void Apply(WpfPlot plot, HistogramBinning binning, AxisOrientation orientation);
    }
}
