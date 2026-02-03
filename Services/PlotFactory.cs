using System;
using System.Linq;
using ScottPlot.Interactivity.UserActionResponses;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Services
{
    public class PlotFactory
    {
        private readonly HistogramPlotContextMenu _histogramContextMenu;
        private readonly PseudocolorPlotContextMenu _pseudocolorContextMenu;
        private readonly SpectralRibbonPlotContextMenu _spectralRibbonContextMenu;

        public PlotFactory()
            : this(new HistogramPlotContextMenu(), new PseudocolorPlotContextMenu(), new SpectralRibbonPlotContextMenu())
        {
        }

        public PlotFactory(
            HistogramPlotContextMenu histogramContextMenu,
            PseudocolorPlotContextMenu pseudocolorContextMenu,
            SpectralRibbonPlotContextMenu spectralRibbonContextMenu)
        {
            _histogramContextMenu = histogramContextMenu;
            _pseudocolorContextMenu = pseudocolorContextMenu;
            _spectralRibbonContextMenu = spectralRibbonContextMenu;
        }

        public WpfPlot CreatePlot(double width, double height, PlotType plotType, out PlotView plotView)
        {
            var plot = CreateBasePlot(width, height);

            switch (plotType)
            {
                case PlotType.Histogram:
                    var histogramView = new HistogramPlotView(_histogramContextMenu);
                    histogramView.Configure(plot);
                    plotView = histogramView;
                    return plot;
                case PlotType.Pseudocolor:
                    var pseudocolorView = new PseudocolorPlotView(_pseudocolorContextMenu);
                    pseudocolorView.Configure(plot);
                    plotView = pseudocolorView;
                    return plot;
                case PlotType.SpectralRibbon:
                    var spectralRibbonView = new SpectralRibbonPlotView(_spectralRibbonContextMenu);
                    spectralRibbonView.Configure(plot);
                    plotView = spectralRibbonView;
                    return plot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plotType), plotType, "Unsupported plot type.");
            }
        }

        public WpfPlot CreatePlot(double width, double height, PlotType plotType, AxisScaleType axisScale, out PlotView plotView)
        {
            var plot = CreateBasePlot(width, height);

            switch (plotType)
            {
                case PlotType.Histogram:
                    var histogramView = new HistogramPlotView(_histogramContextMenu);
                    histogramView.Configure(plot, axisScale);
                    plotView = histogramView;
                    return plot;
                case PlotType.Pseudocolor:
                    var pseudocolorView = new PseudocolorPlotView(_pseudocolorContextMenu);
                    pseudocolorView.Configure(plot);
                    plotView = pseudocolorView;
                    return plot;
                case PlotType.SpectralRibbon:
                    var spectralRibbonView = new SpectralRibbonPlotView(_spectralRibbonContextMenu);
                    spectralRibbonView.Configure(plot);
                    plotView = spectralRibbonView;
                    return plot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plotType), plotType, "Unsupported plot type.");
            }
        }

        private static WpfPlot CreateBasePlot(double width, double height)
        {
            var plot = new WpfPlot
            {
                Width = width,
                Height = height,
            };

            // Disable pan/zoom/etc. by removing common UIP responses
            var uip = plot.UserInputProcessor;
            uip.IsEnabled = true;

            uip.UserActionResponses.RemoveAll(r =>
                r is MouseDragPan ||
                r is MouseDragZoom ||
                r is MouseDragZoomRectangle ||
                r.GetType().Name.Contains("Wheel", StringComparison.OrdinalIgnoreCase) ||
                r.GetType().Name.Contains("Scroll", StringComparison.OrdinalIgnoreCase)
            );

            plot.Plot.FigureBackground.Color = ScottPlot.Color.FromARGB(0);
            plot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#FFFFFFFF");

            // Show the data-area border so thumbs visually "sit" on it
            plot.Plot.DataBorder.Width = 1;

            return plot;
        }
    }
}
