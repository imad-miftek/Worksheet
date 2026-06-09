using System;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Services
{
    public sealed class ParameterPlotPipeline : IPlotPipeline
    {
        private readonly PlotProcessor _processor;
        private readonly Func<long> _getVersion;

        public ParameterPlotPipeline(PlotProcessor processor, Func<long> getVersion, TimeSpan cadence)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _getVersion = getVersion ?? throw new ArgumentNullException(nameof(getVersion));
            Cadence = cadence;
        }

        public TimeSpan Cadence { get; }
        public long Version => _getVersion();

        public ProcessedPlotData? Process(PlotSettings settings, RenderTargetSize targetSize)
        {
            return _processor.Process(settings, targetSize);
        }

        public int GetSettingsHash(PlotSettings settings, RenderTargetSize targetSize)
        {
            var hash = new HashCode();
            hash.Add(settings.GetBinCount());
            hash.Add(settings.XFeature);
            hash.Add(settings.YFeature);
            hash.Add(settings.XAxisScaleType);
            hash.Add(settings.YAxisScaleType);
            hash.Add(settings.MinValue);
            hash.Add(settings.MaxValue);
            hash.Add(targetSize.PixelWidth);
            hash.Add(targetSize.PixelHeight);
            return hash.ToHashCode();
        }

        public void ResetState()
        {
            _processor.ResetIncrementalState();
        }

        public (long deltaAppliedCount, long fullRebuildCount, long sequenceGapCount) GetDeltaStats()
        {
            return _processor.GetDeltaStats();
        }
    }
}
