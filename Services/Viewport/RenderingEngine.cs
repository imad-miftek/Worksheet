using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Threading;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Views.PlotViews;

namespace Worksheet.Services
{
    public class RenderingEngine : PollingEngine
    {
        private readonly DataStore _dataStore;
        private readonly Dispatcher _dispatcher;
        private readonly object _lock = new();
        private readonly List<RenderTarget> _targets = new();
        private readonly object _metricsLock = new();
        private double _histRenderTotalMs;
        private long _histRenderCount;
        private double _pcRenderTotalMs;
        private long _pcRenderCount;
        private double _srRenderTotalMs;
        private long _srRenderCount;

        public RenderingEngine(DataStore dataStore, Dispatcher dispatcher, TimeSpan interval)
            : base(interval)
        {
            _dataStore = dataStore;
            _dispatcher = dispatcher;
        }

        public void Register(WpfPlot plot, PlotView plotView, PlotSettings settings)
        {
            lock (_lock)
            {
                _targets.Add(new RenderTarget(plot, plotView, settings.Id, settings.PlotType));
            }
        }

        public void Unregister(Guid plotId)
        {
            lock (_lock)
            {
                _targets.RemoveAll(t => t.PlotId == plotId);
            }
        }

        protected override void Tick()
        {
            List<RenderTarget> snapshot;
            lock (_lock)
            {
                snapshot = new List<RenderTarget>(_targets);
            }

            foreach (var target in snapshot)
            {
                if (_dataStore.TryGetProcessedData(target.PlotId, out var data))
                {
                    if (ReferenceEquals(data, target.LastRenderedData))
                        continue;

                    _dispatcher.Invoke(() =>
                    {
                        var stopwatch = Stopwatch.StartNew();
                        try
                        {
                            target.PlotView.Render(target.Plot, data);
                        }
                        catch (Exception ex)
                        {
                            // Avoid crashing the dispatcher thread. Mark as rendered to prevent tight exception loops.
                            AppLog.Exception(ex, $"RenderingEngine.Render plotId={target.PlotId} view={target.PlotView.GetType().Name}");
                        }
                        finally
                        {
                            stopwatch.Stop();
                            RecordRenderTime(target.PlotType, stopwatch.Elapsed.TotalMilliseconds);
                            target.LastRenderedData = data;
                        }
                    });
                }
            }
        }

        public PlotTimingSnapshot GetAverageRenderTimes()
        {
            lock (_metricsLock)
            {
                return new PlotTimingSnapshot(
                    HistogramAverageMs: ComputeAverage(_histRenderTotalMs, _histRenderCount),
                    PseudocolorAverageMs: ComputeAverage(_pcRenderTotalMs, _pcRenderCount),
                    SpectralRibbonAverageMs: ComputeAverage(_srRenderTotalMs, _srRenderCount));
            }
        }

        private void RecordRenderTime(PlotType plotType, double elapsedMs)
        {
            lock (_metricsLock)
            {
                switch (plotType)
                {
                    case PlotType.Histogram:
                        _histRenderTotalMs += elapsedMs;
                        _histRenderCount++;
                        break;
                    case PlotType.Pseudocolor:
                        _pcRenderTotalMs += elapsedMs;
                        _pcRenderCount++;
                        break;
                    case PlotType.SpectralRibbon:
                        _srRenderTotalMs += elapsedMs;
                        _srRenderCount++;
                        break;
                }
            }
        }

        private static double ComputeAverage(double totalMs, long count)
        {
            return count > 0 ? totalMs / count : 0;
        }

        private sealed class RenderTarget
        {
            public RenderTarget(WpfPlot plot, PlotView plotView, Guid plotId, PlotType plotType)
            {
                Plot = plot;
                PlotView = plotView;
                PlotId = plotId;
                PlotType = plotType;
            }

            public WpfPlot Plot { get; }
            public PlotView PlotView { get; }
            public Guid PlotId { get; }
            public PlotType PlotType { get; }
            public object? LastRenderedData { get; set; }
        }
    }
}
