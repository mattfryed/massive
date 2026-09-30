# Particle Beam Turret

Stationary wall enemy using EnemyBase, EnemyDirector, EnemyHurtbox, central defeat
scoring and the existing enemy spawn telegraph. The prefab is Enemy_ParticleBeamTurret.
Player Actions contains one top and one bottom mount under Particle Beam Turret Test;
the encounter participates in the action gallery's normal suppression/pause behavior.
Top is the +Z wall in the gameplay view. Mounting fields update placement immediately
in the Inspector, including multi-selection and Undo. Apply wall mount is also
available as a context command. Wall Inset 0 puts the base plane on the grid boundary.

The base uses the sixteen alternating triangles of a gyroelongated square cupola's
octagonal antiprism, forming one continuous strip between open octagonal rims.
See the [J23 construction reference](https://www.qfbox.info/4d/J23).
The local XY octagon sits against the wall; local +Z faces into the arena. A smaller
ring of eight floating black triangles with white outlines sits forward of the base
around a Drone-style seven-lobe core. Only the firing apparatus pivots. The base
has a fixed convex collision hull and a separate trigger for sword damage.
The Visuals component exposes Base Height (.44774037 along local Z), Base Radius
(.68), and Firing Pivot Offset (-.01774037 relative to the forward rim). Height
changes keep the mounting plane fixed, resize collision and move the apparatus.
All triangles are equilateral at the original height; other heights stretch them.

An open-front **2V backing dome** surrounds the rear of the core: 40 triangles,
26 vertices, 65 edges and one decagonal rim. It uses the frequency-two icosahedral
hemisphere construction ([geometry reference](https://www.ziptiedomes.com/geodesic-dome-calculators/2v-geodesic-dome-calculator.htm)).
Dome Radius (.3) controls its size. It pivots with the apparatus and retains black
faces and white outlines. All 64 faces share staggered arrival and shatter effects.

Current prefab tuning (all Inspector-adjustable, including the user's applied top-turret edits):

- Top/Bottom wall mount, horizontal Wall Position, or Authored for manual placement.
- Detection Range 10, forward aiming cone ±75 degrees, tracking 60 degrees/second.
- Charge 2 seconds, one sustained blast 2 seconds, cooldown 3 seconds.
- Firing Tracking Degrees Per Second 10 follows the player during fire. Zero
  enables a locked blast, braking tracking near the end of the charge.
- Tracking Ease Seconds .25 and Firing Ease Seconds .35 smooth acquisition,
  settling, speed changes and target-loss stopping. The aiming cone still limits
  rotation; pause freezes the velocity as well as the pivot.
- Beam Range 16, radius 0.1, player mass damage 0.08/second, with a short opening and
  closing envelope. Ordinary colliders and arena bounds clip the beam. Shields
  block through the real player shield/mass-loss rules. Resonance remains a solid
  beam obstacle, as with the Particle Accelerator; the Ranged Drone bullet's
  pass-through behavior is specific to its projectile.
- Three health, one damage per sword hit, generic ENEMY_DEFEAT reward from the
  current score economy. These are prototype balance values on ED_ParticleBeamTurret.
- A 2-second arrival warning, 1.1-second staggered face tracing, then collision and
  detection activate. On defeat, combat stops immediately and faces break up for
  0.8 seconds before shared destruction/bookkeeping completes.

ParticleBeamTurretVisuals exposes idle breathing, ring rotation, independent charge
jitter, core growth and firing mouth opening. Charge displays a thin opaque dashed aim line,
grows and agitates the core, and tightens the panel motion. The ring hinges open
during the long blast. Movement and presentation use the shared enemy pause gate.
Disable/death cancels the beam and any pending warning. Target loss during charge
cancels to cooldown; a fired, locked beam finishes along its announced direction.

The turret reuses ParticleAcceleratorBeamVisual and the power-up's beam material.
The renderer fits a narrow mesh around the segment. Its legacy power-up mode respects the actual
48-ball budget and authored spacing, and connects the finite sphere chain with a
tapered SDF tube so long beams cannot develop gaps. The beam shader writes the
raymarched surface depth instead of the bounding cube depth and fixes the
orthographic camera ray direction. The turret supplies its active attack clock
to freeze beam flicker during pause. The power-up retains its projectile behavior.

The turret opts into a V3DLaser_09-inspired opaque plasma treatment on that shared
renderer. Both white and black are separate volumetric SDF tubes with independently
flowing centers and radii. They pass above/below one another and leave actual empty
space between strands; colors come from the nearest tube surface, not an angular
texture on a single white body. Eight independently phased strands bend at different
rates, reverse local twist, overlap and taper to points. At eight strands, six are
white and two black; White Fraction controls their relative cross-sectional area.
The prefab uses .756; projected coverage varies with taper, depth and view.
Each tube uses up to 65 sampled cross-sections in fixed reusable buffers, with
conservative distance bounds and fitted mesh extents. A thin white feeder prevents
the entire bundle vanishing at once. Tube positions/radii share the paused clock.
The curve is a small visual displacement around the beam axis, with the muzzle
and contact endpoints pinned; gameplay uses the original straight sphere trace.
There is no alpha blending, glow, lighting gradient or alpha fade in the plasma.
The shader outputs exactly black or white and actual surface depth.

Beam controls on the **Particle Accelerator beam** child, under **Opaque plasma
(turret)**: White Fraction (.756), Strand Bend Frequency (.8), Strand Flow Speed (16),
Thickness Wave Amplitude (.914, adjustable from 0 to 1), Wave Length (3),
Curve Amplitude (1.117 times radius, adjustable from 0 to 2). **Plasma stranding** exposes Strand Count (8),
Strand Wander (1.615), Strand Taper (.283), and Strand Separation (1.201; increase to open
more space between the 3D strands). The original serialized
swirlTurnsPerUnit / swirlSpeed fields are retained under the new Inspector labels.
**Beam thickness ranges (visual multipliers)** provides Overall Thickness plus
Start Thickness, Body Thickness and End Thickness. Each foldout has Minimum and
Maximum sliders (0..4); equal limits fix a section's thickness. The applied prefab
ranges are Overall .8–1.4, Start .14–.51, Body .83–1.53 and End .5–1.
The overall multiplier combines with the local section's
multiplier. Start/body/end blend over Muzzle Ramp World at either end of the beam.
Thickness Variation Speed (4) controls range animation using the paused beam clock;
the body variation travels along the length. These visual controls layer on the
existing radius, endpoint multipliers, waves and opening/closing envelope. They
do not change the controller's straight collision trace or damage radius.
The root controller's **Beam opening pulse** controls the initial 1.7x radius,
settling over .32 seconds. Beam Fade Seconds is .36 on the prefab; during this final
interval both radius and length shrink back into the core, stopping contact damage
and emission as the beam tip leaves the surface.

The **Sustained contact plasma** child pools at most 20 drops / 45 SDF balls. A
pulsating coating sheds connected droplets which drift down the arena (-Z), stretch
and shrink to zero. Inspector controls include Drops Per Second (22), Drop Lifetime
(.65), Drop Radius (.065), Splash Speed (.65), Contact Radius (.17), Drip Acceleration
and White Fraction (.75). Residue drains during cooldown; disable/death clears it
immediately. Its clock is advanced by the turret so pause freezes all droplet motion,
emission and shrink. Both render volumes fit their live bounds, including separated
old/new contact points, and reuse buffers/meshes without per-drop GameObjects.

The **Core firing plasma** child reuses that same pooled opaque effect at the
beam/core connection. Its smaller drops sparkle outward only during firing:
28 drops/second, .75-second lifetime, .1 radius, .9 splash speed, .075 contact
radius, .426 White Fraction and no drip acceleration. The controller's Core Plasma Forward Offset (.1)
places the emitter in front of the pivot/core center along the firing direction.
It continues on missed shots, follows the blast envelope, freezes on pause,
drains during cooldown and clears immediately on disable/death. Tune its own
Particle Beam Plasma Contact component independently from the far-end contact.

Authoring menus: MASSIVE > Enemies > Particle Beam Turret > Create Prefab /
Place In Player Actions / Validate In Player Actions. Setup preserves existing
asset tuning; placement supports Undo. Validation runs temporary fixtures in the
actual scene, returns to Edit Mode, and writes reports and renders to
Library/ParticleBeamTurretValidation. Production spawn schedules and final balance
are outside this prototype.

Validated in Unity 6000.0.28f1: 88 Play Mode checks passed with no runtime Console
errors. Covers both mounts, regular base triangles, references, warning/face reveal,
stationary tracking, range and arc gates, charge and cooldown timing, sustained
damage, aim lock, target loss, walls/bounds, real held shield, pause, disable/reuse,
death/score/cleanup, and the original Particle Accelerator hit contract. Rendered
checks verify a continuous 24-unit thin beam, fitted bounds, foreground/background
depth occlusion, and orthographic/perspective rendering. Additional plasma checks
cover the thick opening, receding tip, sustained contact, paused/detached droplets,
bounded fitted volumes, curved/rippling geometry and independently animated strands.
Rendered cross-sections now contain true empty gaps between independent tubes.
Isolating each color proves both white-in-front and black-in-front crossings.
The reference fixture's isolated un-antialiased render measured 75.2% white / 24.8% black with
zero intermediate pixels. Thickness wave amplitude 1 and curve amplitude 2 are
checked for rendering and bounds coverage. Start/body/end and overall thickness
profiles are checked independently, including full disappearance at zero overall
thickness. Core sparkle is checked before/during firing, on misses, through pause
and cooldown, and on disable/death.
Pause freezes the complete tube buffer, and full retraction clears every strand.
Existing player-spawn
kinematic-velocity warnings remain; EnemyBase pause now avoids such writes for
already-kinematic stationary enemies.

Apparatus checks cover continuous base topology, the dome's 2V topology and radius,
all 64 arrival faces, edited height/collider agreement, pivot-following dome panels,
zero-inset placement on both walls, and eased acquisition/firing/target-loss motion.
The applied prefab's beam is also rendered using the user's settings; canonical
renderer tests use a separate stable fixture without modifying the prefab.
