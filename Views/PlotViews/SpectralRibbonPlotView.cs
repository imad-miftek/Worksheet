using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
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
            plot.Plot.Axes.Bottom.Label.Text = "Sample";
            plot.Plot.Axes.Left.Label.Text = "Intensity";
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not SpectralRibbonProcessedData spectralData)
                return;

            plot.Plot.Clear();
            var palette = new ScottPlot.Palettes.Category10();

            for (int i = 0; i < spectralData.Channels.Length; i++)
            {
                var sig = plot.Plot.Add.Signal(spectralData.Channels[i]);
                sig.Color = palette.GetColor(i);
            }

            plot.Plot.Axes.Bottom.Label.Text = "Sample";
            plot.Plot.Axes.Left.Label.Text = "Intensity";
            plot.Refresh();
        }
    }
}
