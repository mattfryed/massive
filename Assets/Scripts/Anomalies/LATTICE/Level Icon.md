# LATTICE Level Select icon

`LS_LATTICE Icon.prefab` is a presentation-only prefab using the existing `LevelIcon` selection component, assigned to `LevelDefinition-LATTICE`. The current authored size is four cells per axis, including the interior: 125 unique intersection points and 300 axis-aligned connections. The original ten-cell volume remains available through Cells Per Axis.

One coherent 3D noise volume drifts and evolves through the cube. This extends the level's warped, layered noise treatment into depth; it is not six independently animated faces. Disrupted endpoints release a single tether from the intact endpoint. Interior cuts leave two strands. Held tips follow the volume boundary, while reconnected strands grow back. Independent smooth random curve handles bend threads in two perpendicular planes, with zero looseness and tangent deviation at their connected roots.

Dots use the level's independent RGB jitter and six fading, shrinking afterimages per channel. Blue retains the 30% green tint toward cyan, and three-way overlaps remain white. Jitter grows with distance into disruption. A padded volume of sampled contour crossings estimates distance in cells, including boundaries beyond the cube. Sampling runs at up to 20 Hz; strand and dot motion animate every frame. Surface wires and outer edges are brighter than interior wires to preserve the cube silhouette; they still disconnect with the same field.

The prefab's child is sized and angled for the existing Level Select camera. `LevelIcon` owns selection scaling. Menu animation uses unscaled time. The icon owns a single transient mesh, two state buffers and a property block; it releases them on disable/destroy. It has no gameplay grid, actors, colliders, singleton registration, or persistent history textures. The gameplay anomaly is unchanged.

## Preview and tuning

- **MASSIVE > LATTICE > Create and Preview Level Icon** (Ctrl+Shift+Alt+I) opens an animated isolated preview without changing the open scene. Creation is idempotent and does not overwrite an existing prefab.
- Select/open the prefab and edit its **Lattice Level Icon** component to persist tuning. Its Inspector also animates Edit Mode/Prefab Mode previews.
- Noise frequency, threshold, drift and evolution control the disruption volume. Connected/Disconnected modes provide comparison views.
- Hold Tips At Boundary, Thread Looseness, Breeze Speed and the independent motion seed control the threads.
- Dot diameter, jitter radius/depth/speed/seed, trail duration/opacity, and appearance controls are authored independently for the icon's small size.
- Animate In Editor and Preview Time control editor playback. The preview clock is transient and never dirties the prefab.
- Slow Tumble exposes Rotate Icon, Rotation Speed, Rotation Direction Seconds and Rotation Seed. Smooth noise changes the angular velocity on all three axes. Rotation uses unscaled time and is applied to rendered geometry, preserving the authored Transform and carousel placement. The default speed limit is 8 degrees/second with direction changes over roughly 8 seconds. Turning rotation off holds the current orientation.

## Validation

**Validate Level Icon** (Ctrl+Shift+Alt+J) exercises geometry, topology, boundary attachment, depth, actual rendered colors/ghosts, animation, reconnection, prefab references and resource ownership in an isolated preview scene. **Validate Icon in Play Mode** (Ctrl+Shift+Alt+K) exercises automatic initialization, unscaled menu time, selection scaling, disable/re-enable and independent instances. Reports and captured renders are written to `Library/LatticeIconValidation`.

LATTICE is registered as the first selectable stage. **Register Playable Level** (Ctrl+Shift+Alt+U) creates missing level assets and wires its standard catalog/build/scene references without overwriting icon tuning or an existing instruction card. **Validate Playable Level** (Ctrl+Shift+Alt+Y), from the Level Select scene in Edit Mode, runs the real carousel, instructions, gameplay, timed match expiry and return flow. Its report and full Game view captures are saved under `Library/LatticePlayableValidation`. Twenty-five flow checks and the twenty-four dense-volume rendering checks passed after adding rotation; the original ten-cell fixture is separate from the authored four-cell icon.

Validated 2026-10-03 in Unity 6000.0.28f1 / DX11: 24 isolated geometry/render checks and 10 Play Mode lifecycle/selection checks passed. Large, menu-size, carousel-orientation, RGB trail and runtime captures were visually inspected. The final full field/state refresh averaged 10.95 ms across 20 Editor samples (up to 20 Hz; rendering excluded), reduced from 54.08 ms in the initial implementation. The renderer remains animated between field samples. No standalone player build or target-device GPU profile was run.
