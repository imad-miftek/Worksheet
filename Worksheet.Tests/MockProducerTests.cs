using System;
using System.Threading;
using System.Threading.Tasks;
using Worksheet.Models;
using Worksheet.Services;
using Xunit;

namespace Worksheet.Tests;

public sealed class MockProducerTests
{
    [Fact]
    public async Task MaxThroughputModeEmitsColumnMajorBatches()
    {
        var options = ChasmOptions.Default with
        {
            BatchSize = 4,
            ChannelCapacityBatches = 2,
            SignalLayout = new SignalLayout(1, 1, 3),
            ThroughputMode = ProducerThroughputMode.MaxThroughput
        };

        using var producer = new MockProducer(options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        producer.Start();
        IEventBatch batch = await producer.Reader.ReadAsync(timeout.Token);
        producer.Stop();

        var columnMajor = Assert.IsType<ColumnMajorEventBatch>(batch);
        Assert.Equal(options.BatchSize, columnMajor.Count);
        Assert.Equal(options.SignalLayout.SignalCount, columnMajor.SignalCount);
        Assert.Equal(options.BatchSize * options.SignalLayout.SignalCount, columnMajor.Values.Length);
    }
}
