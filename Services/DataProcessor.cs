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
            int binCount = settings.GetBinCount();

            var counts = new double[binCount];
            foreach (var raw in values)
            {
                double pos = settings.DataValueToBinPosition(raw, settings.XAxisScaleType);
                int index = (int)Math.Floor(pos);

                if (index < 0)
                    index = 0;
                else if (index >= binCount)
                    index = binCount - 1;

                counts[index]++;
            }

            var positions = new double[binCount];
            for (int i = 0; i < binCount; i++)
                positions[i] = i + 0.5;

            return new HistogramProcessedData(settings.Id, positions, counts, binCount, settings.XAxisScaleType);
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
