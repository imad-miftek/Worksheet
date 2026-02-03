using ScottPlot.WPF;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Axes
{
    public class LinearAxisItem : AxisItem
    {
        public override AxisScaleType ScaleType => AxisScaleType.Linear;

        public override void Apply(WpfPlot plot)
        {
            // Linear X-axis scale (0 to 100M) with automatic tick spacing and SI prefix formatter
            var minorTickGen = new ScottPlot.TickGenerators.EvenlySpacedMinorTickGenerator(10);
            var tickGen = new ScottPlot.TickGenerators.NumericAutomatic()
            {
                MinorTickGenerator = minorTickGen,
                LabelFormatter = (double x) => FormatSIPrefix(x)
            };

            plot.Plot.Axes.Bottom.TickGenerator = tickGen;

            // Configure minor grid styling (match log axis)
            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;

            // Set and lock X axis range (0 to 100M)
            plot.Plot.Axes.SetLimitsX(0, 100_000_000);
        }

        private static string FormatSIPrefix(double value)
        {
            if (value == 0) return "0";

            double absValue = Math.Abs(value);
            string sign = value < 0 ? "-" : "";

            if (absValue >= 1_000_000_000)
                return $"{sign}{absValue / 1_000_000_000:0.##}G";
            else if (absValue >= 1_000_000)
                return $"{sign}{absValue / 1_000_000:0.##}M";
            else if (absValue >= 1_000)
                return $"{sign}{absValue / 1_000:0.##}k";
            else
                return $"{sign}{absValue:0.##}";
        }
    }
}
