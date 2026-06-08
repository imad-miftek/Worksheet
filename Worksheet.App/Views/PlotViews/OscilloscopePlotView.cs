using System;
using System.Collections.Generic;
using System.Windows.Threading;
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
        private readonly List<Signal> _signals = new();
        private readonly List<double[]> _waveformData = new();
        private readonly Color[] _channelColors = new[]
        {
            Color.FromHex("#2196F3"),  // Blue
            Color.FromHex("#4CAF50"),  // Green
            Color.FromHex("#FF9800"),  // Orange
            Color.FromHex("#E91E63"),  // Pink
        };
        private readonly Random _random = new();
        private DispatcherTimer? _updateTimer;
        private WpfPlot? _plot;
        private double _phaseOffset = 0;

        public OscilloscopePlotView(
            OscilloscopeContextMenu contextMenu,
            PlotSettings settings)
            : base(contextMenu, settings)
        {
        }

        public override PlotType PlotType => PlotType.Oscilloscope;

        public override void Configure(WpfPlot plot)
        {
            _plot = plot;

            // Initialize with test data immediately
            InitializeWithTestData(plot);

            // Start timer for 30ms updates
            StartUpdateTimer();
        }

        public override void Render(WpfPlot plot, ProcessedPlotData data)
        {
            // Not used for oscilloscope - renders independently
        }

        private void InitializeWithTestData(WpfPlot plot)
        {
            plot.Plot.Clear();
            _signals.Clear();
            _waveformData.Clear();

            int sampleCount = 1750;

            for (int channel = 0; channel < 1; channel++)
            {
                // Generate test waveform data
                double[] waveform = GenerateTestWaveform(sampleCount, channel);
                _waveformData.Add(waveform);

                // Add signal to plot
                var signal = plot.Plot.Add.Signal(waveform);
                signal.Color = _channelColors[channel % _channelColors.Length];
                signal.LineWidth = 1;

                _signals.Add(signal);
            }

            // Set axis limits
            plot.Plot.Axes.SetLimitsY(-0.6, 0.6);
            plot.Plot.Axes.SetLimitsX(0, 1750);
            plot.Plot.YLabel("Voltage (V)");
            plot.Plot.XLabel("Time (samples)");
        }

        private void StartUpdateTimer()
        {
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_plot == null || _waveformData.Count == 0)
                return;

            // Increment phase to create scrolling effect
            _phaseOffset += 0.05;

            // Regenerate waveform with new phase
            for (int channel = 0; channel < _waveformData.Count; channel++)
            {
                double[] waveform = _waveformData[channel];
                double frequency = 0.002 + channel * 0.001;
                double amplitude = 0.4 - channel * 0.05;

                for (int i = 0; i < waveform.Length; i++)
                {
                    // Generate clean sine wave with phase offset
                    double sineWave = amplitude * Math.Sin(2 * Math.PI * frequency * i + _phaseOffset);

                    // Add noise (-0.03 to +0.03 V)
                    double noise = (_random.NextDouble() - 0.5) * 0.06;

                    waveform[i] = sineWave + noise;
                }
            }

            // Refresh the plot to show updated data
            _plot.Refresh();
        }

        private double[] GenerateTestWaveform(int sampleCount, int channel)
        {
            double[] data = new double[sampleCount];

            // Different frequency and amplitude for each channel
            double frequency = 0.002 + channel * 0.001;  // 0.002, 0.003, 0.004, 0.005 Hz (fewer cycles)
            double amplitude = 0.4 - channel * 0.05;    // 0.4, 0.35, 0.3, 0.25 V

            for (int i = 0; i < sampleCount; i++)
            {
                // Generate sine wave
                data[i] = amplitude * Math.Sin(2 * Math.PI * frequency * i);
            }

            return data;
        }
    }
}
