# LATTICE

Choose **LATTICE / STAGE_001** in Level Select, or open `Assets/Scenes/S-1_LATTICE.unity` directly. The existing roster, input, combat, encounters, objectives, camera and 28 x 12 arena remain the foundation. `LevelDefinition-LATTICE` is registered first in the level catalog at scale exponent -35, and its scene is enabled in Build Settings. The shared `LevelSceneContext` supplies the same identity on direct and carousel launches. LATTICE has its own instruction card and uses the shared gameplay audio profile and match economy.

## Field and presentation

Select `PLAYING FIELD / GRID / VectorGridGPU`, then its **Lattice Disruption Field** component. Drifting Noise creates slowly changing disconnected patches. Domain-warped, layered noise translates and evolves, so the shapes can form, disappear, merge and split. Each horizontal/vertical connection samples the field and has hysteresis to avoid threshold chatter.

Connections briefly become loose, flutter and retract. When one endpoint is disrupted, a single whole tether detaches from that endpoint and retracts toward the intact point. A disruption crossing the interior between two intact endpoints creates two free strands. Reconnection grows the strand(s) back across the missing span. Dots replace the missing connections. The field stores actual strand extension, and both the dots and locomotion read the resulting node connection state.

**Hold Tips At Boundary** holds free strand tips at the actual evolving noise contour instead of retracting all the way. Each intact endpoint keeps only the portion up to its first contour crossing; disrupted endpoints do not sprout an opposite tether. The tips stay on the contour while the strand bodies flutter. This switch is authored in the Inspector and also applies during gameplay.

Loose threads use cubic curves with independently drifting control points. Smooth, aperiodic noise changes their bends and free-tip drift, rather than sending a sine wave down each strand. Every strand end has a stable random identity controlling its resting lean, slack, drift rates and time offsets, so even opposite ends of one broken connection move differently. A quadratic looseness ramp keeps the connected root and its tangent taut, progressively releasing the bend toward the disconnected end. Held tips stay exactly on the contour; their interiors can bow and drift. Connected threads straighten as before.

Disrupted dots consist of separate red, green and blue circles with independent random offsets. Blue includes a small green contribution (30%) to shift it toward cyan for visibility on black; its ghost trails share the tint. The layers combine into yellow, cyan or tinted magenta where two overlap, and white only where all three overlap; there is no white dot underneath. Jitter fades to zero at the disruption contour and increases with geometric distance into the region. Distance is measured against a sampled noise contour, including nearby areas outside the arena, and reaches full strength after the configured number of grid cells. The contour/depth cache updates at up to 20 Hz; channel offsets animate every frame using the field clock, so pausing freezes them too. Dot color never changes the grid coordinates or locomotion state.

Optional ghost trails place six fading afterimages along each RGB layer's recent jitter path. Older echoes become smaller and fainter while the current dot stays crisp. History is evaluated from the same seeded motion at fixed ages, independent of frame rate, using the current boundary-depth strength. The trails freeze with pause and collapse with zero jitter. Per-channel maximum coverage avoids brightness accumulating when a dot stays still. They use the existing draw without persistent history textures or objects.

The normal grid's GPU simulation, player attraction, Amplifier deformation and Repulsor displacement feed the replacement rendering. Disconnected points settle to the fixed lattice coordinates used by movement. The original independent border remains visible. All meshes, materials, textures and buffers are transient and released on disable. Disabling the field restores the original grid renderer.

Useful controls:

- **Noise Frequency:** higher values make smaller regions.
- **Threshold:** lower values disconnect more of the field.
- **Drift / Evolution Speed:** translation and changing shape respectively.
- **Retract / Reconnect / Loose Seconds:** strand timing.
- **Thread Looseness / Breeze Speed:** amount of slack and the rate of gentle drift. These retain the old Flutter Amplitude / Frequency serialized values. Speed zero freezes normalized thread shapes.
- **Strand Variation:** how independently the threads differ in shape, slack and motion; defaults to 1 for full variation.
- **Strand Motion Seed / Randomize Strand Motion:** reproducible thread shapes and motion or a new random arrangement. The button supports Undo and changes only this seed, leaving disruption noise and gameplay untouched.
- **Line Width / Dot Diameter:** pixels referenced to 1080p, scaled for higher resolutions.
- **RGB Dot Jitter:** enable or disable color separation; disabling restores white dots.
- **Dot Jitter Radius / Speed:** maximum per-channel offset in pixels at 1080p, and random position changes per second. Radius zero produces white dots; speed zero freezes the offsets.
- **Dot Jitter Depth:** distance in grid cells from the noise boundary where jitter reaches full strength.
- **Dot Jitter Seed:** reproducible independent motion for every dot and color, separate from the noise and strand seeds.
- **Dot Ghost Trails:** enable or disable the RGB afterimages.
- **Dot Trail Duration / Opacity:** seconds of recent jitter motion to show, and the visibility of its fading echoes. Opacity zero restores the untrailed dots.
- **Connected / Disconnected modes:** comparison controls for tuning and testing.
- **Animate In Editor:** preview while the component's Inspector is open. Preview progress is transient; Play Mode starts a fresh field.
- **Show Noise Boundary:** faint dashed contour in the Editor's Scene and Game views, independent of the general Gizmos switch. This guide is compiled only into the Editor, including Editor Play Mode, and never appears in a player build.

## Movement

Each scene actor has a **Lattice Player Motor** bound to this field. `PlayerControllerScript` calls it before ordinary traction. It uses the player's existing input frame, deadzone, maximum movement speed and movement modifiers. There is no additional input polling or alternative player prefab.

Within sufficiently disconnected regions, the player snaps to a reachable corner and holds there between eight-direction steps. The distance clock shares the ordinary controller's traction calculation: analog input, acceleration, mass, speed cap, movement modifiers, braking and Rigidbody damping. Fractional distance carries over after each hop, and diagonal hops consume their longer world-space distance. Over a straight traversal, visible movement trails continuous movement by at most the next hop; it does not lose distance every physics tick. Swept sphere and destination-overlap queries respect physical blockers, collision-layer/pair exclusions and arena body clearance. An occupied destination blocks the step and resets its distance credit. Separate entry/exit thresholds stabilize region transitions.

This first version quantizes joystick locomotion, including shield-slowed movement. Attacks, movement abilities, stun and protected knockback retain their existing motion, followed by a short recovery before reacquiring the lattice. Aim remains continuous. Enemies, projectiles, pickups and the Amplifier are not quantized. Pause freezes noise and locomotion together.

**Quantize Movement** permits a visual-only comparison; **Step Speed Multiplier** tunes traversal relative to the shared player's maximum speed. Changing the common grid scale changes both the rendered corner spacing and the step spacing.

## Setup and validation

`MASSIVE > LATTICE > Install in current scene` adds missing components and bindings without replacing the scene or overwriting field tuning. The saved LATTICE scene is already installed. Common player/playing-field prefabs are not modified.

`MASSIVE > LATTICE > Validate prototype` (Ctrl+Shift+Alt+L) starts a temporary Play Mode fixture using the canonical player. It checks connection transitions, movement in all eight directions, diagonal timing, collision blocking, arena edges, input locks, action ownership, pause and disable/re-enable cleanup. It captures the actual scene and field renders in `Library/LatticeValidation`. Validation exits Play Mode and does not save its fixtures or tuning.

Validated 2026-10-03 in Unity 6000.0.28f1 on DX11: all 92 Play Mode checks passed, with no errors or exceptions in the completed run. Coverage includes single-tether endpoint cuts, interior cuts with two tethers, moving boundary tips, toggling full retraction back on, changing the strand motion seed, and four comparisons with an ordinary canonical player. Full-stick cardinal/diagonal, partial-stick and increased mass/drag comparisons matched total continuous distance to three decimal places (including the pending hop fraction). Actual gameplay, strand states and the independent dashed editor guide were visually inspected. Close-up breeze renders at successive times and with an alternate seed were reviewed; the Inspector randomize button and Undo were verified in Edit Mode. RGB depth was compared against an independent radial search for the contour. Rendered-pixel checks verified separate RGB layers, white and secondary-color overlaps, animation, pause, and disabling the effect; normal-scale and enlarged diagnostic dot captures were visually reviewed. Trail captures verified visible afterimages, preserved dot heads, opacity and duration controls, pause, and no halo at zero jitter; trails-on/off and longer-history renders were visually reviewed. Existing player-spawn warnings about setting velocity on kinematic bodies remain. No standalone build or controller feel playtest was performed.

Design context: [current MASSIVE GDD](https://www.notion.so/11e617c6d8ae4eeda7f0424645db5203), read 2026-10-03 (page last edited 2026-09-28), especially Player Core Physics, Player Actor System, The Grid and Art / Visual Direction. The user's LATTICE brief supplies the new stage behavior.
