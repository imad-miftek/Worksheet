using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class PseudocolorPlotView : PlotView
    {
        public PseudocolorPlotView(PseudocolorPlotContextMenu contextMenu, PlotSettings settings)
            : base(contextMenu, settings)
        {
        }

        public override PlotType PlotType => PlotType.Pseudocolor;

        public override void Configure(WpfPlot plot)
        {
            int bins = Settings.GetBinCount();
            plot.Plot.Axes.SetLimitsX(0, bins);
            plot.Plot.Axes.SetLimitsY(0, bins);

            switch (Settings.XAxisScaleType)
            {
                case AxisScaleType.Linear:
                    plot.Plot.Axes.Bottom.TickGenerator = LinearAxisItem.CreateDataTickGenerator(Settings);
                    plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
                    plot.Plot.Grid.MinorLineWidth = 1;
                    break;
                case AxisScaleType.Logarithmic:
                    plot.Plot.Axes.Bottom.TickGenerator = LogarithmicAxisItem.CreateDataTickGenerator(Settings);
                    plot.Plot.Grid.MajorLineColor = ScottPlot.Colors.Black.WithOpacity(.15);
                    plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
                    plot.Plot.Grid.MinorLineWidth = 1;
                    break;
            }

            switch (Settings.YAxisScaleType)
            {
                case AxisScaleType.Linear:
                    plot.Plot.Axes.Left.TickGenerator = LinearAxisItem.CreateDataTickGenerator(Settings);
                    break;
                case AxisScaleType.Logarithmic:
                    plot.Plot.Axes.Left.TickGenerator = LogarithmicAxisItem.CreateDataTickGenerator(Settings);
                    break;
            }
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not HeatmapProcessedData heatmapData)
                return;

            RenderOnce(plot, () =>
            {
                plot.Plot.Clear();
                var heatmap = plot.Plot.Add.Heatmap(heatmapData.Data);
                heatmap.Extent = new ScottPlot.CoordinateRect(0, Settings.GetBinCount(), 0, Settings.GetBinCount());
                plot.Plot.Axes.SetLimitsX(0, Settings.GetBinCount());
                plot.Plot.Axes.SetLimitsY(0, Settings.GetBinCount());
                heatmap.Colormap = CreateColormap();
                ApplyAxisTicks(plot);
            });
        }

        private void ApplyAxisTicks(WpfPlot plot)
        {
            ApplyAxisTicks(plot, AxisOrientation.Bottom, Settings.XAxisScaleType, Settings);
            ApplyAxisTicks(plot, AxisOrientation.Left, Settings.YAxisScaleType, Settings);
        }

        private static void ApplyAxisTicks(WpfPlot plot, AxisOrientation orientation, AxisScaleType scaleType, PlotSettings settings)
        {
            switch (scaleType)
            {
                case AxisScaleType.Linear:
                    ApplyLinearTicks(plot, orientation, settings);
                    break;
                case AxisScaleType.Logarithmic:
                    ApplyLogarithmicTicks(plot, orientation, settings);
                    break;
                default:
                    break;
            }
        }

        private static void ApplyLinearTicks(WpfPlot plot, AxisOrientation orientation, PlotSettings settings)
        {
            var tickGen = LinearAxisItem.CreateDataTickGenerator(settings);
            if (orientation == AxisOrientation.Bottom)
            {
                plot.Plot.Axes.Bottom.TickGenerator = tickGen;
                plot.Plot.Axes.SetLimitsX(0, settings.GetBinCount());
            }
            else if (orientation == AxisOrientation.Left)
            {
                plot.Plot.Axes.Left.TickGenerator = tickGen;
                plot.Plot.Axes.SetLimitsY(0, settings.GetBinCount());
            }

            plot.Plot.Grid.MinorLineColor = ScottPlot.Colors.Black.WithOpacity(.05);
            plot.Plot.Grid.MinorLineWidth = 1;
        }

        private static void ApplyLogarithmicTicks(WpfPlot plot, AxisOrientation orientation, PlotSettings settings)
        {
            var tickGen = LogarithmicAxisItem.CreateDataTickGenerator(settings);
            if (orientation == AxisOrientation.Bottom)
            {
                plot.Plot.Axes.Bottom.TickGenerator = tickGen;
                plot.Plot.Axes.SetLimitsX(0, settings.GetBinCount());
            }
            else if (orientation == AxisOrientation.Left)
            {
                plot.Plot.Axes.Left.TickGenerator = tickGen;
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
    }
}
