using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class SpectralRibbonPlotView : PlotView
    {
        public SpectralRibbonPlotView(SpectralRibbonPlotContextMenu contextMenu)
            : base(contextMenu)
        {
        }

        public override PlotType PlotType => PlotType.SpectralRibbon;

        public override void Configure(WpfPlot plot)
        {
            // Generate sample spectral data (multiple lines at different wavelengths)
            int pointCount = 100;
            var palette = new ScottPlot.Palettes.Category10();

            for (int i = 0; i < 10; i++)
            {
                var y = ScottPlot.Generate.Sin(pointCount, phase: i * 0.5);
                var sig = plot.Plot.Add.Signal(y);
                sig.Color = palette.GetColor(i);
            }

            plot.Plot.Axes.Bottom.Label.Text = "Sample";
            plot.Plot.Axes.Left.Label.Text = "Intensity";
        }
    }
}
