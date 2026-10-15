HOW TO PLAY - PANE EXPERIMENT

Open Assets/Scenes/S-0_HOW-TO-PLAY-PANES.unity and press Play.
The original How To scene and production scene flow are unchanged.

Edit a pane by opening its prefab in this folder. Each prefab contains its
title and instructions in the top boxes, with supporting labels, live gameplay
demonstrations and animated controller diagrams under Demo area in the lower box.
The scene owns the boxes, HOW TO PLAY MASSIVE heading, page number and navigation.

COMBAT has three columns: ATTACK builds from Thrust through Sweep and Repulsor
against multiple Drones; CHARGE sends a charged Thrust through three Drones;
DEFEND shows a held block, a timed parry, and an immediate counter-thrust.
Each column has its own camera, real Player actor and input mirror. The three
scenarios are How To FULL COMBO, How To CHARGED THRUST, and How To DEFEND.
DEFEND mirrors the defending player's controls. The demonstrations use
shared gameplay attack tuning and the canonical Drone definition, health,
hurtboxes and death effects; stationary targets are isolated from live matches.
All pane scenarios use Actor Scale = 1, preserving the current gameplay player
size. Enemy prefab scales are unmodified. Match Screen World Scale on each
PlayerDemoView keeps pixels per world unit equal to the main camera (the same
orthographic scale as gameplay), so every pane shows the same native size.
The remaining panes use local scenario copies to keep this experiment isolated.

POWER-UPS combines Time Dilation, Particle Accelerator and Decoherence in three
columns, each with its own demonstration, action label and animated controls.
Edit 04 Power-ups.prefab to change this pane. Its accelerator scenario uses a
closer target so the complete encounter fits its column at native gameplay size.
The carousel has four panes: Combat, Enemies, Multipliers and goals, and Power-ups.
The separate Defending prefab and original three individual power-up prefabs
remain available as source assets but are no longer in the scene's sequence.

Decoherence now teaches Hold Shield + Move to paint a damaging four-second trail,
health-scaled enemy exposure, a shrinking brush as charge drains, release to refill,
and reactivation from 15%. Its canonical-player demo claims the pickup, paints past
two normal Drones, releases Shield, and retreats while the trail finishes the second
Drone. The Shield control stays lit while held. The field, grid echoes and split
charge rings all come from gameplay. No health, charge or effect lifetime is reset.
After the Drones die, the current Dyson Sphere forms and the player paints a new
field around it. Repeated passes retain the Dyson's normal health and panel erosion;
fresh pickups are claimed from the clear side when the equipped power-up expires.
The optional Decoherence Second Enemy reference selects this follow-up. The assigned
Drone Definition enables the paint sequence; legacy scenarios without one retain
their stationary activation. Supporting captions remain editable in the prefab.
The header icon is a nested copy of the normal Decoherence pickup. Its manifest
animation replays on pane activation; pickup logic, colliders and tether physics
are disabled so it remains visible until the pane is deactivated. Shared pickup
prefabs and gameplay lifetime are unchanged.
Validation covered a complete Drone/Dyson sequence at accelerated playback,
normal-speed painting and damage, a 40-second header lifetime, and navigation
away/back with no remaining fields or enemies and a fresh icon entrance.

03 Enemies.prefab uses seven evenly spaced columns based on the authored DRONE
group: Drone, Ranged Drone, Seeker, Particle Beam Turret, Carrier, Dyson Sphere
and Amplified Dyson Sphere. The old lunge Dyson and the compatibility definition
are not separate enemy types. Each group retains editable text, native-scale
enemy models, five stat rows and a movement/attack description. The three-Drone
formation is preserved. Models keep their idle visuals, with gameplay disabled
only on these instruction-pane instances; shared enemy prefabs are unchanged.

Enemy bars are relative visual levels, authored from current gameplay tuning.
Each row uses sqrt(value / highest displayed value) so small values remain
readable; zero is empty. Health uses definition health, speed uses normal movement,
score uses the unmultiplied defeat reward. Attack uses damage per hit (Turret:
one second of beam damage). Range uses projectile reach, lunge distance, Dyson
attack distance, or Drone contact radius. Carrier has no direct attack/range:
its spawned Drones are represented separately. Bars are editable Shapes rectangles;
they do not automatically change when gameplay tuning changes.

Keep top text inside x=-12..12, z=4.61..7.61; the left title box ends at x=-3.
The lower demonstration box is x=-12..12, z=-6.37..4.63. Camera bounds on each
PlayerDemoView define its visible region. Remote stages must stay separated.

TMPTextTransition on the scene heading and pane titles uses the existing SDF
Grow + Spacing Shrink configuration from InstructionsPanel-NOVA / Anomaly Type.
It changes the SDF material, never the text object's scale. Descriptions use
the same prefab's Instructions body transition: Typewriter + Alpha with the
per-letter fade. Both retain that reference's one-second timing and easing.
Timings and effects can be edited on each text object; pane text replays whenever
its prefab is activated. Supporting labels remain static.

To add a pane, duplicate one of these prefabs and edit it. Drag the new prefab
under How To Play Carousel / Panes - add and reorder prefab instances here.
Keep the pane root at position (0, 0, 0), rotation (0, 0, 0), scale (1, 1, 1).
Reorder those children to change playback order. Remove an instance to remove
it from the sequence; inactive instances are still included. For an Edit Mode
preview, enable just the pane you want to see.

The carousel discovers its direct child panes when the scene starts and binds
each demo's screen camera. Only the selected pane runs. Its demonstration
restarts when revisited. Every pane loops back to the first after the last.

Seconds Per Pane on How To Play Carousel is 12 by default. Duration Override
on a pane uses that default when zero; COMBAT and Multipliers and goals use 16
seconds. POWER-UPS uses 38 seconds to include the Dyson follow-up and repainting.
Any player's MoveH joystick action navigates once per push, with a deadzone.
Left/right arrow keys are also available for editor testing. Navigation resets
the white bar. The timer uses unscaled time.

The scene's arrow background / default general area keeps the authored circle's
position and orientation. Its Disc is disabled; the Navigation metaball surface
child uses the existing MASSIVE/MetaballSDF shader with a dedicated white material.
HowToPlayNavigationMetaballs exposes Metaball Count (1-48, including the center),
Metaball Size (base radius in local units), and Size Variation (0-1; zero makes
every ball equal). The center is largest when variation is enabled; a single
ball always uses the base size. Defaults retain the original seven-ball look.
Radius controls the cluster's spread separately from individual ball size.
Idle Speed, Idle Drift, Push Distance, Push Seconds and Settle Seconds control
motion. The render volume expands to fit larger balls. These settings also
update the resting shape in Edit Mode.
Manual left/right navigation pulls the shape toward that
direction with ease-in-out motion, then returns it to idle. Quick reversals begin
from the current deformation. Automatic pane changes leave the ambient motion
undisturbed. Play the scene to preview motion; Edit Mode shows the resting shape.

How To Play Carousel also owns HowToPlayDemoWipe. Manual changes and automatic
advance reveal the lower demo area with a directional fluid edge: right moves
left-to-right, left moves right-to-left. Duration defaults to 0.45 seconds;
Ripple Amount and Edge Softness tune the boundary. Frame Inset leaves the box
outline intact. The headers, navigation and Begin prompt stay outside the wipe.
The outgoing pane is captured once after all cameras render. Incoming demos
prepare at their native scale and wait to start their actions until revealed.
The pane timer starts after the reveal. Rapid navigation queues the latest
requested pane; it does not interrupt an in-progress wipe with a hard cut.
One GPU capture texture is reused and released when the transition is disabled
or the scene closes. A resolution change ends the wipe to avoid stretching it.

PRESS ANY BUTTON TO BEGIN retains the original text animation, 3-second input
wait, Sword/Shield actions for any player, Return fallback, and Level Select
destination through HowToPlayScreenController. The experiment includes the
existing Rewired bootstrap so it can be played directly from the Editor.

The alternate scene is intentionally outside Build Settings and is not wired
into SceneFlow.GoToHowToPlay. Open it directly to compare the experiment.
