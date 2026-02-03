using System;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.Histogram;

namespace Worksheet.Views.PlotViews.Axes
{
    public class LogarithmicAxisItem : AxisItem
    {
        public override AxisScaleType ScaleType => AxisScaleType.Logarithmic;

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
            var positions = new double[9];
            var labels = new string[9];

            for (int i = 0; i <= 8; i++)
            {
                var value = Math.Pow(10, i);
                positions[i] = binning.DataValueToBinPosition(value);
                labels[i] = FormatLogLabel(i);
            }

            plot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(positions, labels);
            plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
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

        private static string FormatLogLabel(int exponent)
        {
            string[] superscripts = { "⁰", "¹", "²", "³", "⁴", "⁵", "⁶", "⁷", "⁸", "⁹" };

            if (exponent < 0)
                return $"10⁻{ConvertToSuperscript(Math.Abs(exponent), superscripts)}";

            return $"10{ConvertToSuperscript(exponent, superscripts)}";
        }

        private static string ConvertToSuperscript(int number, string[] superscripts)
        {
            string numStr = number.ToString();
            string result = "";
            foreach (char digit in numStr)
            {
                result += superscripts[digit - '0'];
            }
            return result;
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
