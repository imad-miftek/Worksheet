using System;
using System.Collections.Generic;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class SpectralRibbonPlotView : PlotView
    {
        public SpectralRibbonPlotView(SpectralRibbonPlotContextMenu contextMenu, PlotSettings settings)
            : base(contextMenu, settings)
        {
        }

        public override PlotType PlotType => PlotType.SpectralRibbon;

        public override void Configure(WpfPlot plot)
        {
            plot.Plot.Axes.Bottom.Label.Text = "Channel";
            plot.Plot.Axes.Left.Label.Text = "Intensity";
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not SpectralRibbonProcessedData spectralData)
                return;

            RenderOnce(plot, () =>
            {
                plot.Plot.Clear();
                var heatmap = plot.Plot.Add.Heatmap(spectralData.Data);
                int bins = spectralData.Data.GetLength(0);
                int channelCount = spectralData.Data.GetLength(1);
                heatmap.Smooth = false;
                heatmap.Extent = new ScottPlot.CoordinateRect(0.5, channelCount - 0.5, 0.5, bins - 0.5);
                heatmap.Colormap = CreateColormap();

                plot.Plot.Axes.SetLimitsX(0, channelCount);
                plot.Plot.Axes.SetLimitsY(0, bins);
                plot.Plot.Axes.Bottom.Label.Text = "Channel";
                plot.Plot.Axes.Left.Label.Text = "Intensity";
                ApplyChannelTicks(plot, spectralData.ChannelNames, channelCount);
                ApplyYAxisTicks(plot);
            });
        }

        private void ApplyYAxisTicks(WpfPlot plot)
        {
            switch (Settings.YAxisScaleType)
            {
                case AxisScaleType.Linear:
                    plot.Plot.Axes.Left.TickGenerator = LinearAxisItem.CreateDataTickGenerator(Settings);
                    plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
                    plot.Plot.Grid.MinorLineWidth = 1;
                    break;
                case AxisScaleType.Logarithmic:
                    plot.Plot.Axes.Left.TickGenerator = LogarithmicAxisItem.CreateDataTickGenerator(Settings);
                    plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
                    plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
                    plot.Plot.Grid.MinorLineWidth = 1;
                    break;
                default:
                    break;
            }
        }

        private static void ApplyChannelTicks(WpfPlot plot, IReadOnlyList<string> channelNames, int channelCount)
        {
            var positions = new double[channelCount];
            var labels = new string[channelCount];
            for (int i = 0; i < channelCount; i++)
            {
                positions[i] = i + 0.5;
                labels[i] = channelNames[i];
            }

            plot.Plot.Axes.Bottom.TickGenerator = new FixedLinearTickGenerator(
                positions,
                labels,
                Array.Empty<double>());
            plot.Plot.Axes.Bottom.TickLabelStyle.Rotation = 90;
            plot.Plot.Axes.SetLimitsX(0, channelCount);

            // Use minor grid lines to align with ribbon edges.
            plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
            plot.Plot.Grid.MajorLineWidth = 1;
            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(0);
            plot.Plot.Grid.MinorLineWidth = 0;
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
    }
}
