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
        private ScottPlot.Plottables.Heatmap? _heatmap;
        private readonly ScottPlot.IColormap _colormap = CreateColormap();
        private readonly GateVisualManager _gateVisualManager;
        private PlotConfigSnapshot? _lastAppliedConfig;
        private double[,]? _emptyIntensities;
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
                EnsureHeatmap(plot, heatmapData.Data);

                if (_heatmap == null)
                    return;

                if (heatmapData.IsEmpty)
                {
                    _heatmap.Opacity = 0;
                    return;
                }

                _heatmap.Opacity = 1;
                _heatmap.Intensities = heatmapData.Data;
                _heatmap.Update();
            });
        }

        public override void Clear(WpfPlot plot)
        {
            RenderOnce(plot, () =>
            {
                int bins = Settings.GetBinCount();
                var empty = GetEmptyIntensities(bins);

                if (_heatmap != null)
                {
                    bool stillInPlot = plot.Plot.GetPlottables<ScottPlot.Plottables.Heatmap>().Contains(_heatmap);
                    if (!stillInPlot)
                        _heatmap = null;
                }

                if (_heatmap == null)
                    EnsureHeatmap(plot, empty);

                if (_heatmap == null)
                    return;

                _heatmap.Opacity = 0;
                _heatmap.NaNCellColor = ScottPlot.Colors.Transparent;
                _heatmap.Extent = new ScottPlot.CoordinateRect(0, bins, 0, bins);
                _heatmap.Intensities = empty;
                _heatmap.Update();
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

        private void EnsureHeatmap(WpfPlot plot, double[,] initialData)
        {
            if (_heatmap != null)
                return;

            _heatmap = plot.Plot.Add.Heatmap(initialData);
            _heatmap.Extent = new ScottPlot.CoordinateRect(0, Settings.GetBinCount(), 0, Settings.GetBinCount());
            _heatmap.Colormap = _colormap;
            _heatmap.NaNCellColor = ScottPlot.Colors.Transparent;
            _heatmap.Opacity = 1;
            plot.Plot.MoveToBottom(_heatmap);
        }

        private double[,] GetEmptyIntensities(int bins)
        {
            if (_emptyIntensities != null && _emptyBins == bins)
                return _emptyIntensities;

            var empty = new double[bins, bins];

            _emptyIntensities = empty;
            _emptyBins = bins;
            return empty;
        }

        private void ApplyConfigIfChanged(WpfPlot plot)
        {
            var current = PlotConfigSnapshot.From(Settings);
            if (_lastAppliedConfig.HasValue && _lastAppliedConfig.Value.Equals(current))
                return;

            ApplyAxisTicks(plot, resetLimits: false);
            ApplyAxisLabels(plot);
            if (_heatmap != null)
                _heatmap.Extent = new ScottPlot.CoordinateRect(0, Settings.GetBinCount(), 0, Settings.GetBinCount());

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

        private static ScottPlot.IColormap CreateColormap()
        {
            try
            {
                return new ScottPlot.Colormaps.Turbo();
            }
            catch
            {
                return new ScottPlot.Colormaps.Viridis();
            }
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
