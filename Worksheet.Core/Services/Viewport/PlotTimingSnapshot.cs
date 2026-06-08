namespace Worksheet.Services
{
    public readonly record struct PlotTimingSnapshot(
        double HistogramAverageMs,
        double PseudocolorAverageMs,
        double SpectralRibbonAverageMs);
}
