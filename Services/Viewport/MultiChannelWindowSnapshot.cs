using System;

namespace Worksheet.Services
{
    public readonly record struct MultiChannelWindowSnapshot(
        double[][] ChannelValues,
        int StartIndex,
        int Count,
        int Capacity,
        long Version,
        long StartSequence,
        long EndSequence)
    {
        public bool IsContiguous => Count == 0 || StartIndex + Count <= Capacity;

        public int PhysicalIndexAt(int logicalIndex)
        {
            return (StartIndex + logicalIndex) % Capacity;
        }

        public int PhysicalIndexForSequence(long sequence)
        {
            return PhysicalIndexAt((int)(sequence - StartSequence));
        }

        public double ValueAt(int channelIndex, long sequence)
        {
            return ChannelValues[channelIndex][PhysicalIndexForSequence(sequence)];
        }

        public ChannelWindowSnapshot Channel(int channelIndex)
        {
            return new ChannelWindowSnapshot(
                Values: ChannelValues[channelIndex],
                StartIndex: StartIndex,
                Count: Count,
                Capacity: Capacity,
                Version: Version,
                StartSequence: StartSequence,
                EndSequence: EndSequence);
        }

        public static MultiChannelWindowSnapshot Empty { get; } = new(
            ChannelValues: Array.Empty<double[]>(),
            StartIndex: 0,
            Count: 0,
            Capacity: 1,
            Version: 0,
            StartSequence: 0,
            EndSequence: 0);
    }
}
