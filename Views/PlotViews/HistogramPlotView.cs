using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class HistogramPlotView : PlotView
    {
        private readonly AxisFactory _axisFactory;

        public HistogramPlotView(
            HistogramPlotContextMenu contextMenu,
            AxisFactory axisFactory,
            PlotSettings settings)
            : base(contextMenu, settings)
        {
            _axisFactory = axisFactory;
        }

        public override PlotType PlotType => PlotType.Histogram;

        public AxisScaleType CurrentAxisScale => Settings.XAxisScaleType;

        public override void Configure(WpfPlot plot)
        {
            plot.Plot.Axes.Margins(bottom: 0);
            plot.Plot.YLabel("Frequency");
            plot.Plot.XLabel("Intensity");
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not HistogramProcessedData histogram)
                return;

            plot.Plot.Clear();

            var barPlot = plot.Plot.Add.Bars(histogram.Positions, histogram.Counts);

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

            double maxCount = 0;
            for (int i = 0; i < histogram.Counts.Length; i++)
            {
                if (histogram.Counts[i] > maxCount)
                    maxCount = histogram.Counts[i];
            }

            if (maxCount <= 0)
            {
                plot.Plot.Axes.SetLimitsY(0, 10);
            }
            else
            {
                plot.Plot.Axes.AutoScaleY();
            }

            _axisFactory.Apply(Settings.XAxisScaleType, plot, histogram.Binning);
            plot.Refresh();
        }

        public void UpdateAxisScale(PlotItem plotItem, AxisScaleType newScale)
        {
            Settings.XAxisScaleType = newScale;
        }
    }
}
