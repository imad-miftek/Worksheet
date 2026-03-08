# Low-latency interactive visualization for many simultaneous plots in C# using ScottPlot

## Executive summary

Rendering “many plots at once” (e.g., 60) is usually limited less by a single line-drawing routine and more by **end-to-end frame cost**: data ingestion → decimation/LOD → layout/ticks → drawing → UI presentation → garbage collection and synchronization. In .NET desktop UI, **the UI thread and framework paint/composition model** often dominate latency once you scale to dozens of plot surfaces. This is consistent with (a) WPF’s architecture (retained-mode scene caching and a separate composition system) citeturn1search1turn1search13 and (b) real-world ScottPlot reports where multiple plot controls refreshed from multiple threads show “stacking” refresh times because painting is serialized through UI constraints. citeturn16view0turn10search9

A synthesis of academic and industry literature yields three high-leverage principles for low-latency multi-plot dashboards:

- **Match rendering work to screen resolution**: time-series downsampling/aggregation should aim for “pixel-meaningful” output sizes. Academic work such as **M4** (pixel-aware aggregation) targets *loss-free plots at high reduction rates* by selecting extrema aligned with rasterization constraints. citeturn9search0turn9search24 Evaluation work submitted to IEEE VIS emphasizes computationally feasible “value-preserving” algorithms (e.g., MinMax, LTTB, M4) and highlights **visual stability** challenges under streaming and small interaction changes. citeturn7view0  
- **Reduce per-plot overhead, not only per-point overhead**: when you have 60 plots, per-plot fixed costs (layout computation, tick generation, autoscaling, event dispatch) can dominate. ScottPlot 5’s `RenderManager` runs a multi-stage pipeline including layout and tick regeneration on every render unless you constrain it, and it may re-render up to 5 times if axis limits change within a render pass. citeturn5view0  
- **Choose a rendering architecture appropriate for your latency target**: retained-mode UI frameworks can reduce redraw coordination work, but immediate-mode rendering paths (Direct2D, OpenGL, “GL views”) can provide more direct control and batching. WPF is explicitly retained-mode; Win32/GDI-style drawing and Direct2D are immediate-mode. citeturn1search1turn3search1turn3search5

For ScottPlot specifically, the most actionable path to low-latency updates for ~60 plots is:

- Prefer **one plot control with a Multiplot layout** (grid of subplots) over 60 independent plot controls when possible. citeturn6search3turn6search19turn6search15  
- Use **signal-style plottables** and **fixed-length arrays** where possible; ScottPlot documents this as the most performant approach for changing data. citeturn6search24turn6search21turn6search5  
- Turn off or stabilize expensive dynamic behaviors (continuous autoscale, frequent axis changes) to avoid repeated renders and tick/layout churn. citeturn5view0  
- Decimate aggressively (MinMax/M4/LTTB) per subplot based on pixel width and interaction needs; this is strongly supported by both M4’s goals and VIS-oriented algorithm assessments. citeturn9search0turn7view0turn0search2  
- Drive rendering from **one UI-timed loop** (coalesced invalidation) rather than spawning per-plot refresh threads. citeturn16view0turn6search24

Unspecified details that materially affect design (and are therefore treated as variables in this report): target FPS/latency, per-plot point counts, data rates, interaction intensity (pan/zoom), OS version, .NET version, CPU/GPU model, and display resolution.

## Evidence from academic and industry literature

### Latency targets for interactive feel

Classic usability guidance commonly used in systems design sets qualitative thresholds around **100 ms** for “direct manipulation feel,” **1 s** for uninterrupted flow, and **10 s** for attention retention. citeturn9search3 While these numbers are not visualization-specific, they are useful for specifying *latency budgets* for interactions like zoom box, cursor readout, and crosshair tracking across many plots. citeturn9search3

### Downsampling and aggregation for time-series visualization

**Pixel-aware / visualization-aware aggregation (M4).**  
entity["people","Uwe Jugel","m4 time-series paper"] and coauthors introduce **M4**, motivated by “hard latency requirements and high ingestion rates of interactive visualizations,” and propose a visualization-oriented aggregation that can provide **error-free visualizations** for line charts at high reduction rates by respecting rasterization constraints. citeturn9search0turn9search12 A later demo-oriented paper emphasizes that M4 transmits a bounded number of values per pixel column and is “loss-free” relative to plotting the full-resolution line (in the pixel sense). citeturn9search24

**Value-preserving selection algorithms and stability under interaction.**  
entity["people","Jonas Van Der Donckt","time-series downsampling eval"] and collaborators propose a metrics-based methodology evaluating **representativeness** and **visual stability** of point-selection downsampling methods, focusing on practical algorithms used in practice (EveryNth, MinMax, LTTB, M4) and explicitly noting stability issues during **streaming, panning, and zooming**. citeturn7view0 This is directly relevant to “60 plots,” because multi-plot UIs amplify user-visible instability (sparkle/flicker) when each subplot resamples slightly differently under small axis changes. citeturn7view0

**LTTB and “largest triangle” sampling.**  
entity["people","Sveinn Steinarsson","lttb thesis"]’s thesis is a widely cited source for **Largest-Triangle-Three-Buckets (LTTB)** and related techniques for downsampling time series for visual representation. citeturn0search2 Recent literature continues to refine/assess large-triangle sampling variants in time-series visualization. citeturn0search10turn7view0

### Industry performance guidance relevant to many plots

Commercial high-performance charting vendors for WPF emphasize **render-loop control** (e.g., decoupling rendering from WPF’s composition pass) and **DirectX-based engines** to increase performance and reduce lag. citeturn8search17turn0search18 While not directly prescriptive for ScottPlot, they reinforce the general pattern: to scale well, plotting systems often (a) constrain the render loop and (b) use GPU/back-end-optimized pipelines. citeturn8search17turn0search18

On the open-source side, ScottPlot issue reports from 2026 describe scaling problems when refreshing many plot controls from multiple threads, with per-control refresh time increasing as more charts run, suggesting blocking/synchronization on shared UI resources. citeturn16view0 This is a concrete “practical literature” datapoint for the 60-plot requirement. citeturn16view0

## Rendering architectures and performance mechanisms

### Retained-mode vs immediate-mode in .NET desktop rendering

**WPF (retained mode).** WPF caches the visual/drawing instruction tree; this retained representation allows re-painting “at high refresh rates” without blocking on user callbacks, which can help responsiveness when the scene is largely static. citeturn1search1turn1search13

**Win32/GDI-style drawing and Direct2D (immediate mode).** By contrast, Win32/GDI and Direct2D follow immediate-mode principles (draw commands issued each frame). Microsoft describes Direct2D as a **hardware-accelerated, immediate-mode 2D API**. citeturn3search1turn3search5 Immediate mode can be advantageous when you want explicit control over batching, dirty regions, and frame pacing for dashboards. citeturn3search1

A practical consequence for “60 plots” is that retained mode can reduce coordination overhead for UI elements, but plotting libraries that redraw full bitmaps every update may still behave like immediate-mode renderers from a cost perspective (i.e., they repaint every time). citeturn1search1turn6search24

### Event loops and frame pacing

In WPF, `CompositionTarget.Rendering` can be used as a per-frame callback for custom animations and frame-driven updates. citeturn8search1turn8search21 This provides a natural “render tick” aligned with WPF’s rendering process, but it can also introduce synchronization overhead between UI and render threads if abused, and it may fire more than once per frame in some scenarios (practical reports). citeturn8search5turn8search9

In WinForms, rendering is driven by WM_PAINT (invalidations) and the message loop; `Invalidate()` schedules a paint message. citeturn8search36turn8search12 For 60 plots, the key is to avoid triggering more paints than the monitor can present (wasted rendering), a point echoed in ScottPlot community guidance about not rendering faster than displayable. citeturn10search9turn16view0

### Double buffering and tearing/flicker mitigation

In WinForms, double buffering renders into a memory buffer then copies to the screen to reduce flicker. citeturn8search4turn1search8 The `Control.DoubleBuffered` property and `ControlStyles` flags formalize this. citeturn1search0turn1search29

In WPF image-based rendering pipelines (e.g., custom rasterization to a bitmap), `WriteableBitmap` supports a model where the UI thread writes to a back buffer and the render thread reads from a front buffer; changes are tracked via dirty rects, and `Unlock()` triggers a render pass if dirty regions were registered. citeturn8search15turn8search3turn3search0

### CPU vs GPU rendering

GPU acceleration is not “automatic” for all 2D drawing in .NET; it is architecture-dependent:

- WPF uses GPU acceleration when available but can be forced into software mode via `RenderOptions.ProcessRenderMode`. citeturn8search2turn9search30  
- SkiaSharp provides GPU-capable controls such as `SKGLControl` described as a hardware-accelerated control. citeturn1search2turn1search6  
- Direct2D is explicitly designed as a hardware-accelerated 2D API. citeturn3search1

For multi-plot dashboards, GPU acceleration helps primarily when you can **batch** work (many primitives per draw call) and avoid CPU-side per-plot overhead (layout/ticks/text measurement). Otherwise, GPU may not save you from UI-thread serialization and repeated rasterization. citeturn1search1turn5view0

### Batching, caching, and “fixed costs” per plot

When you render 60 independent plot controls, you often pay 60× the fixed overhead: axis ticks, layout boxes, labels, legends, grids, hit-testing structures, etc. ScottPlot 5’s render pipeline shows multiple actions per render (clear, autoscale, layout, tick regeneration, grids, plottables, legends, etc.). citeturn5view0 This suggests two batching strategies:

1. **Batch in the UI layer**: render many subplots within one control (single render pipeline execution for the whole figure, with shared layout), e.g., ScottPlot’s Multiplot. citeturn6search3turn6search19  
2. **Batch in the render back-end**: GPU-based scatter/line rendering where many series are drawn in one pass (common in specialized chart engines), but ScottPlot’s GPU path is more limited/optional (see below). citeturn13view0turn10search6

## Downsampling, level-of-detail, and streaming pipelines

### Why decimation is mandatory for low latency

A monitor may be 60–240 Hz, but each plot’s pixel width may only be a few hundred pixels in a dense grid. Plotting more than O(pixels) points per subplot often wastes time because multiple points map to the same column. M4 formalizes this observation by focusing on rasterization and pixel columns for “error-free” plots in the pixel sense. citeturn9search0turn9search24

VIS-oriented evaluations similarly highlight that practical interactive downsampling methods must be O(N) time, low-memory, and often parallelizable for throughput; they compare EveryNth, MinMax, M4, and LTTB under such constraints. citeturn7view0

### Comparison of common decimation algorithms

The table below summarizes algorithms frequently used for interactive time-series line charts.

| Method | Core idea | Time / memory (practical) | Visual properties and caveats |
|---|---|---|---|
| EveryNth | sample every k-th point | O(n_out) time, O(1) extra memory (as described in evaluation literature) citeturn7view0 | Fast but can alias away spikes and extrema; can be visually misleading under pan/zoom. citeturn7view0 |
| MinMax | per bucket, keep min and max | O(N) time, O(1) memory; parallelizable citeturn7view0 | Preserves extrema but not necessarily ordering patterns; can repeat minima/maxima and miss alternations. citeturn7view0 |
| M4 | per bucket/pixel column keep first, last, min, max | Proposed as pixel-perfect / visualization-oriented aggregation for error-free plots citeturn9search0turn9search24 | Very strong at preserving what the rasterized line would show; produces more points per bucket than MinMax/LTTB. citeturn7view0turn9search0 |
| LTTB | pick point maximizing triangle area between buckets | O(N) time, O(1) memory; sequential (not parallelizable in standard form) citeturn7view0turn0search2 | Good shape preservation; computationally heavier than MinMax/M4; sequential dependency can matter if you decimate 60 series every frame. citeturn7view0turn0search2 |
| RDP / Visvalingam-Whyatt | line simplification by geometric error/area | Often O(N log N) + extra structures; less suitable at scale for interactive streaming citeturn7view0 | High quality for simplification but can be too slow / too stateful for real-time dashboards. citeturn7view0 |

### Practical hyperparameters for 60 plots

Because the user did not specify per-plot pixel sizes or point counts, here are options commonly used to align work to screen constraints:

| Parameter | Default starting point | Rationale |
|---|---:|---|
| Output points per plot per frame | ≈ 2–4 × plot pixel width | Matches “pixel-columns matter” intuition; M4 effectively targets bounded values per pixel column. citeturn9search24turn9search0 |
| Bucket count for MinMax/LTTB | n_out = plotWidthPx × (2 to 4) | Provides headroom for anti-aliased slopes while limiting overdraw. citeturn7view0turn0search2 |
| Re-decimation frequency | on view change (pan/zoom) or on data append batch | Helps stability and reduces wasted compute; stability is a known concern in evaluation literature. citeturn7view0 |
| Streaming buffer length | fixed N per plot (ring buffer) | Enables in-place updates; ScottPlot recommends fixed-length arrays for live data performance. citeturn6search24turn15search1 |

### Streaming architecture patterns

A robust pattern for interactive time-series dashboards is **producer-consumer with batching**:

- Producers ingest data at high rate (I/O thread, network callback, hardware acquisition).
- Consumers batch updates (e.g., every 16–50 ms) and update plot data structures, then request a UI redraw.

In .NET, `System.Threading.Channels` provides “synchronization data structures for passing data between producers and consumers asynchronously.” citeturn15search1turn15search25

A high-level dataflow is:

```mermaid
flowchart LR
  S[Data source(s)\n(sockets, files, sensors)] --> P[Producer tasks]
  P -->|Channel<T>| C[Consumer/batcher]
  C --> D[Decimation + LOD\n(per plot)]
  D --> U[UI-thread apply\n(update arrays, set limits)]
  U --> R[Render request\n(coalesced invalidation)]
```

This architecture is particularly important for “60 plots” because it prevents pathological behavior where each plot schedules its own redraw independently. The ScottPlot issue report showing increasing refresh time with many charts and per-chart threads is an example of what to avoid. citeturn16view0turn10search9

## C# and ScottPlot-specific guidance for low-latency multi-plot rendering

### ScottPlot 5 rendering backend and customization hooks

ScottPlot 5 uses **SkiaSharp** for improved performance compared to earlier versions that used `System.Drawing.Common`. citeturn0search0turn0search23 This change is aligned with Microsoft’s platform guidance: `System.Drawing.Common` became Windows-only in .NET 6+ and requires migration for cross-platform apps. citeturn15search0turn15search8

ScottPlot 5 explicitly exposes a **customizable rendering system**: developers can manipulate `Plot.RenderManager.RenderActions` to remove default actions or add new ones. citeturn6search8turn5view0 Internally, `RenderManager` executes a sequence of render actions including autoscale, layout calculation, tick regeneration, grid rendering, plottable rendering, and more. citeturn5view0 It also provides:

- `RenderActions` list (modifiable) citeturn5view0  
- `Remove<T>()` for removing actions of a given type citeturn5view0  
- a `PreRenderLock` event and guidance to lock `Plot.Sync` for safe mutation citeturn5view0  
- up to 5 internal render attempts if axis limits change during rendering (important when autoscaling is enabled in live plots). citeturn5view0  

This implies a performance lever: **stabilize axis limits and ticks** so you render once per frame, rather than triggering multi-pass renders and repeated tick regeneration. citeturn5view0

### Prefer Multiplot over 60 separate controls when possible

ScottPlot supports multi-plot figures in two ways: (1) the Multiplot system within a single plot control, or (2) placing multiple plot controls and attempting shared layouts/axes. citeturn6search3turn6search15 For a 60-plot requirement, Multiplot often reduces UI overhead because the UI must paint fewer controls and you can share layout constraints more effectively. citeturn6search19turn6search3

At minimum, treat “60 plots” as a design decision:

- **60 controls**: maximum interactivity isolation but maximum UI overhead (layout, hit-testing, invalidation, message dispatch). citeturn1search1turn16view0  
- **1 control with 60 subplots**: reduced UI overhead and better batching; interactivity may need custom mapping (hit-testing per subplot). citeturn6search19turn6search15  

### Use the most performance-oriented plottables and data update patterns

ScottPlot’s documentation emphasizes that:

- Updating **fixed-length arrays in-place** and re-rendering is “the most performant option for displaying changing fixed-length data.” citeturn6search24  
- Signal plots are recommended over scatter plots when possible (evenly spaced X) and can display millions of points interactively. citeturn6search0turn6search21turn6search32  
- `SignalXY` is optimized when X values are ascending (a middle ground between Signal and Scatter). citeturn6search5turn0search24  
- Rendering can be limited by index ranges for some plot types (partial signal rendering), which is a built-in LOD control knob. citeturn6search27turn6search31  

These are precisely the kinds of “library-native” hooks you should exploit before re-engineering your rendering backend. citeturn6search24turn6search27

### GPU options in ScottPlot and related libraries

ScottPlot includes an OpenGL demo comparing standard CPU rendering vs OpenGL (GPU) rendering for certain plot types, and it uses a `ScatterGL` path in the demo. citeturn13view0turn10search6 This suggests GPU acceleration is **selective**, not universal, and may primarily help specific high-density primitives (e.g., scatter). citeturn13view0turn10search6

Other .NET charting libraries make different trade-offs:

- LiveCharts2 supports **hardware accelerated views via SkiaSharp**, but notes GPU views are not enabled by default due to stability concerns; it provides GPU control properties. citeturn2search12turn2search16  
- OxyPlot has a SkiaSharp renderer; release notes describe an alternative WPF `PlotView` using SkiaSharp for immediate-mode rendering. citeturn2search27turn2search3  
- Plotly.NET produces JSON rendered by plotly.js, and plotly.js supports WebGL traces like `scattergl` for WebGL-based plotting. citeturn2search5turn2search33turn4search3  

A cautionary note from the web ecosystem: many separate WebGL charts can run into WebGL context limits; Plotly discusses WebGL vs SVG trade-offs, and issue threads mention multiple WebGL contexts per graph. citeturn4search39turn4search35

image_group{"layout":"carousel","aspect_ratio":"16:9","query":["ScottPlot multiplot demo screenshot","ScottPlot OpenGL ScatterGL demo","SkiaSharp SKGLControl WinForms example","WPF D3DImage Direct3D interop sample"],"num_per_query":1}

### Comparative table of common .NET plotting/rendering options

The following summarizes the requested libraries in the specific context of “60 simultaneous plots.” (Latency and throughput are qualitative; you should confirm via benchmarks on your target machine.)

| Library | Rendering backend and mode | Strengths for 60 plots | Typical bottlenecks / risks |
|---|---|---|---|
| ScottPlot | SkiaSharp-based rendering in v5 for performance and cross-platform support citeturn0search0turn0search23; customizable render pipeline via RenderActions citeturn6search8turn5view0 | Strong “native app” feel; supports Multiplot to reduce control count citeturn6search3turn6search19; guidance for in-place array updates citeturn6search24 | Per-plot render pipeline includes layout/ticks; autoscaling can cause repeated renders citeturn5view0; UI-thread serialization if many controls refresh independently citeturn16view0 |
| OxyPlot | Cross-platform plotting citeturn2search11; SkiaSharp renderer available for WPF as immediate-mode rendering citeturn2search27turn2search3 | Familiar MVVM patterns; SkiaSharp renderer option for WPF citeturn2search27 | Community reports of poor performance with large datasets / multiple plots in WPF contexts citeturn1search15turn1search3 |
| LiveCharts2 | Renders via SkiaSharp backend; supports optional GPU views but notes stability issues citeturn2search12turn2search16; builds chart “representation”/geometries citeturn2search8 | Nice animation model; cross-platform story; can leverage SkiaSharp acceleration citeturn2search12 | Performance guidance suggests point virtualization is needed beyond ~10k points in some cases citeturn6search22; GPU view stability & native Skia version issues may arise citeturn6search6turn6search33 |
| Plotly.NET | Generates JSON for plotly.js rendering citeturn2search33turn2search5; plotly.js offers WebGL traces (scattergl) citeturn4search3turn4search39 | Strong interactivity in browser/notebooks; WebGL traces can handle large scatter/line sets citeturn4search3turn4search39 | Embedding many WebGL charts can hit browser/WebGL context constraints; multi-chart dashboards may need careful composition citeturn4search35turn4search39 |
| HelixToolkit | 3D components; WPF SharpDX edition uses DirectX 11 scene graph citeturn2search2turn2search14turn2search6 | Best suited when you truly need GPU 3D (surfaces, volumes) rather than 2D line plots citeturn2search6turn2search14 | Added complexity; for pure 2D time-series it may be the wrong tool unless you implement custom 2D-on-3D overlays citeturn2search6 |

## Benchmark and reproducibility plan for 60 plots

### Benchmark goals and what is currently unspecified

Unspecified in the request: target FPS, allowable end-to-end latency, per-plot pixel sizes, points per plot, update rate, and target OS/.NET/runtime version.

Because these determine architecture, define one of these performance profiles first:

- **Monitoring dashboard**: 10–20 FPS, moderate interaction, 1k–50k points/plot, batch updates every 50–200 ms. (Often acceptable if cursor feels responsive.) citeturn9search3turn6search24  
- **Trading-style panels**: 30+ FPS feel, frequent crosshair/zoom, 500–10k on-screen points/plot after decimation, updates 10–60 Hz. (100 ms interaction lag becomes noticeable.) citeturn9search3turn16view0  
- **Oscilloscope-like**: consistent 60 FPS, strict latency, high-rate ingestion that must be decimated. (Usually requires very aggressive LOD and often GPU/native rendering.) citeturn9search24turn3search1  

### A reproducible benchmark matrix

A suggested matrix for “60 plots” (each plot shows one line series; vary if you have multiple series per plot):

| Scenario | Raw points per plot (buffer length) | On-screen decimated points | Update cadence | Interaction | Expected bottleneck |
|---|---:|---:|---:|---|---|
| A: small | 10,000 | ~plotWidthPx×2 | 30 Hz | minimal | per-plot pipeline overhead, UI thread citeturn5view0turn16view0 |
| B: medium | 200,000 | ~plotWidthPx×2 | 10 Hz | moderate pan/zoom | decimation compute + layout/ticks churn citeturn7view0turn5view0 |
| C: stress | 1,000,000 | M4-style ~4 values/px column | 10–30 Hz | frequent | memory bandwidth + GC + render time; may require GPU/offload citeturn9search24turn6search21 |

Use synthetic data (sine/random walk) for controlled tests; ScottPlot’s own demos use these patterns for performance demonstrations. citeturn13view0turn6search21 For realism, include at least one real dataset (e.g., ECG), but real dataset selection is optional and not specified here.

### Metrics to collect

For each scenario, collect:

- **FPS and frame time distribution**: mean, p95, p99 render time per frame (and per plot if possible). ScottPlot can display per-render benchmark overlays; internal discussions mention a benchmark display like “rendered in X ms.” citeturn14search2turn13view0  
- **Input-to-photon latency proxy**: time from data batch arrival → UI update visible (instrument with timestamps around data apply and render completion). WPF frame-driven callbacks can assist. citeturn8search1turn8search21  
- **CPU usage and GC**: allocations per second and GC pauses; ETW/PerfView is Microsoft’s recommended infrastructure for performance tracing in .NET. citeturn15search7turn15search19turn15search31  
- **GPU usage (if applicable)**: especially if you use SKGLControl, Direct2D, or a DirectX interop path. citeturn1search2turn3search1  
- **Memory footprint**: per-plot buffers + decimation buffers + bitmap surfaces; ensure ring buffers are reused to avoid GC pressure. citeturn6search24turn15search1  

### Tooling and reproducibility notes

- Use **BenchmarkDotNet** for microbenchmarks of decimation methods and data transforms; it is designed for reliable and repeatable benchmarking. citeturn15search2turn15search22  
- Use ETW-based tools (**PerfView**) to connect UI thread time, GC events, and WPF/WinForms rendering behavior. citeturn15search19turn15search11turn15search39  
- Record environment: OS build, .NET runtime version, CPU model, GPU model/driver, display resolution and scaling, and whether WPF is in hardware or software mode (`ProcessRenderMode`). citeturn8search2turn1search1  

## Critical evaluation, actionable checklist, and follow-ups

### Strengths and weaknesses of the “ScottPlot for 60 plots” approach

**Strengths.** ScottPlot 5 is designed for performance and modern rendering via SkiaSharp, and it provides explicit hooks for customizing the render pipeline (RenderActions). citeturn0search0turn6search8turn5view0 It also provides Multiplot to reduce the number of UI controls, and it documents high-performance plot types and in-place update patterns for live data. citeturn6search3turn6search24turn6search21

**Weaknesses / limitations.** A 60-control design will often hit UI-thread serialization and framework paint limitations; real-world reports show multi-chart refresh scaling issues, especially when refresh is driven from many threads. citeturn16view0turn10search9 Additionally, ScottPlot’s render pipeline includes fixed overhead that can be repeated (layout/ticks, autoscaling), and axis limit changes can trigger repeated internal renders. citeturn5view0 GPU acceleration in ScottPlot appears selective (e.g., ScatterGL demos), so you should not assume all chart types become GPU-accelerated by default. citeturn13view0turn10search6

### Prioritized optimization checklist for achieving low-latency updates with 60 plots

The list below is ordered by “expected payoff per engineering effort” for ScottPlot-centric systems.

1. **Decide: 60 controls vs Multiplot.** If the plots can share a single surface, prefer Multiplot to reduce UI overhead. citeturn6search3turn6search19  
2. **Coalesce refresh into one UI-scheduled render tick.** Avoid per-plot threads calling `Refresh()`; instead update data buffers off-thread and request redraws from one UI timer/frame callback. This directly addresses scaling pathologies like the reported refresh stacking. citeturn16view0turn15search1  
3. **Use fixed-length arrays and mutate in place.** This is explicitly recommended as the most performant live-data approach. citeturn6search24  
4. **Prefer Signal / SignalXY (and disable markers).** ScottPlot documents signal plots as the performant default and notes SignalXY for ascending X data; scatter with markers is typically slower and more memory intensive. citeturn6search21turn6search5turn13view0  
5. **Stabilize axes and avoid continuous autoscale.** Autoscaling and axis-limit changes cause tick/layout work and can trigger multi-pass renders in ScottPlot’s render loop. citeturn5view0  
6. **Implement LOD/decimation based on pixel width.** Use MinMax or M4 for “guaranteed extrema,” and prefer M4-like per-pixel-column aggregation when strict fidelity is required; reserve LTTB for cases where shape preservation matters more than compute simplicity. citeturn7view0turn9search0turn0search2  
7. **Batch data ingestion with Channels and apply at fixed cadence.** This smooths bursts and avoids pathological invalidation storms. citeturn15search1turn15search25  
8. **Profile with ETW/PerfView before redesigning.** Confirm whether you are CPU-bound in rendering, blocked on UI thread, or spending time in GC/layout. citeturn15search19turn15search31turn15search39  
9. **Use ScottPlot render customization for last-mile gains.** Remove/adjust render actions or events when you know they are unnecessary (e.g., disabling benchmark overlays, disabling certain grid/legend behaviors in dense dashboards). ScottPlot explicitly supports render action customization. citeturn6search8turn5view0  
10. **Escalate to GPU-native approaches when necessary.** If you need sustained 60 FPS with large on-screen data density across 60 subplots, consider a GPU-accelerated pipeline (Direct2D/DirectX, SkiaSharp GL views) or specialized DirectX chart engines; industry guidance emphasizes DirectX engines and controlling render loops for lag reduction. citeturn3search1turn1search2turn8search17turn0search18  

### C# patterns and pseudocode snippets

#### Coalesced update loop for many plots (WinForms-style)

This pattern updates all plot data off-thread, then triggers refresh on a single UI timer tick (avoids N independent refresh threads). The need for this coalescing is motivated by observed multi-chart refresh scaling issues. citeturn16view0turn10search9

```csharp
// Pseudocode / pattern sketch (WinForms)
// Goal: update 60 plots without 60 refresh threads.

// Shared: one producer-consumer channel for incoming batches
readonly Channel<DataBatch> _batches = Channel.CreateBounded<DataBatch>(new BoundedChannelOptions(8)
{
    SingleReader = true,
    SingleWriter = false,
    FullMode = BoundedChannelFullMode.DropOldest
});

readonly object _dataLock = new();

// Each plot has a fixed-length ring buffer already bound to a ScottPlot Signal plot.
readonly double[][] _buffers = new double[60][];
volatile bool _dirty = false;

async Task ConsumerLoopAsync(CancellationToken ct)
{
    await foreach (var batch in _batches.Reader.ReadAllAsync(ct))
    {
        lock (_dataLock)
        {
            // Apply new samples to ring buffers (in-place)
            foreach (var sample in batch.Samples)
                AppendToRing(_buffers[sample.PlotIndex], sample.Value);

            _dirty = true;
        }
    }
}

// UI timer tick at 30-60 Hz (or lower if needed)
void UiTimer_Tick(object? sender, EventArgs e)
{
    if (!_dirty) return;

    lock (_dataLock)
    {
        // Optionally compute decimated views here or cache pre-decimated arrays
        _dirty = false;
    }

    // Refresh plots from the UI thread (coalesced)
    // If using Multiplot, this becomes ONE Refresh().
    foreach (var control in plotControls)
        control.Refresh();
}
```

Channels are a standard .NET mechanism for producer/consumer flows and are documented as synchronization data structures for passing data between producers and consumers asynchronously. citeturn15search1turn15search25

#### WPF per-frame render tick

Use `CompositionTarget.Rendering` when you truly need frame pacing aligned to WPF’s rendering process. citeturn8search1turn8search21

```csharp
// WPF pattern: coalesced render scheduling
void Start()
{
    CompositionTarget.Rendering += OnFrame;
}

void OnFrame(object? sender, EventArgs e)
{
    if (!dirty) return;
    dirty = false;

    // Update plot data (ensure UI-thread affinity if required by your control)
    // wpfPlot.Refresh();
}
```

#### Manual double buffering (WinForms) for custom drawing surfaces

WinForms supports double buffering via `DoubleBuffered` and/or `ControlStyles`. citeturn1search0turn1search8turn1search29

```csharp
public sealed class DoubleBufferedPanel : Panel
{
    public DoubleBufferedPanel()
    {
        DoubleBuffered = true; // reduces flicker
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        UpdateStyles();
    }
}
```

#### WriteableBitmap double-buffer workflow (WPF raster back-end)

Microsoft’s documentation describes a workflow: `Lock() → BackBuffer write → AddDirtyRect() → Unlock()` where unlocking requests a render pass if dirty rects were set. citeturn8search15turn8search3turn8search19

```csharp
// Sketch only:
// wb.Lock();
// unsafe write to wb.BackBuffer
// wb.AddDirtyRect(rect);
// wb.Unlock();
```

#### DirectX interop to WPF via D3DImage

`D3DImage` is specifically intended to host Direct3D content in a WPF app; it exposes `Lock`, `SetBackBuffer`, `AddDirtyRect`, and `Unlock`. citeturn3search6

```csharp
// Sketch only:
// d3dImage.Lock();
// d3dImage.SetBackBuffer(... shared D3D surface ...);
// d3dImage.AddDirtyRect(...);
// d3dImage.Unlock();
```

### Open questions and suggested follow-ups

- **What is the real requirement**: 60 independent interactive plots, or 60 small “sparklines” where only a subset needs full interactivity? If the latter, UI virtualization and “detail-on-demand” can drastically cut render cost. The request does not specify interaction requirements. citeturn9search3turn7view0  
- **What is the data regime**: high-frequency streaming vs low-frequency updates? M4 was motivated by hard latency + ingestion and can support pixel-aligned aggregation, but your use case may not need pixel-perfect guarantees. citeturn9search0turn9search24  
- **Is the bottleneck UI-thread serialization vs compute?** The ScottPlot multi-refresh scaling report suggests synchronization/serialization can dominate even when CPU usage is not high. Confirm with ETW/PerfView traces. citeturn16view0turn15search31turn15search39  

### Prioritized primary sources to consult

The list below emphasizes originals, official documentation, and authoritative repos.

1. **ScottPlot 5 “What’s New” and performance notes** (SkiaSharp backend, customizable rendering system). citeturn0search0turn6search8  
2. **ScottPlot RenderManager source** (RenderActions pipeline, axis-limit multi-pass behavior, customization points). citeturn5view0  
3. **ScottPlot live-data and Signal/SignalXY performance docs** (fixed-length arrays, signal vs scatter, partial rendering). citeturn6search24turn6search21turn6search5turn6search27  
4. entity["people","Uwe Jugel","m4 time-series paper"] et al., **M4: A Visualization-Oriented Time Series Data Aggregation** (VLDB). citeturn9search0turn9search12  
5. entity["people","Jonas Van Der Donckt","time-series downsampling eval"] et al., **Data point selection/downsampling evaluation with stability metrics** (arXiv:2304.00900, IEEE VIS submission). citeturn7view0  
6. entity["people","Sveinn Steinarsson","lttb thesis"], **Downsampling Time Series for Visual Representation** (LTTB thesis). citeturn0search2  
7. **WPF architecture and CompositionTarget.Rendering docs** (retained mode, per-frame callbacks). citeturn1search1turn8search1turn8search21  
8. **WinForms double buffering docs** (`DoubleBuffered`, buffered painting model). citeturn1search0turn1search8turn8search4  
9. **Direct2D official docs** (hardware-accelerated immediate-mode 2D). citeturn3search1turn3search5  
10. **System.Threading.Channels docs** (producer/consumer pipelines). citeturn15search1turn15search25  

For convenience, here are direct links (raw URLs included in a code block as requested):

```text
https://scottplot.net/faq/version-5.0/
https://github.com/ScottPlot/ScottPlot/blob/main/src/ScottPlot5/ScottPlot5/Rendering/RenderManager.cs
https://scottplot.net/faq/live-data/
https://scottplot.net/cookbook/5/ScottPlotQuickstart/SignalPerformance/
https://scottplot.net/faq/multiplot/
https://www.vldb.org/pvldb/vol7/p797-jugel.pdf
https://arxiv.org/pdf/2304.00900
https://skemman.is/bitstream/1946/15343/3/SS_MSthesis.pdf
https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-architecture
https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-render-on-a-per-frame-interval-using-compositiontarget
https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-reduce-graphics-flicker-with-double-buffering-for-forms-and-controls
https://learn.microsoft.com/en-us/windows/win32/direct2d/direct2d-portal
https://learn.microsoft.com/en-us/dotnet/core/extensions/channels
```

