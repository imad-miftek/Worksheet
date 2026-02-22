using System;
using System.Windows.Threading;
using ScottPlot.WPF;
using Worksheet.Models;

namespace Worksheet.Services
{
    public class ViewportSession : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly DataStore _dataStore;
        private readonly DataSource _dataSource;
        private readonly DataProcessor _dataProcessor;
        private readonly ProcessingEngine _processingEngine;
        private readonly RenderingEngine _renderingEngine;
        private readonly FeatureSelectionStrategy _featureSelection;

        public ViewportSession(Dispatcher dispatcher, TimeSpan processingInterval, TimeSpan renderingInterval)
        {
            _dispatcher = dispatcher;
            _dataStore = new DataStore();
            _dataSource = new DataSource();
            _dataProcessor = new DataProcessor(_dataSource);
            _processingEngine = new ProcessingEngine(_dataStore, _dataProcessor, processingInterval);
            _renderingEngine = new RenderingEngine(_dataStore, dispatcher, renderingInterval);
            _featureSelection = new FeatureSelectionStrategy();
        }

        public event EventHandler? MemoryCleared;

        public DataStore DataStore => _dataStore;
        public FeatureSelectionStrategy FeatureSelection => _featureSelection;
        public bool IsStreamingEnabled => _dataProcessor.IsStreamingEnabled;

        public void Start()
        {
            _processingEngine.Start();
            _renderingEngine.Start();
        }

        public void Stop()
        {
            _processingEngine.Stop();
            _renderingEngine.Stop();
        }

        public void Dispose()
        {
            Stop();
            _processingEngine.Dispose();
            _renderingEngine.Dispose();
        }

        public void RegisterPlot(PlotSettings settings)
        {
            _dataStore.UpsertSettings(settings);
        }

        public void UnregisterPlot(Guid plotId)
        {
            _dataStore.RemovePlot(plotId);
            _renderingEngine.Unregister(plotId);
        }

        public void RegisterRenderTarget(WpfPlot plot, Views.PlotViews.PlotView plotView, PlotSettings settings)
        {
            _renderingEngine.Register(plot, plotView, settings);
        }

        public void SetStreamingEnabled(bool enabled)
        {
            _dataProcessor.SetStreamingEnabled(enabled);
        }

        public void ClearMemory()
        {
            _dataProcessor.ClearMemory();

            // Clear visuals immediately on the UI thread without relying on a new render cycle.
            if (_dispatcher.CheckAccess())
                MemoryCleared?.Invoke(this, EventArgs.Empty);
            else
                _dispatcher.BeginInvoke(() => MemoryCleared?.Invoke(this, EventArgs.Empty));
        }
    }
}
