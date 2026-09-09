# Spawn-cycle validation — 2026-09-08

Unity 6000.0.28f1, current DYNAMO prototype, Built-in renderer.

## Executed checks

- Existing Resonance math / interaction regression suite: 105 passed.
- Existing particle renderer regression suite: 22 passed.
- Spawn placement: 32 passed (separate/scaled/rotated grid, no-go triggers/solids, player/goal spacing, pattern footprint, exhausted search and overlap capacity).
- Pair rules: 39 passed (delay distribution, invalid values, collider radius, ordered entries/loop, cleanup and preserved assets).
- Formation lifecycle: 31 passed (pause, reversal, endpoint behavior, collider/grid gates, hidden rebuilds, cleanup and unchanged authoring).
- Formation GPU: 6 passed (nonblank idle, exact idle pixel parity, field dispersion, actual billboard growth, hidden output, shader compilation).
- Real Play Mode cycle: 12 passed, including two physical goal deposits, no duplicate awards/Cores, two ordered entries, post-dissolution delay, fully blocked placement followed by recovery, and stop/disable restoration.

Total: **247 checks passed**. Final Unity Console contained zero errors. Inspected real Play Mode captures at dispersed, condensing and settled states. This was Editor/Play validation, not a standalone player build or performance benchmark.

The existing scene's two Audio Listeners still generate a warning in Play Mode; neither camera/listener was changed by this work.

The initial GPU fixture used baked billboard snapshots to handle uninitialized Edit Mode buffers. The subsequent edit-mode preview repair now initializes the real particle buffers with zero-time simulation; the GPU fixture has been simplified to render actual ParticleSystems directly. See `Assets/Resonance/Edit Mode Formation Preview.md` for that regression and its validation.

## Preservation and handoff

Original Option A and Option B controller/definition JSON exactly matches the pre-change capture. Spawn assets contain zero generated pattern children. New Core and pattern snapshots preserve the current scene tuning independently.

Unity returned to Edit Mode; test-only list entries, captures, timing, movement and visibility edits were not retained. The current scene already had unsaved edits before installation and remains unsaved; save it normally to keep the new coordinator wiring. No other scenes were modified. In particular, NOVA's star still requires an explicit no-go trigger assignment when this system is added there.
