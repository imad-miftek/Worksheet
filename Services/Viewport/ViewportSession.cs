using System;
using System.Windows.Threading;
using ScottPlot.WPF;
using Worksheet.Models;
using Worksheet.Services;
using Worksheet.Models.Gates;
using Worksheet.Services.Viewport.Gates;

namespace Worksheet.Services
{
    public class ViewportSession : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly DataStore _dataStore;
        private readonly DataSource _dataSource;
        private readonly ChasmDataSource _chasmDataSource;
        private readonly Chasm _chasm;
        private readonly PlotProcessor _plotProcessor;
        private readonly GateProcessor _gateProcessor;
        private readonly ProcessingEngine _processingEngine;
        private readonly RenderingEngine _renderingEngine;
        private readonly FeatureSelectionStrategy _featureSelection;
        private readonly ChasmOptions _chasmOptions;

        public ViewportSession(Dispatcher dispatcher, TimeSpan processingInterval, TimeSpan renderingInterval)
        {
            _dispatcher = dispatcher;
            _dataStore = new DataStore();
            _chasmOptions = ChasmOptions.Default;
            _dataSource = new DataSource(_chasmOptions.WindowCapacityEvents);
            _chasmDataSource = new ChasmDataSource(_dataSource);
            var producer = new MockProducer(_chasmOptions);
            var consumer = new ChasmConsumer(producer.Reader, _chasmDataSource);
            _chasm = new Chasm(producer, consumer, _chasmDataSource);

            _plotProcessor = new PlotProcessor(_chasmDataSource);
            _gateProcessor = new GateProcessor(_chasmDataSource);
            _processingEngine = new ProcessingEngine(_dataStore, _plotProcessor, _gateProcessor, () => _chasm.DataVersion, processingInterval);
            _renderingEngine = new RenderingEngine(_dataStore, dispatcher, renderingInterval);
            _featureSelection = new FeatureSelectionStrategy();
        }

        public event EventHandler? MemoryCleared;

        public DataStore DataStore => _dataStore;
        public FeatureSelectionStrategy FeatureSelection => _featureSelection;
        public bool IsStreamingEnabled => _chasm.IsStreamingEnabled;

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
            if (enabled)
                _chasm.StartStreaming();
            else
                _chasm.StopStreaming();
        }

        public void ClearMemory()
        {
            _chasm.ClearMemory();

            // Clear visuals immediately on the UI thread without relying on a new render cycle.
            if (_dispatcher.CheckAccess())
                MemoryCleared?.Invoke(this, EventArgs.Empty);
            else
                _dispatcher.BeginInvoke(() => MemoryCleared?.Invoke(this, EventArgs.Empty));
        }

        public void UpsertGate(GateSettings gate)
        {
            _dataStore.UpsertGate(gate);
        }

        public void RemoveGate(Guid gateId)
        {
            _dataStore.RemoveGate(gateId);
        }
    }
}
