using System;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Services
{
    public class DataProcessor
    {
        private readonly DataSource _dataSource;

        public DataProcessor(DataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public ProcessedPlotData Process(PlotSettings settings)
        {
            return settings.PlotType switch
            {
                PlotType.Histogram => ProcessHistogram(settings),
                PlotType.Pseudocolor => ProcessHeatmap(settings),
                PlotType.SpectralRibbon => ProcessSpectralRibbon(settings),
                _ => throw new ArgumentOutOfRangeException(nameof(settings.PlotType), settings.PlotType, "Unsupported plot type.")
            };
        }

        private ProcessedPlotData ProcessHistogram(PlotSettings settings)
        {
            var values = _dataSource.GetHistogramValues(settings.XFeature);
            int binCount = settings.BinCount > 0 ? settings.BinCount : 256;
            var binning = new HistogramBinning(binCount, settings.XAxisScaleType);
            var counts = binning.CreateCounts(values);
            var positions = binning.CreateBinPositions();

            return new HistogramProcessedData(settings.Id, positions, counts, binning);
        }

        private ProcessedPlotData ProcessHeatmap(PlotSettings settings)
        {
            var data = _dataSource.GetHeatmapData(settings.XFeature, settings.YFeature);
            return new HeatmapProcessedData(settings.Id, data);
        }

        private ProcessedPlotData ProcessSpectralRibbon(PlotSettings settings)
        {
            var channels = _dataSource.GetSpectralChannels(settings.YFeature);
            return new SpectralRibbonProcessedData(settings.Id, channels);
        }
    }
}
