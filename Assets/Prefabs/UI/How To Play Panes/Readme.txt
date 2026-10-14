HOW TO PLAY - PANE EXPERIMENT

Open Assets/Scenes/S-0_HOW-TO-PLAY-PANES.unity and press Play.
The original How To scene and production scene flow are unchanged.

Edit a pane by opening its prefab in this folder. Each prefab contains its
title and instructions in the top boxes, with supporting labels, live gameplay
demonstrations and animated controller diagrams under Demo area in the lower box.
The scene owns the boxes, HOW TO PLAY MASSIVE heading, page number and navigation.

COMBAT has three columns: Thrust against one Drone, a full Thrust/Sweep/Repulsor
combo against multiple Drones, and a charged Thrust through three Drones.
Each column has its own camera, real Player actor and input mirror. The three
How To scenario assets in this folder configure these demonstrations. They use
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
The carousel now has four panes: Combat, Defending, Multipliers and goals,
and Power-ups. The original three individual power-up prefabs remain available
as source assets but are no longer in the scene's sequence.

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
on a pane uses that default when zero; COMBAT, Multipliers and goals, and POWER-UPS
use 16 seconds.
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
