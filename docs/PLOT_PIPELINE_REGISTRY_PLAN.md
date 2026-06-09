# Plot Pipeline Registry Refactor Plan

## Goal

Make `ViewportSession` support any plot/item type with any data source and any processing cadence without adding another timer, another engine, or plot-type-specific branches in the session.

The design must not reduce processing or rendering performance.

## Current Problem

The current design is better than the earlier duplicate-engine version, but it is still hardcoded:

```text
ViewportSession
  creates DataSource
  creates OscilloscopeBuffer
  creates PlotProcessor
  creates OscilloscopePlotProcessor
  creates PlotProcessingRouter
  creates ProcessingEngine
```

`PlotProcessingRouter` then knows:

```text
if PlotType.Oscilloscope:
    use OscilloscopePlotProcessor + OscilloscopeBuffer.Version
else:
    use PlotProcessor + CHASM DataVersion
```

That is exactly the shape that will become painful when the next plot type needs another source, another version, or another cadence.

## Target Shape

`ViewportSession` should compose pipelines, not hardcode plot type behavior.

```text
ViewportSession
  owns lifecycle
  owns DataStore
  owns CHASM lifetime
  registers plot pipelines
  starts one ProcessingEngine
  starts one RenderingEngine
```

`ProcessingEngine` should ask a registry:

```text
Which pipeline owns this PlotType?
What is its cadence?
What is its current version?
How do I process it?
```

## Proposed Contracts

### `IPlotPipeline`

```csharp
public interface IPlotPipeline
{
    TimeSpan Cadence { get; }
    long Version { get; }
    ProcessedPlotData? Process(PlotSettings settings, RenderTargetSize targetSize);
    int GetSettingsHash(PlotSettings settings);
}
```

Why `GetSettingsHash(...)` belongs here:

- Each pipeline knows which settings matter.
- `ProcessingEngine` should not know oscilloscope channels, bin counts, axis types, or future plot-specific settings.
- This removes `OscilloscopeChannelHash` from the generic `SettingsFingerprint`.

### `PlotPipelineRegistry`

```csharp
public sealed class PlotPipelineRegistry
{
    public void Register(PlotType plotType, IPlotPipeline pipeline);
    public IPlotPipeline GetRequired(PlotType plotType);
    public TimeSpan FastestCadence { get; }
}
```

Performance rule:

- Build the registry once in `ViewportSession`.
- Store lookup as an array indexed by `PlotType` or a prebuilt dictionary.
- Do not use LINQ or allocate during processing ticks.
- `FastestCadence` is computed during registration, not per tick.

## Initial Pipelines

### `ParameterPlotPipeline`

Owns:

```text
Histogram
Pseudocolor
SpectralRibbon
```

Source:

```text
ChasmDataSource / DataSource
```

Version:

```text
Chasm.DataVersion
```

Cadence:

```text
250 ms default
```

Processor:

```text
PlotProcessor
```

Settings hash includes:

```text
PlotType
BinCount
XFeature
YFeature
XAxisScaleType
YAxisScaleType
MinValue
MaxValue
target pixel width/height should remain in ProcessingEngine fingerprint
```

### `OscilloscopePlotPipeline`

Owns:

```text
Oscilloscope
```

Source:

```text
OscilloscopeBuffer
```

Version:

```text
OscilloscopeBuffer.Version
```

Cadence:

```text
33 ms default
```

Processor:

```text
OscilloscopePlotProcessor
```

Settings hash includes:

```text
OscilloscopeChannelIndices
OscilloscopeChannelCount if still needed by UI
```

## ProcessingEngine After Refactor

`ProcessingEngine` becomes generic:

```text
Tick:
  now = DateTime.UtcNow
  settings = DataStore.GetAllSettings()
  for each plotSettings:
      pipeline = registry.GetRequired(plotSettings.PlotType)
      if not due(plotId, pipeline.Cadence): continue
      fingerprint = plot type + pipeline.Version + pipeline.GetSettingsHash(settings) + target size
      if unchanged: record checked time and continue
      processed = pipeline.Process(settings, targetSize)
      DataStore.SetProcessedData(processed)
  if gates due: process gates
```

The engine no longer needs:

```text
PlotProcessingRouter
_parameterPlotInterval
_oscilloscopePlotInterval
ComputeOscilloscopeChannelHash(...)
GetProcessingInterval(plotType)
```

It only needs:

```text
PlotPipelineRegistry
per-plot last processed time
per-plot fingerprint
gate cadence
```

## Performance Invariants

The refactor must preserve these:

1. One processing timer.
2. One rendering timer.
3. No per-tick pipeline allocation.
4. No per-tick LINQ in the plot loop.
5. O(1) pipeline lookup by `PlotType`.
6. Fingerprint calculation remains cheap value-type work.
7. No extra copies of retained parameter data.
8. No extra copies of analog captures before `OscilloscopePlotProcessor`.
9. Rendering path remains unchanged.
10. Existing profile tests should stay within normal noise.

## Why This Should Not Reduce Performance

Current hot path does this per plot:

```text
if plot type is oscilloscope
compute hardcoded fingerprint
call router
router branches by plot type
process
```

Target hot path does this per plot:

```text
array/dictionary lookup by PlotType
ask pipeline for cadence/version/settings hash
process
```

That replaces branching with one lookup and virtual/interface calls. For the current number of plots, this is negligible compared with:

```text
histogram binning
pseudocolor binning/render pixel generation
spectral ribbon processing
ScottPlot/WPF rendering
```

To make this defensible, use array lookup if possible:

```csharp
private readonly IPlotPipeline?[] _pipelinesByPlotType;
```

Then lookup is:

```csharp
var pipeline = _pipelinesByPlotType[(int)plotType];
```

No dictionary hashing is needed.

## Implementation Steps

1. Add `IPlotPipeline`.
2. Add `PlotPipelineRegistry`.
3. Add `ParameterPlotPipeline`.
4. Add `OscilloscopePlotPipeline`.
5. Update `ProcessingEngine` constructor to accept `PlotPipelineRegistry`.
6. Move cadence/version/settings-hash logic out of `ProcessingEngine` and into pipelines.
7. Remove `PlotProcessingRouter`.
8. Update `ViewportSession` to register:

```text
Histogram -> ParameterPlotPipeline
Pseudocolor -> ParameterPlotPipeline
SpectralRibbon -> ParameterPlotPipeline
Oscilloscope -> OscilloscopePlotPipeline
```

9. Keep `RenderingEngine` unchanged.
10. Update tests.
11. Run focused processing tests and profile tests.

## Tests To Add Or Update

### Registry tests

- registering a plot type returns the pipeline
- duplicate registration fails clearly
- unregistered plot type fails clearly
- fastest cadence is the minimum registered cadence

### Processing engine tests

- parameter plots respect parameter cadence
- oscilloscope respects oscilloscope cadence
- oscilloscope version changes trigger processing
- parameter data version changes trigger parameter processing
- pipeline-specific settings hash triggers reprocessing
- removing a plot clears stale fingerprint state

### Performance/profile gates

Run:

```powershell
dotnet test .\Worksheet.Tests\Worksheet.Tests.csproj --no-restore --filter "FullyQualifiedName~ProcessingEngineOscilloscopeTests|FullyQualifiedName~Oscilloscope|FullyQualifiedName~DataSourceTests"
```

Run full suite:

```powershell
dotnet test .\Worksheet.Tests\Worksheet.Tests.csproj --no-restore
```

Run profile tests before and after implementation and compare output:

```powershell
dotnet test .\Worksheet.Tests\Worksheet.Tests.csproj --no-restore --filter "Category=Profile" --logger "console;verbosity=detailed"
```

Acceptable performance result:

- no large regression in processing profile timings
- no new allocation-heavy path in processing loop
- rendering profile unchanged except normal run noise

## Migration Risk

Low-to-medium.

Behavioral risk is mostly in fingerprinting. If a pipeline forgets to include a setting in `GetSettingsHash(...)`, a settings change may not reprocess. That is why settings hash tests matter.

Performance risk is low if registry lookup is array-based and registrations are immutable after construction.

## Anti-Goals

- Do not add plugin loading.
- Do not make a DI container.
- Do not add multiple processing timers.
- Do not split `RenderingEngine`.
- Do not make plot views own data source lookup.
- Do not make `ViewportSession` branch by plot type.

## Decision

Move from:

```text
ProcessingEngine + PlotProcessingRouter with hardcoded stream branches
```

to:

```text
ProcessingEngine + PlotPipelineRegistry + registered IPlotPipeline implementations
```

This supports any plot type with any source and cadence while preserving the single processing timer and keeping the render path unchanged.
