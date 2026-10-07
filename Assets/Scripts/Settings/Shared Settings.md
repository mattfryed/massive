# Shared project tuning

## Power-ups

Open **MASSIVE > Power-up Settings** (also linked from Shared Settings).
`Assets/Power-ups/Resources/PowerUpSettings.asset` is the authoritative profile
for every scene and for builds. Existing spawners, pickup animators, lifecycle
timers and toast systems read it automatically; no scene migration or per-scene
profile assignment is needed. Power-up consumers have no local tuning opt-out.

- **Visuals:** shell and inner-icon scale, wire thickness/color, spawn duration,
  inner start fraction, edge timing/speed variation, metaball and particle intro/
  acquisition timing, and face separation, rotation, undraw and easing. Natural
  despawn exactly reverses the configured spawn timeline. Shell Scale also fits
  both the pickup trigger and solid sphere collider to the shell radius. Inner
  Scale affects only the representative icon. Live pickups refresh together
  when the profile changes; collider sizes are recomputed without accumulating scale.
- **Life cycle:** uniform world lifetime (zero means unlimited), cleanup delay,
  optional use of the shared per-definition lifetimes, and equipped-duration multiplier. These
  apply on the next spawn/equip; active timers finish normally. Explicit tutorial
  never-expire flags and demo duration overrides remain respected.
- **Spawning:** master switch, minimum/maximum delay, per-scene concurrency cap,
  border/player/pickup clearance, blocking mask, placement attempts, and a spawn
  chance mixer. Changing a percentage redistributes the others proportionally.
  The mixer edits the shared definitions' existing weights. Zero excludes a
  type; all-zero weights or a zero cap prevent standard spawns. Duplicate active
  spawners elect one scheduler per scene and share the cap. Scripted demo pickups
  are excluded from the standard spawn count.
- **Toasts:** prefab, intro/total/outro duration, scale, placement, description
  toggle/size, panel padding, maximum width, billboard, sorting and easing.
  Typography and border artwork remain in the shared toast prefab.
- **Scene setup:** Install / Repair reuses or adds the existing PowerUpSpawner
  and PowerUpPickupToastSystem, fills missing grid/camera/prefab references,
  preserves disabled demo systems, supports Undo and marks the scene dirty.
  Save that scene after installing. Existing bindings are not overwritten;
  select the spawner Inspector to change its grid. A grid must exist first.

Each catalogue definition gets its own tab with its real gameplay fields and
Open/Ping links to pickup, projectile, shield and other referenced prefabs.
Edits save immediately, including in Play Mode; Undo/Redo is supported. Settings
validation checks missing references and duplicates. Full regression validation
is available from Scene setup in PLAYER ACTIONS and writes its report under
`Library/PowerUpIconDemoValidation`. Scene-local grid, camera, owner and active
state remain local. Scene-local legacy tuning is retained only as a fallback if
the global asset is absent; normal project operation always resolves the asset.

## Other shared settings

Open **MASSIVE → Shared Settings**. The same appearance controls in **Player → Melee Visual Preview** and **Amplifier Treatments** now edit these shared assets by default. **Resonance → Shared Formation & Timing** and **Text Animation → Shared Settings** open their respective tabs directly.

Changes made through these menus save immediately, including during Play Mode. Undo is supported. The assets are in `Assets/Scripts/Settings/Resources`, so scenes and builds resolve the same defaults without depending on a previously opened scene.

| Profile | Shared across scenes | Kept local |
| --- | --- | --- |
| Melee | Treatment selection, thrust/sweep/Repulsor layers, materials, colors, density, motion, lingering trails, Repulsor body feedback and grid pulse | Attack-controller and collider bindings, player-specific reference dimensions, preview stage, playback speed, looping and scrub position |
| Amplifier | Goal treatment layers and wave settings, Core scale/physics/lifecycle/surface motion, pull and capped-goal repulsion strength, capture/max toast appearance | Goal team and capture point, capture/attraction radii, grid and mass-surface references, canvas, preview state |
| Resonance | Formation/dissolution animation, grain condensation and vibration, encounter delays/variation, time-out and warning duration | Pattern order, geometry definitions, neutral/no-go regions, anchors, field dimensions and standalone example selection |
| Text | Score-digit preset and intensity, shared tier-style profile and its promotion/loop presets | Text targets, layout references, sample text, test score/multiplier, playback and preview range |

Each participating component has **Use Shared Project Settings**. Disable it for a local exception: its original serialized scene/prefab values are retained. Each profile also has **Shared Enabled**, which restores local values for the whole group when disabled. A missing shared asset falls back to local tuning. Blank material/font or text-preset assignments retain the component's local resource fallback.

The initial profiles were seeded from the current Dynamo setup, including P1's melee treatment, the encounter's Core prefab, the scene's Resonance animation, and current HUD tuning. Existing scene and prefab data were not overwritten. Shared defaults do not install missing gameplay systems into a level.

Gameplay attack timing, reach and damage remain in the existing **Player Attack Profile**. Text animation modules remain reusable **Text Animation Preset** assets; the Text tab assigns them and provides direct access to the score preset and tier-style library.

Developer note: public/local serialized fields retain their original names for compatibility. Rendering and gameplay consumers resolve shared values through effective accessors. Code that deliberately needs an instance variation should set `UseSharedSettings = false` before changing its local fields. Editor preview-scene fixtures retain local values; normal loaded scenes and runtime spawns resolve the shared Resources assets. No scene object references or live preview state are stored in those assets.


## Original Particles

**MASSIVE > Shared Settings > Melee** and **Player Tuning > Appearance** expose three Original Particles prefab assignments and Open buttons. The assets live in `Assets/Prefabs/Player/Attack Stages/Original Particles`.

Thrust and Sweep each contain the actual **GPU Particle Trail** emitter. Their particle count, length, widths, speed, drag, lifetimes, sizes and emission rate are now independently owned by each prefab. The window's GPU foldouts edit and save those same prefab fields during Play Mode. Open the whole prefab to add Particle Systems or other effects. Saved prefab changes refresh running actors, including new children. The root/emitter Inspector provides a looping Scene-view preview.

Repulsor keeps its existing body/grid feedback and has a disabled optional particle layer. Enable it in the prefab to add particles. Each stage has independent placement, activation gating and a bounded visual tail; gameplay timing, hitboxes and damage remain in the attack profile.

The old `legacyParticles` group remains under **Legacy fallback (actors without stage prefabs)**. Player-root local fields and external/Time Dilation configuration are retained. Stage-prefab GPU emitters always use their own authored values. Player Tuning snapshots capture prefab assignments; whole prefab contents use their own asset history.

See **Editing these effects.md** beside the three prefabs for the full workflow.

## Thrust steering

**MASSIVE > Player Tuning > Movement > Thrust steering** exposes **Max turn from start (each side, degrees)**. The default is 15 degrees per side (30 degrees total). The heading captured at Thrust start, including initial aim assistance, remains the center for the entire stage; repeated small turns cannot move the center. The constraint applies to the combat-facing root and its damage collider, body-facing target, and attack visual direction. Scripted aim uses the same rule. Thrust displacement keeps its existing stage-start movement direction.

A value of 0 locks facing; 180 restores unrestricted turning. The setting saves during Play Mode through Player Tuning, participates in tuning snapshots, and has the existing per-component local fallback. Sweep, Repulsor, idle movement, and facing after completion/cancellation retain their normal turning.

## Particle Accelerator plasma beam

**MASSIVE > Power-up Settings > Particle Accelerator** edits the shared sustained-beam definition. Every acquisition starts with a full dotted energy ring. Sword fires immediately and holding it sustains the beam while draining energy. Release stops firing once the minimum burst has elapsed; idle time immediately refills the ring. Any energy above zero can fund a new press, and low energy always caps the burst. At exhaustion the beam stops; release and press again to use recovered energy. The power-up's normal lifetime still limits use.

**Full Meter Fire Seconds** defaults to 6, **Empty To Full Refill Seconds** to 4, and **Minimum Burst Seconds** to 1 (one sixth of a full meter). All three use gameplay time and pause with match input locks. A tap cannot grant free energy, refill while firing, or restart itself after exhaustion. The old charge time and post-shot cooldown are retained only for asset compatibility. The **Energy ring appearance** foldout edits the existing shared ring prefab; its old charge orb and aiming dots are disabled. Ring/beam prefab appearance changes apply on the next equip, while definition timing values are read live.

The beam uses the Particle Beam Turret's authored plasma strands, emitter and impact plasma, growth, opening thickness and fade. Its own shared prefab is linked from the tab. Expand **Beam, emitter and impact appearance** to edit that prefab's effects for every scene; existing spawned copies refresh on their next equip. Definition values (range, radius, DPS, movement and arc) are read while firing. Player size scales the beam width, muzzle and plasma; projectile reach scales range.

**Beam Grow Seconds** also controls extension after a target or obstacle leaves the path. Growth resumes from the last blocked length along the initial growth curve; newly exposed targets receive damage and impact plasma only when the tip reaches them. A nearer obstruction clips immediately.

Under **Damage / Mass**, **Player Damage Per Second** controls player mass loss before shield reduction; **Enemy Damage Per Second** controls enemy health loss independently. Both initially remain at 0.08, preserving the previous balance. Changes apply live across scenes, and zero disables damage only for that target type while keeping beam contact effects. **Transfer Mass To Shooter** applies only to mass removed from players. Enemy damage never grants player mass.

Under **Firing movement**, **Movement Speed Scale While Firing** scales propulsion and the movement speed limit (default 0.65). **Turning Speed Scale While Firing** scales the player's normal maximum turning speed for the beam's firing direction and combat facing (default 1). Both range from 0 to 1: 0 disables movement input or locks the firing direction, 0.5 gives half speed, and 1 gives full speed. They apply throughout a minimum tap burst and update live; release after that burst, exhaustion and unequip restore normal control. **Aim Direction Ease Seconds** (initially 0.2 seconds) smooths turning acceleration, braking and reversals while firing; zero restores the constant-speed response. Easing respects the turning speed scale, including a complete lock at zero, and resets when firing ends. These controls affect the beam's actual collision direction before distance-based arc propagation.

Acquiring the power-up cannot reuse that frame's attack press or a held collecting attack. Release and press Sword again to fire. The equipped, idle player displays the turret-style white dotted aim guide, clipped at obstacles and eligible targets; the guide hides during firing and death. The energy ring shrinks with the player's death effect, remains hidden while the player is hidden, and reforms on respawn if the power-up has time remaining. Death stops the beam immediately and requires a fresh press after respawn; it does not change the existing power-up duration policy.

**Turn Propagation Speed** is world units per second: lower values create more lag and bend. **Max Turn Delay** caps the stored delay (zero restores instant aim). **Max Bend Angle** limits delayed tangents during sharp reversals. The muzzle follows the player immediately; aim history propagates outward along the beam. Range is measured along its forward axis. Collision sweeps the same animated, curved plasma bundle and clips at the first eligible player, enemy, shield, obstacle or arena edge. Shield parries and stun interrupt even a minimum burst; retraction after firing stops never deals residual damage.

Use **Validate sustained beam in PLAYER ACTIONS** with that scene open in Edit Mode. Fixtures and cloned tuning are discarded on return to Edit Mode. Results and a rendered arc image are saved under `Library/ParticleAcceleratorBeamValidation`. The old pulse projectile remains only for legacy prefab compatibility; the equipped power-up uses `ParticleAcceleratorSustainedBeam.prefab`.
