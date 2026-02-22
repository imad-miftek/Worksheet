namespace Worksheet.Views.PlotViews.Gates
{
    public sealed class RectangleGate : GateBase
    {
        public RectangleGate(double xMin, double xMax, double yMin, double yMax, GateStyle style)
            : base(xMin, xMax, yMin, yMax, style)
        {
        }

        protected override ScottPlot.Coordinates[] BuildCoordinates() =>
            new[]
            {
                new ScottPlot.Coordinates(XMin, YMin),
                new ScottPlot.Coordinates(XMax, YMin),
                new ScottPlot.Coordinates(XMax, YMax),
                new ScottPlot.Coordinates(XMin, YMax),
            };
    }
}
