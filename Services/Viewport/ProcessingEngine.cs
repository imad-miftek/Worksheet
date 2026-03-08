using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Worksheet.Models;
using Worksheet.Models.Gates;
using Worksheet.Services.Viewport.Gates;

namespace Worksheet.Services
{
    public class ProcessingEngine : PollingEngine
    {
        private readonly DataStore _dataStore;
        private readonly PlotProcessor _plotProcessor;
        private readonly GateProcessor _gateProcessor;
        private readonly Func<long> _getDataVersion;
        private readonly object _processingLock = new();
        private readonly Dictionary<Guid, SettingsFingerprint> _lastProcessedSettings = new();
        private readonly Dictionary<Guid, GateFingerprint> _lastProcessedGates = new();
        private readonly object _metricsLock = new();
        private double _histComputeTotalMs;
        private long _histComputeCount;
        private double _pcComputeTotalMs;
        private long _pcComputeCount;
        private double _srComputeTotalMs;
        private long _srComputeCount;

        public ProcessingEngine(DataStore dataStore, PlotProcessor plotProcessor, GateProcessor gateProcessor, Func<long> getDataVersion, TimeSpan interval)
            : base(interval)
        {
            _dataStore = dataStore;
            _plotProcessor = plotProcessor;
            _gateProcessor = gateProcessor;
            _getDataVersion = getDataVersion;
        }

        protected override void Tick()
        {
            lock (_processingLock)
            {
                long dataVersion = _getDataVersion();

                var settings = _dataStore.GetAllSettings();
                var activePlotIds = new HashSet<Guid>();

                foreach (var plotSettings in settings)
                {
                    activePlotIds.Add(plotSettings.Id);
                    var fingerprint = SettingsFingerprint.From(plotSettings, dataVersion);

                    if (_lastProcessedSettings.TryGetValue(plotSettings.Id, out var previous) && previous.Equals(fingerprint))
                        continue;

                    var stopwatch = Stopwatch.StartNew();
                    var processed = _plotProcessor.Process(plotSettings);
                    stopwatch.Stop();
                    RecordComputeTime(plotSettings.PlotType, stopwatch.Elapsed.TotalMilliseconds);

                    if (processed != null)
                    {
                        _dataStore.SetProcessedData(processed);
                        _lastProcessedSettings[plotSettings.Id] = fingerprint;
                    }
                }

                var staleIds = _lastProcessedSettings.Keys.Where(id => !activePlotIds.Contains(id)).ToArray();
                foreach (var staleId in staleIds)
                {
                    _lastProcessedSettings.Remove(staleId);
                }

                ProcessGates(dataVersion);
            }
        }

        private void ProcessGates(long dataVersion)
        {
            var gates = _dataStore.GetAllGates();
            var activeGateIds = new HashSet<Guid>();

            foreach (var gate in gates)
            {
                activeGateIds.Add(gate.GateId);

                if (!_dataStore.TryGetSettings(gate.Plot.PlotId, out var plotSettings))
                    continue;

                var fingerprint = GateFingerprint.From(gate, plotSettings, dataVersion);
                if (_lastProcessedGates.TryGetValue(gate.GateId, out var prev) && prev.Equals(fingerprint))
                    continue;

                var result = _gateProcessor.Process(gate, plotSettings, dataVersion);
                _dataStore.SetGateResult(result);
                _lastProcessedGates[gate.GateId] = fingerprint;
            }

            var stale = _lastProcessedGates.Keys.Where(id => !activeGateIds.Contains(id)).ToArray();
            foreach (var id in stale)
                _lastProcessedGates.Remove(id);
        }

        public PlotTimingSnapshot GetAverageComputeTimes()
        {
            lock (_metricsLock)
            {
                return new PlotTimingSnapshot(
                    HistogramAverageMs: ComputeAverage(_histComputeTotalMs, _histComputeCount),
                    PseudocolorAverageMs: ComputeAverage(_pcComputeTotalMs, _pcComputeCount),
                    SpectralRibbonAverageMs: ComputeAverage(_srComputeTotalMs, _srComputeCount));
            }
        }

        public IncrementalProcessingStats GetIncrementalStats()
        {
            var (deltaAppliedCount, fullRebuildCount, sequenceGapCount) = _plotProcessor.GetDeltaStats();
            return new IncrementalProcessingStats(deltaAppliedCount, fullRebuildCount, sequenceGapCount);
        }

        public void ResetMetrics()
        {
            lock (_metricsLock)
            {
                _histComputeTotalMs = 0;
                _histComputeCount = 0;
                _pcComputeTotalMs = 0;
                _pcComputeCount = 0;
                _srComputeTotalMs = 0;
                _srComputeCount = 0;
            }

            _plotProcessor.ResetIncrementalState();
        }

        public void OnDataCleared()
        {
            lock (_processingLock)
            {
                _lastProcessedSettings.Clear();
                _lastProcessedGates.Clear();
                _plotProcessor.ResetIncrementalState();
            }
        }

        private void RecordComputeTime(PlotType plotType, double elapsedMs)
        {
            lock (_metricsLock)
            {
                switch (plotType)
                {
                    case PlotType.Histogram:
                        _histComputeTotalMs += elapsedMs;
                        _histComputeCount++;
                        break;
                    case PlotType.Pseudocolor:
                        _pcComputeTotalMs += elapsedMs;
                        _pcComputeCount++;
                        break;
                    case PlotType.SpectralRibbon:
                        _srComputeTotalMs += elapsedMs;
                        _srComputeCount++;
                        break;
                }
            }
        }

        private static double ComputeAverage(double totalMs, long count)
        {
            return count > 0 ? totalMs / count : 0;
        }

        private readonly record struct SettingsFingerprint(
            PlotType PlotType,
            int BinCount,
            int XFeature,
            int YFeature,
            AxisScaleType XAxisScaleType,
            AxisScaleType YAxisScaleType,
            double MinValue,
            double MaxValue,
            long DataVersion)
        {
            public static SettingsFingerprint From(PlotSettings settings, long dataVersion) =>
                new(
                    settings.PlotType,
                    settings.GetBinCount(),
                    settings.XFeature,
                    settings.YFeature,
                    settings.XAxisScaleType,
                    settings.YAxisScaleType,
                    settings.MinValue,
                    settings.MaxValue,
                    dataVersion);
        }

        private readonly record struct GateFingerprint(
            Guid PlotId,
            GateType GateType,
            int GeometryHash,
            PlotType PlotType,
            int BinCount,
            int XFeature,
            int YFeature,
            AxisScaleType XAxisScaleType,
            AxisScaleType YAxisScaleType,
            double MinValue,
            double MaxValue,
            long DataVersion)
        {
            public static GateFingerprint From(GateSettings gate, PlotSettings settings, long dataVersion) =>
                new(
                    gate.Plot.PlotId,
                    gate.GateType,
                    gate.Geometry.GetGeometryHash(),
                    settings.PlotType,
                    settings.GetBinCount(),
                    settings.XFeature,
                    settings.YFeature,
                    settings.XAxisScaleType,
                    settings.YAxisScaleType,
                    settings.MinValue,
                    settings.MaxValue,
                    dataVersion);
        }
    }
}
