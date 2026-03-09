# Plot Processing and Rendering Audit

This audit covers the current processing/rendering path for:

- `Histogram`
- `Pseudocolor`
- `SpectralRibbon`

## Current Pipeline

1. `ProcessingEngine.Tick()` recomputes processed data when `DataVersion` changes.
2. `DataStore` holds the latest `ProcessedPlotData` per plot.
3. `RenderingEngine.Tick()` compares object reference and renders when data changed.
4. Plot views assign data to plottables and call `plot.Refresh()`.

## Findings (by impact)

### 1) Full recompute per data version (high)

Every data version change triggers full plot recompute for each active plot.

- `Services/Viewport/ProcessingEngine.cs`
  - fingerprints include `DataVersion`
  - recomputes each plot when version changes

Impact:

- Work scales with retained window size and number of plots.
- No incremental/delta processing yet.

### 2) Spectral compute lock contention on snapshots (high)

`SpectralRibbon` fetches channel snapshots inside `Parallel.For`, and snapshot fetch is lock-based.

- `Services/Viewport/PlotProcessor.cs`
  - `Parallel.For(... channelCount ...)`
  - `_buffer.GetSnapshot(...)` inside parallel body
- `Services/Viewport/DataSource.cs`
  - `GetSnapshot()` uses lock

Impact:

- Parallel workers contend on one lock, reducing effective parallelism.

### 3) Pseudocolor transient allocation pressure (high)

`Pseudocolor` allocates large temporary structures each processing pass:

- thread-local `int[bins,bins]`
- `double[bins*bins]` flat merge buffer
- `double[bins,bins]` output array

File:

- `Services/Viewport/PlotProcessor.cs`

Impact:

- GC/memory bandwidth overhead.
- Processing cost increases with bins and core count.

### 4) Synchronous UI invocation per render target (high)

Rendering dispatch is synchronous (`Dispatcher.Invoke`) for each target in the loop.

- `Services/Viewport/RenderingEngine.cs`

Impact:

- Background engine blocks waiting for UI thread for each plot.
- Under multiple plots, frame scheduling and throughput degrade.

### 5) Forced refresh every render call (medium)

`RenderOnce()` always executes `plot.Refresh()` after updating plottables.

- `Views/PlotViews/PlotView.cs`

Impact:

- Aggressive redraw behavior can over-render under bursty updates.

### 6) Gate recompute is full-rescan per data version (medium)

Gate processing also keys on `DataVersion`, so gates rescan window each change.

- `Services/Viewport/ProcessingEngine.cs`
- `Services/Viewport/Gates/GateProcessor.cs`

Impact:

- Additional compute load, especially for pseudocolor gates.

### 7) Runtime metrics are lifetime averages only (low)

Metrics accumulate from app start and never reset.

- `Services/Viewport/ProcessingEngine.cs`
- `Services/Viewport/RenderingEngine.cs`

Impact:

- Hard to run clean A/B measurements for optimizations.

## How Far Bitmap Preparation Can Go

This section answers: how much work can be moved out of `Render()` for `Pseudocolor` and `SpectralRibbon`.

### ScottPlot Heatmap behavior constraints

From local package docs (`ScottPlot 5.1.57` XML):

- `Heatmap.Intensities` requires `Heatmap.Update()` after data change.
- `Heatmap.CellColors` is generated on `Update()`.
- `Heatmap.Bitmap` is generated at render and cleared on `Update()`.
- `Heatmap.RenderStrategy` can be customized (`Bitmap` / `Rectangles`).

Reference file:

- `C:\\Users\\ishei\\.nuget\\packages\\scottplot\\5.1.57\\lib\\net8.0\\ScottPlot.xml`

### Practical precompute levels

#### Level A (low risk, no plottable change)

Keep ScottPlot `Heatmap`, but minimize work in `Render()`:

- Reuse the same intensity array instances when possible.
- Call `_heatmap.Update()` only when data actually changed.
- Avoid setting properties (`Extent`, `Colormap`, labels/ticks) unless settings changed.
- Keep `Smooth = false`.
- Set `ManualRange` when appropriate to avoid per-update data-range scans.

Expected result:

- Less per-frame heatmap regeneration overhead.
- Lowest code risk.

#### Level B (medium risk, still heatmap)

Precompute more in processing:

- Reuse pooled buffers for normalized/NaN-masked output.
- Keep dimensions stable to avoid plottable recreation.
- Optionally precompute ARGB arrays from intensities (if using custom render strategy path).

Expected result:

- Reduced compute allocations.
- Render still pays `Heatmap.Update()` cost when intensities change.

#### Level C (higher risk, custom image path)

Bypass heatmap update work by preparing final bitmap payload before render:

- Convert processed matrix to final pixel image in processing thread.
- Render prebuilt image in UI via image plottable/custom plottable.

Expected result:

- `Render()` becomes mostly image assignment/draw.
- More control and lower UI-side heatmap costs.

Tradeoffs:

- More custom code.
- Must preserve axis mapping, color bar behavior, and hit-testing semantics manually.

### Maximum realistic extent in current architecture

Without replacing plottable type, the ceiling is:

- "very light `Render()` setup + one `Heatmap.Update()` when data changes"

You cannot fully eliminate heatmap update work while still driving ScottPlot `Heatmap` from changing `Intensities`.
To make render almost no-op, you need Level C (pre-rendered image path).

## Recommended Next Steps (ordered)

1. Move spectral snapshot fetch out of per-channel parallel lock contention (single metadata snapshot + lock-free channel reads).
2. Reuse/pool pseudocolor temporary buffers in `PlotProcessor`.
3. Add metric reset or rolling-window metrics for reliable benchmark comparisons.
4. Introduce optional "render decimation mode" for pseudocolor (`BinCount` reduction).
5. Evaluate Level C image-based path only if prior steps are insufficient.
