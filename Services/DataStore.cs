using System;
using System.Collections.Generic;
using System.Linq;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Services
{
    public class DataStore
    {
        private readonly object _lock = new();
        private readonly Dictionary<Guid, PlotSettings> _settings = new();
        private readonly Dictionary<Guid, ProcessedPlotData> _processed = new();
        private readonly Viewport _viewport = new();

        public Viewport Viewport
        {
            get
            {
                lock (_lock)
                {
                    return _viewport;
                }
            }
        }

        public void UpsertSettings(PlotSettings settings)
        {
            lock (_lock)
            {
                _settings[settings.Id] = settings;
                if (!_viewport.PlotIds.Contains(settings.Id))
                    _viewport.PlotIds.Add(settings.Id);
            }
        }

        public bool TryGetSettings(Guid plotId, out PlotSettings settings)
        {
            lock (_lock)
            {
                return _settings.TryGetValue(plotId, out settings!);
            }
        }

        public IReadOnlyList<PlotSettings> GetAllSettings()
        {
            lock (_lock)
            {
                return _settings.Values.ToList();
            }
        }

        public void RemovePlot(Guid plotId)
        {
            lock (_lock)
            {
                _settings.Remove(plotId);
                _processed.Remove(plotId);
                _viewport.PlotIds.Remove(plotId);
            }
        }

        public void SetProcessedData(ProcessedPlotData data)
        {
            lock (_lock)
            {
                _processed[data.PlotId] = data;
            }
        }

        public bool TryGetProcessedData(Guid plotId, out ProcessedPlotData data)
        {
            lock (_lock)
            {
                return _processed.TryGetValue(plotId, out data!);
            }
        }
    }
}
