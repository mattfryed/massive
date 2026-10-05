# COSMOS layout

`Assets/Scenes/S-9_COSMOS.unity` is the user's duplicated LATTICE scene, now with
an oval arena and an evolving cosmic web background.
The inherited encounters and shared match/score systems remain available for testing.

The field and goals share one exact ellipse. The user has tuned it to 36 units
wide and 13 units high, with playable separators at approximately X = +/-13.846.
The original installation defaults were 31.2 x 14; the current dimensions are
authored on the grid and must not be reset by installation tools.
The original camera size (9), HUD, players and all other authored poses are retained.
Three unused spawn markers moved by the first prototype were restored to their
original positions (migration revision 3); the actual Respawn objects were never moved.
The camera stays fixed, including when the chosen oval extends beyond its view.

`ArenaBoundsFromVectorGrid` owns the optional oval profile. Rectangular scenes default
to their original behavior. Queries, padded placement, and recovery use this profile;
`CosmosArenaLayout` builds transient solid walls and an outline mesh from it. The
grid's simulation rest positions and production GPU sampler both curve each row:
vertical row spacing is widest at the centre and condenses to about 64% at the ends.
Player, attack and grid forces operate in those physical coordinates. Displaced
strokes and simulation nodes are constrained by the same ellipse. No generated
mesh or collider hierarchy is saved into scene YAML. COSMOS disables the original
four-wall builder and its old wall objects. The outer goal-cap outline is generated
without colliders. The original goal Discs remain transparent size templates.
The existing mask planes retain their transforms and depth; a scene-specific mask
material opens the full ellipse so they do not cut the taller grid back to a rectangle.
`CosmosGoalShape` maps score fills and animated promotion rings into the elliptical
cap, so they no longer use a semicircular outline inside an elliptical outer frame.

Goal transforms, capture/attraction radii, score size limits and local capture-blast
dimensions follow the goal-height ratio. The score renderer independently fits
the cap's width and height; the score shader and promotion mesh share the same
half-disc-to-ellipse-cap mapping. The legacy
ScoreSphereScript targets the separate visual child so increasing score cannot
resize the Core capture trigger. Score economy, tier thresholds and promotion
timing stay shared.

Editor commands under **MASSIVE > COSMOS**:

- **Select layout controls** (`Ctrl+Shift+Alt+F8`): select the grid's
  `CosmosArenaLayout` component. Its **Overall Oval Width** (16–48) and
  **Overall Oval Height** (6–24) Inspector sliders resize the full ellipse in
  world units, including both goals. They preserve the separator proportion,
  update the grid, walls, goal fills, score growth limits and capture effects
  immediately in Edit Mode, and support Undo/Redo. Save the scene to retain edits.
- **Apply oval layout** (`Ctrl+Shift+Alt+F10`): apply/reconcile goal scaling and scene
  identity, then save COSMOS. Reapplying does not compound scaling or change cameras
  or non-arena poses. Shape defaults are installed once (layout revision 2). A pre-install
  scene backup is retained at `Library/CosmosLayout/COSMOS-before.unity`.
- **Validate layout in Play Mode** (`Ctrl+Shift+Alt+F11`): direct-launch startup,
  production GPU row sampling and rectangular fallback, padded bounds, respawns,
  all physical wall segments, moving rigidbody contacts,
  score/tier previews, fixed capture volumes, and actual Core captures for both
  teams. Returns to Edit Mode. Reports and renders are in `Library/CosmosLayout`.
- **Inspect current layout** (`Ctrl+Shift+Alt+F9`): save a hierarchy/configuration
  inventory to that folder.

For overall shape tuning, use the sliders on `CosmosArenaLayout` at
`02_Arena/Surface/GRID/VectorGridGPU`; no Apply step is needed. For separate
goal/field proportions, edit the bounds profile and run **Apply oval layout**.
The camera stays fixed while sizing; its current framing targets the cabinet's 16:9 format.
The level has its own definition/stage assets for direct launch; carousel icon,
instructions and catalog registration remain later work.

## Cosmic web

`CosmicWebBackground` is on the same `VectorGridGPU` object as the layout controls.
It uses 56,000 persistent matter/galaxy tracers, rendered in one procedural GPU draw.
No particle GameObjects, colliders, gameplay forces or per-frame buffer uploads are
created. The buffer/material are transient and released on disable or scene unload.
Changing the seed, particle count or Organic Strength rebuilds the distribution;
dimensions, padding, colors, brightness, evolution variation, edge condensation and
preview age only update shader parameters.

- The existing regulation clock owns age: start/countdown = 0, half of regulation =
  0.5, regulation end = 1. Paused regulation, including bonus phases, holds age.
- Early matter is nearly uniform. It gathers onto interconnected filaments and
  pink cluster junctions by the midpoint; late matter gathers more tightly at
  junctions, leaving only a tenuous filament population and dominant black voids.
- The web follows the current oval with a **0.25 world-unit padding** default.
  Outer filament ends are paired with curved return paths on the model's enclosing
  ellipsoid. A smooth compact projection condenses the outer network progressively;
  a second smooth compression keeps the far sides before the goal separators.
  Projection includes the inset plus a small glow allowance; the fragment mask is
  a final safety boundary for glow, rather than cutting off filament centerlines.
  Its thin depth slab sits behind the grid and
  above the existing ground plane. Camera, HUD and goal poses remain untouched.
- **Preview Age** scrubs all three eras in Edit Mode. Play Mode always follows the
  actual match clock. Palette, density, galaxy size, brightness and padding remain
  Inspector controls. **Organic Strength** controls multiscale curvature and branch
  spread; **Evolution Variation** controls regional collapse timing and infall drift;
  **Edge Condensation** controls the gradual lens-like compression. Save scene edits
  to keep them.

### Scientific basis and artistic interpretation

This is a seeded geometric model of structure formation, not a downloaded JWST
catalogue, an N-body gravity calculation or an exact reconstruction of our sky.
Irregularly seeded three-dimensional Voronoi void cells define a coarse connected
skeleton. Several octaves of coherent spatial noise curve that skeleton; thinner
offshoots leave and rejoin parent filaments, and filament width varies spatially.
Continuous trajectories carry the same particles from a homogeneous distribution
to filaments and knots. Coherent regional timing differences, curved infall and
curved accretion paths make evolution uneven, with exact anchors at 0, 0.5 and 1.
No time-varying random reseeding or particle respawning is involved.

The closed outer return paths and lens-like oval projection are arena presentation
choices, not a claim that the universe has an oval boundary or a physical simulation
of gravitational lensing. NASA's simulation visualizations and Hubble mapping work
inform the hierarchical filaments, irregular voids and dense junctions.

NASA's January 2026 JWST/COSMOS weak-lensing map covers a limited observed sky
region and shows clumpy dark matter associated with galaxies. NASA's 2023 early
filament observation and CMB material support the homogeneous-to-filamentary
progression. The CMB era predates stars: the initial luminous dots represent
matter tracers. Blue/pink are the supplied reference's illustrative palette, not
literal visible-light dark matter colors. The mid-match "present" and late void
dominance are deliberately compressed artistic epochs, not a calibrated age scale
or a prediction of future cosmic evolution.

References:
- https://svs.gsfc.nasa.gov/14598/
- https://science.nasa.gov/missions/hubble/slime-mold-simulations-used-to-map-dark-matter-holding-universe-together/
- https://www.nasa.gov/missions/webb/nasa-reveals-new-details-about-dark-matters-influence-on-universe/
- https://www.nasa.gov/universe/nasas-webb-identifies-the-earliest-strands-of-the-cosmic-web/
- https://imagine.gsfc.nasa.gov/observatories/satellite/wmap/anisotropy.html

**MASSIVE > COSMOS > Cosmic web > Install background** (`Ctrl+Shift+Alt+F7`)
adds/wires this scene-local component while preserving existing transforms and
the user's oval dimensions. Reinstallation preserves authored web settings.
**Validate evolution in Play Mode** (`Ctrl+Shift+Alt+F12`) verifies topology,
boundary reconnection, CPU/GPU trajectory agreement, padded projection, nonlinear
regional timing, occupancy progression, clock binding, paused-clock behavior and
resource lifecycle. It captures five eras including both transitions and returns to
Edit Mode. Reports and captures
are in `Library/CosmosWeb`; the pre-install scene backup is `BeforeWeb.unity` there.
