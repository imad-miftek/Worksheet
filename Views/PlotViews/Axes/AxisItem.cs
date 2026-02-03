using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Views.PlotViews.Axes
{
    public abstract class AxisItem
    {
        public abstract AxisScaleType ScaleType { get; }
        public abstract void Apply(WpfPlot plot, HistogramBinning binning, AxisOrientation orientation);
    }
}
