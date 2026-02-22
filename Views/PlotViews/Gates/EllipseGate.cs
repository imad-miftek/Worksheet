using System;

namespace Worksheet.Views.PlotViews.Gates
{
    public sealed class EllipseGate : GateBase
    {
        private const int SegmentCount = 64;

        public EllipseGate(Guid gateId, string name, double xMin, double xMax, double yMin, double yMax, GateStyle style)
            : base(gateId, name, xMin, xMax, yMin, yMax, style)
        {
        }

        public override bool Contains(ScottPlot.Coordinates c)
        {
            double cx = (XMin + XMax) / 2.0;
            double cy = (YMin + YMax) / 2.0;
            double rx = (XMax - XMin) / 2.0;
            double ry = (YMax - YMin) / 2.0;

            if (rx <= 0 || ry <= 0)
                return false;

            double dx = (c.X - cx) / rx;
            double dy = (c.Y - cy) / ry;
            return dx * dx + dy * dy <= 1.0;
        }

        protected override ScottPlot.Coordinates[] BuildCoordinates()
        {
            double cx = (XMin + XMax) / 2.0;
            double cy = (YMin + YMax) / 2.0;
            double rx = (XMax - XMin) / 2.0;
            double ry = (YMax - YMin) / 2.0;

            var coords = new ScottPlot.Coordinates[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                double t = (2.0 * Math.PI * i) / SegmentCount;
                coords[i] = new ScottPlot.Coordinates(
                    cx + rx * Math.Cos(t),
                    cy + ry * Math.Sin(t));
            }

            return coords;
        }
    }
}
