using System;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Services
{
    public class DataProcessor
    {
        private readonly DataSource _dataSource;
        private readonly double[][] _spectralChannels;
        private readonly double[,] _heatmapData;

        public DataProcessor(DataSource dataSource)
        {
            _dataSource = dataSource;
            _spectralChannels = BuildSpectralChannels(60, 240);
            _heatmapData = BuildHeatmapData(60, 60);
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
            var values = _dataSource.Get(settings.XFeature);
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
            return new HeatmapProcessedData(settings.Id, _heatmapData);
        }

        private ProcessedPlotData ProcessSpectralRibbon(PlotSettings settings)
        {
            return new SpectralRibbonProcessedData(settings.Id, _spectralChannels);
        }

        private static double[][] BuildSpectralChannels(int channelCount, int pointCount)
        {
            var random = new Random(4);
            var channels = new double[channelCount][];

            for (int i = 0; i < channelCount; i++)
            {
                double baseFreq = 0.015 + i * 0.0015;
                double phase = i * 0.2;
                double amplitude = 0.8 + (i % 5) * 0.12;

                var values = new double[pointCount];
                for (int j = 0; j < pointCount; j++)
                {
                    double x = j;
                    double noise = (random.NextDouble() - 0.5) * 0.2;
                    values[j] = amplitude * Math.Sin(baseFreq * x + phase) + noise + i * 0.015;
                }

                channels[i] = values;
            }

            return channels;
        }

        private static double[,] BuildHeatmapData(int width, int height)
        {
            var data = new double[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double v1 = Math.Sin(x * 0.12) * Math.Cos(y * 0.18);
                    double v2 = Math.Sin((x + y) * 0.07);
                    data[x, y] = v1 + v2;
                }
            }

            return data;
        }
    }
}
