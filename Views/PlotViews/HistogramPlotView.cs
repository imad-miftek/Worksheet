using System;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;
using Worksheet.Views.PlotViews.Histogram;

namespace Worksheet.Views.PlotViews
{
    public class HistogramPlotView : PlotView
    {
        private const int BinCount = 256;
        private readonly AxisFactory _axisFactory;
        private double[] _values = Array.Empty<double>();

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

            _values = values;
            Render(plot, axisScale);
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
            Render(plot, newScale);
            plot.Refresh();
        }

        private void Render(WpfPlot plot, AxisScaleType axisScale)
        {
            var binning = new HistogramBinning(BinCount, axisScale);
            var counts = binning.CreateCounts(_values);
            var positions = binning.CreateBinPositions();

            plot.Plot.Clear();

            var barPlot = plot.Plot.Add.Bars(positions, counts);

            foreach (var bar in barPlot.Bars)
            {
                bar.Size = 1;
                bar.LineWidth = 0;
                bar.FillStyle.AntiAlias = false;
                bar.FillColor = ScottPlot.Color.FromHex("#4CAF50");
            }

            plot.Plot.Axes.Margins(bottom: 0);
            plot.Plot.YLabel("Frequency");
            plot.Plot.XLabel("Intensity");

            _axisFactory.Apply(axisScale, plot, binning);
        }
    }
}
