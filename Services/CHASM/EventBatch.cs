using System;

namespace Worksheet.Services
{
    public sealed class EventBatch
    {
        public int Count { get; }
        public double[][] Channels { get; }

        public EventBatch(int count, double[][] channels)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (channels == null)
                throw new ArgumentNullException(nameof(channels));
            if (channels.Length != 60)
                throw new ArgumentException("Channels must have length 60.", nameof(channels));

            for (int c = 0; c < channels.Length; c++)
            {
                if (channels[c] == null)
                    throw new ArgumentException($"Channels[{c}] is null.", nameof(channels));
                if (channels[c].Length != count)
                    throw new ArgumentException($"Channels[{c}] length must equal count.", nameof(channels));
            }

            Count = count;
            Channels = channels;
        }
    }
}
