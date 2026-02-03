using System;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class PseudocolorPlotView : PlotView
    {
        public PseudocolorPlotView(PseudocolorPlotContextMenu contextMenu)
            : base(contextMenu)
        {
        }

        public override PlotType PlotType => PlotType.Pseudocolor;

        public override void Configure(WpfPlot plot)
        {
            // Generate sample heatmap data
            double[,] data = new double[50, 50];
            for (int i = 0; i < 50; i++)
            {
                for (int j = 0; j < 50; j++)
                {
                    data[i, j] = Math.Sin(i * 0.2) * Math.Cos(j * 0.2);
                }
            }

            var heatmap = plot.Plot.Add.Heatmap(data);
            heatmap.Colormap = new ScottPlot.Colormaps.Viridis();
        }
    }
}
