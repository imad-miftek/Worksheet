using ScottPlot.WPF;
using Worksheet.Models;

namespace Worksheet.Views.PlotViews.Axes
{
    public class LogarithmicAxisItem : AxisItem
    {
        public override AxisScaleType ScaleType => AxisScaleType.Logarithmic;

        public override void Apply(WpfPlot plot)
        {
            // Configure logarithmic X-axis scale with fixed ticks (1 to 1e8)
            var minorTickGen = new ScottPlot.TickGenerators.LogDecadeMinorTickGenerator();
            var tickGen = new ScottPlot.TickGenerators.NumericAutomatic()
            {
                MinorTickGenerator = minorTickGen,
                IntegerTicksOnly = true,
                LabelFormatter = (double x) => FormatLogLabel(x)
            };

            plot.Plot.Axes.Bottom.TickGenerator = tickGen;
            plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;

            // Set and lock X axis range in log scale (1 to 1e8)
            plot.Plot.Axes.SetLimitsX(0, 8);  // 10^0 to 10^8
        }

        private static string FormatLogLabel(double exponent)
        {
            // Convert exponent to superscript format (10^x)
            string[] superscripts = { "⁰", "¹", "²", "³", "⁴", "⁵", "⁶", "⁷", "⁸", "⁹" };

            int exp = (int)Math.Round(exponent);
            if (exp < 0)
                return $"10⁻{ConvertToSuperscript(Math.Abs(exp), superscripts)}";

            return $"10{ConvertToSuperscript(exp, superscripts)}";
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
    }
}
