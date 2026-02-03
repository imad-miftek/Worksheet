using System;
using System.Collections.Generic;
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
                _targets.Add(new RenderTarget(plot, plotView, settings.Id));
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
                    _dispatcher.Invoke(() =>
                    {
                        target.PlotView.Render(target.Plot, data);
                    });
                }
            }
        }

        private sealed class RenderTarget
        {
            public RenderTarget(WpfPlot plot, PlotView plotView, Guid plotId)
            {
                Plot = plot;
                PlotView = plotView;
                PlotId = plotId;
            }

            public WpfPlot Plot { get; }
            public PlotView PlotView { get; }
            public Guid PlotId { get; }
        }
    }
}
