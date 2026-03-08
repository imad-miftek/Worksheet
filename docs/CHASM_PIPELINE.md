# CHASM Pipeline

`CHASM` uses a bounded rolling raw-event window.

## Key Semantics

- Incoming batches are written into a fixed-capacity ring buffer.
- Memory usage is bounded by `ChasmOptions.WindowCapacityEvents`.
- Oldest events are overwritten when the window is full.
- Plot and gate processors read logical snapshots over the retained window.
- `GateResult.EventIndices` are window-relative logical indices, not absolute historical indices.

## Design Intent

- Stable ingestion cost under continuous streaming
- Bounded memory growth
- Raw-event access preserved for plots and gates
- Processing and rendering remain decoupled from acquisition
