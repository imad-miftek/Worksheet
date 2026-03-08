using System;

namespace Worksheet.Services
{
    public class DataSource
    {
        private const int ChannelCount = 60;
        private const int PopulationCount = 4;
        private const double MaxValue = 100_000_000d;

        private readonly double[][] _channels;
        private readonly object _lock = new();
        private int _maxSampleCount;
        private readonly int _samplesPerTick;
        private int _visibleSampleCount;
        private bool _streamingEnabled = false;
        private long _dataVersion;

        // Event-driven populations: every event belongs to one of 4 global populations.
        // Each channel maps these 4 populations to 1..4 distinct peaks (some populations may share a peak).
        private readonly Random _rng = new(12345);
        private readonly double[,] _logMeans = new double[ChannelCount, PopulationCount];
        private readonly double[,] _logSigmas = new double[ChannelCount, PopulationCount];
        private readonly double[] _populationWeights = new double[PopulationCount];

        public DataSource()
        {
            _channels = new double[ChannelCount][];
            for (int i = 0; i < ChannelCount; i++)
                _channels[i] = Array.Empty<double>();

            InitializePopulationModel();

            _maxSampleCount = 0;
            _samplesPerTick = 500;
            _visibleSampleCount = 0;
            _dataVersion = 1;
        }

        private int ClampFeatureIndex(int featureIndex)
        {
            if (_channels.Length == 0)
                return 0;

            if (featureIndex < 0)
                return 0;
            if (featureIndex >= _channels.Length)
                return _channels.Length - 1;

            return featureIndex;
        }

        public double[] Get(int featureIndex)
        {
            if (_channels.Length == 0)
                return Array.Empty<double>();

            return _channels[ClampFeatureIndex(featureIndex)];
        }

        public int GetVisibleLength(int featureIndex)
        {
            int idx = ClampFeatureIndex(featureIndex);
            lock (_lock)
            {
                return Math.Min(_visibleSampleCount, _channels[idx].Length);
            }
        }

        public void GetVisible(int featureIndex, out double[] values, out int visibleLength)
        {
            int idx = ClampFeatureIndex(featureIndex);
            lock (_lock)
            {
                values = _channels[idx];
                visibleLength = Math.Min(_visibleSampleCount, values.Length);
            }
        }

        public bool AdvanceStream()
        {
            lock (_lock)
            {
                if (!_streamingEnabled)
                    return false;

                EnsureCapacityFor(_visibleSampleCount + _samplesPerTick);

                _visibleSampleCount = Math.Min(_visibleSampleCount + _samplesPerTick, _maxSampleCount);
                _dataVersion++;
                return true;
            }
        }

        public void SetStreamingEnabled(bool enabled)
        {
            lock (_lock)
            {
                _streamingEnabled = enabled;
            }
        }

        public void ClearMemory()
        {
            lock (_lock)
            {
                for (int i = 0; i < _channels.Length; i++)
                    Array.Resize(ref _channels[i], 0);

                _maxSampleCount = 0;
                _visibleSampleCount = 0;
                _dataVersion++;
            }
        }

        // CHASM append path: acquisition produces batches and the consumer appends them into the buffer.
        public void AppendBatch(double[][] channels, int count)
        {
            if (channels == null)
                throw new ArgumentNullException(nameof(channels));
            if (channels.Length != ChannelCount)
                throw new ArgumentException($"channels must have length {ChannelCount}.", nameof(channels));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0)
                return;

            for (int c = 0; c < ChannelCount; c++)
            {
                if (channels[c] == null)
                    throw new ArgumentException($"channels[{c}] is null.", nameof(channels));
                if (channels[c].Length != count)
                    throw new ArgumentException($"channels[{c}] length must equal count.", nameof(channels));
            }

            lock (_lock)
            {
                int start = _maxSampleCount;
                int newLen = start + count;

                for (int c = 0; c < ChannelCount; c++)
                    Array.Resize(ref _channels[c], newLen);

                for (int c = 0; c < ChannelCount; c++)
                    Array.Copy(channels[c], 0, _channels[c], start, count);

                _maxSampleCount = newLen;
                _visibleSampleCount = newLen;
                _dataVersion++;
            }
        }

        public bool IsStreamingEnabled
        {
            get
            {
                lock (_lock)
                {
                    return _streamingEnabled;
                }
            }
        }

        public long DataVersion
        {
            get
            {
                lock (_lock)
                {
                    return _dataVersion;
                }
            }
        }

        private void InitializePopulationModel()
        {
            // Reasonably balanced population weights (avoid tiny populations that won't be visible).
            double sum = 0;
            for (int p = 0; p < PopulationCount; p++)
            {
                _populationWeights[p] = 0.15 + _rng.NextDouble() * 0.35;
                sum += _populationWeights[p];
            }
            for (int p = 0; p < PopulationCount; p++)
                _populationWeights[p] /= sum;

            // Enforce variety across channels: 1..4 distinct peaks distributed across 60 channels.
            var distinctPeakCounts = new int[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
                distinctPeakCounts[i] = 1 + (i % 4);
            Shuffle(distinctPeakCounts);

            for (int c = 0; c < ChannelCount; c++)
            {
                int peakCount = distinctPeakCounts[c];
                var (distinctMeans, distinctSigmas) = CreateDistinctPeaks(peakCount);

                // Map 4 global populations to 1..4 distinct peaks.
                // This guarantees at most `peakCount` visible peaks for the channel.
                int[] popToPeak = peakCount switch
                {
                    1 => new[] { 0, 0, 0, 0 },
                    2 => new[] { 0, 1, 0, 1 },
                    3 => new[] { 0, 1, 2, 0 },
                    _ => new[] { 0, 1, 2, 3 },
                };

                // Add channel-to-channel variation by shuffling peak identities when multiple peaks exist.
                if (peakCount > 1)
                {
                    int[] peakIds = new int[peakCount];
                    for (int i = 0; i < peakCount; i++) peakIds[i] = i;
                    Shuffle(peakIds);
                    for (int p = 0; p < PopulationCount; p++)
                        popToPeak[p] = peakIds[popToPeak[p]];
                }

                for (int p = 0; p < PopulationCount; p++)
                {
                    int peakIndex = popToPeak[p];
                    _logMeans[c, p] = distinctMeans[peakIndex];
                    _logSigmas[c, p] = distinctSigmas[peakIndex];
                }
            }
        }

        private void EnsureCapacityFor(int requiredVisibleCount)
        {
            if (requiredVisibleCount <= _maxSampleCount)
                return;

            int oldLen = _maxSampleCount;
            int newLen = requiredVisibleCount;
            int toFill = newLen - oldLen;

            for (int c = 0; c < _channels.Length; c++)
                Array.Resize(ref _channels[c], newLen);

            FillEvents(oldLen, toFill);
            _maxSampleCount = newLen;
        }

        private void FillEvents(int startEventIndex, int eventCount)
        {
            int end = startEventIndex + eventCount;
            for (int e = startEventIndex; e < end; e++)
            {
                int pop = SamplePopulation();

                for (int c = 0; c < ChannelCount; c++)
                {
                    double logMean = _logMeans[c, pop];
                    double logSigma = _logSigmas[c, pop];

                    // "Spread only" noise: sample log-space normal then exponentiate.
                    double z = NextStandardNormal();
                    double log10Value = logMean + logSigma * z;
                    double value = Math.Pow(10, log10Value);

                    if (double.IsNaN(value) || double.IsInfinity(value))
                        value = MaxValue;
                    else if (value < 0)
                        value = 0;
                    else if (value > MaxValue)
                        value = MaxValue;

                    _channels[c][e] = value;
                }
            }
        }

        private int SamplePopulation()
        {
            double r = _rng.NextDouble();
            double cdf = 0;
            for (int p = 0; p < PopulationCount; p++)
            {
                cdf += _populationWeights[p];
                if (r <= cdf)
                    return p;
            }
            return PopulationCount - 1;
        }

        private double NextStandardNormal()
        {
            // Box-Muller transform (polar form not needed here).
            double u1 = _rng.NextDouble();
            if (u1 < double.Epsilon) u1 = double.Epsilon;
            double u2 = _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private (double[] means, double[] sigmas) CreateDistinctPeaks(int peakCount)
        {
            // log10(value) range for 0..100M is roughly [0..8]. Pick a safe interior range.
            const double minLog = 0.3;
            const double maxLog = 7.7;

            var means = new double[peakCount];
            var sigmas = new double[peakCount];

            if (peakCount == 1)
            {
                means[0] = 1.5 + _rng.NextDouble() * 5.0;   // ~30 .. 3e6
                sigmas[0] = 0.12 + _rng.NextDouble() * 0.10;
                return (means, sigmas);
            }

            // Evenly spaced with jitter (ensures separation).
            double step = (maxLog - minLog) / peakCount;
            for (int i = 0; i < peakCount; i++)
            {
                double center = minLog + (i + 0.5) * step;
                double jitter = ( _rng.NextDouble() * 2.0 - 1.0) * (step * 0.15);
                means[i] = Math.Clamp(center + jitter, minLog, maxLog);
                sigmas[i] = 0.08 + _rng.NextDouble() * 0.16;
            }

            // Shuffle peak ordering so channels aren't all aligned.
            Shuffle(means, sigmas);
            return (means, sigmas);
        }

        private void Shuffle(int[] values)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private void Shuffle(int[] values, int length)
        {
            for (int i = length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        private void Shuffle(double[] means, double[] sigmas)
        {
            for (int i = means.Length - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (means[i], means[j]) = (means[j], means[i]);
                (sigmas[i], sigmas[j]) = (sigmas[j], sigmas[i]);
            }
        }
    }
}
