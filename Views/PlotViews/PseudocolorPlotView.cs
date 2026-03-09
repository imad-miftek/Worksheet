using System;
using System.Linq;
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
    public class PseudocolorPlotView : PlotView
    {
        private BitmapHeatmapPlottable? _imagePlottable;
        private readonly GateVisualManager _gateVisualManager;
        private PlotConfigSnapshot? _lastAppliedConfig;
        private int _emptyBins;

        public Action<GateSettings>? GateSettingsSink { get; set; }
        public Action<Guid>? GateRemovedSink { get; set; }

        public PseudocolorPlotView(
            PseudocolorPlotContextMenu contextMenu,
            PlotSettings settings,
            GateVisualManager gateVisualManager)
            : base(contextMenu, settings)
        {
            _gateVisualManager = gateVisualManager;
        }

        public override PlotType PlotType => PlotType.Pseudocolor;

        public override void Configure(WpfPlot plot)
        {
            ApplyAxisTicks(plot, resetLimits: true);
            ApplyAxisLabels(plot);
            _lastAppliedConfig = PlotConfigSnapshot.From(Settings);
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not HeatmapProcessedData heatmapData)
                return;

            RenderOnce(plot, () =>
            {
                ApplyConfigIfChanged(plot);
                EnsureImagePlottable(plot, heatmapData);

                if (_imagePlottable == null)
                    return;

                if (heatmapData.IsEmpty)
                {
                    return;
                }

                _imagePlottable.SetPixels(
                    heatmapData.PixelBuffer,
                    heatmapData.Bins,
                    heatmapData.Bins,
                    new ScottPlot.CoordinateRect(0, heatmapData.Bins, 0, heatmapData.Bins));
            });
        }

        public override void Clear(WpfPlot plot)
        {
            RenderOnce(plot, () =>
            {
                int bins = Settings.GetBinCount();
                _emptyBins = bins;
            });
        }

        public void AttachGateInteractions(PlotItem plotItem)
        {
            _gateVisualManager.Attach(
                plotItem,
                () => Settings.GetBinCount(),
                () => Settings.Id,
                () => Settings.PlotType,
                GateSettingsSink,
                GateRemovedSink);
        }

        internal void BeginAddGateRectangle(PlotItem plotItem)
        {
            _gateVisualManager.BeginAddRectangleGate(plotItem);
        }

        internal void BeginAddGateEllipse(PlotItem plotItem)
        {
            _gateVisualManager.BeginAddEllipseGate(plotItem);
        }

        internal void BeginAddGatePolygon(PlotItem plotItem)
        {
            _gateVisualManager.BeginAddPolygonGate(plotItem);
        }

        internal bool HasSelectedGate() => _gateVisualManager.HasSelectedGate;

        internal bool RemoveSelectedGate(PlotItem plotItem) => _gateVisualManager.RemoveSelectedGate(plotItem);

        private void EnsureImagePlottable(WpfPlot plot, HeatmapProcessedData heatmapData)
        {
            if (_imagePlottable != null)
                return;

            _imagePlottable = new BitmapHeatmapPlottable();
            _imagePlottable.SetPixels(
                heatmapData.PixelBuffer,
                heatmapData.Bins,
                heatmapData.Bins,
                new ScottPlot.CoordinateRect(0, heatmapData.Bins, 0, heatmapData.Bins));
            plot.Plot.Add.Plottable(_imagePlottable);
            plot.Plot.MoveToBottom(_imagePlottable);
        }

        private void ApplyConfigIfChanged(WpfPlot plot)
        {
            var current = PlotConfigSnapshot.From(Settings);
            if (_lastAppliedConfig.HasValue && _lastAppliedConfig.Value.Equals(current))
                return;

            ApplyAxisTicks(plot, resetLimits: false);
            ApplyAxisLabels(plot);
            _emptyBins = Settings.GetBinCount();

            _lastAppliedConfig = current;
        }

        private void ApplyAxisLabels(WpfPlot plot)
        {
            plot.Plot.XLabel(GetFeatureLabel(Settings.XFeature));
            plot.Plot.YLabel(GetFeatureLabel(Settings.YFeature));
        }

        private static string GetFeatureLabel(int featureIndex)
        {
            if (FeatureSelectionStrategy.TryGetChannelWavelength(featureIndex, out var wavelength))
                return wavelength;

            return $"Channel {featureIndex + 1}";
        }

        private void ApplyAxisTicks(WpfPlot plot, bool resetLimits)
        {
            ApplyAxisTicks(plot, AxisOrientation.Bottom, Settings.XAxisScaleType, Settings, resetLimits);
            ApplyAxisTicks(plot, AxisOrientation.Left, Settings.YAxisScaleType, Settings, resetLimits);
        }

        private static void ApplyAxisTicks(
            WpfPlot plot,
            AxisOrientation orientation,
            AxisScaleType scaleType,
            PlotSettings settings,
            bool resetLimits)
        {
            switch (scaleType)
            {
                case AxisScaleType.Linear:
                    ApplyLinearTicks(plot, orientation, settings, resetLimits);
                    break;
                case AxisScaleType.Logarithmic:
                    ApplyLogarithmicTicks(plot, orientation, settings, resetLimits);
                    break;
                default:
                    break;
            }
        }

        private static void ApplyLinearTicks(WpfPlot plot, AxisOrientation orientation, PlotSettings settings, bool resetLimits)
        {
            var tickGen = LinearAxisItem.CreateDataTickGenerator(settings);
            if (orientation == AxisOrientation.Bottom)
            {
                plot.Plot.Axes.Bottom.TickGenerator = tickGen;
                if (resetLimits)
                    plot.Plot.Axes.SetLimitsX(0, settings.GetBinCount());
            }
            else if (orientation == AxisOrientation.Left)
            {
                plot.Plot.Axes.Left.TickGenerator = tickGen;
                if (resetLimits)
                    plot.Plot.Axes.SetLimitsY(0, settings.GetBinCount());
            }

            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;
        }

        private static void ApplyLogarithmicTicks(WpfPlot plot, AxisOrientation orientation, PlotSettings settings, bool resetLimits)
        {
            var tickGen = LogarithmicAxisItem.CreateDataTickGenerator(settings);
            if (orientation == AxisOrientation.Bottom)
            {
                plot.Plot.Axes.Bottom.TickGenerator = tickGen;
                if (resetLimits)
                    plot.Plot.Axes.SetLimitsX(0, settings.GetBinCount());
            }
            else if (orientation == AxisOrientation.Left)
            {
                plot.Plot.Axes.Left.TickGenerator = tickGen;
                if (resetLimits)
                    plot.Plot.Axes.SetLimitsY(0, settings.GetBinCount());
            }

            plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;
        }

        private readonly record struct PlotConfigSnapshot(
            int BinCount,
            AxisScaleType XAxisScaleType,
            AxisScaleType YAxisScaleType,
            double MinValue,
            double MaxValue)
        {
            public static PlotConfigSnapshot From(PlotSettings settings) =>
                new(
                    settings.GetBinCount(),
                    settings.XAxisScaleType,
                    settings.YAxisScaleType,
                    settings.MinValue,
                    settings.MaxValue);
        }
    }
}
