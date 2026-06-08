using System;

namespace Worksheet.Services
{
    public sealed record ChasmOptions(
        TimeSpan AcquisitionInterval,
        int BatchSize,
        int ChannelCapacityBatches,
        int WindowCapacityEvents,
        int Seed)
    {
        public static ChasmOptions Default =>
            new(TimeSpan.FromMilliseconds(25), BatchSize: 500, ChannelCapacityBatches: 8, WindowCapacityEvents: 200_000, Seed: 12345);
    }
}
