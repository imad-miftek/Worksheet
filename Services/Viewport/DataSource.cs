using System;

namespace Worksheet.Services
{
    public class DataSource
    {
        private const int ChannelCount = 60;

        private readonly double[][] _channels;
        private readonly object _lock = new();
        private readonly int _windowCapacity;
        private int _writeIndex;
        private int _count;
        private long _totalEventsIngested;
        private bool _streamingEnabled;
        private long _dataVersion;

        public DataSource(int windowCapacity = 200_000)
        {
            if (windowCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowCapacity));

            _windowCapacity = windowCapacity;
            _channels = new double[ChannelCount][];
            for (int i = 0; i < ChannelCount; i++)
                _channels[i] = new double[_windowCapacity];

            _dataVersion = 1;
        }

        private int ClampFeatureIndex(int featureIndex)
        {
            if (featureIndex < 0)
                return 0;
            if (featureIndex >= _channels.Length)
                return _channels.Length - 1;

            return featureIndex;
        }

        public ChannelWindowSnapshot GetSnapshot(int featureIndex)
        {
            int idx = ClampFeatureIndex(featureIndex);
            lock (_lock)
            {
                int startIndex = (_writeIndex - _count + _windowCapacity) % _windowCapacity;
                return new ChannelWindowSnapshot(_channels[idx], startIndex, _count, _windowCapacity, _dataVersion);
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
                    Array.Clear(_channels[i], 0, _channels[i].Length);

                _writeIndex = 0;
                _count = 0;
                _dataVersion++;
            }
        }

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

            int sourceOffset = Math.Max(0, count - _windowCapacity);
            int retainedCount = count - sourceOffset;
            if (retainedCount <= 0)
                return;

            lock (_lock)
            {
                for (int c = 0; c < ChannelCount; c++)
                    CopyIntoRing(_channels[c], channels[c], sourceOffset, retainedCount);

                _writeIndex = (_writeIndex + retainedCount) % _windowCapacity;
                _count = Math.Min(_count + retainedCount, _windowCapacity);
                _totalEventsIngested += count;
                _dataVersion++;
            }
        }

        private void CopyIntoRing(double[] destination, double[] source, int sourceOffset, int count)
        {
            int firstSegment = Math.Min(count, _windowCapacity - _writeIndex);
            Array.Copy(source, sourceOffset, destination, _writeIndex, firstSegment);

            int remaining = count - firstSegment;
            if (remaining > 0)
                Array.Copy(source, sourceOffset + firstSegment, destination, 0, remaining);
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

        public long TotalEventsIngested
        {
            get
            {
                lock (_lock)
                {
                    return _totalEventsIngested;
                }
            }
        }
    }
}
