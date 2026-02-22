using System;

namespace Worksheet.Services
{
    public sealed record ChasmOptions(
        TimeSpan AcquisitionInterval,
        int BatchSize,
        int ChannelCapacityBatches,
        int Seed)
    {
        public static ChasmOptions Default =>
            new(TimeSpan.FromMilliseconds(25), BatchSize: 500, ChannelCapacityBatches: 8, Seed: 12345);
    }
}
