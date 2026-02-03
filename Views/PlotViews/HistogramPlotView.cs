using System;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class HistogramPlotView : PlotView
    {
        private readonly AxisFactory _axisFactory;

        public HistogramPlotView(HistogramPlotContextMenu contextMenu, AxisFactory axisFactory)
            : base(contextMenu)
        {
            _axisFactory = axisFactory;
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
            _axisFactory.Apply(axisScale, plot);

            // Y-axis limits are set automatically based on data
            // Both X and Y axis limits will remain fixed and won't auto-scale on resize
        }
    }
}
