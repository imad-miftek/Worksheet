using System;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using Worksheet.Models;
using Worksheet.Models.Data;

namespace Worksheet.Services
{
    public class PlotProcessor
    {
        private readonly IChannelDataBuffer _buffer;

        public PlotProcessor(IChannelDataBuffer buffer)
        {
            _buffer = buffer;
        }

        public ProcessedPlotData? Process(PlotSettings settings)
        {
            try
            {
                return settings.PlotType switch
                {
                    PlotType.Histogram => ProcessHistogram(settings),
                    PlotType.Pseudocolor => ProcessHeatmap(settings),
                    PlotType.SpectralRibbon => ProcessSpectralRibbon(settings),
                    _ => throw new ArgumentOutOfRangeException(nameof(settings.PlotType), settings.PlotType, "Unsupported plot type.")
                };
            }
            catch (Exception ex)
            {
                AppLog.Exception(ex, $"PlotProcessor.Process plotType={settings.PlotType} plotId={settings.Id} x={settings.XFeature} y={settings.YFeature}");
                return null;
            }
        }

        private static (double scale, double offset, bool isLog, double effMin, double effMax) BuildBinTransform(
            PlotSettings settings, AxisScaleType scaleType)
        {
            double min = settings.MinValue;
            double max = settings.MaxValue;
            int bins = settings.GetBinCount();

            if (scaleType == AxisScaleType.Logarithmic)
            {
                if (min < 1) min = 1;
                if (max <= min) max = min * 10;
                double minLog = Math.Log10(min);
                double maxLog = Math.Log10(max);
                double scale = bins / (maxLog - minLog);
                double offset = -minLog * scale;
                return (scale, offset, true, min, max);
            }

            if (max <= min) max = min + 1;
            double linearScale = bins / (max - min);
            double linearOffset = -min * linearScale;
            return (linearScale, linearOffset, false, min, max);
        }

        private static int ToBin(double value, double scale, double offset, bool isLog,
            int bins, double effMin, double effMax)
        {
            if (value < effMin) value = effMin;
            else if (value > effMax) value = effMax;

            double pos = isLog ? Math.Log10(value) * scale + offset : value * scale + offset;
            return Math.Clamp((int)pos, 0, bins - 1);
        }

        private ProcessedPlotData ProcessHistogram(PlotSettings settings)
        {
            ChannelWindowSnapshot snapshot = _buffer.GetSnapshot(settings.XFeature);
            int binCount = settings.GetBinCount();
            var (scale, offset, isLog, effMin, effMax) = BuildBinTransform(settings, settings.XAxisScaleType);

            var localCounts = new ThreadLocal<double[]>(() => new double[binCount], trackAllValues: true);

            RunParallel(snapshot, physicalIndex =>
            {
                double value = snapshot.Values[physicalIndex];
                localCounts.Value![ToBin(value, scale, offset, isLog, binCount, effMin, effMax)]++;
            });

            var counts = new double[binCount];
            foreach (double[] local in localCounts.Values)
                for (int i = 0; i < binCount; i++)
                    counts[i] += local[i];

            var positions = new double[binCount];
            for (int i = 0; i < binCount; i++)
                positions[i] = i + 0.5;

            return new HistogramProcessedData(settings.Id, positions, counts, binCount, settings.XAxisScaleType);
        }

        private ProcessedPlotData ProcessHeatmap(PlotSettings settings)
        {
            ChannelWindowSnapshot xSnapshot = _buffer.GetSnapshot(settings.XFeature);
            ChannelWindowSnapshot ySnapshot = _buffer.GetSnapshot(settings.YFeature);
            int count = Math.Min(xSnapshot.Count, ySnapshot.Count);
            int bins = settings.GetBinCount();
            bool isEmpty = count <= 0;

            var (xScale, xOffset, xIsLog, xEffMin, xEffMax) = BuildBinTransform(settings, settings.XAxisScaleType);
            var (yScale, yOffset, yIsLog, yEffMin, yEffMax) = BuildBinTransform(settings, settings.YAxisScaleType);

            var localCounts = new ThreadLocal<int[,]>(() => new int[bins, bins], trackAllValues: true);

            RunParallel(xSnapshot, ySnapshot, count, (xPhysicalIndex, yPhysicalIndex) =>
            {
                int xBin = ToBin(xSnapshot.Values[xPhysicalIndex], xScale, xOffset, xIsLog, bins, xEffMin, xEffMax);
                int yBin = ToBin(ySnapshot.Values[yPhysicalIndex], yScale, yOffset, yIsLog, bins, yEffMin, yEffMax);
                int row = (bins - 1) - yBin;
                localCounts.Value![row, xBin]++;
            });

            var flat = new double[bins * bins];
            foreach (int[,] local in localCounts.Values)
                for (int y = 0; y < bins; y++)
                    for (int x = 0; x < bins; x++)
                        flat[y * bins + x] += local[y, x];

            double max = ArrayStatistics.Maximum(flat);

            var counts = new double[bins, bins];
            if (max > 0)
            {
                for (int y = 0; y < bins; y++)
                    for (int x = 0; x < bins; x++)
                    {
                        double raw = flat[y * bins + x];
                        counts[y, x] = raw == 0 ? double.NaN : raw / max;
                    }
            }

            return new HeatmapProcessedData(settings.Id, counts, isEmpty);
        }

        private ProcessedPlotData ProcessSpectralRibbon(PlotSettings settings)
        {
            var channelNames = FeatureSelectionStrategy.ChannelNames;
            int channelCount = channelNames.Count;
            int bins = settings.GetBinCount();

            if (channelCount == 0)
            {
                var emptyData = new double[bins, 1];
                return new SpectralRibbonProcessedData(settings.Id, emptyData, Array.Empty<string>(), isEmpty: true);
            }

            var (scale, offset, isLog, effMin, effMax) = BuildBinTransform(settings, settings.YAxisScaleType);
            var channelIndices = FeatureSelectionStrategy.FilteredChannelIndices;

            var counts = new double[bins, channelCount];

            Parallel.For(0, channelCount, c =>
            {
                ChannelWindowSnapshot snapshot = _buffer.GetSnapshot(channelIndices[c]);
                var col = new double[bins];

                RunSequential(snapshot, physicalIndex =>
                {
                    double value = snapshot.Values[physicalIndex];
                    col[ToBin(value, scale, offset, isLog, bins, effMin, effMax)]++;
                });

                for (int b = 0; b < bins; b++)
                {
                    int row = (bins - 1) - b;
                    counts[row, c] = col[b];
                }
            });

            double max = 0;
            for (int y = 0; y < bins; y++)
                for (int x = 0; x < channelCount; x++)
                    if (counts[y, x] > max) max = counts[y, x];

            if (max > 0)
            {
                Parallel.For(0, bins, y =>
                {
                    for (int x = 0; x < channelCount; x++)
                    {
                        double raw = counts[y, x];
                        counts[y, x] = raw == 0 ? double.NaN : raw / max;
                    }
                });
            }

            return new SpectralRibbonProcessedData(settings.Id, counts, Array.Empty<string>(), isEmpty: max <= 0);
        }

        private static void RunParallel(ChannelWindowSnapshot snapshot, Action<int> action)
        {
            if (snapshot.Count <= 0)
                return;

            if (snapshot.IsContiguous)
            {
                int start = snapshot.StartIndex;
                Parallel.For(0, snapshot.Count, i => action(start + i));
                return;
            }

            Parallel.For(0, snapshot.Count, i => action(snapshot.PhysicalIndexAt(i)));
        }

        private static void RunSequential(ChannelWindowSnapshot snapshot, Action<int> action)
        {
            if (snapshot.Count <= 0)
                return;

            if (snapshot.IsContiguous)
            {
                int end = snapshot.StartIndex + snapshot.Count;
                for (int physicalIndex = snapshot.StartIndex; physicalIndex < end; physicalIndex++)
                    action(physicalIndex);
                return;
            }

            for (int i = 0; i < snapshot.Count; i++)
                action(snapshot.PhysicalIndexAt(i));
        }

        private static void RunParallel(ChannelWindowSnapshot xSnapshot, ChannelWindowSnapshot ySnapshot, int count, Action<int, int> action)
        {
            if (count <= 0)
                return;

            bool xContiguous = xSnapshot.IsContiguous;
            bool yContiguous = ySnapshot.IsContiguous;
            if (xContiguous && yContiguous)
            {
                int xStart = xSnapshot.StartIndex;
                int yStart = ySnapshot.StartIndex;
                Parallel.For(0, count, i => action(xStart + i, yStart + i));
                return;
            }

            Parallel.For(0, count, i => action(xSnapshot.PhysicalIndexAt(i), ySnapshot.PhysicalIndexAt(i)));
        }
    }
}
