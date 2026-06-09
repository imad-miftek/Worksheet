# Worksheet Architecture Audit

## Scope

This audit covers the current split solution:

```text
Worksheet.App
Worksheet.Core
Worksheet.Tests
```

The focus is correctness, ownership boundaries, project structure, and the CHASM ingestion/viewport processing architecture.

## Current Ownership Map

```text
Worksheet.App
  WPF views
  ScottPlot view setup
  plot containers, drag/resize/selection
  ViewportSession orchestration
  RenderingEngine

Worksheet.Core
  shared models
  channel metadata and feature selection
  CHASM ingestion contracts and mock producer
  rolling raw-event DataSource
  plot/gate processing

Worksheet.Tests
  Core behavior tests
  selected App rendering/view tests
  profile tests for ingestion, processing, and rendering
```

The project split is directionally correct: app code depends on core, while core does not depend on WPF.

## CHASM Ingestion

Current ingestion path:

```text
IProducer
  -> Channel<IEventBatch>
  -> ChasmConsumer
  -> ChasmDataSource
  -> DataSource
```

For DAQ event-object batches:

```text
IReadOnlyList<Event>
  -> IEventIngestionPort.PublishEvents(...)
  -> EventBatchConverter<Event>
  -> ColumnMajorEventBatch
  -> Channel<IEventBatch>
```

For DAQ flat-buffer batches:

```text
double[] columnMajorValues + eventCount
  -> IEventIngestionPort.PublishColumnMajor(...)
  -> ColumnMajorEventBatch
  -> Channel<IEventBatch>
```

This is the right boundary. DAQ-specific event objects should be normalized before entering the main CHASM queue. `EventProducer` is the production-shaped push boundary for both object batches and already-flat buffers; `DataSource` should continue to receive known CHASM batch shapes, not arbitrary DAQ SDK objects. `PublishColumnMajor(...)` is the no-copy fast path, so callers must treat the published buffer as transferred to CHASM.

## Correctness Fixes Applied

### Event object shape validation

`EventBatchConverter<TEvent>` now validates `IEventSignalValues.SignalCount` against the configured `SignalLayout` before conversion. This prevents short or wrong-shaped `Event` batches from failing later inside the conversion loop or inside `Parallel.For`.

Evidence:

- `Worksheet.Core/Services/CHASM/Event.cs`
- `Worksheet.Core/Services/CHASM/IEventIngestionPort.cs`
- `Worksheet.Core/Services/CHASM/EventProducer.cs`
- `Worksheet.Core/Services/CHASM/EventBatchConverter.cs`
- `Worksheet.Tests/EventProducerTests.cs`
- `Worksheet.Tests/EventBatchConverterTests.cs`

### Explicit channel count naming

`ChannelSettings.ChannelCount` previously meant source slots, even though most call sites used it as a loaded/connected-channel check. It now exposes:

```text
SourceChannelCount
ConnectedChannelCount
ChannelCount -> ConnectedChannelCount
```

This keeps old call sites working while making the compact event-column count explicit.

Evidence:

- `Worksheet.Core/Services/ChannelSettings.cs`
- `Worksheet.Core/Services/FeatureSelectionStrategy.cs`
- `Worksheet.Tests/ChannelSettingsTests.cs`

### Core package boundary cleanup

`Worksheet.Core` no longer references the unused `ScottPlot` package. ScottPlot remains owned by `Worksheet.App`, where plotting views are created and rendered.

Evidence:

- `Worksheet.Core/Worksheet.Core.csproj`
- `Worksheet.App/Worksheet.App.csproj`

### CHASM lifecycle cleanup

`Chasm.StopStreaming()` and `MockProducer.Stop()` now cancel and observe their background tasks with a bounded wait. `ViewportSession.Dispose()` also disposes the CHASM graph, so producer/consumer resources are tied to the session lifecycle.

Evidence:

- `Worksheet.Core/Services/CHASM/Chasm.cs`
- `Worksheet.Core/Services/CHASM/MockProducer.cs`
- `Worksheet.App/Services/Viewport/ViewportSession.cs`
- `Worksheet.Tests/ChasmLifecycleTests.cs`

### Snapshot access contract tightened

`ChannelWindowSnapshot` and `MultiChannelWindowSnapshot` now document that they are live views over `DataSource` backing arrays, and they reject out-of-range sequence, logical-index, and channel access. This does not make snapshots immutable, but it prevents silent ring-index aliasing if future code asks for a sequence outside the captured logical window.

`DataSource` also exposes copied snapshot APIs for paths that need a stable, contiguous view:

```text
GetSnapshotCopy(signalIndex)
GetSnapshotCopy(params signalIndices)
```

The copied APIs keep the existing logical event order, return snapshots with `StartIndex = 0`, and remain stable after later appends. They should not replace the hot live-snapshot path by default. A profile run on this machine showed one- and two-signal copies are cheap, while repeated 42-signal spectral copies are a meaningful cost:

```text
1x1x51 histogram copy, 1 selected signal:      1.60 ms for 20 snapshots
1x1x51 pseudocolor copy, 2 selected signals:   0.76 ms for 20 snapshots
1x1x51 spectral copy, 42 selected signals:    56.95 ms for 20 snapshots
6x9x60 pseudocolor copy, 2 selected signals:   0.63 ms for 20 snapshots
6x9x60 spectral copy, 42 selected signals:    59.40 ms for 20 snapshots
```

Policy: keep processors on live snapshots unless a path specifically needs isolation from concurrent appends or needs contiguous copied data at a thread boundary.

Evidence:

- `Worksheet.Core/Services/Viewport/ChannelWindowSnapshot.cs`
- `Worksheet.Core/Services/Viewport/MultiChannelWindowSnapshot.cs`
- `Worksheet.Core/Services/Viewport/DataSource.cs`
- `Worksheet.Tests/DataSourceTests.cs`
- `Worksheet.Tests/IngestionProfileTests.cs`

## Main Remaining Risks

### 1. DataSource snapshots are live views, not immutable snapshots

`DataSource.GetSnapshot(...)` returns references to the internal ring-buffer arrays. This is fast, but processing can read those arrays after `DataSource` releases its lock while ingestion writes new data.

Impact:

- Possible inconsistent reads under high ingestion pressure.
- The snapshot metadata is stable, but the backing values can change while being processed.

This is currently a performance tradeoff, not an immediate functional failure observed in tests.

The access contract is now safer: out-of-window sequence reads throw instead of silently mapping to the wrong physical ring index. The remaining risk is concurrent mutation of the backing arrays during processing.

Potential fixes:

- Copy selected snapshot columns before processing.
- Add read/write locking around snapshot consumption.
- Use double-buffered retained storage.
- Keep live snapshots but document them explicitly as weak snapshots and accept minor display inconsistency.

### 2. `FeatureSelectionStrategy` is static global state

Channel metadata is loaded into static state and accessed from instance methods. This is simple for the current app, but it makes independent sessions and tests share channel configuration.

Impact:

- Multiple `ViewportSession` instances cannot naturally use different channel maps.
- Tests that load channel settings affect later tests unless carefully isolated.

Potential fix:

- Make `FeatureSelectionStrategy` instance-owned and inject `ChannelSettings`.
- Keep a static app default only at startup.

### 3. App and Core namespaces overlap

Some `Worksheet.App` classes use the same broad namespaces as Core:

```text
Worksheet.Services
Worksheet.Models
```

Examples include `ViewportSession`, `RenderingEngine`, `PlotItem`, and `PlotContainer`. The project references still enforce the correct assembly direction, but namespace overlap makes ownership less obvious when reading code.

Potential fix:

- Move app-owned services under `Worksheet.App.Services` or `Worksheet.Services.App`.
- Move app-owned view models/container models under `Worksheet.App.Models` or `Worksheet.Models.Worksheet`.
- Keep shared domain/runtime models in `Worksheet.Core`.

This is a structure cleanup, not an urgent correctness bug.

### 4. Test project references the WPF app

`Worksheet.Tests` references both `Worksheet.Core` and `Worksheet.App`. This is currently useful for WPF rendering/DPI tests, but it means the test project is not a pure Core test suite.

Potential split later:

```text
Worksheet.Core.Tests
Worksheet.App.Tests
```

That is not urgent, but it will make ownership cleaner as the app grows.

### 5. Documentation drift needs active maintenance

The CHASM and plot-pipeline docs had stale default channel counts, older throughput numbers, and older rendering assumptions. This audit refreshed the obvious CHASM, README, and plot-pipeline drift. Future architecture changes should update the docs in the same change set.

## Assessment

The current architecture is workable and moving in the right direction:

- Project split is basically correct.
- CHASM ingestion boundaries are understandable.
- `ColumnMajorEventBatch` is the correct fast normalized batch shape.
- `EventBatchConverter<TEvent>` is the event-processing step, but scoped narrowly enough to avoid becoming a vague ingestion god class.
- The main scalability risk is not the class layout; it is the retained snapshot concurrency/performance tradeoff and eventual DAQ integration boundary.

Do not add a broad `EventProcessor` class right now. If ingestion grows, prefer a specifically named boundary object:

```text
DaqEventProducer
EventBatchConverter<TEvent>
EventIngestionPipeline
```

Each name should describe a real responsibility rather than becoming a catch-all processor.
