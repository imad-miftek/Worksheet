using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Worksheet.Models;
using Worksheet.Models.Data;
using Worksheet.Services;
using Worksheet.Views.PlotViews;
using Worksheet.Views.Support;
using Worksheet.Views.Surfaces;
using Xunit;
using Xunit.Abstractions;

namespace Worksheet.Tests;

public sealed class RenderingProfileTests
{
    private const int ChannelCount = 60;
    private const int EventCount = 50_000;
    private const int Iterations = 25;

    private readonly ITestOutputHelper _output;

    public RenderingProfileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Profile")]
    public void ProfilePlotViewRendering()
    {
        LoadChannelConfiguration();

        var histogram = CreateProcessedData(CreateHistogramSettings());
        var pseudocolor = CreateProcessedData(CreatePseudocolorSettings());
        var spectralRibbon = CreateProcessedData(CreateSpectralRibbonSettings());

        RunOnStaThread(() =>
        {
            ProfileRender(PlotType.Histogram, histogram, "Histogram");
            ProfileRender(PlotType.Pseudocolor, pseudocolor, "Pseudocolor");
            ProfileRender(PlotType.SpectralRibbon, spectralRibbon, "SpectralRibbon");
        });
    }

    private void ProfileRender(PlotType plotType, ProcessedPlotData data, string label)
    {
        var factory = new PlotFactory();
        var plot = factory.CreatePlot(520, 360, plotType, out PlotView view);
        var surface = new DynamicBitmap();
        surface.SetDataRect(new Rect(0, 0, 420, 260));
        view.AttachBitmapSurface(plot, surface);

        view.Render(plot, data);

        var elapsed = Measure(() => view.Render(plot, data), Iterations);
        double averageMs = elapsed.TotalMilliseconds / Iterations;
        double rendersPerSecond = 1000.0 / Math.Max(averageMs, 1e-9);
        string line = $"{label} render: {averageMs:F2} ms avg over {Iterations} iterations, {rendersPerSecond:F0} renders/sec";

        _output.WriteLine(line);
        Console.WriteLine(line);

        Assert.True(averageMs >= 0);
    }

    private static TimeSpan Measure(Action action, int iterations)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            action();

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static ProcessedPlotData CreateProcessedData(PlotSettings settings)
    {
        var source = new DataSource(windowCapacity: EventCount);
        source.AppendBatch(CreateBatch(EventCount), EventCount);

        var processor = new PlotProcessor(new ChasmDataSource(source));
        var processed = processor.Process(settings);

        Assert.NotNull(processed);
        return processed;
    }

    private static PlotSettings CreateHistogramSettings() =>
        new()
        {
            PlotType = PlotType.Histogram,
            BinCount = 256,
            XFeature = 0,
            XAxisScaleType = AxisScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static PlotSettings CreatePseudocolorSettings() =>
        new()
        {
            PlotType = PlotType.Pseudocolor,
            BinCount = 256,
            XFeature = 0,
            YFeature = 1,
            XAxisScaleType = AxisScaleType.Logarithmic,
            YAxisScaleType = AxisScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static PlotSettings CreateSpectralRibbonSettings() =>
        new()
        {
            PlotType = PlotType.SpectralRibbon,
            BinCount = 256,
            YAxisScaleType = AxisScaleType.Logarithmic,
            MinValue = 1,
            MaxValue = 100_000_000,
        };

    private static double[][] CreateBatch(int count)
    {
        var channels = new double[ChannelCount][];
        for (int c = 0; c < channels.Length; c++)
        {
            var values = new double[count];
            for (int e = 0; e < values.Length; e++)
                values[e] = 1 + (((e + 1) * (c + 3) * 7919) % 100_000_000);

            channels[c] = values;
        }

        return channels;
    }

    private static void LoadChannelConfiguration()
    {
        string channelConfigPath = Path.Combine(FindRepoRoot(), "Worksheet.App", "channels.json");

        Assert.True(File.Exists(channelConfigPath), $"Channel config not found: {channelConfigPath}");
        FeatureSelectionStrategy.LoadChannelSettings(channelConfigPath);
    }

    private static string FindRepoRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory, Path.GetDirectoryName(sourceFilePath) })
        {
            if (string.IsNullOrWhiteSpace(startPath))
                continue;

            var directory = new DirectoryInfo(startPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Worksheet.sln")))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not find repo root containing Worksheet.sln.");
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        exception?.Throw();
    }
}
