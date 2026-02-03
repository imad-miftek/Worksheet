using System;
using System.Linq;
using ScottPlot.Interactivity.UserActionResponses;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Services;
using Worksheet.Views.PlotViews;
using Worksheet.Views.PlotViews.Axes;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.Support
{
    public class PlotFactory
    {
        private readonly AxisFactory _axisFactory;
        private readonly HistogramPlotContextMenu _histogramContextMenu;
        private readonly PseudocolorPlotContextMenu _pseudocolorContextMenu;
        private readonly SpectralRibbonPlotContextMenu _spectralRibbonContextMenu;
        private readonly FeatureSelectionStrategy _featureSelectionStrategy;

        public PlotFactory()
            : this(new AxisFactory(), new FeatureSelectionStrategy(), new SpectralRibbonPlotContextMenu())
        {
        }

        public PlotFactory(
            AxisFactory axisFactory,
            FeatureSelectionStrategy featureSelectionStrategy,
            SpectralRibbonPlotContextMenu spectralRibbonContextMenu)
        {
            _axisFactory = axisFactory;
            _featureSelectionStrategy = featureSelectionStrategy;
            _histogramContextMenu = new HistogramPlotContextMenu(_featureSelectionStrategy);
            _pseudocolorContextMenu = new PseudocolorPlotContextMenu(_featureSelectionStrategy);
            _spectralRibbonContextMenu = spectralRibbonContextMenu;
        }

        public WpfPlot CreatePlot(double width, double height)
        {
            var plot = CreateBasePlot(width, height);

            // Add sample data
            plot.Plot.Add.Scatter(
                new double[] { 1, 2, 3, 4, 5 },
                new double[] { 1, 4, 9, 16, 25 });

            return plot;
        }

        public WpfPlot CreatePlot(double width, double height, PlotType plotType, out PlotView plotView)
        {
            var plot = CreateBasePlot(width, height);

            var settings = CreateSettings(plotType);
            plotView = CreatePlotView(plotType, settings);
            plotView.Configure(plot);

            return plot;
        }

        public WpfPlot CreatePlot(double width, double height, PlotType plotType, AxisScaleType axisScale, out PlotView plotView)
        {
            var plot = CreateBasePlot(width, height);

            var settings = CreateSettings(plotType);
            settings.XAxisScaleType = axisScale;
            plotView = CreatePlotView(plotType, settings);
            plotView.Configure(plot);

            return plot;
        }

        public PlotSettings CreateSettings(PlotType plotType)
        {
            return plotType switch
            {
                PlotType.Histogram => new PlotSettings
                {
                    PlotType = PlotType.Histogram,
                    BinCount = 256,
                    XFeature = 0,
                    YFeature = 0,
                    XAxisScaleType = AxisScaleType.Linear,
                    YAxisScaleType = AxisScaleType.Linear
                },
                PlotType.Pseudocolor => new PlotSettings
                {
                    PlotType = PlotType.Pseudocolor,
                    BinCount = 256,
                    XFeature = 0,
                    YFeature = 1,
                    XAxisScaleType = AxisScaleType.Linear,
                    YAxisScaleType = AxisScaleType.Linear
                },
                PlotType.SpectralRibbon => new PlotSettings
                {
                    PlotType = PlotType.SpectralRibbon,
                    BinCount = 0,
                    XFeature = 0,
                    YFeature = 0,
                    XAxisScaleType = AxisScaleType.Linear,
                    YAxisScaleType = AxisScaleType.Linear
                },
                _ => throw new ArgumentOutOfRangeException(nameof(plotType), plotType, "Unsupported plot type.")
            };
        }

        public PlotView CreatePlotView(PlotType plotType, PlotSettings settings)
        {
            return plotType switch
            {
                PlotType.Histogram => new HistogramPlotView(_histogramContextMenu, _axisFactory, settings),
                PlotType.Pseudocolor => new PseudocolorPlotView(_pseudocolorContextMenu, settings),
                PlotType.SpectralRibbon => new SpectralRibbonPlotView(_spectralRibbonContextMenu, settings),
                _ => throw new ArgumentOutOfRangeException(nameof(plotType), plotType, "Unsupported plot type.")
            };
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
            plot.Plot.DataBorder.Width = 2;
            plot.Plot.Axes.AntiAlias(true);
            plot.Plot.Axes.Hairline(true);
            plot.Plot.Axes.Right.MinimumSize = 20;
            plot.Plot.Axes.Bottom.TickLabelStyle.FontSize = 13;
            plot.Plot.Axes.Left.TickLabelStyle.FontSize = 13;
            plot.Plot.Axes.Bottom.TickLabelStyle.Bold = true;
            plot.Plot.Axes.Left.TickLabelStyle.Bold = true;
            plot.Plot.Axes.Bottom.MajorTickStyle.Length = 6;
            plot.Plot.Axes.Bottom.MajorTickStyle.Width = 2;
            plot.Plot.Axes.Bottom.MinorTickStyle.Length = 4;
            plot.Plot.Axes.Bottom.MinorTickStyle.Width = 1;
            plot.Plot.Axes.Left.MajorTickStyle.Length = 6;
            plot.Plot.Axes.Left.MajorTickStyle.Width = 2;
            plot.Plot.Axes.Left.MinorTickStyle.Length = 4;
            plot.Plot.Axes.Left.MinorTickStyle.Width = 1;
            plot.Plot.Axes.Left.Label.Padding = 50;

            return plot;
        }
    }
}
