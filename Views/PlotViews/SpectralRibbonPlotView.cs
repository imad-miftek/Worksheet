using System;
using System.Collections.Generic;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Services;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class SpectralRibbonPlotView : PlotView
    {
        private ScottPlot.Plottables.Heatmap? _heatmap;
        private readonly ScottPlot.IColormap _colormap = CreateColormap();
        private SpectralConfigSnapshot? _lastAppliedConfig;
        private double[,]? _emptyIntensities;
        private int _emptyBins;
        private int _emptyChannelCount;

        public SpectralRibbonPlotView(SpectralRibbonPlotContextMenu contextMenu, PlotSettings settings)
            : base(contextMenu, settings)
        {
        }

        public override PlotType PlotType => PlotType.SpectralRibbon;

        public override void Configure(WpfPlot plot)
        {
            int bins = Settings.GetBinCount();
            int channelCount = FeatureSelectionStrategy.ChannelNames.Count;
            if (channelCount <= 0)
                channelCount = 1;

            ApplyAxesAndTicks(plot, bins, channelCount, resetLimits: true);
            _lastAppliedConfig = SpectralConfigSnapshot.Create(Settings, bins, channelCount);
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not SpectralRibbonProcessedData spectralData)
                return;

            RenderOnce(plot, () =>
            {
                int bins = spectralData.Data.GetLength(0);
                int channelCount = spectralData.Data.GetLength(1);
                ApplyConfigIfChanged(plot, bins, channelCount);
                EnsureHeatmap(plot, spectralData.Data, bins, channelCount);

                if (_heatmap == null)
                    return;

                if (spectralData.IsEmpty)
                {
                    _heatmap.Opacity = 0;
                    return;
                }

                _heatmap.Opacity = 1;
                _heatmap.Intensities = spectralData.Data;
                _heatmap.Update();
            });
        }

        public override void Clear(WpfPlot plot)
        {
            RenderOnce(plot, () =>
            {
                int bins = Settings.GetBinCount();
                int channelCount = FeatureSelectionStrategy.ChannelNames.Count;
                if (channelCount <= 0)
                    channelCount = 1;

                var empty = GetEmptyIntensities(bins, channelCount);

                if (_heatmap != null)
                {
                    bool stillInPlot = plot.Plot.GetPlottables<ScottPlot.Plottables.Heatmap>().Contains(_heatmap);
                    if (!stillInPlot)
                        _heatmap = null;
                }

                if (_heatmap == null)
                    EnsureHeatmap(plot, empty, bins, channelCount);

                if (_heatmap == null)
                    return;

                _heatmap.Opacity = 0;
                _heatmap.NaNCellColor = ScottPlot.Colors.Transparent;
                _heatmap.Extent = new ScottPlot.CoordinateRect(0.5, channelCount - 0.5, 0.5, bins - 0.5);
                _heatmap.Intensities = empty;
                _heatmap.Update();
            });
        }

        private void ApplyConfigIfChanged(WpfPlot plot, int bins, int channelCount)
        {
            var current = SpectralConfigSnapshot.Create(Settings, bins, channelCount);
            if (_lastAppliedConfig.HasValue && _lastAppliedConfig.Value.Equals(current))
                return;

            ApplyAxesAndTicks(plot, bins, channelCount, resetLimits: false);
            if (_heatmap != null)
                _heatmap.Extent = new ScottPlot.CoordinateRect(0.5, channelCount - 0.5, 0.5, bins - 0.5);

            _lastAppliedConfig = current;
        }

        private void EnsureHeatmap(WpfPlot plot, double[,] initialData, int bins, int channelCount)
        {
            if (_heatmap != null)
                return;

            _heatmap = plot.Plot.Add.Heatmap(initialData);
            _heatmap.Smooth = false;
            _heatmap.Extent = new ScottPlot.CoordinateRect(0.5, channelCount - 0.5, 0.5, bins - 0.5);
            _heatmap.Colormap = _colormap;
            _heatmap.NaNCellColor = ScottPlot.Colors.Transparent;
            _heatmap.Opacity = 1;
        }

        private double[,] GetEmptyIntensities(int bins, int channelCount)
        {
            if (_emptyIntensities != null && _emptyBins == bins && _emptyChannelCount == channelCount)
                return _emptyIntensities;

            var empty = new double[bins, channelCount];

            _emptyIntensities = empty;
            _emptyBins = bins;
            _emptyChannelCount = channelCount;
            return empty;
        }

        private void ApplyAxesAndTicks(WpfPlot plot, int bins, int channelCount, bool resetLimits)
        {
            ApplyChannelTicks(plot, channelCount, resetLimits);
            ApplyYAxisTicks(plot, bins, resetLimits);
        }

        private static void ApplyChannelTicks(WpfPlot plot, int channelCount, bool resetLimits)
        {
            var channelNames = FeatureSelectionStrategy.ChannelNames;
            var positions = new double[channelCount];
            var labels = new string[channelCount];

            for (int i = 0; i < channelCount; i++)
            {
                positions[i] = i + 0.5;

                // Get channel name and remove "nm" suffix if present
                var channelName = i < channelNames.Count ? channelNames[i] : $"Channel {i + 1}";
                labels[i] = channelName.EndsWith("nm", StringComparison.OrdinalIgnoreCase)
                    ? channelName.Substring(0, channelName.Length - 2)
                    : channelName;
            }

            // Add minor tick positions at channel edges (integer boundaries)
            var minorPositions = new double[channelCount + 1];
            for (int i = 0; i <= channelCount; i++)
                minorPositions[i] = i;

            var tickGen = new FixedLinearTickGenerator(
                positions,
                labels,
                minorPositions);
            tickGen.MaxTickCount = channelCount;

            plot.Plot.Axes.Bottom.TickGenerator = tickGen;
            if (resetLimits)
                plot.Plot.Axes.SetLimitsX(0, channelCount);

            // Configure rotated tick labels - ADJUST THESE VALUES TO ITERATE:
            plot.Plot.Axes.Bottom.TickLabelStyle.Rotation = 90;  // Try: 90, -90, 45, -45
            plot.Plot.Axes.Bottom.TickLabelStyle.Alignment = ScottPlot.Alignment.UpperLeft;  // Try: UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight
            plot.Plot.Axes.Bottom.TickLabelStyle.OffsetX = 6;  // Try: 0, 5, 10, -5, -10
            plot.Plot.Axes.Bottom.TickLabelStyle.OffsetY = 3;  // Try: 0, 5, 10, -5, -10
            plot.Plot.Axes.Bottom.MinimumSize = 50;

            // Use minor grid lines to align with ribbon edges, and hide minor tick marks
            plot.Plot.Grid.XAxisStyle.MajorLineStyle.IsVisible = false;
            plot.Plot.Grid.XAxisStyle.MinorLineStyle.IsVisible = true;
            plot.Plot.Grid.XAxisStyle.MinorLineStyle.Color = ScottPlot.Colors.Black.WithOpacity(.15);
            plot.Plot.Grid.XAxisStyle.MinorLineStyle.Width = 1;
            plot.Plot.Axes.Bottom.MinorTickStyle.Length = 0;
            plot.Plot.Axes.Bottom.MinorTickStyle.Width = 0;
        }

        private void ApplyYAxisTicks(WpfPlot plot, int bins, bool resetLimits)
        {
            switch (Settings.YAxisScaleType)
            {
                case AxisScaleType.Linear:
                    plot.Plot.Axes.Left.TickGenerator = LinearAxisItem.CreateDataTickGenerator(Settings);
                    if (resetLimits)
                        plot.Plot.Axes.SetLimitsY(0, bins);
                    plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
                    plot.Plot.Grid.MinorLineWidth = 1;
                    break;
                case AxisScaleType.Logarithmic:
                    plot.Plot.Axes.Left.TickGenerator = LogarithmicAxisItem.CreateDataTickGenerator(Settings);
                    if (resetLimits)
                        plot.Plot.Axes.SetLimitsY(0, bins);
                    plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
                    plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
                    plot.Plot.Grid.MinorLineWidth = 1;
                    break;
                default:
                    break;
            }
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

        private readonly record struct SpectralConfigSnapshot(
            int Bins,
            int ChannelCount,
            AxisScaleType YAxisScaleType,
            double MinValue,
            double MaxValue)
        {
            public static SpectralConfigSnapshot Create(PlotSettings settings, int bins, int channelCount) =>
                new(
                    bins,
                    channelCount,
                    settings.YAxisScaleType,
                    settings.MinValue,
                    settings.MaxValue);
        }
    }
}
