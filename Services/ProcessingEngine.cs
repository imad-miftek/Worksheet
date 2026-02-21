using System;
using System.Collections.Generic;
using System.Linq;
using Worksheet.Models;

namespace Worksheet.Services
{
    public class ProcessingEngine : PollingEngine
    {
        private readonly DataStore _dataStore;
        private readonly DataProcessor _dataProcessor;
        private readonly Dictionary<Guid, SettingsFingerprint> _lastProcessedSettings = new();

        public ProcessingEngine(DataStore dataStore, DataProcessor dataProcessor, TimeSpan interval)
            : base(interval)
        {
            _dataStore = dataStore;
            _dataProcessor = dataProcessor;
        }

        protected override void Tick()
        {
            _dataProcessor.AdvanceStream();
            long dataVersion = _dataProcessor.DataVersion;

            var settings = _dataStore.GetAllSettings();
            var activePlotIds = new HashSet<Guid>();

            foreach (var plotSettings in settings)
            {
                activePlotIds.Add(plotSettings.Id);
                var fingerprint = SettingsFingerprint.From(plotSettings, dataVersion);

                if (_lastProcessedSettings.TryGetValue(plotSettings.Id, out var previous) && previous.Equals(fingerprint))
                    continue;

                var processed = _dataProcessor.Process(plotSettings);
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
    }
}
