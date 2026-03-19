# Worksheet

Worksheet is a `.NET 8` WPF desktop application for building an interactive plotting workspace over a bounded rolling event stream. It combines a freeform drag-and-resize canvas with ScottPlot-based visualizations for histogram, pseudocolor, spectral ribbon, and oscilloscope views.

The app is currently oriented around simulated acquisition through the in-repo `CHASM` pipeline and a channel map loaded from `channels.json`.

## Features

- Interactive worksheet canvas with draggable, resizable plot tiles
- Plot types:
  - Histograms
  - Pseudocolor heatmaps
  - Spectral ribbon views
  - Oscilloscope plots
- Start/stop streaming controls and configurable rolling window size
- Plot gating support with live gate statistics in the sidebar
- Snap-to-grid layout controls
- Bounded in-memory event retention for stable long-running sessions
- Repo-local or user-local file logging for exceptions and diagnostics

## Tech Stack

- `net8.0-windows`
- WPF
- [ScottPlot.WPF](https://scottplot.net/)
- `MathNet.Numerics`

## Repository Layout

- `Views/`: WPF windows, controls, dialogs, plot views, axes, and context menus
- `Models/`: plot settings, worksheet items, processed data, and gate models
- `Services/Viewport/`: streaming, buffering, processing, rendering, and session orchestration
- `Services/CHASM/`: bounded mock acquisition pipeline and batch transport types
- `docs/`: architecture notes, audits, coding standards, and research writeups

## Getting Started

### Prerequisites

- Windows
- `.NET SDK 8.0`
- An IDE with WPF support such as Visual Studio 2022 or JetBrains Rider

### Run

```powershell
dotnet restore
dotnet run --project .\Worksheet.csproj
```

You can also open `Worksheet.sln` in Visual Studio and run the WPF project directly.

## Configuration

The application loads channel metadata from `channels.json` at startup. The project copies both `channels.json` and `channels.example.json` to the output directory.

Typical setup:

1. Copy `channels.example.json` to `channels.json` if you want a local variant.
2. Edit channel names and wavelengths to match your data source.
3. Start the application.

Channel names affect plot labeling and feature selection:

- Histogram and pseudocolor plots use all configured channels.
- Spectral ribbon plots use only numeric wavelength channels.

## Using the App

On launch, the main window is split into:

- A left sidebar for streaming controls, rolling-window size, gate stats, and processing metrics
- A top toolbar for adding plot types, loading the preset worksheet layout, clearing memory, and changing snap-to-grid behavior
- A worksheet area where plots can be moved and resized freely

Common workflow:

1. Start streaming from the sidebar.
2. Add plots from the toolbar.
3. Drag and resize plots on the worksheet.
4. Adjust plot settings from context menus or plot dialogs.
5. Inspect gate stats and processing/render timing in the sidebar.

The `Load Histogram Config` toolbar action currently builds a preset layout with:

- Two configured pseudocolor plots
- One spectral ribbon plot
- A grid of histogram plots for available channels

## Data and Rendering Pipeline

The current architecture is built around a bounded rolling raw-event window:

1. `MockProducer` emits event batches into the `CHASM` pipeline.
2. `ViewportSession` coordinates ingestion, processing, rendering, and gate evaluation.
3. `DataSource` and related viewport services retain a fixed-capacity logical event window.
4. `ProcessingEngine` produces plot-ready data when retained data changes.
5. `RenderingEngine` pushes updated data into the registered ScottPlot views.

Important semantics:

- Memory usage is bounded by the configured window capacity.
- Oldest events are overwritten when the rolling window is full.
- Gate event indices are relative to the current logical window, not absolute history.

Default mock acquisition settings come from `Services/CHASM/ChasmOptions.cs`:

- Acquisition interval: `25 ms`
- Batch size: `500`
- Window capacity: `200,000` events

## Logging

The app initializes file logging on startup through `Services/AppLog.cs`.

Log directory resolution order:

1. `WORKSHEET_LOG_DIR` environment variable
2. Repo-local `logs/` directory when writable
3. App output `logs/` directory when writable
4. `%LocalAppData%\Worksheet\logs`

## Development Notes

Useful project documents:

- `docs/CHASM_PIPELINE.md`: acquisition and rolling-window semantics
- `docs/PLOT_PIPELINE_AUDIT.md`: current processing/rendering behavior and bottlenecks
- `docs/UI_VISUALIZATION_RESEARCH.md`: background research on low-latency multi-plot visualization
- `docs/CODING_STANDARDS.md`: local coding conventions
- `docs/AI_AGENT_POLICY.md`: repo-specific agent guidance

## Current State

This repository appears to be an actively evolving prototype for interactive multi-plot visualization. The core desktop workflow is in place, and the docs already call out known performance constraints such as full recompute behavior, lock contention in spectral processing, allocation pressure in pseudocolor processing, and synchronous UI render dispatch.
