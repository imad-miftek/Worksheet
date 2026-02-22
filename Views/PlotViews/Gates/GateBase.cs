using ScottPlot.WPF;
using System;
using System.Collections.Generic;

namespace Worksheet.Views.PlotViews.Gates
{
    public abstract class GateBase
    {
        private readonly GateStyle _style;
        private readonly List<ScottPlot.IPlottable> _auxPlottables = new();

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

        public ScottPlot.IPlottable? Plottable { get; protected set; }
        public ScottPlot.IPlottable? LabelPlottable { get; protected set; }
        public IReadOnlyList<ScottPlot.IPlottable> AuxiliaryPlottables => _auxPlottables;

        public virtual bool Contains(ScottPlot.Coordinates c) =>
            c.X >= XMin && c.X <= XMax && c.Y >= YMin && c.Y <= YMax;

        public virtual void SetBounds(double xMin, double xMax, double yMin, double yMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
        }

        public virtual void RebuildPlottable(WpfPlot plot)
        {
            RemovePlottables(plot);
            var gate = AddPolygon(plot, BuildCoordinates(), _style.LineColor, _style.FillColor, _style.LineWidth);
            Plottable = gate;
            LabelPlottable = AddDefaultCenteredLabel(plot, Name, (XMin + XMax) / 2, (YMin + YMax) / 2);
        }

        protected void RemovePlottables(WpfPlot plot)
        {
            if (Plottable != null)
            {
                try { plot.Plot.Remove(Plottable); } catch { }
                Plottable = null;
            }

            if (LabelPlottable != null)
            {
                try { plot.Plot.Remove(LabelPlottable); } catch { }
                LabelPlottable = null;
            }

            if (_auxPlottables.Count > 0)
            {
                foreach (var pl in _auxPlottables)
                {
                    try { plot.Plot.Remove(pl); } catch { }
                }
                _auxPlottables.Clear();
            }
        }

        protected ScottPlot.IPlottable AddPolygon(
            WpfPlot plot,
            IReadOnlyList<ScottPlot.Coordinates> coords,
            ScottPlot.Color lineColor,
            ScottPlot.Color fillColor,
            float lineWidth)
        {
            ScottPlot.Plottables.Polygon gate;
            try
            {
                gate = plot.Plot.Add.Polygon(coords.ToArray());
            }
            catch
            {
                gate = new ScottPlot.Plottables.Polygon(coords.ToArray());
                plot.Plot.PlottableList.Add(gate);
            }

            gate.LineWidth = lineWidth;
            gate.LineColor = lineColor;
            gate.FillColor = fillColor;
            return gate;
        }

        protected ScottPlot.IPlottable? AddDefaultCenteredLabel(WpfPlot plot, string text, double x, double y)
        {
            try
            {
                var label = plot.Plot.Add.Text(text, x, y);
                label.Alignment = ScottPlot.Alignment.MiddleCenter;
                label.LabelFontColor = ScottPlot.Colors.Black;
                label.LabelStyle.FontSize += 2;
                label.LabelStyle.Bold = true;
                return label;
            }
            catch
            {
                return null;
            }
        }

        protected void RegisterAuxiliaryPlottable(ScottPlot.IPlottable plottable)
        {
            _auxPlottables.Add(plottable);
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
