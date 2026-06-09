using System;
using System.Collections.Generic;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Views.PlotViews.ContextMenus;

namespace Worksheet.Views.PlotViews
{
    public class OscilloscopePlotView : PlotView
    {
        private const double FixedYMin = -0.18;
        private const double FixedYMax = 0.52;
        private readonly List<Signal> _signals = new();
        private readonly List<double[]> _waveformData = new();
        private readonly Color[] _channelColors = new[]
        {
            Color.FromHex("#2196F3"),  // Blue
            Color.FromHex("#4CAF50"),  // Green
            Color.FromHex("#FF9800"),  // Orange
            Color.FromHex("#E91E63"),  // Pink
        };

        public OscilloscopePlotView(
            OscilloscopeContextMenu contextMenu,
            PlotSettings settings)
            : base(contextMenu, settings)
        {
        }

        public override PlotType PlotType => PlotType.Oscilloscope;

        public override void Configure(WpfPlot plot)
        {
            ConfigureAxes(plot);
            plot.Refresh();
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            if (data is not OscilloscopeProcessedData oscilloscopeData)
                return;

            RenderOnce(plot, () =>
            {
                plot.Plot.Clear();
                _signals.Clear();
                _waveformData.Clear();
                ConfigureAxes(plot);

                if (oscilloscopeData.IsEmpty || oscilloscopeData.Signals.Length == 0)
                {
                    SetEmptyLimits(plot);
                    return;
                }

                for (int channel = 0; channel < oscilloscopeData.Signals.Length; channel++)
                {
                    double[] waveform = oscilloscopeData.Signals[channel];
                    _waveformData.Add(waveform);

                    var signal = plot.Plot.Add.Signal(waveform);
                    signal.Color = _channelColors[channel % _channelColors.Length];
                    signal.LineWidth = 1;
                    _signals.Add(signal);
                }

                SetSignalLimits(plot, oscilloscopeData.TimestampCount);
            });
        }

        private static void ConfigureAxes(WpfPlot plot)
        {
            plot.Plot.YLabel("Voltage (V)");
            plot.Plot.XLabel("Time (samples)");
        }

        private static void SetEmptyLimits(WpfPlot plot)
        {
            plot.Plot.Axes.SetLimitsX(0, 1);
            plot.Plot.Axes.SetLimitsY(FixedYMin, FixedYMax);
        }

        private static void SetSignalLimits(WpfPlot plot, int timestampCount)
        {
            plot.Plot.Axes.SetLimitsX(0, Math.Max(1, timestampCount - 1));
            plot.Plot.Axes.SetLimitsY(FixedYMin, FixedYMax);
        }
    }
}
