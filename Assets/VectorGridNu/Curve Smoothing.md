# Vector grid curve smoothing

On **VectorGridGPU → Curve Smoothing (Rendering Only)**:

- **Curve Interpolation → Smooth** (default): draws a smooth deformation field between the simulated points. **Linear** restores the previous bilinear appearance for comparison.
- **Curve Tension** (default 0.1): 0 gives flowing tangents; 1 eases deformation to a flat tangent at each simulation node. It does not change physics strength or smooth away the nodes themselves.
- **Prevent Curve Overshoot** (default on): shape-preserving slopes keep each displacement component within its cell-corner range. Disable only if the extra bulging of unrestricted cubic curves is desirable. This cannot undo folds or sharp, narrow peaks already present in the simulated positions.

The shader interpolates **displacement**, leaving the flat lattice unchanged. Horizontal lines, vertical lines and the border use the same sampling function. Both axis orders are averaged to avoid directional bias from slope limiting. Pinned boundaries and the existing intro/outro overscan are preserved.

Changing these controls does not rebuild/reset the simulation. Simulation and rendering subdivisions remain independently adjustable. Existing scene resolution settings are not migrated. A useful low-resolution starting point to compare is **2 simulation subdivisions and 5–8 render subdivisions**; this is a suggestion, not an automatic scene change. Finite render segments can still become visible at extreme zoom, and a coarse simulation cannot resolve arbitrarily narrow forces.

Smooth mode reads a 4×4 neighborhood instead of Linear's 2×2 and does more vertex-shader arithmetic. It adds no simulated points, physics dispatches, CPU readbacks, per-frame collections or new material instances. No target frame-time budget is implied; profile representative gameplay before selecting final settings.

## Validation

**MASSIVE → Vector Grid → Validate Curve Smoothing** runs isolated GPU checks against the production HLSL sampler. It checks node interpolation, unchanged rest geometry, legacy mode, limited displacement ranges, knot tangents, mirror/axis symmetry, pinned borders and overscan. It does not advance the match or change the active scene.

Validated in Unity 6000.0.28f1 on Windows Editor: 17 automated checks passed. A paused-frame comparison in `S-8_DYNAMO-PROTOTYPE` at 2 simulation / 5 rendering subdivisions confirmed rounded bends with the same simulation buffer and render mesh. Inspector mode changes did not schedule a simulation rebuild. Both line and border received matching curve settings; the final Console error count and grid shader message count were zero. Existing duplicate-AudioListener warnings were present before the change. No standalone build or frame-time profiling was performed.
