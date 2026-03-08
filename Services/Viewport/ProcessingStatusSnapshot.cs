namespace Worksheet.Services
{
    public readonly record struct ProcessingStatusSnapshot(
        double EventRatePerSecond,
        double HistogramAverageComputeMs,
        double PseudocolorAverageComputeMs,
        double SpectralRibbonAverageComputeMs,
        double HistogramAverageRenderMs,
        double PseudocolorAverageRenderMs,
        double SpectralRibbonAverageRenderMs);
}
