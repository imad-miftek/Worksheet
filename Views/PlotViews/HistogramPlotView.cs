using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Models.Gates;
using Worksheet.Services;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;
using Worksheet.Views.Support.Gates;

namespace Worksheet.Views.PlotViews
{
    public class HistogramPlotView : PlotView
    {
        private readonly AxisFactory _axisFactory;
        private readonly GateVisualManager _gateVisualManager;
        private ScottPlot.Plottables.BarPlot? _barPlot;
        private HistogramConfigSnapshot? _lastAppliedConfig;

        public Action<GateSettings>? GateSettingsSink { get; set; }
        public Action<Guid>? GateRemovedSink { get; set; }

        public HistogramPlotView(
            HistogramPlotContextMenu contextMenu,
            AxisFactory axisFactory,
            PlotSettings settings,
            GateVisualManager gateVisualManager)
            : base(contextMenu, settings)
        {
            _axisFactory = axisFactory;
            _gateVisualManager = gateVisualManager;
        }

        public override PlotType PlotType => PlotType.Histogram;

        public AxisScaleType CurrentAxisScale => Settings.XAxisScaleType;

        public override void Configure(WpfPlot plot)
        {
            plot.Plot.Axes.Margins(bottom: 0);
            plot.Plot.Axes.SetLimitsY(0, 10);
            plot.Plot.YLabel("Frequency");
            plot.Plot.XLabel(GetXAxisLabel(Settings.XFeature));
            _axisFactory.Apply(Settings.XAxisScaleType, plot, Settings);
            _lastAppliedConfig = HistogramConfigSnapshot.From(Settings);
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not HistogramProcessedData histogram)
                return;

            if (histogram.ScaleType != Settings.XAxisScaleType)
                return;

            if (histogram.BinCount != Settings.GetBinCount())
                return;

            RenderOnce(plot, () =>
            {
                ApplyConfigIfChanged(plot);
                EnsureBarPlot(plot, histogram);
                UpdateBars(histogram);
                UpdateYAxisLimits(plot, histogram.Counts);
            });
        }

        public override void Clear(WpfPlot plot)
        {
            RenderOnce(plot, () =>
            {
                // Remove the bar plottable so the histogram is visually empty immediately.
                plot.Plot.Clear<ScottPlot.Plottables.BarPlot>();
                _barPlot = null;

                // Keep axis configuration, just reset Y range to a sane default.
                plot.Plot.Axes.SetLimitsY(0, 10);
            });
        }

        public void UpdateAxisScale(PlotItem plotItem, AxisScaleType newScale)
        {
            Settings.XAxisScaleType = newScale;
        }

        internal void AttachGateInteractions(PlotItem plotItem)
        {
            _gateVisualManager.Attach(
                plotItem,
                () => Settings.GetBinCount(),
                () => Settings.Id,
                () => Settings.PlotType,
                GateSettingsSink,
                GateRemovedSink);
        }

        internal void BeginAddLineGate(PlotItem plotItem)
        {
            _gateVisualManager.BeginAddLineGate(plotItem);
        }

        internal bool HasSelectedGate() => _gateVisualManager.HasSelectedGate;

        internal bool RemoveSelectedGate(PlotItem plotItem) => _gateVisualManager.RemoveSelectedGate(plotItem);

        private void ApplyConfigIfChanged(WpfPlot plot)
        {
            var current = HistogramConfigSnapshot.From(Settings);
            if (_lastAppliedConfig.HasValue && _lastAppliedConfig.Value.Equals(current))
                return;

            plot.Plot.XLabel(GetXAxisLabel(Settings.XFeature));
            _axisFactory.Apply(Settings.XAxisScaleType, plot, Settings);
            _lastAppliedConfig = current;
        }

        private void EnsureBarPlot(WpfPlot plot, HistogramProcessedData histogram)
        {
            if (_barPlot != null && _barPlot.Bars.Count == histogram.Counts.Length)
                return;

            plot.Plot.Clear<ScottPlot.Plottables.BarPlot>();
            _barPlot = plot.Plot.Add.Bars(histogram.Positions, histogram.Counts);
            foreach (var bar in _barPlot.Bars)
            {
                bar.Size = 1;
                bar.LineWidth = 0;
                bar.FillStyle.AntiAlias = false;
                bar.FillColor = ScottPlot.Color.FromHex("#4CAF50");
            }
        }

        private void UpdateBars(HistogramProcessedData histogram)
        {
            if (_barPlot == null)
                return;

            int count = histogram.Counts.Length;
            for (int i = 0; i < count; i++)
            {
                _barPlot.Bars[i].Position = histogram.Positions[i];
                _barPlot.Bars[i].Value = histogram.Counts[i];
            }
        }

        private static void UpdateYAxisLimits(WpfPlot plot, double[] counts)
        {
            double maxCount = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > maxCount)
                    maxCount = counts[i];
            }

            if (maxCount <= 0)
                plot.Plot.Axes.SetLimitsY(0, 10);
            else
                plot.Plot.Axes.AutoScaleY();
        }

        private readonly record struct HistogramConfigSnapshot(
            int BinCount,
            int XFeature,
            AxisScaleType XAxisScaleType,
            double MinValue,
            double MaxValue)
        {
            public static HistogramConfigSnapshot From(PlotSettings settings) =>
                new(
                    settings.GetBinCount(),
                    settings.XFeature,
                    settings.XAxisScaleType,
                    settings.MinValue,
                    settings.MaxValue);
        }

        private static string GetXAxisLabel(int featureIndex)
        {
            if (FeatureSelectionStrategy.TryGetChannelWavelength(featureIndex, out var wavelength))
                return $"Intensity ({wavelength})";

            return $"Intensity (Channel {featureIndex + 1})";
        }
    }
}
