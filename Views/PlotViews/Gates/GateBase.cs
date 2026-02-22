using ScottPlot.WPF;
using System;

namespace Worksheet.Views.PlotViews.Gates
{
    public abstract class GateBase
    {
        private readonly GateStyle _style;

        protected GateBase(Guid gateId, string name, double xMin, double xMax, double yMin, double yMax, GateStyle style)
        {
            GateId = gateId;
            Name = name;
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
            _style = style;
        }

        public Guid GateId { get; }
        public string Name { get; }
        public double XMin { get; private set; }
        public double XMax { get; private set; }
        public double YMin { get; private set; }
        public double YMax { get; private set; }

        public ScottPlot.Plottables.Polygon? Plottable { get; private set; }
        public ScottPlot.IPlottable? LabelPlottable { get; private set; }

        public bool Contains(ScottPlot.Coordinates c) =>
            c.X >= XMin && c.X <= XMax && c.Y >= YMin && c.Y <= YMax;

        public void SetBounds(double xMin, double xMax, double yMin, double yMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
        }

        public void RebuildPlottable(WpfPlot plot)
        {
            if (Plottable != null)
            {
                try
                {
                    plot.Plot.Remove(Plottable);
                }
                catch
                {
                }
            }

            if (LabelPlottable != null)
            {
                try
                {
                    plot.Plot.Remove(LabelPlottable);
                }
                catch
                {
                }
            }

            ScottPlot.Plottables.Polygon gate;
            var coords = BuildCoordinates();
            try
            {
                gate = plot.Plot.Add.Polygon(coords);
            }
            catch
            {
                gate = new ScottPlot.Plottables.Polygon(coords);
                plot.Plot.PlottableList.Add(gate);
            }

            gate.LineWidth = _style.LineWidth;
            gate.LineColor = _style.LineColor;
            gate.FillColor = _style.FillColor;
            Plottable = gate;

            try
            {
                var label = plot.Plot.Add.Text(Name, (XMin + XMax) / 2, (YMin + YMax) / 2);
                label.Alignment = ScottPlot.Alignment.MiddleCenter;
                label.LabelFontColor = ScottPlot.Colors.Black;
                label.LabelStyle.FontSize += 2;
                label.LabelStyle.Bold = true;
                LabelPlottable = label;
            }
            catch
            {
                LabelPlottable = null;
            }
        }

        protected abstract ScottPlot.Coordinates[] BuildCoordinates();
    }

    public readonly record struct GateStyle(
        ScottPlot.Color LineColor,
        ScottPlot.Color FillColor,
        float LineWidth)
    {
        public static GateStyle DefaultRectangle =>
            new(ScottPlot.Colors.Black, ScottPlot.Colors.Transparent, 2);
    }
}
