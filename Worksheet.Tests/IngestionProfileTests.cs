using System;
using System.Diagnostics;
using Worksheet.Models;
using Worksheet.Services;
using Xunit;
using Xunit.Abstractions;

namespace Worksheet.Tests;

public sealed class IngestionProfileTests
{
    private const int BatchCount = 20;
    private const int BatchSize = 1_000;
    private readonly ITestOutputHelper _output;

    public IngestionProfileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileIngestionStorageBandwidth(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);
        var batch = CreateBatch(layout.SignalCount, BatchSize, offset: 0);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
                source.AppendBatch(batch, BatchSize);
        });

        Assert.Equal(BatchCount * BatchSize, source.TotalEventsIngested);
        WriteThroughput($"Append prebuilt {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    [Theory]
    [Trait("Category", "Profile")]
    [InlineData(1, 1, 60)]
    [InlineData(6, 9, 50)]
    [InlineData(6, 9, 60)]
    public void ProfileIngestionGenerateAndStoreThroughput(int lasers, int features, int channels)
    {
        var layout = new SignalLayout(lasers, features, channels);
        var source = new DataSource(layout, windowCapacity: BatchCount * BatchSize);

        var elapsed = Measure(() =>
        {
            for (int i = 0; i < BatchCount; i++)
            {
                var batch = CreateBatch(layout.SignalCount, BatchSize, offset: i * BatchSize);
                source.AppendBatch(batch, BatchSize);
            }
        });

        Assert.Equal(BatchCount * BatchSize, source.TotalEventsIngested);
        WriteThroughput($"Generate+append {lasers}x{features}x{channels}", layout, BatchCount * BatchSize, elapsed);
    }

    private static TimeSpan Measure(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private void WriteThroughput(string label, SignalLayout layout, int eventCount, TimeSpan elapsed)
    {
        double seconds = Math.Max(elapsed.TotalSeconds, 1e-9);
        double eventsPerSecond = eventCount / seconds;
        double bytesPerSecond = eventsPerSecond * layout.SignalCount * sizeof(double);
        double mebibytesPerSecond = bytesPerSecond / (1024 * 1024);
        double rawPayloadMiB = (double)eventCount * layout.SignalCount * sizeof(double) / (1024 * 1024);

        string line = $"{label}: {elapsed.TotalMilliseconds:F2} ms, {eventsPerSecond:F0} events/sec, {mebibytesPerSecond:F1} MiB/sec raw, {rawPayloadMiB:F1} MiB payload";
        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    private static double[][] CreateBatch(int signalCount, int count, int offset)
    {
        var signals = new double[signalCount][];
        for (int s = 0; s < signals.Length; s++)
        {
            var values = new double[count];
            for (int e = 0; e < values.Length; e++)
                values[e] = 1 + (((offset + e + 1) * (s + 3) * 7919) % 100_000_000);

            signals[s] = values;
        }

        return signals;
    }
}
