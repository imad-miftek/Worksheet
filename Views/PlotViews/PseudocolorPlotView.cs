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
                heatmap.Colormap = new ScottPlot.Colormaps.Viridis();
            });
        }
    }
}
