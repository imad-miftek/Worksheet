using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
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
            });
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
