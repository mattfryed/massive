# SINGULARITY level-select icon

Presentation asset: **LS_SINGULARITY Icon.prefab** in this folder. Its
`LevelIcon` component supplies the existing carousel selection hook. The icon
itself creates no gameplay objects and is independent of the gameplay black
hole and its portal/attraction settings.

The miniature uses two opposing wire-grid wells, continuous rounded folds on
both sides, a black depth-writing event horizon and the user-authored black-hole
particle accretion group.
The upper grid is brighter and the lower grid dimmer. The middle bends in three
dimensions toward the hole, not just a painted distortion. Fine tube geometry
stays dimensional when viewed from different angles. The old generated annulus
and its animated-band shader path have been removed; the particles are the only
accretion effect. The lattice renderer therefore has one material: **Icon
Lattice**. Legacy disk fields remain hidden for serialized compatibility and
no longer generate or control anything.

## Tuning

Select **Folded Grid and Central Well** in the prefab:

- **Flat Width / Depth / Layer Separation / Fold Reach:** proportions of the
  two planes and rounded joins.
- **Well Radius / Well Depth / Radial Pull:** breadth, depth and inward gathering
  of the two central wells. These are icon-only, not gameplay settings.
- **Width Cells / Depth Cells / Wire Radius:** lattice density and line weight.
- **Upper Color / Lower Color:** independent RGB colors for each plane.
- The **accretion disks** group contains the authored particle systems and the
  existing **Rotator** component. Adjust those children for the accretion look;
  rebuilding the lattice does not change their transforms or settings.
- Rotator's **Local Rotation Speed** and **World Rotation Speed** are degrees
  per second. A single local axis gives orbital spin; multiple axes also tumble
  the disk plane. **Unscaled Time** keeps that menu-only motion running while
  gameplay time is paused. No replacement rotation framework is required.
- The **Black Hole - Event Horizon** child's scale controls the black sphere.
- The visual child's rotation gives the three-quarter view and its scale fits
  the roughly one-unit carousel icon convention. Keep the root neutral: the
  carousel will own its placement and rotation. The existing `LevelIcon`
  selection hook is already present.

The lattice updates in Edit Mode when its Inspector settings change. Materials
are explicit prefab references; only the generated mesh is transient, cleaned
up on disable/destruction. Sampling is bounded below 52,000 vertices even at
maximum cell counts. There are no colliders, gravity fields, portals, score
systems, scene searches or changes to shared rendering settings.

## Preview and validation

- **MASSIVE > SINGULARITY > Create Missing Level Icon** creates a missing lattice
  and horizon starter only and preserves an already existing prefab. It does
  not reconstruct the user-authored particle group or regenerate an annulus.
- **MASSIVE > SINGULARITY > Validate Level Select Icon** checks opposing wells,
  fixed fold joins, bounded geometry, resource cleanup, shader and prefab wiring,
  preservation of the authored particle/rotation components, and particle emission
  in an isolated preview. It expects the completed icon, not just the starter.
- Native previews: `Assets/Screenshots/SINGULARITY-icon-preview.png` and
  `Assets/Screenshots/SINGULARITY-icon-carousel-angle.png`. The second applies
  the selected carousel's 180-degree root orientation, without opening or
  modifying the actual carousel scene.

Original 2026-09-25 baseline: Unity compiled; 33 isolated checks passed. Native renders inspected
from both preview orientations. No Play Mode, carousel integration or standalone
build was performed for this presentation-only asset.

2026-09-25 playable integration: the generated accretion annulus was removed;
all three user-authored particle systems, their transforms and Rotator settings
were preserved. The lattice now has one material/submesh. All 37 updated icon
checks passed. The icon is registered as STAGE_006 and is also nested in the
SINGULARITY instructions panel. Its presentation was inspected in the live
carousel and instructions flow. The current three-axis Rotator adds tumbling
as well as spin; a single local rotation axis is an optional future tuning
choice, not a requirement for integration.
