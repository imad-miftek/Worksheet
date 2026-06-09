using System;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Services
{
    public interface IPlotPipeline
    {
        TimeSpan Cadence { get; }
        long Version { get; }
        ProcessedPlotData? Process(PlotSettings settings, RenderTargetSize targetSize);
        int GetSettingsHash(PlotSettings settings, RenderTargetSize targetSize);
        void ResetState();
        (long deltaAppliedCount, long fullRebuildCount, long sequenceGapCount) GetDeltaStats();
    }
}
