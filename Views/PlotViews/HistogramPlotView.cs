using System;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class HistogramPlotView : PlotView
    {
        public HistogramPlotView(HistogramPlotContextMenu contextMenu)
            : base(contextMenu)
        {
        }

        public override PlotType PlotType => PlotType.Histogram;

        public AxisScaleType CurrentAxisScale { get; private set; } = AxisScaleType.Linear;

        public override void Configure(WpfPlot plot)
        {
            Configure(plot, AxisScaleType.Linear);
        }

        public void Configure(WpfPlot plot, AxisScaleType axisScale)
        {
            CurrentAxisScale = axisScale;

            // Generate normal distribution (1-population) using Box-Muller transform
            var random = new Random(0);
            int sampleCount = 50000;
            double mean = 128;
            double stdDev = 30;

            var values = new double[sampleCount];
            for (int i = 0; i < sampleCount; i += 2)
            {
                // Box-Muller transform for normal distribution
                double u1 = random.NextDouble();
                double u2 = random.NextDouble();
                double z0 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                double z1 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

                values[i] = mean + stdDev * z0;
                if (i + 1 < sampleCount)
                    values[i + 1] = mean + stdDev * z1;
            }

            // Create histogram with exactly 256 bins
            var hist = ScottPlot.Statistics.Histogram.WithBinCount(256, values);

            // Display the histogram as a bar plot
            var barPlot = plot.Plot.Add.Bars(hist.Bins, hist.Counts);

            // Customize the style of each bar (filled, no borders)
            foreach (var bar in barPlot.Bars)
            {
                bar.Size = hist.FirstBinSize;
                bar.LineWidth = 0;
                bar.FillStyle.AntiAlias = false;
                bar.FillColor = ScottPlot.Color.FromHex("#4CAF50");
            }

            // Customize plot style
            plot.Plot.Axes.Margins(bottom: 0);
            plot.Plot.YLabel("Frequency");
            plot.Plot.XLabel("Intensity");

            ApplyAxisScale(plot, axisScale);
        }

        public void UpdateAxisScale(PlotItem plotItem, AxisScaleType newScale)
        {
            if (CurrentAxisScale == newScale)
                return;

            UpdateAxisScale(plotItem.Plot, newScale);
        }

        public void UpdateAxisScale(WpfPlot plot, AxisScaleType newScale)
        {
            CurrentAxisScale = newScale;
            ApplyAxisScale(plot, newScale);
            plot.Refresh();
        }

        private void ApplyAxisScale(WpfPlot plot, AxisScaleType axisScale)
        {
            // Configure X-axis scale
            if (axisScale == AxisScaleType.Logarithmic)
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
            else
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

            // Y-axis limits are set automatically based on data
            // Both X and Y axis limits will remain fixed and won't auto-scale on resize
        }

        private string FormatLogLabel(double exponent)
        {
            // Convert exponent to superscript format (10^x)
            string[] superscripts = { "⁰", "¹", "²", "³", "⁴", "⁵", "⁶", "⁷", "⁸", "⁹" };

            int exp = (int)Math.Round(exponent);
            if (exp < 0)
                return $"10⁻{ConvertToSuperscript(Math.Abs(exp), superscripts)}";

            return $"10{ConvertToSuperscript(exp, superscripts)}";
        }

        private string ConvertToSuperscript(int number, string[] superscripts)
        {
            string numStr = number.ToString();
            string result = "";
            foreach (char digit in numStr)
            {
                result += superscripts[digit - '0'];
            }
            return result;
        }

        private string FormatSIPrefix(double value)
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
