using System;
using Worksheet.Models;

namespace Worksheet.Services
{
    public sealed record ChasmOptions(
        TimeSpan AcquisitionInterval,
        int BatchSize,
        int ChannelCapacityBatches,
        int WindowCapacityEvents,
        SignalLayout SignalLayout,
        int Seed)
    {
        public static ChasmOptions Default =>
            new(
                TimeSpan.FromMilliseconds(25),
                BatchSize: 500,
                ChannelCapacityBatches: 8,
                WindowCapacityEvents: 200_000,
                SignalLayout: SignalLayout.Default,
                Seed: 12345);
    }
}
