namespace Worksheet.Services
{
    public readonly record struct ChannelWindowSnapshot(
        double[] Values,
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
    }
}
