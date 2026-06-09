using System;
using System.Threading.Tasks;
using Worksheet.Models;
using Worksheet.Services;
using Xunit;

namespace Worksheet.Tests;

public sealed class EventProducerTests
{
    [Fact]
    public void PublishReturnsZeroWhenProducerIsStopped()
    {
        var producer = new EventProducer(new SignalLayout(1, 1, 2), channelCapacityBatches: 2);

        int written = producer.Publish([new Event([10, 20])]);

        Assert.Equal(0, written);
        Assert.False(producer.Reader.TryRead(out _));
    }

    [Fact]
    public void PublishConvertsEventsToColumnMajorBatches()
    {
        var producer = new EventProducer(
            new SignalLayout(1, 1, 2),
            channelCapacityBatches: 4,
            maxBatchSize: 2);

        producer.Start();

        int written = producer.Publish(
            [
                new Event([10, 20]),
                new Event([11, 21]),
                new Event([12, 22]),
            ]);

        Assert.Equal(2, written);
        Assert.True(producer.Reader.TryRead(out var firstBatch));
        Assert.True(producer.Reader.TryRead(out var secondBatch));

        var first = Assert.IsType<ColumnMajorEventBatch>(firstBatch);
        var second = Assert.IsType<ColumnMajorEventBatch>(secondBatch);
        Assert.Equal([10, 11, 20, 21], first.Values);
        Assert.Equal([12, 22], second.Values);
    }

    [Fact]
    public async Task ChasmCapturesPublishedEventsEndToEnd()
    {
        var layout = new SignalLayout(1, 1, 3);
        var source = new DataSource(layout, windowCapacity: 10);
        var chasmSource = new ChasmDataSource(source);
        var producer = new EventProducer(layout, channelCapacityBatches: 4, maxBatchSize: 10);
        var consumer = new ChasmConsumer(producer.Reader, chasmSource);
        using var chasm = new Chasm(producer, consumer, chasmSource);

        chasm.StartStreaming();
        int written = producer.Publish(
            [
                new Event([10, 20, 30]),
                new Event([11, 21, 31]),
                new Event([12, 22, 32]),
            ]);

        await WaitUntilAsync(() => source.TotalEventsIngested == 3);
        chasm.StopStreaming();

        Assert.Equal(1, written);
        Assert.Equal(3, source.TotalEventsIngested);

        var snapshot = source.GetSnapshot(layout.ToIndex(0, 0, 1));
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(20, snapshot.Values[snapshot.PhysicalIndexForSequence(0)]);
        Assert.Equal(21, snapshot.Values[snapshot.PhysicalIndexForSequence(1)]);
        Assert.Equal(22, snapshot.Values[snapshot.PhysicalIndexForSequence(2)]);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("Condition was not reached before timeout.");

            await Task.Delay(10);
        }
    }
}
