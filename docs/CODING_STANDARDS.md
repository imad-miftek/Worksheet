# Coding Standards

Repository-wide coding standards for `Worksheet` (`net8.0-windows`, WPF, ScottPlot 5).

## Core Principles

- Keep implementations small, explicit, and easy to reason about.
- Fix root causes instead of layering temporary workarounds.
- Preserve behavior unless the task explicitly changes behavior.
- Prefer deterministic, testable code over implicit side effects.

## Project Structure

- `Models/`: domain and data transfer shapes.
- `Services/`: streaming, processing, viewport engines, and business logic.
- `Views/`: WPF UI, plot views, context menus, and interaction wiring.
- `docs/`: repository policy and working agreements.

Do not move files across these boundaries without a clear reason.

## C# Conventions

- Nullable reference types are enabled. Respect nullability annotations.
- Use explicit names (`plotSettings`, `visibleLength`) over vague names (`data`, `tmp`).
- Keep methods focused. Extract private helpers when branching grows.
- Avoid expensive work in property getters.
- Prefer immutable local values in hot paths.

## WPF and UI Rules

- Keep code-behind UI-focused. Processing belongs in `Services/`.
- Avoid repeated layout literals. Promote reused values to constants/resources.
- Reuse existing plottables during updates where possible.
- Avoid unnecessary visual-tree complexity in frequently refreshing views.

## Plot and Performance Rules

The viewport path is performance-sensitive:

- Avoid per-frame allocations in `PlotProcessor`, `GateProcessor`, and plot view `Render()` code.
- Reuse buffers and plottables when practical.
- Keep hot loops simple; avoid avoidable expensive math in tight loops.
- Do not add logging in hot loops.
- Consider scaling with `BinCount`, channel count, and gate count.

## Logging and Diagnostics

- Use `AppLog` for meaningful exceptions/events.
- Temporary diagnostics must be clearly scoped and removed before completion unless explicitly requested.
- Do not leave commented-out debug code.

## Dependencies

- Keep dependencies minimal.
- If a package is added or updated, include rationale in the task/PR summary.

## Validation Expectations

For regular code changes, run at least:

- `dotnet build .\\Worksheet.sln -c Release`

For processing/rendering behavior changes, include a brief manual verification note.

## Completion Checklist

A change is not complete if it leaves:

- dead or duplicated code introduced by the task
- temporary instrumentation
- avoidable performance regressions in hot paths
- unclear naming or hidden side effects
