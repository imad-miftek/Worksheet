using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Models.Gates;
using Worksheet.Services;
using Worksheet.Views.PlotRendering.Presenters;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;
using Worksheet.Views.Support.Gates;

namespace Worksheet.Views.PlotViews
{
    public class HistogramPlotView : PlotView
    {
        private readonly AxisFactory _axisFactory;
        private readonly GateVisualManager _gateVisualManager;
        private readonly HistogramBitmapPresenter _bitmapPresenter = new();
        private readonly HistogramYAxisItem _yAxisItem = new();
        private HistogramConfigSnapshot? _lastAppliedConfig;
        private double _yAxisUpperBound = 10;

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
            plot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFFFF");
            plot.Plot.Axes.Margins(bottom: 0);
            plot.Plot.Axes.SetLimitsY(0, 1);
            plot.Plot.YLabel("Frequency");
            plot.Plot.XLabel(GetXAxisLabel(Settings.XFeature));
            _axisFactory.Apply(Settings.XAxisScaleType, plot, Settings);
            _yAxisUpperBound = 10;
            _yAxisItem.Apply(plot, _yAxisUpperBound);
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

            bool staticChanged = ApplyConfigIfChanged(plot);
            bool yTickLabelsChanged = UpdateHistogramScale(histogram.Counts);
            if (staticChanged || yTickLabelsChanged)
            {
                ExecuteStaticRefresh(plot, () => _yAxisItem.Apply(plot, _yAxisUpperBound));
            }

            RenderHistogramDynamic(histogram);
        }

        public override void Clear(WpfPlot plot)
        {
            ClearDynamicSurface();
            _yAxisUpperBound = 10;
            ExecuteStaticRefresh(plot, () =>
            {
                plot.Plot.Axes.SetLimitsY(0, 1);
                _yAxisItem.Apply(plot, _yAxisUpperBound);
            });
        }

        public override void InvalidateStatic(WpfPlot plot)
        {
            ApplyConfigIfChanged(plot);
            ExecuteStaticRefresh(plot);
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

        private bool ApplyConfigIfChanged(WpfPlot plot)
        {
            var current = HistogramConfigSnapshot.From(Settings);
            if (_lastAppliedConfig.HasValue && _lastAppliedConfig.Value.Equals(current))
                return false;

            plot.Plot.XLabel(GetXAxisLabel(Settings.XFeature));
            _axisFactory.Apply(Settings.XAxisScaleType, plot, Settings);
            _yAxisItem.Apply(plot, _yAxisUpperBound);
            _lastAppliedConfig = current;
            return true;
        }

        private bool UpdateHistogramScale(double[] counts)
        {
            double maxCount = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > maxCount)
                    maxCount = counts[i];
            }

            double snappedUpperBound = _yAxisItem.GetSnappedUpperBound(maxCount);
            if (snappedUpperBound.Equals(_yAxisUpperBound))
                return false;

            _yAxisUpperBound = snappedUpperBound;
            return true;
        }

        private void RenderHistogramDynamic(HistogramProcessedData histogram)
        {
            if (!TryGetDynamicSurfaceHost(out var surfaceHost))
                return;

            _bitmapPresenter.Render(histogram, surfaceHost, _yAxisUpperBound);
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
