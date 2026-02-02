using System;
using ScottPlot.Interactivity.UserActionResponses;
using ScottPlot.WPF;
using Worksheet.Interfaces;

namespace Worksheet.Services
{
    public class PlotFactory : IPlotFactory
    {
        public WpfPlot CreatePlot(double width, double height)
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

            // Add sample data
            plot.Plot.Add.Scatter(
                new double[] { 1, 2, 3, 4, 5 },
                new double[] { 1, 4, 9, 16, 25 });

            return plot;
        }
    }
}
