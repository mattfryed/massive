# Responsive player attraction

## Design intent

The player should pull a smooth, rounded region of the visible grid along with it,
without dragging a long rubbery wake behind it. This is a visual analogy to a mass
deforming a field, not a general-relativity simulation. Movement feel and collision
physics should be tunable without also retuning this visual response.

## Where to tune

Select `Assets/VectorGridNu/Player Interaction.asset`. Its **Gravity** module now
uses **Responsive Radial**. The starting values are:

| Control | Initial value | Meaning |
| --- | --- | --- |
| Radius | 3.25 | World-space reach at player size 1; the existing player-size multiplier still applies. |
| Pull | 0.8 | Visual compression strength; overlapping fields blend with a smooth safety cap. Not a literal percentage. |
| Response Time (s) | 0.06 | Time to cover 95% of a stationary position, radius, or strength change. Lower follows movement more tightly; zero is immediate. |
| Release Time (s) | 0.2 | Time for source strength to fade 95% when the interactor is disabled or despawns. Does not add a wake behind a moving player. |
| Scale By Speed | Off | Optional existing speed curves; keep off initially to judge a consistent field. |

Current P1/P3 size 0.66 gives a radius of 2.145; P2/P4 size 1 gives 3.25.
Profile multipliers and explicit GridInteractor radius/strength overrides still
apply. All players using this shared profile receive the same tuning, including
the Pseudo Player prefabs used by demonstrations. There is also a **Player
Attraction (Responsive)** preset in the profile editor's add-module menu; do not
add a second Gravity module to this profile unless two effects are intentional.

Change Radius for reach, Pull for depth, and Response Time for following speed.
Global grid spring/damping/falloff settings do not alter this responsive module.
They continue controlling the other, physically simulated grid effects.

## Evaluation of the previous behavior

The previous player module was Constant Radial, radius 2, strength 6, with inner
fraction 0.263. The grid used spring 12, damping 2.2 and global inner fraction 0.2.
It repeatedly accelerated displaced grid points, which then retained velocity
and returned on underdamped springs. The force collector also ran after the grid's
Update, introducing a one-frame queue delay. Spatial curve interpolation could
round those lines, but could not remove the temporal inertia.

Rigidbody mass was not directly read by player/grid attraction. Mass changes
affected movement through ForceMode.Force propulsion, thereby changing the path
and time spent pulling on the grid. No player mass, damping, movement, collider,
or Rigidbody interpolation settings were changed for this work.

The inspected saved DYNAMO (including prototype), NOVA, PULSAR, HIGGS and ORBITAL
scenes inherited the same core grid simulation settings from PLAYING FIELD.
There were visual overrides, but no evidence that per-level spring drift caused
this particular issue. Simulation and render resolution were already at their
Inspector maxima in the current DYNAMO setup; neither was increased.

## Implementation boundaries

- Responsive Radial is appended to the existing module enum, preserving old
  serialized values. Other profiles remain on their existing force modes.
- GridInteractor submits a visual source directly to its own grid, and does not
  also enqueue a spring force for that module.
- The grid binds current-frame sources in LateUpdate after the existing collector.
  Global spring simulation timing is unchanged.
- The renderer adds a compact radial displacement based on rest coordinates.
  Its envelope is `(1 - (distance/radius)^2)^3` inside the radius and zero outside.
  Both the center and the outer edge are smooth; grid boundaries remain pinned.
- Center, radius and strength use frame-rate-independent exponential tracking.
  Missing sources fade and retire. Each grid has a bounded 16-source pool and
  reusable shader data; no per-frame managed allocations are introduced here.
- The new effect composes with existing simulation, Amplifier treatments and
  Repulsor displacement. It does not change Resonance attraction or impact physics.
- This is a renderer-only field, not a displacement written back to simulation
  buffers. Systems sampling the simulation do not receive this visual offset.

To restore the previous player behavior, set the existing Gravity module back to
**Constant Radial**, Radius **2**, Strength **6**, Inner Frac **0.263**. New response
fields are ignored in legacy modes. No scene or prefab migration is required.

## Validation (2026-09-19)

Executed inside Unity in Edit Mode:

- 27 responsive state checks, including 30/60/120 fps timing, removal, pause,
  capacity, invalid inputs, symmetry, overlap order and world-scale handling.
- 19 native GPU checks, comparing the actual HLSL with its CPU reference and the
  unchanged spring compute shader with the new player profile tuning.
- 17 existing native grid curve-smoothing regression checks.
- Actual scene P1 force routing: one responsive source, radius 2.145, zero legacy
  forces. An offscreen Game-camera render confirmed a smooth compact bend in the
  existing grid renderer; original render properties were restored afterward.

Isolated 60 Hz GPU comparison, 225x97 simulation, 28x12 arena, source in grid plane:

| Measure | Previous spring module | New responsive module |
| --- | --- | --- |
| First 95% of each mode's held displacement | 0.4333 s | 0.0500 s |
| Remaining displacement 0.25 s after release | 68.87% | 4.00% |
| Release zero crossings over the test | 2 | 0 |
| Deformation-centroid lag at 6 world units/s | 2.5256 | 0.0770 |
| Centroid lag 0.5 s after reversing direction | 1.2201 | 0.0770 |

These are isolated behavior measurements, not gameplay frame-rate benchmarks.
The old effective P1 radius was 1.32; the new one is 2.145. Step and release
percentages are normalized to each mode's own held displacement.

The user-approved DYNAMO Play Mode check also passed: P1 was driven through a
2.6-second move/reverse/stop sequence using its existing controller. Across 158
samples it traveled up to 2.2736 world units; sampled field-center lag averaged
0.0250 units and peaked at 0.1009. Both player sources remained active. Mass,
Rigidbody damping and interpolation were unchanged, and the original input state
and mode were restored. The runtime Console had zero errors and Unity was returned
to Edit Mode without saving the scene. These samples verify live routing and
following, not an isolated frame-rate benchmark. Subjective tuning remains a
user review step.

Repeat checks from **MASSIVE > Vector Grid > Validate Responsive Attraction State**
and **Validate Responsive Attraction GPU and Baseline**. GPU tests use cloned
shaders and private buffers; they do not change live scene bindings.
