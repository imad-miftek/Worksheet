using System;
using System.Threading;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
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
            catch (Exception)
            {
                return null;
            }
        }

        public bool AdvanceStream()
        {
            return _dataSource.AdvanceStream();
        }

        public void SetStreamingEnabled(bool enabled)
        {
            _dataSource.SetStreamingEnabled(enabled);
        }

        public void ClearMemory()
        {
            _dataSource.ClearMemory();
        }

        public bool IsStreamingEnabled => _dataSource.IsStreamingEnabled;
        public long DataVersion => _dataSource.DataVersion;

        // Precompute scale/offset and effective clamp bounds once per Process call.
        // Hot loop calls ToBin which only does a clamp + one multiply + one add (+ Log10 for log scale).
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
            else
            {
                if (max <= min) max = min + 1;
                double scale = bins / (max - min);
                double offset = -min * scale;
                return (scale, offset, false, min, max);
            }
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
            var values = _dataSource.Get(settings.XFeature);
            int count = _dataSource.GetVisibleLength(settings.XFeature);
            int binCount = settings.GetBinCount();
            var (scale, offset, isLog, effMin, effMax) = BuildBinTransform(settings, settings.XAxisScaleType);

            // Each thread accumulates into its own array — no locks needed.
            var localCounts = new ThreadLocal<double[]>(() => new double[binCount], trackAllValues: true);

            Parallel.For(0, count, i =>
            {
                localCounts.Value![ToBin(values[i], scale, offset, isLog, binCount, effMin, effMax)]++;
            });

            var counts = new double[binCount];
            foreach (var local in localCounts.Values)
                for (int i = 0; i < binCount; i++)
                    counts[i] += local[i];

            var positions = new double[binCount];
            for (int i = 0; i < binCount; i++)
                positions[i] = i + 0.5;

            return new HistogramProcessedData(settings.Id, positions, counts, binCount, settings.XAxisScaleType);
        }

        private ProcessedPlotData ProcessHeatmap(PlotSettings settings)
        {
            var xValues = _dataSource.Get(settings.XFeature);
            var yValues = _dataSource.Get(settings.YFeature);
            int xCount = _dataSource.GetVisibleLength(settings.XFeature);
            int yCount = _dataSource.GetVisibleLength(settings.YFeature);
            int count = Math.Min(xCount, yCount);
            int bins = settings.GetBinCount();

            var (xScale, xOffset, xIsLog, xEffMin, xEffMax) = BuildBinTransform(settings, settings.XAxisScaleType);
            var (yScale, yOffset, yIsLog, yEffMin, yEffMax) = BuildBinTransform(settings, settings.YAxisScaleType);

            // Thread-local int[,] accumulators — int is sufficient for raw counts.
            var localCounts = new ThreadLocal<int[,]>(() => new int[bins, bins], trackAllValues: true);

            Parallel.For(0, count, i =>
            {
                int xBin = ToBin(xValues[i], xScale, xOffset, xIsLog, bins, xEffMin, xEffMax);
                int yBin = ToBin(yValues[i], yScale, yOffset, yIsLog, bins, yEffMin, yEffMax);
                localCounts.Value![xBin, yBin]++;
            });

            // Merge into a flat double[] for SIMD-accelerated max via ArrayStatistics.
            var flat = new double[bins * bins];
            foreach (var local in localCounts.Values)
                for (int x = 0; x < bins; x++)
                    for (int y = 0; y < bins; y++)
                        flat[x * bins + y] += local[x, y];

            double max = ArrayStatistics.Maximum(flat);

            var counts = new double[bins, bins];
            if (max > 0)
            {
                for (int x = 0; x < bins; x++)
                    for (int y = 0; y < bins; y++)
                    {
                        double v = flat[x * bins + y] / max;
                        counts[x, y] = v == 0 ? double.NaN : v;
                    }
            }
            else
            {
                for (int x = 0; x < bins; x++)
                    for (int y = 0; y < bins; y++)
                        counts[x, y] = double.NaN;
            }

            return new HeatmapProcessedData(settings.Id, counts);
        }

        private ProcessedPlotData ProcessSpectralRibbon(PlotSettings settings)
        {
            var channelNames = FeatureSelectionStrategy.ChannelNames;
            int channelCount = channelNames.Count;
            int bins = settings.GetBinCount();

            if (channelCount == 0)
            {
                var emptyData = new double[bins, 1];
                for (int i = 0; i < bins; i++)
                    emptyData[i, 0] = double.NaN;
                return new SpectralRibbonProcessedData(settings.Id, emptyData, Array.Empty<string>());
            }

            var (scale, offset, isLog, effMin, effMax) = BuildBinTransform(settings, settings.YAxisScaleType);
            var channelIndices = FeatureSelectionStrategy.FilteredChannelIndices;

            var counts = new double[bins, channelCount];

            // Each channel is fully independent — parallelize across channels.
            Parallel.For(0, channelCount, c =>
            {
                var values = _dataSource.Get(channelIndices[c]);
                int count = _dataSource.GetVisibleLength(channelIndices[c]);
                var col = new double[bins];

                for (int i = 0; i < count; i++)
                    col[ToBin(values[i], scale, offset, isLog, bins, effMin, effMax)]++;

                // Each c is unique so writing distinct columns is race-free.
                for (int b = 0; b < bins; b++)
                    counts[b, c] = col[b];
            });

            // Max over all cells then normalize.
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
                        double v = counts[y, x] / max;
                        counts[y, x] = v == 0 ? double.NaN : v;
                    }
                });
            }
            else
            {
                for (int y = 0; y < bins; y++)
                    for (int x = 0; x < channelCount; x++)
                        counts[y, x] = double.NaN;
            }

            return new SpectralRibbonProcessedData(settings.Id, counts, Array.Empty<string>());
        }
    }
}
