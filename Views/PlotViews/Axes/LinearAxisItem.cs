using System;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.Histogram;

namespace Worksheet.Views.PlotViews.Axes
{
    public class LinearAxisItem : AxisItem
    {
        public override AxisScaleType ScaleType => AxisScaleType.Linear;

        public override void Apply(WpfPlot plot, HistogramBinning binning, AxisOrientation orientation)
        {
            if (orientation == AxisOrientation.Bottom)
            {
                ApplyBottom(plot, binning);
            }
            else if (orientation == AxisOrientation.Left)
            {
                ApplyLeft(plot);
            }
        }

        private static void ApplyBottom(WpfPlot plot, HistogramBinning binning)
        {
            var majorValues = new double[] { 0, 20_000_000, 40_000_000, 60_000_000, 80_000_000, 100_000_000 };
            var majorPositions = new double[majorValues.Length];
            var majorLabels = new string[majorValues.Length];

            for (int i = 0; i < majorValues.Length; i++)
            {
                majorPositions[i] = binning.DataValueToBinPosition(majorValues[i]);
                majorLabels[i] = FormatSIPrefix(majorValues[i]);
            }

            var tickPositions = new System.Collections.Generic.List<double>(majorPositions);
            var tickLabels = new System.Collections.Generic.List<string>(majorLabels);

            for (int i = 0; i < majorValues.Length - 1; i++)
            {
                double start = majorValues[i];
                double step = (majorValues[i + 1] - start) / 5;
                for (int j = 1; j <= 4; j++)
                {
                    double minorValue = start + step * j;
                    tickPositions.Add(binning.DataValueToBinPosition(minorValue));
                    tickLabels.Add(string.Empty);
                }
            }

            var tickGen = new ScottPlot.TickGenerators.NumericManual(tickPositions.ToArray(), tickLabels.ToArray());

            plot.Plot.Axes.Bottom.TickGenerator = tickGen;

            // Configure minor grid styling
            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;

            plot.Plot.Axes.SetLimitsX(0, binning.BinCount);
        }

        private static void ApplyLeft(WpfPlot plot)
        {
            plot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic()
            {
                LabelFormatter = (double x) => FormatSIPrefix(x)
            };
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
