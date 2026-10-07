# MASSIVE project context â€” resonance task, 2026-09-06

Confirmed live project: `C:/Users/mattf/Documents/GitHub/massive`; inspected commit `02f5e1872c8d7cbd3784a17c1d74ebe3dcdeee5f`. This ChatGPT workspace is a separate incomplete reference mirror; `sources/` remains read-only.

- Unity 6000.0.28f1, Windows standalone target. Built-in render pipeline confirmed in Editor and GraphicsSettings, despite installed URP 17.0.3. Legacy input handler is active.
- Coplay Unity MCP v10.1.2 is installed and the editor is connected. Scene inspection, compilation, code execution, and isolated physics checks are available.
- Relevant first-party code uses MonoBehaviours, ScriptableObjects, private serialized references plus some public authoring fields, and feature namespaces (Massive.Player, Massive.Multiplier). No first-party gameplay asmdef was found; vendor assemblies have their own asmdefs.
- `Assets/VectorGridNu/ArenaBoundsFromVectorGrid.cs` provides an optional grid reference, BoundsSnapshot, world containment, and explicit RefreshNow. Grid coordinates are local XY, rotated into gameplay XZ. Current grid: position (0,-0.09,0), rotation (270,0,0), size 28x12, unit scale.
- `Assets/VectorGridNu/ArenaBoundaryCollidersFromVectorGrid.cs` generates physical bounds, deferring inspector rebuilds. Resonance does not modify that builder.
- `Assets/Power-ups/Amplifier Core/AmplifierCoreGameplay.cs` owns core lifecycle and attack impulses, with scoring owned elsewhere. It uses Rigidbody.linearVelocity and XZ thrust; prefab has mass 3 and continuous collision detection. Scoreboard copies are presentation-only.
- `Assets/Scripts/Player/Actions/PlayerAttackController.cs` provides StopAtSolidImpact and line-of-sight masks. Resonance does not alter attacks.
- Active scene was `Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity`, already dirty in Editor and modified on disk. Existing gallery files under Assets/Editor and EditorUserSettings were also changed; preserve them.
- Build settings start with S-0_ATTRACT, then onboarding/menu scenes, gameplay stages, and POSTGAME. The prototype is not an enabled build scene.
- No first-party test assembly or direct Unity Test Framework dependency was found in inspected files. Resonance adds a dependency-free editor validation menu; it does not install packages.
- No current GDD was located in the available mirror or targeted repository search. The user's current 120-second match and scoring description supplied design context. Network implementation and whole-project test/build health were not audited.

New feature: `Assets/Resonance/README.md` describes structure, integration, controls, defaults, tests, and limitations. Scope: additive obstacle prototype, no changes to existing scoring, player, core, arena, package, or project settings files. The task leaves the scene unsaved and saves a reusable prefab plus pattern/material assets.

## Player action arena worktree â€” 2026-09-28

Active Unity project: `C:/Users/mattf/massive`, branch `v5`, commit `b505e7c7`. Verified against running Unity Editor process 12484 and the updated Codex project configuration. The arena work was transferred here from the temporary worktree with byte-identical assets and .meta GUIDs; the temporary branch/worktree is retired. The historical live-path/MCP statements above describe an earlier environment, not this session.

Confirmed from this checkout: Unity 6000.0.28f1, built-in rendering (`GraphicsSettings.m_CustomRenderPipeline` is empty), Rewired input through PlayerInputFrame, Coplay MCP package present but no callable Unity MCP in this session. Local Unity batch execution is available. Current design context was read from the user's MASSIVE Notion GDD on 2026-09-28.

The planar template composes the shared Players_Gameplay roster, canonical nested PlayerActor prefab, goals, grid, MatchRuntime and HUD. PlayerDemoDirector creates non-match actors for How To Play; a scoring arena must retain normal match participation. `S-T_PLAYER-ACTIONS` reuses the composed template and adds an input-only experiment driver for manual / combo / attack-and-shield modes. It stays outside the level catalog and build list. Runtime score authority remains MatchScoreService. The separate test economy changes round length only; shared Player Tuning remains authoritative for actions and visuals.

See `Assets/Scripts/Player/Demonstrations/Action Test Arena.md` for controls and validation entry points. No package, rendering, production economy or common PlayerActor changes are required.


### Shield tap / hold experiment (2026-09-28)

The live PlayerShieldAbility now takes shieldHeld from the common scripted/Rewired
input path. Tap lifetime remains .65s; holding retains that opening parry and then
blocks all damage until release or 3s total. ApplyMassLoss centrally rejects damage
while held, including external hazard/projectile drains. Melee reserves a parry on
first shield contact and allows up to .08s for the original lunge to close half the
remaining body gap before applying the original stun/knockback. Extra parry rings
have separate lifetimes. Nugget shudder and padded-rim response are render-only
displacements shared by both nugget shader passes, leaving the simulation intact.
The action arena has a Hold shield toggle and supports shield lead times up to 3s.
Regression runner: MASSIVE > Demonstrations > Validate Shield Actions (temporary
play-mode fixture; preserves the editor's open scenes).


### Simultaneous action gallery (2026-09-28)

`S-T_PLAYER-ACTIONS` now defaults to AllDemos: seven independent PlayerDemoDirector
CombatPair scenarios cover melee tap/hold/exposed/Decoherence and Particle Accelerator
tap/hold/Decoherence. PlayerDemoGallery pauses the real match clock and encounter,
suppresses the match roster, and owns cleanup on mode changes. The non-scoring demo
actors use ConfigureDemonstration and shared gameplay tuning; equipped fixture power-ups
have unlimited lifetime. Manual / Combo / Attack + shield retain the scoring roster.
PlayerControllerScript.SharesSimulationWith scopes melee, lock-on and projectile
queries; normal match actors still share the null scope. Cross-pair physics collisions
are ignored, including after respawn. Real combat events verify loop outcomes.
Scenario assets and label material live in Demonstrations/Action Gallery. Authoring:
PlayerDemoGallerySetup. Batch regression: PlayerDemoGalleryValidation, including
repeated loops, natural death/respawn, late blocks, cleanup and manual restoration.


### Solo third gallery demo (2026-09-28)

Gallery lane 3 now uses PlayerDemoKind.SoloCombo: one canonical attacker runs the
real Thrust / Sweep / Repulsor input sequence, then returns to its centered start
position. There is no defender (13 gallery actors total). MovementAndCombo retains
the How To movement lead-in; SoloCombo skips it. Gallery physics isolation supports
single actors, and Repulsor hit queries now use the same simulation-scope check as
melee/projectiles. Normal match actors continue sharing the null scope.


### Persistent legacy GPU particles (2026-09-28)

AttackTrailGPU implements ISharedSettingsConsumer against
MeleeVisualProfile.legacyParticles. Its eleven effective numeric parameters are
read live; particle capacity changes rebuild buffers, and dispatch uses the
allocated capacity. Existing local fields and scene/compute/material bindings
are retained for opt-out and preview fixtures. Shared Settings, the component
Inspector, and Player Tuning > Appearance expose the same persistent group.
Canonical defaults seed the new fields; Thrust and Sweep share this profile.
Validation entry point: LegacyParticleSettingsValidation.RunBatch (isolated
batch Editor only), including GPU readback and persistence across Play Mode exit.

Verified in Unity 6000.0.28f1: 17 Play Mode checks plus saved-asset reload after
Play Mode exit passed. Covers all 13 gallery consumers, actual GPU readback,
capacity changes, shared/local toggles, Undo/Redo, new spawns, and seven demo
loops. Evidence: Library/LegacyParticleSettingsValidation/report.txt and
unity-verified.log. Tested source hashes match the live project.


### Original Particles stage prefabs (2026-09-28)

The current Original Particles treatment has separate Thrust, Sweep and Repulsor prefabs under Assets/Prefabs/Player/Attack Stages/Original Particles, referenced by MeleeVisualProfile. Each is an OriginalAttackStageVfx hierarchy. Thrust/Sweep contain the original AttackTrailGPU with stage-local tuning; all three include disabled optional Shuriken layers. Repulsor retains its pre-existing body/grid feedback. No gameplay collider or PlayerActor prefab modification was needed.

PlayerMeleePlasma.OriginalParticles owns cached stage instances, current-stage sampling, tails and cancellation/style/disable cleanup. It suppresses the player-root melee trail while prefab emitters replace it; the existing external trail and missing-prefab fallback remain. Stage GPU sampling preserves the original compute/material and uses the prefab transform for placement. Stage initialization arrays are reused across activations.

The shared window edits prefab-owned GPU fields and opens the complete prefab; saved prefab imports and shared-editor Undo/Redo invalidate the runtime copies. OriginalAttackStageVfxEditor provides an explicit Scene-view loop usable in Prefab Mode, including during Play Mode. The custom GPU emitter is not a Shuriken conversion. Added active Particle Systems play with the stage and stop emission before the bounded tail. Prefab contents are separate from Player Tuning snapshot data; snapshots preserve assignments.

OriginalAttackStagePrefabSetup.Install creates missing assets without overwriting existing prefabs. OriginalAttackStageValidation.RunBatch passed 16 checks in Unity 6000.0.28f1: three assigned prefabs, GPU Prefab Mode preview and real combo emission, separate Thrust/Sweep values, live prefab hierarchy additions, Undo/Redo, optional Repulsor emission, all seven demo loops, Off/cancellation/gallery cleanup, and persistence after Play Mode exit/reimport. Rendered frames were inspected. Evidence: Library/OriginalStagePrefabValidation/report.txt, unity-final.log and gallery PNGs. LegacyParticleSettingsValidation now explicitly exercises the unassigned-prefab fallback path.

### Stage particle tail direction (2026-09-28)

Finished Original Particles stage effects retain their own last direction and world rotation instead of inheriting the next attack stage's aim. Follow During Tail controls following player position; disabling it preserves the last sampled world position too. Particle modules remain prefab-owned. A World-simulation Particle System with local Velocity over Lifetime can still steer when its emitter rotates; retaining the finished stage's frame prevents this through combo transitions. During emission, local velocity still follows the active stage's own aim.

Verified against the user's saved Thrust/Center line in Unity 6000.0.28f1, using an isolated project copy: baseline Thrust tail rotated 60 degrees with Sweep, fixed tail rotated 0 degrees, Sweep retained its 60-degree arc, particles survived the transition, all seven demos passed at least two loops, and cancellation, tail expiry, frozen-position tails and gallery cleanup passed. Thirteen targeted checks passed. The Thrust prefab was byte-identical before/after; no authored particle values changed. Evidence and reproduction harness: Library/ThrustTrajectoryValidation. OriginalAttackStageValidation also contains a combo-tail rotation regression assertion.

### Carrier enemy prototype (2026-09-29)

Read the current MASSIVE Notion GDD (page 11e617c6-d8ae-4eed-a7f0-424645db5203,
last edited 2026-09-28). Implemented the user's Carrier brief in the live project
at C:/Users/mattf/massive, Unity 6000.0.28f1. The existing Coplay package remains
installed; this session used the Editor through Computer Use and project-specific
Editor setup/validation commands rather than an MCP connection.

Enemy_Carrier.prefab and ED_Carrier live under Enemy System/Enemy Types/Melee/Carrier.
Six visual-only docked Drones launch clockwise at 0.5-second intervals, wait 5 seconds
after the last launch, then reform at 0.5-second intervals in the same order. The
Carrier is stationary, has 6 health, and uses the existing EnemyBase/Hurtbox/ScoreReward
and ENEMY_DEFEAT reward. Released enemies are the canonical Drone prefab, with a brief
outward departure before normal steering. Director launch/scene registration APIs retain
shared caps, pause and destruction bookkeeping. The default scene Drone cap is 24.

Player Actions contains Carrier Test/Enemy_Carrier at the center, with a dedicated
Director waiting for the normal match gate. The action lab defaults to Manual; the
Carrier root is included in the existing gallery's encounter suppression list. It is
not installed in production stages or the build list. See the Carrier README for tuning.

CarrierPrototypeValidation passed 53 Play Mode checks in the actual Player Actions
scene: initial bays, registration/health, startup pause, departure, two ordered volleys,
0.5/5/0.5 timing, pause/resume, cap hold/release, shared damage, single death and cleanup.
No runtime Console errors occurred in the successful run. Evidence is in
Library/CarrierValidation/report.txt. A first timing fixture hid players before roster
startup and was corrected to wait for regulation; that fixture failure was not a Carrier
gameplay defect. Windows player builds and final balance were not part of this validation.

The Carrier revision uses a regular rhombicuboctahedron with a 0.9-unit circumradius,
approximately half the original hull radius. Its threefold axis is upright so six
square faces provide equally spaced horizontal docks. Drone midsections sit on those
faces with their tails inside the hull; Drone scale remains canonical. All 24 vertices,
26 regular faces and 48 equal edges are verified, with only the upward triangle open
for the core. The shared hitbox radius is now 0.775 and spawn clearance is 1.25. The
prefab was refined in place and the scene instance inherits it. Additional checks cover
face topology, inset placement and clearance; loaded/empty/oblique renders are in the
same validation folder. The 0.5/5/0.5 timing and existing gameplay systems still pass.

Carrier presentation now adds a self-owned 3-second shared EnemySpawnTelegraph warning,
1.2-second staggered face tracing from random vertices, 8-degrees/second Y spin of its
presentation assembly, and three core metaballs per launch following the moving Drone.
Fuel uses fixed arrays in one dynamically fitted SDF volume. The physics root stays fixed.
EnemyBase's 0.8-second cleanup delay permits independent outward/tumbling face breakup;
gameplay death and scoring resolve immediately, and Director counts release on destruction.
Collision is disabled during
arrival and death. All presentation clocks respect pause, including nested docked Drone
visuals. A new optional rotationAxis on EnemySpawnTelegraph defaults to forward for the
existing Drone/Dyson indicators; Carrier uses up. CarrierPresentationSetup authors its
warning mesh/material/prefab and preserves the Carrier GUID. The latest 53-check run also
verified arrival phases, spin, fuel motion, blocked-launch silence, death breakup,
disable/re-enable reset and warning cancellation with no runtime Console errors.

The Carrier now has a solid convex MeshCollider built from its exact 24 hull vertices,
following the rotating shell independently of the animated render mesh. Drone movement
uses trigger capsules and obstacle sweeps, which ignored the old trigger-only Carrier.
The solid hull is now detected by that existing filter. An inset launching Drone ignores
only its owning hull until its tail clears or the launch window expires. The radius-0.775
damage trigger and EnemyHurtbox live on a dedicated child, preventing the root Rigidbody's
additional shell contacts from applying duplicate sword hits. Both colliders are disabled
during arrival and death. Fuel now travels on a straight core-to-Drone line through the
interior; filled faces naturally occlude it instead of the packets arcing over the body.
The regression run verifies the collider against all six docking planes, six incoming
Drones blocked at 8 units/second, owning-hull clearance restored after launch, straight
fuel motion, and one damage event for a physical sword overlap touching both colliders.

### Ranged Drone prototype (2026-09-29)

Enemy Types/Ranged/Ranged Drone contains Enemy_RangedDrone, ED_RangedDrone and
Ranged Drone Metaball Shot.prefab. RangedDroneController composes with the existing
DroneController movement/targeting and DroneVisuals lifecycle; normal Drones and
Carrier docked visuals retain their existing behavior. It preserves the 0.8 scale,
six front facets and short tail. Only the front panels float along their normals,
vibrate during charge, and hinge outward for each shot. The seven-lobe core grows
up to 35% and moves faster while charging. The same spawn tracing and breakup apply.

Detection range is 6 units, firing range 4.5. It approaches targets, stops for a
2-second charge and 1.5-second burst (shots at 0 / 0.5 / 1.0), then cools down for
3 seconds. Shots aim at the current target position from the muzzle, then fly
straight. Target loss/range loss/blocked sight cancels into cooldown. Its body
does not deal melee contact damage. Health and central reward match ED_Drone:
one health and ENEMY_DRONE_DEFEAT, 100 meV base with normal multipliers.

RangedDroneProjectile derives from EnemyProjectileBase to retain player damage
and reflection credit. It uses bounded overlap/sphere-sweep buffers, active-time
lifetime, world/bounds impacts and shared match/Director pause. The four-lobe
metaball packet uses the existing material and a MaterialPropertyBlock. Initial
shot tuning is speed 5, lifetime 4 seconds, damage 0.04 player mass. Reflected
shots can kill their firing Drone and award the reflecting player. Emitted shots
continue their bounded lifetime after owner death.

RangedDroneSetup provides idempotent asset creation and Undo-aware placement in
Player Actions. The Ranged Drone Test has its own empty-schedule Director and is
registered with the demo gallery's encounter suppression. Carrier Test is disabled
to focus the new encounter. All new prefab references were validated in Play Mode.
RangedDroneValidation passed 32 real Player Actions checks, including exact attack
timing, stationary charge/burst, core growth and mouth opening, pause, target loss,
body-contact safety, thin-wall sweep, projectile expiry, real shield reflection,
central score credit, cleanup, and ordinary Drone contact-damage regression.
Evidence: Library/RangedDroneValidation/report.txt and idle/charge/firing renders.
The latest run had no runtime Console errors. Windows builds, production encounter
schedules and final balance were not part of this prototype validation.

Ranged Drone refinements (2026-09-29): shots now emerge from the core (muzzle offset
zero) and stop after 1.5 times positioning range, initially 6.75 units including
reflected travel. Expiry stops damage/travel immediately and eases radii/union
softness to zero over 0.35s. Impacts retain a 0.06s contact beat followed by ten
random fragments spreading/shrinking for 0.4s. EnemyProjectileBase now has a
protected virtual OnImpactResolved hook, defaulting to Destroy; shared damage and
reflection still resolve only once. PlayerVisualController.OutlineContact mirrors
the vector shader's outline to keep contact VFX connected to the damage dent.
Projectile effects continue after owner death and obey the existing pause gates.

DroneVisuals exposes charge face vibration amplitude/speed, normal lift and hinge
tilt plus idle hover speed. Existing authored hover/lift/opening tuning is preserved.
DroneController.Swarm smooths steering with a 0.45s response, fades orbit force
near its center, and bounds idle heading speed/acceleration at 100°/s and 240°/s².
Both variants share this behavior; obstacle sweeps and boundary guards still apply.
The expanded validation passed 46 Player Actions Play Mode checks with no runtime
Console errors, including actual outline contact renders, fragment animation,
exact range cutoff, pause, both variants' measured angular acceleration, real shield
reflection/score credit and normal contact damage. Contact/impact/expiry PNGs and
report are in Library/RangedDroneValidation. Runtime fixtures were discarded on exit.

Charge panel motion is now independent of idle breathing. DroneVisuals retains
its serialized field names but labels idle/charging/firing in separate Inspector
sections. Idle amplitude 0.1, offset 0.075 and speed 5 rad/s are preserved. Charge
amplitude 0.006, absolute offset 0.025 and speed 110 rad/s replace the old additive
values. Separate clocks drive the idle sine and bounded two-frequency charge
jitter, with a 0.18s eased blend on charge entry, fire and cancellation. Charge
pose applies throughout charging after the short blend; it no longer inherits
idle amplitude/speed/lift or waits for charge progress to reach its target pose.

Ranged Drone shots now pass through ResonanceSegment colliders, and those colliders
do not block the attack companion's firing line of sight. The projectile prefab's
Resonance passage section exposes a Resonance Speed Multiplier slider (0.05–1),
authored at 0.75. Physics overlaps and sweeps detect passage without changing
global layers or actor collision. Overlapping fields do not stack; full speed
returns after exit or collider removal. Ordinary walls still block shots and
sight. The expanded validation passed 58 Player Actions Play Mode checks with no
runtime Console errors, including actual generated Resonance geometry, adjustable
speed, overlapping field removal, player impact after passage, and a normal wall
behind Resonance. Evidence remains in Library/RangedDroneValidation/report.txt.

### Particle Beam Turret prototype (2026-09-29)

The user accepted Ranged Drone as complete and moved to Particle Beam Turret.
Enemy Types/Ranged/Particle Beam Turret contains Enemy_ParticleBeamTurret,
ED_ParticleBeamTurret, an empty-schedule test profile, collision hull and spawn
indicator. ParticleBeamTurretSetup creates assets idempotently and places top/bottom
mounts in Player Actions, disables the Ranged Drone test, and registers the encounter
with the gallery's existing suppression mechanism. Top maps to the gameplay +Z wall,
independently of the grid's local axis orientation. Authored placement is also supported.

The fixed base is the eight equilateral faces sharing edges with the bottom octagon
of a gyroelongated square cupola's octagonal antiprism; remaining faces are omitted.
Eight narrower floating triangles form a forward, inset, slowly rotating ring around
a seven-lobe core. The apparatus pivots independently of the solid fixed hull. Damage
uses a separate EnemyHurtbox and EnemyBase/EnemyScoreReward authority. Defaults are
three health, generic ENEMY_DEFEAT reward, 10-unit detection, ±75-degree forward arc,
80-degree/second tracking, 2-second charge, single 2-second beam, 3-second cooldown.
Beam range 16, radius 0.1 and damage 0.08 mass/second remain tunable. Aim locks at fire
by default; Firing Tracking Degrees Per Second enables slow tracking when positive.
Target loss during charge cancels; a locked blast finishes its announced direction.

Spawn uses a shared 2-second warning and 1.1-second randomized face tracing. Charge
shows a faint aim line, faster independent face jitter and growing/agitated core;
firing hinges the ring open. Death stops combat immediately and breaks the faces up
over 0.8 seconds. All active clocks respect shared pause. EnemyBase pause now skips
velocity writes for already-kinematic bodies, retaining dynamic save/restore behavior.

Turret and Particle Accelerator share ParticleAcceleratorBeamVisual/material. The
visual uses fitted narrow render geometry, 48-ball capacity and an analytic tapered
connection to prevent long-beam gaps. The PA beam shader fixes orthographic ray
direction and outputs the raymarched surface depth rather than its proxy cube depth.
The turret supplies its active clock for pause-safe flicker; the player power-up's
projectile behavior is preserved. Ordinary obstacles/arena bounds clip turret beams;
real player shields block them. Resonance still blocks beams (the completed Ranged
Drone bullet's passage exception is specific to its projectile).

ParticleBeamTurretValidation passed 42 real Player Actions Play Mode checks with no
runtime Console errors, including mounting/geometry, lifecycle, tracking, timing,
damage/shield/pause, death/score, original power-up projectile regression, 24-unit
thin-beam continuity, fitted bounds and foreground/background depth in rendered
orthographic/perspective views. Renders and report: Library/ParticleBeamTurretValidation.
Runtime fixtures were discarded on exit. Production encounter scheduling, final
balance and a Windows player build were not part of this prototype validation.

### Turret opaque plasma beam refinement (2026-09-29)

V3DLaser_09 from Assets/SineVFX/Volumetric3DLasers is a local motion/layering reference;
its additive particles/materials are not used. The turret's existing PA beam child now
opts into plasmaLayers: whiteFraction .75, swirl .8 turns/unit at 7 radians/s, width
wave .12 at wavelength 1.6, lateral curve .4 times radius with pinned endpoints.
The shader combines a curved metaball chain with an analytic rippling/lobed connector
and a binary black/white helical surface. Opaque depth writing remains; no alpha or
intermediate colors. Power-up plasmaLayers stays off. Visual curvature is small and
does not steer the gameplay sphere trace around cover.

Controller openingRadiusMultiplier 1.7 settles over .32s; prefab beamFadeSeconds .36
retracts the endpoint while shrinking. Contact damage and emission stop when the tip
leaves the surface. Charging's aim indicator is now thin opaque dashes instead of
alpha. ParticleBeamPlasmaContact is an owned prefab child with reusable 20-drop /
45-ball buffers, pulsating coating, viscous satellite necks, world-space down-arena
drift and geometric shrinking. It drains during cooldown, clears on disable/death,
and freezes under the shared pause gate. Both beam and contact volumes use fitted
runtime meshes and property blocks. Setup migrates older turret prefabs once without
resetting existing enemy tuning. Player Actions instances inherit the prefab update.

Validation now passes 56 checks in Unity 6000.0.28f1 with no runtime Console errors.
The isolated plasma image measured 75.4% white / 24.6% black and zero intermediate
pixels. Render assertions also cover continuous curved/rippling geometry and moving
spiral; gameplay checks cover the thick opening, tip retraction, contact emission,
pause, cooldown drainage and disable cleanup. Fitted contact bounds stay narrow when
the hit point moves away from old droplets. The original power-up regression remains
green. Report and before/after time samples are in Library/ParticleBeamTurretValidation.

The follow-up stranding pass replaces the candy-cane angular stripe with six
independently advected filaments, seeded multi-frequency bends (including local
twist reversals), width envelopes that taper fully away, and two fine white
counter-filaments. The surface fluting is irregular too. Beam child controls are
Strand Count (6), Strand Wander (1.15), Strand Taper (1), Strand Bend Frequency (.8),
and Strand Flow Speed (7). The last two preserve the existing serialized
swirlTurnsPerUnit / swirlSpeed fields via InspectorName. Pause still supplies the
only animation clock; collision, damage and contact plasma are unchanged.

Latest validation: 57 Play Mode/render checks pass, no runtime Console errors.
The sampled render contains up to three separate black filaments across the visible
beam width (79 sampled columns have multiple filaments), 76.5% white / 23.5% black,
and no intermediate colors. Existing lifecycle/power-up regression checks still pass.

### True volumetric plasma strands (2026-09-29)

The next user refinement replaces the surface-color strands entirely: both colors
are now independent 3D SDF tubes. ParticleAcceleratorBeamVisual.Strands.cs samples
up to eight tubes with 65 position/radius cross-sections each, using fixed reusable
buffers. The shader interpolates their distance fields, takes the material from
the nearest tube surface, and writes that surface depth. Black and white can pass
in front of each other, with true empty space between white strands. No shared
white cylinder remains in plasma mode; legacy power-up mode is unchanged. Tube
motion still uses the turret's pause-safe gameplay clock. The shared shader targets
3.5 for dynamic tube-buffer indexing on the project's DX11 target.

Strand Count is total tubes (six defaults to four white / two black). White Fraction
weights their relative cross-sectional area; visible coverage varies with depth
and taper. New Strand Separation defaults to 1 and opens the bundle when increased.
Thickness Wave Amplitude's Inspector range is now 0..1, with actual live tube bounds
covering the enlarged waves. A fully retracted segment explicitly clears tube count.
The existing turn, flow, taper and curve controls retain their serialized names.

The user's unsaved top-turret edits were captured and saved before validation:
White Fraction .503, Flow Speed 16, Thickness Wave Amplitude .3, Wave Length 3,
Curve Amplitude .75, Strand Taper .639. No authoring settings were reset. Scene hash
after that preservation save stayed unchanged through testing, and prefab hash
stayed unchanged for the entire pass. Changes to the runtime renderer apply to both
instances while preserving those overrides.

Latest evidence: 61 Play Mode/render checks pass with no runtime Console errors.
Isolated-color rendering proves both white-over-black (3362 sampled pixels) and
black-over-white (2786 pixels) crossings. Real gaps separate tubes in 158 sampled
cross-sections. Prefab render is 75.2% white / 24.8% black, all pure binary colors.
Checks include amplitude 1, fitted bounds, paused tube geometry and complete
retraction cleanup, plus the existing combat/power-up regressions. Images and report
remain in Library/ParticleBeamTurretValidation. Unity returned to Edit Mode.

### Beam thickness ranges and core sparkle (2026-09-29)

ParticleAcceleratorBeamVisual now exposes Overall Thickness and Start/Body/End
Thickness foldouts, each with Minimum/Maximum visual multipliers (0..4). All
default to 1–1 to retain authored geometry. Overall scales the whole bundle;
local profiles blend over Muzzle Ramp World. Thickness Variation Speed controls
smooth range animation on the paused beam clock, with traveling body variation.
These controls affect plasma presentation, leaving the controller's straight
collision trace and damage radius unchanged. Curve Amplitude now permits 0..2.

The turret prefab gains a Core firing plasma child, using ParticleBeamPlasmaContact
with 28 drops/sec, .32 lifetime, .028 drop radius, .9 splash speed, .11 contact
radius, zero drip acceleration, and the existing pure black/white material. The
controller points it along the firing direction at Core Plasma Forward Offset .1
from the core/pivot center. It emits for every blast, even on misses, follows the
blast envelope, shares pause handling, drains in cooldown and clears on disable
or death. The existing far-end contact volume keeps its independent settings.
Setup upgrades only missing plasma children and preserves authored settings.

The latest top-turret overrides were saved before edits: White Fraction .756,
Flow Speed 16, Thickness Wave Amplitude .733, Curve Amplitude .75 and Strand
Separation 1.201. Scene SHA256 stayed
9E5BC98FBEF1C4EFC800A5C1A05A34D9C5AC132E2DE0FF3BD3D31F44DC298B72 through validation.
The prefab was upgraded through PrefabUtility; both scene instances inherit its
new core emitter. No scene overrides were reset.

Latest validation: 73 Play Mode/render checks pass in Unity 6000.0.28f1 with no
runtime Console errors. New checks cover individual/overall thickness scaling,
zero-thickness cleanup, binary rendering and fitted bounds at curve amplitude 2,
and core emission through charge, fire, misses, pause, cooldown, disable and death.
Existing turret combat and Particle Accelerator regressions pass. Images include
turret-core-fire.png, plasma-thickness-overall.png, plasma-thickness-zones.png and
plasma-curve-two-profile.png under Library/ParticleBeamTurretValidation.

### Applied turret tuning and expanded apparatus (2026-09-29)

The user authorized applying the current top turret settings to Enemy_ParticleBeamTurret.
Applied individual tuning properties through PrefabUtility, excluding the instance
name, root placement, Top/Bottom mount selection, wall position, scene Director and
arena references. Preserved the source .meta GUIDs. Both scene turrets inherit the
updated tuning. Snapshots of the original unsaved overrides/components, prefab and
saved scene are in Library/ParticleBeamTurretValidation/Apparatus.

Latest prefab beam values: overall thickness .8–1.4, start .14–.51, body .83–1.53,
end .5–1; White Fraction .756, Flow Speed 16, Wave Length 3, Thickness Wave Amplitude
.914, Curve Amplitude 1.117, Strand Count 8, Wander 1.615, Taper .283, Separation
1.201. Visual changes applied: idle amplitude .1, charge amplitude .025, charge
speed 35, core charge growth 1. Tracking speed is 60 degrees/sec; firing speed 10.
Core plasma uses .1 drop radius, .75 lifetime, .075 contact radius and .426 white
fraction. Other authored settings remain intact.

Base geometry now completes the octagonal-antiprism band: sixteen triangles rather
than eight disconnected wedges. Base Height defaults to the original regular height
.44774037 and stretches along local Z from the wall. Firing Pivot Offset is -.01774037
from the forward rim, preserving the old .43 pivot position at the default height.
Shell scale and hurtbox dimensions follow size changes; the original hurtbox is
retained at the original dimensions. The spawn outline now shows the complete band
and scales with authored base radius/height.

The new open-front 2V dome is centered on the firing pivot and occupies its local
negative-Z hemisphere. It has 40 triangular faces, 26 vertices, 65 edges and a
decagonal opening; radius defaults to .3. It shares the pivot, black fill/white
outline style, arrival reveal and death shatter. Visual buffers now cover all
64 faces (16 base + 8 ring + 40 dome), with fixed storage and cached dome geometry.

The reported 1px wall gap was stale Edit Mode placement: Wall Inset was 0 while
the top root still used Z=5.96 from the prior .04 inset. Both mounts were reapplied
at the true ±6 boundaries. Custom inspectors update placement and dependent geometry
immediately, record Undo and support multi-selection. No pixel-offset workaround
or arena-boundary change was introduced.

Tracking Ease Seconds (.25) and Firing Ease Seconds (.35) ease speed-limit transitions
and damp yaw toward the target. Target loss decelerates toward rest. Locked firing
(speed 0) brakes during the end of charge and stays fixed throughout its blast;
the user's moving-fire default (10) is retained. Motion uses the existing fixed
gameplay clock and pause gate. Enable resets easing state.

Latest validation: 88 Play Mode/render checks pass, including both exact wall
mounts, band/dome topology, height/collider agreement, dome radius and pivot motion,
all-face reveal, smooth acquisition/firing/stop behavior, authored beam rendering,
and the existing turret combat, pause, lifecycle and Particle Accelerator checks.
No runtime Console errors. New captures include turret-apparatus-pivot.png and
plasma-authored-prefab.png; the scene returns to Edit Mode after testing.
Native Inspector verification also passed: changing Wall Inset from 0 to .2 moved
the top turret from Z=6 to 5.8 immediately, and one Undo restored both the field
and placement. The final authoring audit removed matching generated overrides and
confirmed no remaining top-turret tuning overrides; all tuning inherits from the
prefab. Scene references, wall positions and mount selections stay per-instance.

## Particle Beam Turret finishing controls — 2026-09-30

Applied the latest live top-turret overrides to the source prefab: Base Radius 1,
Base Height .25, Firing Pivot Offset .25 and Wall Inset .02. Both mounts inherit
the tuning and their derived collision/pivot dimensions. Captured live overrides,
component snapshots and the original scene/prefab under
Library/ParticleBeamTurretFinishTask before applying. Scene references, names,
mount selections and horizontal positions remain per-instance.

ParticleBeamTurretVisuals now exposes Panel Float Offset (.04): rigid translation
of the entire ring along the firing axis without resizing the ring or its faces.
The open hinge and existing idle/charge motion remain independent. The backing
dome translates along the firing axis with Dome Vibration Amplitude (.015 local
units) and Dome Vibration Frequency (3 cycles/sec). It pivots with the apparatus,
leaves the core fixed, resets on enable and freezes with Director pause. Amplitude
zero stops the motion.

The arrival telegraph now includes all 64 faces, rather than only the base band.
The visual's shared pose generator supplies the runtime outline at each instance's
authored dimensions/offsets, and the editor setup rebuilds the saved warning mesh
from the prefab. EnemySpawnTelegraph.Begin optionally takes ownership of a
generated outline, updating its join weights, ghost meshes and glow bounds;
destroying the warning releases both generated meshes and the bounds-cache entry.
Existing callers keep their shared outline assets.

Validation: 97 Player Actions Play Mode/render checks passed, with no runtime
Console errors. Added full arrival mesh/ghost checks and rendered capture
turret-full-spawn-warning.png, panel translation/shape preservation, per-instance
warning geometry and cleanup, bounded axial dome motion, pause and zero amplitude.
Existing beam, combat, score, lifecycle and Particle Accelerator checks pass.
Unity returned to Edit Mode; temporary runtime fixtures were discarded.

### Dome vibration by attack state — 2026-09-30

Core backing dome now has separate Idle, Charge and Fire Vibration Amplitude /
Frequency pairs. Existing serialized domeVibrationAmplitude/Frequency remain the
idle/cooldown pair; domeChargeAmplitude/Frequency and domeFireAmplitude/Frequency
are independent. New prefab defaults match the prior .015-unit, 3-Hz motion.
The existing charge/mouth blends transition settings over .15 seconds while the
oscillator phase stays continuous. Director pause freezes all dome motion.
Validated 102 Player Actions checks, including measured charge/fire amplitudes and
frequencies with idle disabled, pause in both attack states and return to idle
settings on cooldown. No runtime Console errors; scene remains unchanged.

### Floating ring axial offset correction — 2026-09-30

Panel Float Offset now translates all eight ring triangles together along the
firing pivot's local +Z/beam direction. The previous outward face-normal offset
also expanded the ring: a +.2 change measured .163846 radial drift and .085308
axial distance error. The correction preserves radius, panel geometry and relative
positions during spin, charge jitter and firing hinge motion. The offset no longer
widens the hurtbox, and the saved spawn outline was regenerated using the corrected
pose. Current unsaved top-turret dome tuning was captured and saved before testing;
snapshots and the baseline measurement are in Library/ParticleBeamTurretRingTask.
Validation passed 106 Player Actions checks with no runtime Console errors,
including pure axial translation at a rotated/spinning pivot and during charge/fire,
unchanged hurtbox width, warning geometry and the existing combat/beam checks.

### Seeker prototype — 2026-09-30

Added `Enemy Types/Melee/Seeker/Enemy_Seeker.prefab`, `ED_Seeker`, the shared-style
arrival outline/telegraph and an empty authored-test spawn profile. `Seeker Test`
is active in Player Actions and registered with the gallery's encounter pause.
The turret encounter and its separately parented top instance are inactive;
their tuning was preserved. A pre-Seeker scene snapshot is retained in
`Library/SeekerValidation/PlayerActions-before-seeker.unity`.

`SeekerController` owns the Seeking/Charging/Firing/Hit/Cooldown clock. Initial
settings: 7-unit detection, 3.8 seek speed, 4.5 attack range/travel, 1.25-second
charge plus .12-second compression, aim locked after 80% of charge, 22-unit/s
lunge, .24-second hit recovery and 1.75-second cooldown. One directed player
attack activation offers one avoidance roll (55%, .16-second reaction), followed
by a .3-second lateral strafe. Charge/cooldown are stationary with no evasion.
Shared health is 1, damage is .04 player mass, reward is `ENEMY_DEFEAT`.

Movement uses bounded capsule overlap/sweep queries, shared obstacle avoidance,
arena clamps and EnemyBase movement influences. A lunge resolves at most one hit,
stops on shields/obstacles and has no homing after aim lock. EnemyHurtbox,
EnemyScoreReward and authored Director registration supply existing damage,
scoring and pause behavior.

`SeekerVisuals` generates 18 white-outlined, black-filled faces: an open-backed
hexagonal pyramid nose, six large forward/outward rear triangles and six smaller
backward triangles. All three groups have independent axial offsets/dimensions
and alternating signed spin speeds. Charge pulls both rear groups back, stretches
the metaball core, vibrates/inward-tilts the large panels, then compresses at
launch. The sword hurtbox expands with the exposed rear assembly. All faces
participate in the shared warning, staggered random-vertex reveal and shatter
death animation. Runtime generated meshes are unsaved and destroyed on disable.

`ParticleBeamPlasmaContact.EmitBurst` reuses the turret's bounded opaque plasma
pool without changing sustained emission. The Seeker detaches its owned exhaust
at runtime so Rigidbody interpolation cannot carry it along; the owner advances
its paused clock, clears it on disable and destroys it on destruction.

Editor menu: `MASSIVE/Enemies/Seeker` provides create, place and validation. Setup
preserves existing authored tuning. Play-mode validation passed **40 checks**
with no runtime Console errors, covering full arrival, geometry/offsets, charge
stretch/hurtbox, aim lock, one-hit damage, shield blocking, thin-wall and arena
boundary sweeps, missed lunge range, movement influence, real-input lateral
avoidance, world-space exhaust/lifetime, target loss, pause, death and scoring.
The shared pool also passed sustained-emission regression checks. Captured
idle/charge/compression/launch/death renders were reviewed; the report and PNGs
are in `Library/SeekerValidation`. Existing player spawn routines still produce
kinematic-velocity warnings at PlayerControllerScript.cs:399–400; no player code
was changed for this feature. Unity returned to Edit Mode, discarding fixtures.

### Seeker thrust and stalking polish — 2026-09-30

The nose now accelerates progressively to 8x spin during charge, eases back after
launch, stretches 18% during powered thrust, and settles with a damped compression
bounce. Each parameter is exposed in Seeker Visuals. A bounded pool of world-space
outline snapshots trails powered thrust: .4-unit spacing, .24-second lifetime,
12 active slots by default (16 maximum). Opaque white lines thin to nothing;
pause freezes their pose/lifetime and death/disable clears the pool.

Contacts recoil .18 units over the existing .24-second hit recovery. Misses coast
for .1 seconds with continuous starting speed and squared ease-out, adding about
.73 units at default speed; the glide retains swept contact checks. Accepted
player damage selects a separate .65-second cooldown; blocked contacts and
misses retain 1.75 seconds. Both enter a 3-second Stalking phase, maintaining
3–4.25 units while circling/strafing, before seeking another attack. Stalk duration,
distance, radial/orbit/strafe speeds and frequency are independent controls.
Stalking uses the same targeting, avoidance, arena bounds, movement influence and
pause systems. Existing enum values were preserved by appending the new phases.

Player Actions validation passed 60 checks with no runtime Console errors,
including ghost world-space stability/retirement, visual and gameplay pause,
charge spin, nose elasticity, miss glide, obstacle/player recoil, cooldown
selection, a full timed stalking orbit and subsequent player hit. The previous
arrival, damage, shield, wall/boundary, avoidance, score/death and shared plasma
checks also pass. Rendered ghost and arrival captures were reviewed. The user's
current scene geometry tuning was preserved byte-for-byte against
`Library/SeekerPolish/PlayerActions-authored.unity`; new defaults are serialized
on the prefab. Validation discards its runtime fixtures on returning to Edit Mode.

### Alternate Dyson Sphere Repulsor — 2026-09-30

Read the current MASSIVE Notion GDD again for context. Added the genuine prefab
variant `DysonSphere/Enemy_DysonSphere_Repulsor.prefab`, its own definition and
authored-test profile. The original Dyson prefab, definition and spear/lunge
controller are unchanged. `Dyson Repulsor Test` is active in Player Actions;
the Seeker encounter remains intact and inactive. The new encounter participates
in the gallery's existing encounter pause list. Scene rollback snapshot:
`Library/DysonRepulsorValidation/PlayerActions-before.unity`.

The alternate reuses DysonSpherePanels, core, health-driven panel loss and score
reward. DysonSphereRepulsorController seeks within 8 units and charges at 1.5.
Default charge .75 seconds contracts the whole body to .85 scale and closes all
panels onto the base outline, with independent .008-unit/32-Hz vibration.
An .08-second expansion reaches 1.15 body scale and 1.3 base panel radius, holds
for .1 seconds with .015-unit/38-Hz vibration, then springs back over .4 seconds
(1.5 cycles, damping 3). It resumes normal breathing and cools down for 1.65 seconds.
All these values are Inspector controls. Core particles use local simulation and
hierarchy scaling on this variant to follow the whole-body animation.

The outgoing damage sphere and sword hurtbox are measured from live panel
vertices, including rotation, body scale and vibration. Default peak radius is
about 1.121 units plus vibration. Damage is active during expansion/hold/return,
once per eligible player per burst; shields and solid obstructions block it.
Health and score remain the existing Dyson values; damage preserves its 1.25
multiplier. No forward lunge or visual-ripple damage is applied.

DysonSpherePanels has an opt-in radial pose/paused-clock API; its original path
retains normal behavior. PlayerRepulsorGridPulse has an explicit world-space
trigger and optional external clock so the enemy reuses the same grid wave while
player pulses retain their original timing. Enemy scale, panels, spin, particles,
ripple and combat freeze together during pause. Death disables collision and
lets the existing panel shatter finish over the .8-second cleanup delay.

Unity Player Actions validation passed 30 checks with no runtime Console errors:
references/original preservation, spawn, charge/expansion/hold/rebound, core
scaling, independently measured mesh-to-hitbox agreement, stationary attack,
pause, actual player damage, missed-area safety, shield, repeated bursts, player
ripple regression, death and scoring. Idle/charge/burst/death renders were
reviewed. Reports/captures: `Library/DysonRepulsorValidation`. Setup and validation
menus are under `MASSIVE/Enemies/Dyson Repulsor`; controls are documented in
`DysonSphere/Repulsor Variant.md`.


## Enemy encounter authoring — 2026-09-30

Player Actions (`Assets/Scenes/S-T_PLAYER-ACTIONS.unity`) now saves Enemy Lab in
Encounter Timeline mode; Columns remains available on the same EnemyLab owner.
`EnemyDirector.Timeline.cs` extends the existing director with optional authored
`EnemyEncounterTimeline` and `EnemyFormation` assets, `EnemyArenaLayout` regions,
exclusions and mount sockets, atomic reservations, explicit warning lead,
seeded variants, fallback/expiry and shared pressure/population caps (including
Carrier launches). A null timeline retains legacy random/batch spawning.
The starter assets are in `Assets/Enemy System/Encounters/Enemy Lab`.

`EnemyEncounterLab` supplies a shared pseudo-player scope, scripted player actors,
open/blocked-center/side-mount fixtures, single-cue/full-timeline preview, loop,
pause and restart controls. This is a placement fixture, not integration into
all six level scenes. `EnemyBase.SharesSimulationWith` accepts all players in a
shared scope when DemonstrationTarget is null; fixed-target column scopes retain
prior isolation. Optional Drone entrances use `EnemyFormationEntry` before AI.
See `Assets/Enemy System/Encounters/README.md` for authoring and scope boundaries.

Setup: MASSIVE > Demonstrations > Set Up Encounter Timeline Lab.
Validation: MASSIVE > Demonstrations > Validate Encounter Timeline.
Reports: `Library/EnemyEncounterValidation`. Existing canonical enemy prefabs,
health and score assets are reused; pressure numbers and example cue pacing are
initial tuning values. Scene setup preserves an existing preview if rerun.


### Carrier turret-lane clearance — 2026-09-30

CarrierController.Avoidance adds a short, eased lateral reposition when the
Carrier is the first solid blocker of a same-scope, same-allegiance turret's
eligible player sightline. ParticleBeamTurretController.Clearance supplies the
request even before target acquisition (or along the current firing direction).
The Carrier selects a collision-free exit, respects bounds/exclusions, pauses
and HoldPosition, then stays at its new position. It continues normal drone
launch/rebuild cycles. Inspector controls are under Soft turret lane avoidance.
The canonical Carrier Rigidbody is kinematic with planar translation enabled;
Awake also upgrades older scene overrides so FreezeAll cannot prevent movement.
Play Mode checks in Enemy Lab passed all 18 cases with no runtime errors.
Menu: MASSIVE > Enemies > Validate Carrier Turret Avoidance.
Report: Library/CarrierLaneAvoidanceValidation/report.txt.

### Drone formation library — 2026-10-02

The Enemy Lab timeline now includes Drone/Ranged Paired Rows (20 each), Center
Ring (10 each), and Goal Phalanxes (20 each). Rows release center-out at .25s
per top/bottom pair; rings release opposite pairs at .25s, alternating arcs.
Phalanxes use simultaneous 1–2–3–4 ranks, centered at x±5 and facing the goals.
Authored positions normalize against the current 28×12 arena. Ranged positions
interleave with the matching normal batch (minimum combined clearance 1.15).
Lab ranged cap is 24; total40/pressure60 allow matching 20+20 arrivals. Actor
horizontal position defaults to .72 to keep phalanx arrival space free. The
original six cue times remain; four new cues extend the preview to 181 seconds.
Validate Drone Formations exercises geometry and live simultaneous release;
reports are under Library/EnemyDroneFormationValidation.

Validation on 2026-10-02: Unity compiled; 52 Drone formation checks passed,
including simultaneous matching batches with live AI and full release timing.
The existing 29-check encounter regression also passed using its original compact
formation/actor fixture, including all enemy types, wall mounts, looping and
column isolation. Existing Player match-spawn kinematic velocity warnings remain.

### Encounter Composer — 2026-10-02

Native dockable EditorWindow at MASSIVE > Enemies > Encounter Composer, also
opened by timeline asset double-click or either Enemy Lab inspector. Editor-only
files: EnemyEncounterComposer.cs / .Preview.cs and EnemyEncounterAuthoring.cs.
Reuses EnemyEncounterTimeline/EnemyFormation and the existing Director; there is
no new runtime scheduler. Supports formation-library drops onto enemy tracks,
timing drags with snapping, duplicate/remove, Undo/Redo, auto-save, per-cue
settings, seeded placement previews and authoring diagnostics. Live Enemy Lab
controls expose all/single-cue playback, pause/restart, cue outcomes, budgets and
an aligned playhead; editing is locked in Play Mode. Blocks show spawn windows,
not enemy survival, and the insertion cursor does not scrub runtime state.
Dragging retains cue indices (seed identity); removal can change later seeds.
Validation menu: MASSIVE > Enemies > Validate Encounter Composer. Report:
Library/EnemyEncounterComposerValidation/report.txt. Usage is in the encounters
README. Existing timeline timings and formation assets are preserved.

Validation: 19 editor checks passed, including 20-seed parity with the Director.
Native UI verified quick library drops, block dragging, Undo/save and Fit. Live
single-cue playback spawned all 20 Ranged phalanx members, aligned the playhead
to the authored 159s cue, paused without clock drift and restarted cleanly.
Full playback displayed completed Drone rows and a subsequent Ranged batch
skipped because living enemies occupied its arrival space. No new compile or
runtime errors; pre-existing player spawn kinematic-velocity warnings remain.

### Flexible encounter arrivals — 2026-10-02

EnemyFormation now owns Strict/Flexible integrity, bounded pre-warning position
adjustment, per-slot grace, an adjustment pattern, placement groups and optional
linked release groups. EnemyEncounterTimeline.Cue can override the three policy
values; existing cues inherit formation defaults. The six Drone/Ranged library
batches are Flexible with maximum adjustment 1 world unit and grace 3 seconds.
The user's current 16-cue timeline (pressure120, duration181 and all timings) is
preserved. Earlier pressure60/ten-cue notes describe the starter preset.

EnemyDirector.Placement prefers coherent row/phalanx translations and ring
rotations before bounded local corrections. All candidates pass existing
clearance rules. EnemyDirector.Flexible reserves the whole batch budget but
releases clear slots independently; blocked slots wait/expire individually.
Announced locations never move and late-clearing slots still receive a complete
warning. Linked positive release IDs are atomic and require matching delays.
Wall sockets never shift. Population and pressure caps still gate full batches.
Max lateness controls initial reservation; Flexible slot grace controls release
deadlines relative to each authored arrival, with no indefinite queue.

EnemyDirector.ArrivalAvoidance supplies bounded steering around active warnings.
Normal Drone, Ranged, Seeker and Dyson movement consumes it through existing
obstacle steering; committed attack motion bypasses it. Carriers yield through
their existing collision-checked eased movement, prioritizing turret clearance.
HoldPosition, pause and simulation scope apply; players/turrets are not moved.
Composer exposes shared defaults, per-cue overrides, counts and slot reasons;
its live arena shows adjusted/blocked/spawned footprints. Static previews remain
authored-position checks. Detailed controls and behavior: encounters README.

Validation menu: MASSIVE > Demonstrations > Validate Flexible Spawning.
Report: Library/EnemySpawnFlexValidation/report.txt. Runtime-copy fixtures cover
partial release/expiry, linked groups, late warnings, immutable telegraphs,
placement shape/bounds, budgets, sockets, pause/restart and live Drone steering.
Composer regression additionally verifies override inheritance, duplication and
serialization. The original compact encounter fixture explicitly uses Strict.

Validation: Unity compiled; 37 Flexible spawn checks, 22 Composer editor checks,
29 existing encounter checks and 52 Drone formation checks passed (140 total).
Live simultaneous Drone/Ranged rows, rings and phalanxes complete with normal AI;
clear staggered releases preserve at least .25s despite warning/frame drift.
Recovered blocked slots do not shift the remaining clear sequence. No runtime
errors in these suites; existing player spawn kinematic-velocity warnings remain.
Native Composer UI verified shared defaults and inherited cue policy. In the
user-authored timeline's live preview, Ranged rows reported Complete with all 20
spawned and green arrival footprints. Returned to Edit Mode with Composer open.
The authored timeline remained byte-for-byte identical to the pre-change copy.


### Timeline population authority (2026-10-02)

EnemyEncounterTimeline now owns maxAliveTotal, optional populationLimits per definition,
and maxPressure/pressureCosts. Timeline mode replaces all legacy profile/category,
definition and rule caps; CanSpawnEnemy returns the timeline decision directly.
EnemyPopulationBudget supplies shared runtime and Composer admission checks with
alive/reserved/requested diagnostics. CurrentPressure includes legacy reservations;
consuming one's own reservation excludes it before counting the requested spawn.
Carrier launches use the same gate and launched children count once. CarrierController
also skips its scene-wide maxActiveDrones/definition pre-check in timeline mode;
standalone and legacy Carriers retain it.

Composer exposes these controls and pressure costs. EnemyEncounterLab owns the
ignorePopulationLimits preference; Director only bypasses budgets when the active
Lab points at that Director and owns its current SimulationRoot. It never changes
timeline limits. HUD and Composer mark UNLIMITED PREVIEW; spatial, telegraph and
pause gates stay active. Default is off. Timeline asset migrated to explicit total
40, Drone/Ranged 24 each, Seeker12, Turret8, Carrier2, Dyson0; pressure120 and all
16 user-authored cues/timings retained. Legacy profile/definition caps unchanged.
Validation menu/report: Validate Timeline Population / Library/EnemyPopulationValidation.

Validation: Unity compiled and all 176 checks passed (population30, Composer28,
flexible37, encounter29, formations52). Actual Carrier controller launch was
exercised beyond both its legacy local and definition ceilings. Lab bypass kept
warnings, collision checks and pause, restored caps without killing enemies, and
rejected an unrelated simulation scope. Composer Undo/Redo and save/reload passed.
No runtime errors in the suites; existing player spawn kinematic-velocity warnings
remain. Native Composer population UI checked and left open in Edit Mode. The
16-cue timeline matches its pre-change copy after removing only the new population
fields. Scene was not saved; existing unsaved scene edits were preserved.

### Thrust steering cone (2026-10-02)

PlayerCombatSettings.thrustMaxTurnDegrees defaults to 15 degrees per side and is exposed in MASSIVE > Player Tuning > Movement > Thrust steering. PlayerAttackController.ConstrainThrustDirectionWS clamps planar aim around stageAttackDirectionWS captured at stage start, after lunge assistance; the center never follows later input. Zero locks aim; 180 removes the bound. Existing shared/local tuning and snapshots include the field.

The controller applies the bound to explicit aim writes and attack visual direction, and establishes the start heading before stage listeners enable damage. PlayerVisualController retains raw desired input but uses bounded facing for the body target and combat root; smoothed combat rotations are bounded too. The child Sword collider therefore obeys the same limit. Lunge displacement remains stage-locked. Completion, cancellation and subsequent stages restore normal facing; old VFX tail handling remains intact.

ThrustSteeringValidation.RunBatch passed 31 checks in an isolated Unity 6000.0.28f1 Editor: real canonical collider/input behavior, rear-target exclusion with a positive damage control, both limits and reversals, no accumulated steering drift, scripted/smoothed facing, wraparound, local/shared tuning, new stage centers, normal exit/cancellation/Sweep, all seven demos for two loops, snapshots and save/reimport persistence. Evidence: Library/ThrustSteeringValidation/report.txt, unity.log and tested-source-sha256.txt. The live scene was not saved or switched.

### Manual timeline preview player (2026-10-02)

Composer's details panel begins with Preview Player > Manual control (P1), also
available as EnemyEncounterLab.manualPlayerControl in the Inspector. It hands the
existing left actor to Rewired Player 1 (ID 0); the right actor retains scripted
movement and attacks. The preview actor remains a pseudo-player in the Lab's own
simulation scope, uses shared gameplay tuning, and does not join match scoring.
Current keyboard maps: arrows move, S Sword, D Shield. Normal P1 controller
assignment is reused; no new input maps or raw keyboard polling were introduced.

EnemyEncounterLab runs before PlayerControllerScript to hand off input without an
extra frame of scripted commands. Handoffs clear scripted input, cancel outgoing
attack/shield and velocity, then select Rewired, Scripted or Disabled. Timeline
pause locks both actors; Application.isFocused gates manual input. The toggle
does not restart playback, and restart/loop carries the preference to new actors.
Actors Move/Attack only govern automatic actors. Restore Player Mass still applies.

Validate Timeline Manual Control writes routing, pause, lifecycle and isolation
results to Library/EnemyManualControlValidation, plus keyboard bindings and scene
placement context. Physical keyboard/controller gameplay is not covered by this
automated fixture; native automation key taps did not reliably reach Rewired.

The unsaved scene assigns AmplifierResonance as the timeline director's optional
placementRegion, unlike the saved scene's null region. This initially caused
floor formations to fail neutral-territory/height checks; resolved by the Lab
placement correction below. The scene assignment and its settings were preserved.

Validation: Unity compiled; the initial manual-control run passed all 22 checks,
and the isolated encounter regression passed all 29. Later native automation could
not establish Application.isFocused even after clicking/activating the Game view;
the final focused-input rerun was stopped at that precondition. No physical input
playthrough is claimed. Existing player-spawn kinematic-velocity warnings remain.
Unity returned to Edit Mode; no scene or authored timeline settings were saved.

### Timeline placement and Lab roster isolation (2026-10-02)

EnemyArenaLayout.gameplayHeight defaults to world Y=0. World() now uses that height
for actors, enemy arrivals and wall sockets instead of the decorative grid's
Y=-.364. Timeline MemberClear uses AmplifierSpawnRegion.IsClearOfExclusionsCached
for shared no-go volumes, Resonance footprints and solid obstacles. It opts out
of the Amplifier neutral stripe, spawn height, goal-attraction radius and global
player-distance policy. EnemyArenaLayout governs territory/height and the Director
still checks its scoped players, physical blockers, live enemies and reservations.
Amplifier and legacy random/batch placement retain IsValidCached and all previous
restrictions. The goal-attraction opt-out is necessary for the authored Ranged and
Dyson flanks, which are clear of physical goals but inside their 4.5-unit power-up
attraction radius.

EnemyLab disables both preview children while waiting for match startup. After
Regulation begins, it suppresses and fully deactivates the same-scene normal
player roots before starting the selected Lab mode. Root deactivation removes
P1/P3 from ActivePlayers and input/placement logic. Leaving the Lab reactivates
only the previously active roster, then releases suppression to restore visuals;
P2/P4 remain inactive. Re-entering suspends the roster again.

The encounter regression now keeps the actual assigned Amplifier region in its
integrated run, checks shared exclusions and power-up-specific policies separately,
and checks Y=0 arrivals plus roster shutdown/restoration/re-entry. It verifies
the authored timeline and assigned region settings remain unchanged.

Validation: Unity compiled and all 43 encounter checks passed. All six enemy types
and Carrier children spawned with the assigned shared region; every integration
cue completed. Looping, the original columns, roster visual restoration and Lab
re-entry passed with no Unity errors or exceptions. Existing player-spawn
kinematic-velocity warnings remain. Evidence: Library/EnemyEncounterValidation/
report.txt and timeline.txt. Scene was not saved and authored settings were retained.

### ORBITAL playable encounter integration (2026-10-02)

S-6_ORBITAL now enables its existing Drone Swarm Spawner instance as
ORBITAL Enemy Timeline. It references the shared Enemy Lab Timeline.asset directly,
so Composer edits affect both scenes. A level-owned ORBITAL Encounter Profile.asset
provides enabled/anomaly policy; timeline population/pressure remains authoritative.
EnemyArenaLayout adds the Lab's five named regions and four top/bottom turret
sockets at Y=0. The Director waits for scoring and runs with normal match players;
there are no Lab actors, automatic player controls or demonstration scope in the
saved scene. The match schedule runs once, without the Lab's loop/reset behavior.

ORBITAL does not use AmplifierSpawnRegion for enemy placement. Its obstacle,
NoSpawnZone and PowerUps masks retain ordinary blockers. Per-layout
allowResonanceAndAmplifierOverlap permits those component hierarchies if they are
in the query mask, and spawnOverlapRoots includes the Cloud hierarchy (including
its runtime nuggets). ORBITAL's current Resonance prefab uses Default-layer
colliders, outside the mask already. Explicit exclusions, player/enemy occupancy,
reservations and arena boundaries remain enforced. No physics collision layers,
Cloud behavior, Resonance sludge, power-up placement or spawner timing were changed.

Composer now accepts a scene EnemyDirector as well as a Lab. It discovers the
active scene's Director, offers Play all / Play cue and reads live status/budgets.
Restart in a playable level clears that Director's enemies and schedule only;
normal players, match state and environmental systems remain. Manual-player and
Unlimited Preview controls remain Lab-only. EnemyDirector's Inspector has an
Open Encounter Composer button.

OrbitalEncounterSetup installs and saves the wiring through Unity with Undo and
compares Cloud, spawner, region and timeline serialization before/after. Evidence
and the pre-install scene backup are in Library/OrbitalEncounterValidation.
OrbitalEncounterValidation exercises actual generated environmental overlap,
match gating, normal roster scope, the opening waves and individual later cues.
The unattended fixture restores player mass and repositions players between
isolated cues; these are temporary test actions, never saved level behavior.

Final Play Mode validation passed all 39 checks, covering actual telegraphed
arrivals over live Resonance, Amplifier and dense Cloud positions; all six enemy
types and Carrier children; normal roster targeting; timeline population limits;
Composer pause/restart; and match start/end gating. No Unity errors or exceptions
occurred. Existing player-spawn kinematic-velocity warnings remain. Coverage used
the opening timeline plus isolated later cues, not a human-played full loop.
Unity returned to Edit Mode with ORBITAL saved and the Composer bound to its
Director. Detailed results are in Library/OrbitalEncounterValidation/report.txt.

### Enemy passage through mass pickups (2026-10-03)

Nuggets/nugglets were solid obstacles to enemy physics and explicit movement
queries. Reproduced with the actual enemy and pickup prefabs in a local physics
scene: Ranged Drone and other solid shells reflected pickups, while the normal
Drone's trigger body passed physically but its avoidance still rejected them.
Baseline: 144 failures across 156 contact/query/lifecycle checks.

MatterNuggetScript now registers active pickups and ignores their collider pairs
with EnemyBase instances on pickup birth/reuse and enemy enable. Component-based
pairs cover both Enemy-layer bodies and Default-layer Dyson prefabs, including
disabled spawn colliders. EnemyObstacleAvoidance, formation entrances, Carrier
sidesteps, ranged sight/projectile queries and turret beam queries also filter
MatterNuggetScript hierarchies. Player contacts, wall bounce, collision layers,
scene/prefab settings and environmental spawn settings are unchanged.

MASSIVE > Enemies > Validate Nugget Passage runs the retained editor regression.
All 159 final checks passed in Play Mode: six current enemy types plus legacy
Dyson against all three pickup prefabs; physical passage with unchanged velocity;
spawn collider toggles; pooled pickup and enemy reactivation; query filtering;
player contact preservation; and actual wall bounce. The two interim shot-test
failures came from testing authored-inactive pooled pickups before Eject; the
fixture now activates them through their normal lifecycle. Unity compiled with
no new errors; pre-existing compiler/player-spawn warnings remain. Tests used an
isolated physics scene, discarded all fixtures and returned ORBITAL to Edit Mode
without saving or modifying the level. Reports: Library/EnemyNuggetCollisionValidation/
baseline.txt and report.txt. No standalone build was run.


### Repulsor damage zones (2026-10-03)

Repulsor now has two flat damage tiers for NPCs and players. The yellow outer
zone retains the configured maximum radius and deals one normal hit. The red
inner zone defaults to 1.5 times the physical player body radius and deals 2x
damage. Inner reach is clamped to outer reach. Radius, multiplier and the gameplay
circle toggle are persistent AttackStage settings, exposed in MASSIVE > Player
Tuning > Impact & recovery and the Repulsor appearance/preview controls.

The active pulse captures both radii and its release origin. A target whose
closest eligible body surface touches the inner sphere gets the inner tier;
compound hurtboxes are checked together so callback order cannot choose a lower
tier. Each target still receives at most one hit per pulse. Player mass damage
is enabled at normal-hit strength in the shared settings and canonical PlayerActor.
Only knockback/stun retain distance falloff. TryApplyHit accepts an optional damage
multiplier and resolves amplified damage as one transaction, retaining cooldown,
held-shield protection, invulnerability and death attribution. Existing transfer
VFX/reward fractions remain capped at their previous one-hit limit.

PlayerRepulsorRangeRings is attached automatically at runtime to existing AOEs and
draws dotted red/yellow boundaries only during the active pulse. Scene selection
guides and the Player Tuning diagram/overlay show matching boundaries. The
renderer uses the existing Shapes package and supports the Singularity surface
adapter; it creates no colliders or saved scene objects.

Validation found both sword-gating components also enabled the sword during
Repulsor. A forward opponent could receive a normal sword hit first, consuming
the damage cooldown and rejecting the radial 2x hit. PlayerMelee and
AttackHitboxWindow now keep the sword off for FinisherRepulsor; the radial AOE
owns that stage's hits. Actual contact changed from 0.08 to 0.16 player mass loss.

Unity 6000.0.28f1 compiled and passed 42 new runtime/render/persistence checks
plus the 19 existing Repulsor combat checks in an isolated project copy. Coverage
includes actual player/NPC trigger contacts, inner-boundary overlap, compound
hurtbox ordering, cancellation, held shields, invulnerability, same-team filtering,
death attribution, all seven demos completing two loops, and saved tuning reload.
The red/yellow dotted circles were rendered and visually inspected. No Unity
errors or exceptions occurred in the final run; pre-existing compiler and
player-spawn warnings remain. The saved scene currently disables the action
arena for Enemy Lab; validation enabled it only in its unsaved test copy.
The live scene/settings for Enemy Lab were preserved. Reports, PNG and validated
source hashes: Library/RepulsorZonesValidation. No standalone build was run.


### Repulsor expanding damage guides (2026-10-03)

The original dotted guides showed final reach from the moment the pulse released,
although the physical damage collider already grew from the live player outline.
Game-view tuning dots now use RadiusWorld and min(InnerRadiusWorld, RadiusWorld).
The inner boundary stops at its configured limit while the outer front continues.
Scene-view live guides and the attack timeline diagram use the same expanding
reach, and Play Mode guides disappear with the active hit window. Idle Edit Mode
selection still shows explicitly labelled maximum reach for static tuning.

The radius curve reaches 1 at 80% of the active window: with current settings,
0.20s expansion followed by 0.05s at full size. The stage duration, activation
start/end and damage tiers are unchanged. The curve remains editable under
Player Tuning > Impact & recovery > Expansion over active window. The renderer
and its automatic attachment are now UNITY_EDITOR-only; the toggle reads
Show live damage guides (Editor only). These are tuning overlays, excluded from
standalone player builds.

Unity compiled and all 57 expanded runtime/render/persistence checks plus the
19 existing combat checks passed in an isolated copy. New temporal checks prove
outline-sized release, an intermediate radius, no early damage to distant players
or NPCs, full reach before expiration, and immediate removal at the end. Start,
middle and full-size frames were rendered and inspected; all seven demos passed
two loops. Live changed files match the tested copies. Reports, captures and
hashes: Library/RepulsorExpansionValidation. No standalone build was run.


### LATTICE disruption and quantized locomotion prototype (2026-10-03)

The new S-1_LATTICE scene has a scene-local LatticeDisruptionField on VectorGridGPU
and LatticePlayerMotor overrides on its four canonical player actors. The common
player and playing-field prefabs are not modified by this feature. The scene is
not yet added to the release level catalog or Build Settings. Design context is
the Notion MASSIVE GDD (11e617c6-d8ae-4eed-a7f0-424645db5203), read October 3,
last edited September 28, plus the user's Planck-scale LATTICE brief.

Domain-warped, evolving noise disconnects grid edges into fluttering, retracting
strands and intersection dots. Reconnection grows and straightens the strands.
Transient GPU geometry retains the shared grid simulation, responsive attraction,
Amplifier/Repulsor deformation and arena border. The renderer and locomotion use
the same connection state; disrupted nodes settle to fixed lattice coordinates.
Disabling the field restores the original renderer and releases its resources.

The optional motor runs inside canonical player traction, using existing input,
deadzone, speed and movement modifiers. Disrupted regions use actual discrete
eight-direction node steps with no interpolation, diagonal distance-based timing,
swept body queries, occupied-destination checks and arena clearance. Normal grid
restores continuous locomotion. Joystick movement, including shielding, is
quantized; attacks, movement abilities, stun and protected knockback keep their
existing motion. Aim, enemies, projectiles and objectives remain continuous.
Pause freezes both noise and movement. No alternate player prefab was introduced.

Tuning is on PLAYING FIELD / GRID / VectorGridGPU, Lattice Disruption Field.
MASSIVE > LATTICE contains idempotent scene installation and prototype validation.
The saved scene is already installed and left in Edit Mode. Implementation and
tuning notes are in Assets/Scripts/Anomalies/LATTICE/README.md.

Unity 6000.0.28f1 / DX11 compiled and passed all 59 Play Mode checks, including
all eight directions, transitions, diagonal timing, collision/edge blocking,
match locks, attacks, knockback, pause and disable/re-enable behavior. Actual
gameplay and field-state renders were visually inspected. The completed run had
no errors or exceptions; existing kinematic player-spawn velocity warnings remain.
An earlier run was interrupted by script reload and was repeated after imports
settled. Reports, renders and source hashes are in Library/LatticeValidation.
No standalone build or controller feel playtest was performed.

LATTICE refinement: endpoint cuts now detach a single whole tether from the
disrupted point, anchored at its intact neighbor. Interior cuts between intact
points can still produce two strands. Hold Tips At Boundary on the field keeps
free tips on the actual moving noise contour; it defaults off. Show Noise
Boundary defaults on and draws a faint dashed guide in the Editor's Scene/Game
views without enabling general Gizmos. The guide renderer is UNITY_EDITOR-only,
its shader is under Editor, and its transient mesh/material are disposed with
the field.

The distance clock now shares canonical joystick traction via
PlayerControllerScript.Locomotion.cs, including analog input, mass, acceleration,
braking, speed cap and Rigidbody damping. Each hop subtracts its actual distance
and retains the fractional remainder; multiple adjacent hops can be consumed in
one tick with individual collision checks. This replaces the original max-speed
approximation and discarded remainder. Ordinary locomotion uses the same
extracted calculation with its original force/clamp order.

The expanded suite passed all 73 Play Mode checks on DX11. Four simultaneous
ordinary-player comparisons (cardinal, diagonal, partial stick, heavier body
with more drag) matched accumulated distance to three decimals, including pending
fractional travel. Endpoint/interior cuts, exact contour tracking, toggle behavior
and both shaders passed. Final guide and boundary-held renders were inspected.
No new runtime errors occurred; existing spawn warnings remain. No player build
or physical controller feel playtest was run. Reports/renders/hashes remain in
Library/LatticeValidation; RefinementBaseline preserves the preceding version.

LATTICE breeze refinement: the strand shader now uses cubic curves driven by
smooth aperiodic noise. Each strand end has independent bend timing, resting
lean and slack. Held tips remain at the contour while the interiors drift.
Thread Looseness and Breeze Speed retain the existing serialized flutter
settings. Strand Variation defaults to 1; Strand Motion Seed and the Inspector's
Randomize Strand Motion button alter only thread shapes and motion. Randomize
and Undo were verified in the native Inspector. The scene retains the user's
held tips, 18 strand segments and hidden editor boundary guide.

Unity compiled the refinement on DX11 and all 74 Play Mode checks passed.
Successive close-up breeze renders and an alternate seed were visually reviewed.
No new runtime errors occurred; the existing player-spawn warnings remain.
Reports, captures and source hashes are in Library/LatticeValidation;
BreezeBaseline preserves the preceding implementation and user-tuned scene.

LATTICE strand-root refinement: the shader multiplies its drifting bend by the
squared normalized distance along each strand. The connected root has zero
bend and zero bend tangent; looseness increases toward the disconnected end.
The existing boundary-held tip constraint is preserved.
The 74-check Play Mode suite passed again on DX11, including shader compilation;
successive close-up renders confirmed straight roots and bending outer portions.

LATTICE RGB dot jitter: each dot now combines independent red, green and blue
circles in the existing shader. Two overlapping channels produce secondary
colors; three produce white, with no underlying white sprite. Jitter amplitude
increases with geometric distance inside the disruption contour. Radius, speed,
full-strength depth (grid cells), independent seed and an enable toggle are in
the field Inspector. The shared contour is sampled at up to 20 Hz and includes
padding outside the arena; the editor guide clips to the arena. The node
texture's green channel carries depth, while red remains the locomotion state.
The effect uses the paused field clock and independent per-node/channel timing.

Unity 6000.0.28f1 / DX11 passed all 85 checks. New checks compare depth against
an independent radial contour search and inspect rendered pixels for RGB layers,
white and secondary-color overlaps, animation, pause and the disabled effect.
Normal-scale and enlarged diagnostic captures were visually reviewed. No new
runtime errors occurred; existing spawn warnings remain. No player build was run.
Library/LatticeValidation/DotJitterBaseline preserves the preceding sources.

LATTICE RGB ghost trails: the existing dot shader samples six prior positions
per color channel from the seeded jitter motion. Older echoes shrink and fade;
maximum coverage per channel preserves the current dot and prevents stationary
brightness buildup. Dot Ghost Trails, Dot Trail Duration and Dot Trail Opacity
are on the field. History uses the paused field clock and current disruption
depth, with no persistent history textures or extra scene objects. The user's
jitter-radius tuning is preserved. DotTrailBaseline stores the previous sources.

The expanded suite passed all 92 checks on DX11. Rendered-pixel comparisons
confirm visible ghosts, preserved dot heads, frozen trails on pause, opacity and
duration behavior, and no halo at zero jitter. On/off and longer-history captures
were visually reviewed. No runtime errors occurred in the completed run; existing
spawn warnings remain. No standalone player build was run.

LATTICE blue visibility refinement: the blue dot layer and its ghosts now include
30% green, shifting them slightly toward cyan against black. Maximum channel
coverage preserves white three-way overlaps and zero-jitter dots. Existing pixel
assertions were updated for the tint; all 92 Play Mode checks passed on DX11,
and dot/ghost captures were visually reviewed. User trail tuning (duration 0.6,
opacity 0.75) was saved and preserved. CyanDotBaseline stores preceding sources.

LATTICE Level Select icon (2026-10-03): LS_LATTICE Icon.prefab is a standalone
presentation prefab with the existing LevelIcon selection hook. Ten cells along
each axis fill the volume with 1,331 nodes and 3,630 connections. LatticeLevelIcon
owns a transient mesh and edge/node buffers; its shader adapts the accepted
strand curves into two bending planes and retains RGB jitter, cyan-blue tint and
six ghost samples. A coherent warped volume combines two oblique native Perlin
projections. Boundary tips, reconnect growth and geometric interior jitter depth
are sampled at up to 20 Hz; nearby contour buckets bound the distance search.
The menu clock is unscaled. Skin/frame wires are emphasized for readability.

Create and Preview Level Icon (Ctrl+Shift+Alt+I) opens an isolated animated Editor
window; prefab edits reload the preview. Validate Level Icon (Ctrl+Shift+Alt+J)
passed 24 geometry/render/lifecycle checks, and Validate Icon in Play Mode
(Ctrl+Shift+Alt+K) passed 10 runtime checks including selection scaling, unscaled
time and multiple-instance cleanup. Large/menu-size/runtime renders were reviewed.
Evidence: Library/LatticeIconValidation. Full CPU field/state refresh averaged
10.95 ms in 20 Editor samples; rendering is excluded. No standalone build or GPU
target profile was run. Gameplay grid sources, release catalog, Build Settings
and existing level definitions are unchanged. See LATTICE/Level Icon.md.

### LATTICE playable registration and icon rotation (2026-10-03)

LevelDefinition-LATTICE now leads LevelCatalog as STAGE_001 at exponent -35.
The six existing definitions retain their order and become stages 002 through 007.
EditorBuildSettings appends enabled S-1_LATTICE, preserving every prior build index.
The existing gameplay scene now binds a standard LevelSceneContext, its own empty
StageProfile-LATTICE (the grid effect is continuous), explicit match/anomaly links,
and corrected stage labels. Its accepted field and player adapters are preserved.
The definition references the shared gameplay audio profile and a dedicated
InstructionsPanel-LATTICE derived from the established instruction layout.

The user reduced the icon to four cells per axis (125 nodes / 300 connections)
and tuned its size, noise and dots. Those values are preserved. Slow Tumble adds
smooth independently seeded angular velocity on three axes, default limit 8 deg/s
and direction timescale 8 s. A transient quaternion rotates shader geometry using
a property block; authored transforms and carousel placement are untouched. Bounds
include every rotated orientation. Menu animation uses unscaled time, and the
preview caption reads the actual authored size. The original ten-cell validator
now explicitly builds a dense fixture without changing the four-cell prefab.

LatticePlayableSetup.Register Playable Level is idempotent for catalog/build
registration and creates missing definitions/instructions/stage assets. The normal
carousel and SceneFlow code require no changes. LatticePlayableValidation exercises
the real Level Select -> Instructions -> LATTICE -> PostGame -> Level Select path,
shortening only the runtime fixture's remaining clock for match expiry. Twenty-five
flow checks passed, including all actors, standard regulation/economy, the scale
ruler, navigation wrap, paused-time rotation and authored tuning. Twenty-four dense
volume rendering/geometry checks also passed after adding rotation. Evidence is in
Library/LatticePlayableValidation and Library/LatticeIconValidation. No standalone
player build or target GPU profile was run.

Final screen captures verify centered LATTICE, STAGE_001, the -35 ruler marker and readable instructions. Unity's existing SceneReference.OnValidate also synchronized DYNAMO's stale serialized sceneName from S-8_MAGNETOSPHERE to its already assigned S-8_DYNAMO SceneAsset. Validation logged kinematic-velocity warnings in the shared player spawn routine and duplicate EventSystem warnings during return to Level Select; no runtime errors occurred. These shared components were not edited.


### Swipe live hitbox tuning outline (2026-10-04)

PlayerSwipeHitboxGuide adds an Editor-only yellow dotted XZ capsule outline to
PlayerMelee during the actual enabled Swipe/ComboSwipe damage window. It reads
that component's real collider at render time, including center, width, length,
scale and rotation. The outline appears in Game and Scene views, uses the same
body-relative dot sizing as Repulsor, and maps through the Singularity adapter
when present. Thrust, recovery, cancellation and idle do not show it. No damage
or attack motion logic changed; no scene or prefab wiring is required.

The default-on persistent toggle is MASSIVE > Player Tuning > Attack timeline >
Sweep > Show live hitbox outline (Editor only). The Play Mode Scene overlay no
longer duplicates Swipe with a full-sector arc; its Edit Mode planning overlay
is retained. Rendering and automatic guide attachment are excluded from builds.

An existing discrepancy became visible during validation: PlayerActor.prefab,
used by the demonstration actors, has no AttackSwordArcDriver. Its damage
capsule follows combat facing while its VFX follows the Swipe arc. The guide
intentionally reports the current physical shape. This request did not change
that behavior. The initial validator assumed natural arc movement and failed;
the corrected transform-following check explicitly rotates the fixture collider.
The initial failure and successful run are both retained as evidence.

Unity 6000.0.28f1 / DX11 compiled and passed 28 runtime/render/persistence
checks in an isolated copy, including 64 PhysX surface comparisons per tested
geometry, altered size/rotation/dimensions, active-window gating, cancellation,
toggle persistence after Play Mode/reimport, and all seven demos for two loops.
Windup, active, rotated-fixture and recovery captures were inspected. No runtime
errors occurred; existing kinematic-body spawn warnings remain. The test scene
now resolves by GUID after its move to Assets/Scenes/EXPERIMENTS. Evidence,
validated-source hashes and pre-edit backups: Library/SwipeOutlineValidation.
No standalone build was run. SwipeOutlineValidation.RunBatch is the repeatable
isolated-copy harness.

### Swipe physical sweep repair (2026-10-04)

The user confirmed that the new capsule guide flashed without sweeping. The
canonical PlayerActor Sword was missing AttackSwordArcDriver, and the controller
spread its arc across the whole 0.4-second stage while damage was enabled only
from frame 4 through 10 at 60 fps (0.1 seconds). This let the old arc finish after
the damage window. The preceding outline validation already reproduced the
stationary capsule; its failing rotation report remains in SwipeOutlineValidation.

PlayerActor now attaches the existing driver to its centered Sword transform.
AttackStage.SwipeArcProgress maps the full authored arc to the damage window;
controller/VFX and Edit Mode preview share this timing and curve. Windup holds
the starting edge, recovery holds the ending edge, and stage completion,
cancellation or disabling the driver restores the neutral sword pose. The driver
runs after combat-facing updates and before attack presentation. The attack's
existing duration, active frames, arc degrees, damage and user VFX assets remain
unchanged. Tuning labels now describe rotation during the hit window. Restart
Play Mode to recreate any actors instantiated before the prefab change.

SwipeOutlineValidation.RunBatch now tests natural motion instead of manually
rotating the fixture. Unity 6000.0.28f1 / DX11 passed 47 checks, including left,
front and right PhysX overlaps; hitbox/VFX heading agreement; reverse sweeps;
rotated player aim; cancellation, completion and disable reset; straight Thrust
and separate Repulsor; guide geometry/visibility/persistence; and all seven demos
for two loops. At the authored timings, 10 active samples covered -59.6 to +58.5
degrees. Early/middle/late rendered captures were visually reviewed. No runtime
errors occurred; existing kinematic spawn warnings remain. No standalone build
was run. Evidence and pre-edit backups: Library/SwipeMotionValidation.

### Thrust launch-frame experiment reverted (2026-10-04)

The user preferred the original effect traveling with the character and requested
undoing the launch-frame freeze because it left the particles too far behind.
Restored the six affected source/editor/prefab files byte-for-byte from the
pre-experiment backups. Removed the freeze toggle, detached-instance ownership
changes and GPU tail-direction change. The experiment's harness is retained under
Library/ThrustLaunchValidation/retired-harness, outside Unity's imported sources.
Particle settings and earlier Swipe hitbox/motion fixes are preserved. The small
visual quirk on abrupt turns at the end of Thrust is accepted for now. No new
motion treatment was introduced. Reversion verified against the saved originals;
no new runtime test run was needed for the exact restoration.

### LATTICE smooth Resonance deformation (2026-10-04)

LatticeDisruptionField now presents Resonance using SINGULARITY's continuous
quintic attraction profile and soft displacement limit. The saved LATTICE scene
uses the same gain 0.06, maximum 0.65 and boundary feather 0.8. Its Inspector toggle
restores the original simulated response. An optional IResonanceGridPresentation
on the target VectorGrid consumes the live arc samples before spring forces are
submitted; other standard grids retain their existing behavior. No shared grid
prefab or SINGULARITY assets changed. Existing noise, strand-root/tip constraints,
RGB trails and hop coordinates remain authoritative.

Unity 6000.0.28f1 / DX11 validation: 107 checks passed, including 405 GPU samples
against the SINGULARITY reference, no duplicate spring forces, interaction gate
and destruction cleanup, legacy fallback, and byte-identical isolated RGB dot
renders with Resonance off/on. Connected and disrupted captures were inspected.
The first new pixel comparison included other scene draws; isolating the field
mesh and property block resolved that test-fixture issue. Evidence is in
Library/LatticeValidation/report.txt and resonance-*.png. No runtime errors or
exceptions in the final run; existing shared player-spawn kinematic warnings
remain. No standalone build was run.

### LATTICE grid attacks (2026-10-04)

Attacks starting inside disruption now capture their grid movement per combo
stage. Thrust follows its existing animation curve to one neighboring dot in the
nearest eight-way input direction; diagonals travel farther in the same authored
duration, as requested. Swipe and Repulsor hold their starting dot while their
weapon/effect animations continue. Thrust heading and endpoint survive changing
input and leaving the disruption. Stages starting outside use normal movement.
Held-stick locomotion is suppressed while the attack owns the body, with immediate
grid locomotion after successful completion. Blocked cell routes hold the starting
dot; dynamic obstruction uses the existing impact cancellation. Stun, disabling,
movement abilities and protected knockback release the captured path.

The implementation is isolated in PlayerAttackController.Lattice.cs plus small
hooks in the attack controller, LatticePlayerMotor and player movement ownership.
No attack profile, scene or prefab tuning changed for this feature. Existing Swipe
and Thrust presentation edits from other work are preserved.

Unity 6000.0.28f1 / DX11: focused attack validation passed 85 checks; the subsequent
full LATTICE regression passed 185 checks, including existing movement, rendering
and Resonance coverage. Counts include repeated frame assertions. Intermediate
Thrust streaks/particles were visually reviewed. No runtime errors or exceptions;
existing shared player-spawn kinematic warnings remain. Evidence:
Library/LatticeValidation/report.txt and attack-thrust-mid.png. Repeat via
MASSIVE > LATTICE > Validate grid attacks (Ctrl+Shift+Alt+T), or Validate prototype
(Ctrl+Shift+Alt+L) for the full suite. No standalone build or controller feel
playtest was run.

### LATTICE standard scene organization (2026-10-04)

S-1_LATTICE follows NOVA's six-root layout: 00_Systems, 01_Presentation,
02_Arena, GameplayObjects, 04_UI and 90_EditorOnly. The existing field now lives
at 02_Arena/Surface/GRID/VectorGridGPU; its shader, noise, RGB trails, smooth
Resonance and movement/attack adapters remain unchanged. Existing players and
respawn references, two encounters, camera stack, goals, HUD and end effect are
retained. The enemy timeline is in GameplayObjects/LevelContent/LATTICE, and the
paired Amplifier/Resonance encounter is in GameplayObjects/Encounters.

Added the standard TeamAmplifierToastPresenter using NOVA's shared settings.
Bound LevelSceneContext.spawners to GameplayObjects/Spawners (power-up spawning),
replaced its inactive legacy title/number references with the visible labels,
bound the match's shared score economy explicitly, and bound the timer's phase
label. Existing enemy and Amplifier encounters retain their own score gates.
LevelSceneContext owns input startup: the redundant Rewired Initializer was
removed because it persists its whole root, which would incorrectly move the
grouped match/roster into DontDestroyOnLoad. Global session/music/audio continue
using their existing bootstraps; no runtime manager implementation changed.

The outer legacy PLAYING FIELD prefab instance was unpacked to move its existing
children into standard groups. Shared prefab assets were not edited. Migration
checks compared original instance references, world poses/activation, and
serialized component settings before saving. Evidence and the original scene
backup are under Library/LatticeSceneLayout. Editor entry points are Apply
standard scene layout (Ctrl+Shift+Alt+B), Validate saved scene layout, and Validate
standard level flow (Ctrl+Shift+Alt+N); the latter returns the editor to LATTICE
after the carousel/instructions/match/results/return test.

Validation in Unity 6000.0.28f1 / DX11: saved-scene structural/reference checks
passed, then 187 full LATTICE Play Mode checks and 28 complete launch/return
checks passed. This includes single input ownership, scene-local roster startup,
live HUD identity, amplifier feedback, spawner preparation/resolution gating,
existing disruption/Resonance presentation and quantized locomotion/attacks.
Gameplay capture visually reviewed; no runtime errors or exceptions in final
runs. Existing shared kinematic-spawn and menu EventSystem warnings remain.
No standalone build was run. Flow validation now compares noise/dot tuning to
the current icon prefab, preserving the user's authored values. Reports:
Library/LatticeSceneLayout, Library/LatticeValidation/report.txt and
Library/LatticePlayableValidation/report.txt.

### COSMOS custom oval layout (2026-10-04)

S-9_COSMOS is the user's duplicated LATTICE scene. Reviewed the Notion MASSIVE
GDD and the recent LATTICE/PLAYER cabinet chats before this pass. COSMOS now has
its own LevelDefinition and empty StageProfile, stage 009 / exponent 26, and the
visible COSMOS identity. The current pass is the layout only, with inherited
encounters retained for testing. Catalog, icon and instructions are still pending.

The field is 28 units wide and 12 high at the centre. Broad circular top/bottom
arcs join the round outer goal caps tangentially to form one convex oval. Goal
score containers shrink from 12 to 8.4 units high; separators are about 8.66 high.
The complete outline is 36.4 units wide. Main-camera orthographic size 10.8 frames
both caps at the cabinet's 16:9 aspect, with the existing HUD clear of the field.
This is an authored oval silhouette, not an exact astronomical map projection.

ArenaBoundsFromVectorGrid has an opt-in oval profile, enabled only in COSMOS.
CosmosArenaLayout clips the grid, draws the complete outline and generates 194
transient solid wall segments around the playable field. Goal caps remain outside
that field and have no outer-cap colliders. The original rectangular wall builder
and its old wall objects are disabled in this scene. Padded bounds queries,
recovery, spawn anchors, power-up and Amplifier Core placement follow the curve.
Generated geometry is not serialized into scene assets.

Goal transforms, Core capture/attraction distances, local capture VFX and legacy
score size limits scale with the shorter containers. Score voids and promotion
rings derive their dimensions from the resized Disc templates. The original
Discs are transparent because the layout draws the tangent caps; promotion clones
receive visible colors as before. ScoreSphereScript now targets its separate
visual child, preventing score growth from resizing its capture collider. Shared
score authority, tier thresholds and promotion timing are unchanged.

Unity 6000.0.28f1 / DX11: the final Play Mode run passed 82 checks. Coverage includes
convex shoulders, complete camera framing, padded bounds, four spawn anchors and
four respawns, all 194 physical walls, six moving-rigidbody contacts, regulation
startup, score/tier previews through 999999999999999, visible promotion rings,
fixed capture volumes and real Core captures by both teams. Gameplay and high-score
captures were visually reviewed. No runtime errors or exceptions; existing shared
kinematic-spawn warnings remain. No standalone build or cabinet-controller feel
playtest was run. COSMOS is saved and left open in Edit Mode.

Tools: MASSIVE > COSMOS > Apply oval layout (Ctrl+Shift+Alt+F10), Validate layout
in Play Mode (Ctrl+Shift+Alt+F11), Inspect current layout (Ctrl+Shift+Alt+F9).
Evidence and the pre-install scene backup are in Library/CosmosLayout. Layout
source and tuning notes are in Assets/Scripts/Anomalies/COSMOS/README.md. Reapply
the layout after changing the bounds ratio to reconcile goal scaling.

Future visual brief from the user: blue/white/pink cosmic-web filaments on black
voids, initially near-uniform distribution, clumping toward present-day structure
around mid-match, then void dominance by regulation end. Research/source selection
and cosmic-web simulation have not started in this layout pass.

### COSMOS true ellipse refinement (2026-10-04)

This supersedes the first COSMOS outline/camera geometry above. The user restored
camera size 9 and explicitly requested that only grid/goal geometry change, with
permission for a taller field to overlap the timer. COSMOS now uses a single exact
ellipse, 31.2 wide by 14 high, with a 24-wide playable field. The goal separators
are at X = +/-12; each elliptical cap is 3.6 deep and approximately 8.95 high.
Both cameras and all HUD/player poses are retained. The three unused spawn markers
clamped by the first pass were restored to their original authored positions;
actual player Respawn references remain unchanged. The setup checks all 277
non-grid/goal transforms and both camera configurations before saving, and no
longer adjusts the camera or clamps marker transforms on reapplication.

ArenaBoundsFromVectorGrid now describes the exact ellipse. VectorGridGPU creates
curved simulation rest rows, and GridCurveSampling.hlsl applies the same map in
production GPU sampling. Row spacing is widest at the centre, decreasing to about
64% at the goal separators. Thus force sampling, rendering and the physical field
share coordinates. Grid fragments and displaced simulation nodes use the ellipse
boundary; rectangular scenes leave this opt-in profile disabled. The original mask
planes retain their poses/depth but use a COSMOS mask material that opens the taller
ellipse, preventing the old rectangular aperture from hiding its upper/lower rows.

CosmosGoalShape maps the existing half-disc score animation into the elliptical
cap. ScoreVoidMetaballsVisual uses the adapter only when assigned in COSMOS; its
normal Disc path remains the default elsewhere. Score-fill inverse mapping and
promotion-ring forward mapping agree. Both the mass and corona are clipped to the
same cap, avoiding renderer-box rim artifacts at the narrow tips. Capture ranges,
local VFX dimensions and legacy size limits still track goal height, while score
renderer width and height fit the cap independently. Scoring authority is unchanged.

The editor migration is revision 3. Backups from the start of this refinement are
under Library/CosmosLayout/EllipseBefore; the initial duplicate is also preserved
as Library/CosmosLayout/COSMOS-before.unity. Runtime meshes remain transient.
Detailed settings and editor commands are in Assets/Scripts/Anomalies/COSMOS/README.md.

Final refinement validation in Unity 6000.0.28f1 / DX11 passed 89 Play Mode checks:
production GPU curved-row sampling, row compression, rectangular fallback, exact
elliptical goal mapping, original camera framing, padded bounds, actual respawn
references, all 194 walls, six physical contact probes, score growth, visible
elliptical promotions, fixed capture volumes and Core capture by both teams.
Gameplay and high-score captures were visually reviewed, including removal of the
right goal's renderer-box rim artifact. No runtime errors or exceptions occurred;
existing shared kinematic-spawn warnings remain. No standalone build or controller
feel playtest was run. Report: Library/CosmosLayout/report.txt. COSMOS is saved and
left open in Edit Mode with the user's camera size 9.

COSMOS Inspector sizing (2026-10-04): `CosmosArenaLayoutEditor` adds overall
width (16-48 world units, including both caps) and height (6-24) sliders to the
layout component. `MASSIVE > COSMOS > Select layout controls` selects it (F8 with
Ctrl+Shift+Alt). Grid size remains the source of truth; the bounds' separator
proportion stays fixed. The Editor shares `CosmosLayoutSetup.FitGoals` for score,
capture and local VFX scaling, previews immediately, and supports complete-object
Undo for legacy RectTransforms. Sliders are Edit Mode only; save the scene to
retain changes. Camera/HUD poses and current 31.2 x 14 dimensions were preserved.
Manual Inspector resize, linked-scaling and Undo/Redo checks plus compilation
passed; details in `Library/CosmosLayout/sliders-validation.txt`. No additional
Play Mode run was needed for this Editor-only addition.
COSMOS cosmic web (2026-10-04): the user chose a 36 x 13 overall oval (grid
27.692307 x 12.999997). `CosmicWebBackground` is now installed on VectorGridGPU
with its scene-local GameManagerScript and serialized shader reference. It draws
56,000 persistent GPU matter tracers across a deterministic 3-D Voronoi filament
network, evolving from uniform initial positions to
filaments/clusters at regulation midpoint and concentrated knots at regulation end.
2% of late particles remain in narrow filaments. No gameplay forces or collisions
are added. This is an observationally inspired geometric approximation, not actual
JWST catalogue data or an N-body simulation; science sources and limitations are in
the COSMOS README. The latest NASA JWST/COSMOS weak-lensing map was reviewed.

The component reads the actual regulation clock, holds at age 0 through preparation
and countdown, and respects paused regulation. Its shader follows the live oval
profile with 0.25-world-unit padding. The organic revision reconnects outer
filaments through an enclosing ellipsoid and smoothly condenses the projection
before the oval edge and goal separators. The fragment mask is a glow safety
boundary. It renders behind the grid, above the ground.
Existing live transforms, camera settings and the user's oval proportions were
verified unchanged during installation. Inspector controls: Padding, seed, particle
count, filament/cluster palette, brightness, galaxy size and Edit Mode Preview Age
(0 early, 0.5 current-style web, 1 late voids). Play Mode ignores the preview age.

Organic revision (2026-10-04): replaced regular site spacing and weak sine warps
with density-modulated irregular sites, three scales of coherent noise, curved
filaments and fine offshoots that rejoin their parents. Local noise controls
nonlinear formation/accretion timing and curved infall. Same particles and exact
age 0/0.5/1 anchors are retained. Added Organic Strength (rebuilds), Evolution
Variation and Edge Condensation (GPU parameters) to the Inspector; authored defaults
are 1, 1 and 0.6. All 182 clipped outer endpoints have continuous return connections.
The return topology/lens-like projection is an arena presentation choice, not literal
cosmic geometry or a physical gravitational lensing calculation.

Validation: 27 Play Mode checks passed, including deterministic paths, actual
production HLSL trajectory/projection comparison (max CPU/GPU error 0.000004),
padded bounds at nine sampled ages, regional collapse variation, exact era anchors,
unchanged gameplay random state, clock/bonus-pause behavior, GPU buffer reuse and
release/re-enable. At 120 x 44 sampling resolution, empty cell fractions were 0.7%
early, 17.9% at midpoint and 65.9% late. All five 1920 x 1080 gameplay captures were
visually reviewed. No runtime errors/exceptions; existing shared startup warnings
remain. No standalone build or cabinet frame-rate qualification was performed.
Reports/captures: Library/CosmosWeb/validation.txt and early.png, forming.png,
middle.png, evacuating.png, late.png. Before/after scene comparison contains only
the three added web controls; camera, oval, HUD and existing element poses are unchanged.
Backups: Library/CosmosWeb/BeforeWeb.unity and OrganicBefore (previous source/captures
and the user's saved scene). Install shortcut Ctrl+Shift+Alt+F7;
validation shortcut Ctrl+Shift+Alt+F12. Scene is saved with midpoint Edit Mode preview.

COSMOS supernova extension (2026-10-05): based on pulled v5 commit 3d9127b1,
which adds the user's revised web motion/heat controls, normal supernova scheduler,
level icon and dictionary registration. The current saved oval is 36 x 13,
camera size 9, web padding 0.05, brightness 1.5 and Edit Mode preview age 1.
Those authored settings, all 494 existing transforms and both cameras were
preserved during this extension; earlier preview/padding notes above are historical.

VectorGridGPU now has two CosmicSupernovae components sharing the existing web
and regulation-clock scheduling logic, each with its own controls, pool and random
stream. Normal retains its authored 3/sec peak and now chooses gold, slightly cyan
white, cyan-blue or light purple once per event. Superluminous uses a half-height
curve (including halved tangents), yielding 1.5/sec at midpoint with the same
rate scalar. It releases the existing pooled Orbital Mass Nugget (reward multiplier
1) instead of the normal Mass Nugglet (0.1). Its radius is 1.8 vs 0.75, brightness
8 vs 4, burst duration 0.65 vs 0.4 and glow die-off 0.75 vs 0.3 seconds. The shared
shader adds a blue-white core, cyan inner glow and noisy orange expanding shell.
The Inspector distinguishes both layers and uses the same 0–1 curve display scale.

Supernova validation passed 24 Play Mode checks in Unity 6000.0.28f1 / DX11,
including frequency across 201 ages, both real pickup types/reward multipliers,
per-event color stability, buildup, pause, rewind, pool reuse and independent
layer cleanup. No runtime errors/exceptions; four existing kinematic startup
warnings remain. Shader comparison and live gameplay captures were reviewed.
Installation preserves existing settings; validation leaves the saved scene
unchanged and returns to Edit Mode. Reports/images: Library/CosmosSupernovae.
Menu: MASSIVE > COSMOS > Supernovae; install shortcut Ctrl+Shift+Alt+F6, validation
Ctrl+Shift+Alt+F5. No standalone build, cabinet profiling or collection-balance
playtest was performed. Full controls are documented in the COSMOS README.

COSMOS cloud refinement (2026-10-05): superluminous clouds now expand for 1.8
seconds and then dissolve over 2.4 seconds, independently of the existing 0.65s
center flash and 0.75s localized glow. New Inspector controls expose these two
times, Cloud Distortion (0.85), Cloud Mixing Speed (0.45) and Telegraph Distortion
(0.85). Warped noise mixes blue/orange gas into asymmetric moving lobes and wisps,
replacing concentric shells and the circular telegraph. Normal visuals and all
spawn/pickup controls retain their prior behavior. The existing shellColor and
shellDetail fields are relabelled Warm Ejecta Color and Cloud Fine Detail.

CosmosArenaLayout now owns a transient continuous black mask quad using the
existing CosmosArenaMask shader and the live full ellipse, including goal caps.
The new serialized shader reference and Mask Outside Oval toggle are installed.
The quad spans the cabinet view, immediately above the cosmic depth slab and
behind gameplay/HUD. This avoids the rectangular masks' corner gaps and preserves
world-space score glyphs. Buffers/materials are released with the layout. The saved
scene diff for this refinement adds only the mask reference/toggle and cloud fields;
299 authored transforms, both cameras, web settings and existing tuning are unchanged.
Backup: Library/CosmosSupernovae/BeforeCloudRevision.unity. Apply menu/shortcut:
MASSIVE > COSMOS > Supernovae > Apply cloud and oval mask revision (Ctrl+Shift+Alt+F4).

All 28 supernova Play Mode checks passed, including the longer cloud lifetime,
3,292/3,292 sampled border spill pixels covered, and 5,239/5,239 HUD pixels retained
after startup (allowing the existing unit-label pulse). Actual cloud/telegraph,
live gameplay, border and HUD captures were reviewed. No runtime errors/exceptions;
existing shared warnings remain. Latest report/images overwrite the prior ones in
Library/CosmosSupernovae. No standalone build or cabinet performance qualification.

## Particle Accelerator sustained turret plasma — 2026-10-05

The player power-up now charges while Sword is held, sustains until release, and
cancels on stun/unequip. Release stops damage/contact emission immediately while
the beam retracts. The canonical definition references the new
`ParticleAcceleratorSustainedBeam.prefab`, created once by
`ParticleAcceleratorBeamSetup` from the Particle Beam Turret's authored visual,
core plasma and contact plasma children. Enemy controllers/colliders are not
copied; the turret prefab and its tuning are unchanged. Legacy pulse fields and
projectile remain serialized for compatibility but are hidden from current tuning.

`ParticleBeamAimTrail` keeps bounded unwrapped aim history and integrates delayed
tangents along the beam's forward axis. The shared renderer optionally adds this
path to both its plasma strands and collision envelope; turret users have no aim
trail and retain their prior behavior. Swept segment queries clip the curved beam
against shields, bodies, enemy hurtboxes, solid geometry and arena bounds. Filters
preserve simulation isolation. The player ability advances the rig's explicit
clock, so match locks and zero delta freeze it; the rig owns all transient VFX.

Power-up Settings > Particle Accelerator exposes charge, range, DPS, grow/fade,
opening radius, movement, muzzle, collision mask, propagation speed (45 world
units/s), maximum delay (.35s) and bend cap (35 degrees). Delay zero disables the
arc. Its appearance foldout edits the shared beam/emitter/contact prefab; new
equips instantiate that tuning. Definition controls are read live. Player size
scales width/muzzle/plasma and projectile reach scales axial range. Updated demo
choreography holds through charge and releases after beam contact.

Unity compilation succeeded. `ParticleAcceleratorBeamValidation` passed 26 checks
covering high-framerate/wrapped heading history, visible arcing, curved collision,
impact placement, damage, release/retraction, simulation isolation, real input,
match pause, cleanup and all three accelerator shield/parry demo lanes, with no
runtime Console errors. The report and reviewed rendered arc are in
`Library/ParticleAcceleratorBeamValidation`. Run via the Particle Accelerator tab
or MASSIVE > Power-ups > Validate Sustained Particle Accelerator in PLAYER ACTIONS.

The final live beam resolves in LateUpdate after shield/release input, before its
renderer uploads the path. Charge completion does not double-spend the same frame
delta on beam growth/damage. This also preserves parries during slow capture
frames. Final validation: 26 beam checks plus all 189 power-up/icon/settings and
collider checks passed, with zero runtime Console errors. Existing startup
kinematic-body warnings remain. No standalone build was run.

### Particle Accelerator energy-meter revision (2026-10-05)

Supersedes the charge/recharge behavior above: acquisition fills the ring and
Sword fires immediately. Holding drains a normalized meter; idle gameplay time
refills it. Shared defaults are six seconds of continuous fire, four seconds
empty-to-full refill and a one-second minimum burst per press. Releasing before
that minimum keeps the burst running, capped by available energy. Release after
the minimum, exhaustion, stun, parry or unequip stops it. Exhaustion requires a
fresh press to use recovered energy. There is no full-charge threshold or
post-shot cooldown. The normal power-up lifetime still applies.

The ability computes energy and firing time in the beam's LateUpdate callback,
after all actors apply input; a partially funded last frame advances collision
only for its available firing time. Remaining frame time refills and advances
the non-damaging fade. Match input locks and transit pause these clocks. The
existing ParticleAcceleratorVFXModule has a ring-only energy mode; its orb and
aim dots stay disabled. PlayerPowerUpController exposes read-only
ParticleAcceleratorEnergy01. The shared settings tab exposes the three timing
controls, derived minimum-burst percentage and ring appearance prefab fields.
Legacy serialized charge/cooldown fields remain hidden for asset compatibility.

The accelerator demo choreography now raises parry shields before instant
firing; long power-up demo sequences wait for meter refill. Unity compilation
and 40 focused Play Mode checks passed, including real input, one-second taps,
partial-charge caps, exhaustion, refill, pause, cleanup and all three shield/
parry lanes. The energy-ring firing capture was visually reviewed at
Library/ParticleAcceleratorBeamValidation/energy-ring-firing.png.
The broader 189 power-up/icon/settings/collider checks also passed, for 229
checks total with no runtime Console errors. Unity was returned to Edit Mode
with the Particle Accelerator settings tab open. No standalone build was run.

### Particle Accelerator enemy damage verification (2026-10-06)

`MASSIVE > Power-ups > Validate Particle Accelerator Enemy Damage` passed 58
Play Mode checks against the seven canonical enemy prefabs: Drone, Ranged Drone,
Seeker, Particle Beam Turret, Carrier and both Dyson Sphere variants. Collision,
continuous DPS, impact plasma, pause, simulation isolation, non-damaging fade,
and one-time lethal defeat attribution to the shooter passed without runtime
Console errors. The isolated fixture advances the beam clock directly for lethal
checks; it does not bypass energy in normal gameplay or validate score awards.
Report: `Library/ParticleAcceleratorBeamValidation/enemy-report.txt`.

Current shared DPS is 0.08 against both player mass and enemy health. Six seconds
of charge supplies slightly under 0.48 damage after beam growth, versus health
1 for Drone/Ranged Drone/Seeker, 3 for the turret, 6 for Carrier and 20 for Dyson.
The damage path works, but current tuning cannot kill a fresh Drone in one tank.
No production damage code or balance values were changed during this check.

### Separate Particle Accelerator target damage (2026-10-06)

The shared definition now exposes `playerDamagePerSecond` and
`enemyDamagePerSecond` under Damage / Mass in Power-up Settings. The beam selects
the rate by target type; player shields and actual-loss mass transfer retain their
existing behavior. Both rates remain 0.08 initially. The previous serialized
`damagePerSecond` maps to the player field through `FormerlySerializedAs`; the
single canonical definition explicitly stores both values. Player demo safety
limits use the player rate. These shared definition values are read live.

Unity compilation and 78 focused Play Mode checks passed with no runtime Console
errors. The enemy validator now covers independent live rate changes and zero
damage on all seven enemy prefabs, plus a real player target and player mass
transfer. Results remain in `Library/ParticleAcceleratorBeamValidation/enemy-report.txt`.
Existing unrelated compiler and player-spawn kinematic-body warnings remain.
No scene/prefab changes or standalone build were needed.

### Particle Accelerator firing movement controls (2026-10-06)

Power-up Settings > Particle Accelerator > Firing movement exposes movement and
turning speed scales from 0 to 1. Existing movement tuning remains 0.65; turning
defaults to 1. Movement scales propulsion and the speed cap, including a true
zero. Turning scales the player's maximum yaw speed and supplies the same limited
heading to combat facing and the beam's visual/collision path, before distance
based arc propagation. Shared definition edits apply live during firing. Both
restrictions last through minimum tap bursts and clear when firing stops or the
power-up is removed. Idle aim remains unrestricted.

Unity compilation and all 18 focused Play Mode checks passed with no runtime
Console errors in S-0_ATTRACT. Coverage includes zero movement/turning, half and
full turn rates, live edits, beam/facing alignment, pause, minimum bursts, meter
exhaustion and unequip. Run MASSIVE > Power-ups > Validate Particle Accelerator
Firing Movement; results are in
`Library/ParticleAcceleratorBeamValidation/movement-report.txt`.
The settings labels and sliders were verified in the native window. No scenes
were saved and no standalone build was run.

### Particle Accelerator easing, pickup input and life presentation (2026-10-06)

The shared Particle Accelerator definition exposes **Aim Direction Ease Seconds**
under Firing movement, initially 0.2. Firing yaw uses SmoothDampAngle with retained
angular velocity and the existing turn-rate limit, so direction reversals brake
before accelerating the other way. Zero restores constant-speed turning; zero
turn scale locks immediately. Idle facing and beam arc propagation retain their
separate behavior. User tuning present before this change (movement 0.2, turn
0.05, enemy DPS 4, player DPS 0.2, radius 0.2, duration 150) was preserved.

A controlled Play Mode reproduction confirmed both reported defects before the
fix: equipping during the acquiring attack's input frame fired immediately, and
the ring remained visible after DeathHidden. The ability now rejects the equip
frame's attack and remembers whether Sword was already held before pickup;
release/repress arms the next shot. Death cancels the beam and also requires a
fresh press after respawn. Existing power-up lifetime and idle recharge remain.

ParticleAcceleratorVFXModule follows the player's life-effect radius during death
and respawn, using existing life events to hard-hide the ring between them.
Bindings are removed on rebind/destruction. ParticleAcceleratorAimGuide draws the
turret's white volumetric dash style while equipped and idle, clips against beam
eligible blockers and arena bounds, and hides during firing/death. It is created
and destroyed with the beam; no scene or prefab installation is needed.

Unity 6000.0.28f1 compilation, 22 refinement Play Mode checks and all 18 existing
firing-movement checks passed with no runtime Console errors in S-0_ATTRACT.
Refinement coverage includes equip-frame/held input, fresh press, obstacle-clipped
guide, idle direction, reversing with easing, live zero easing, zero turn scale,
release, actual death while firing, shrinking/hidden ring, respawn and unequip.
Rendered captures of idle/clipped guides and shrinking/hidden death states were
inspected. Run MASSIVE > Power-ups > Validate Particle Accelerator Refinements;
reports and images are in `Library/ParticleAcceleratorBeamValidation`, including
`refinements-before.txt`, `refinements-report.txt`, and `movement-report.txt`.
No scenes were saved or standalone build run.

### Pickup contacts, icon displacement and beam regrowth (2026-10-06)

ParticleAcceleratorBeam resumes its initial smooth growth curve from the last
blocked length using Beam Grow Seconds when an obstruction leaves. Collision,
damage and impact plasma are restricted to the growing tip; nearer blockers
still clip immediately. Aim history and the full visual reference path remain
independent of the clipped length, preserving the existing arc.

PowerUpPickup shares an idempotent TryActivate path between trigger-enter,
trigger-stay and Repulsor's final-radius overlap sweep. Thrust, Sweep and active
Repulsor all qualify; the player's body does not. MatterNuggetScript now requires
the non-trigger Player-tagged body for collection, so swords, Repulsor and shields
cannot gather free Matter/Orbital nuggets or nugglets.

PU_MassNode's nested decorative nugget now removes MatterNuggetScript as well as
its Rigidbody/colliders. Merely disabling that script let RequireComponent
restore a child Rigidbody, leaving the inner particles behind during a shove.
The normal-physics fixture reproduced a 0.256-unit separation before the fix and
zero afterward. The other three canonical icons stayed centered in the same
physical-contact test before and after; no unrelated visual changes were made.

MASSIVE > Power-ups > Validate Pickup Contacts and Beam Growth passed 56 Play
Mode checks in Unity 6000.0.28f1, with no runtime Console errors. It covers real
stage collider contacts, duplicate claims, existing-overlap Sweep, final-radius
Repulsor, collection filtering, all four icons during physical displacement,
beam regrowth, delayed next-target damage/plasma, live growth speed and very short
frame steps. Reports and inspected bump captures are under
Library/PowerUpContactValidation. No scenes or user tuning were saved/changed.
The existing 78 enemy/player damage checks and 22 input/aim/death refinement
checks also passed with no runtime Console errors (156 checks total). Enemy
validation now uses low DPS on its cloned definition for nonlethal assertions,
then authored DPS for kill attribution; the former live-tuning assumption failed
because the user's 6 enemy DPS killed targets before contact could be inspected.
Compilation and diff whitespace checks passed; no standalone build was run.

### COSMOS enemy formation mapping (2026-10-06)

`EnemyArenaLayout.World` now follows the grid's oval column mapping when the
referenced bounds has `ovalOutline`. `Resolve` adds a scene-level
`ovalSpawnInset` (default 0.25 world units) to non-socket placements using the
offset supporting lines of the 96-segment physical boundary. Goal caps stay
outside spawn territory. Top/bottom sockets remain on the wall, using an inward
ellipse normal and preserving authored aim offsets; mobile facing angles remain
authored. Rectangular layouts keep the original mapping. Scene gizmos and the
Encounter Composer's boundary preview reflect the oval. No encounter assets,
Director scheduling, camera, HUD, goals, or saved scene values were edited.

Unity 6000.0.28f1 compiled this change. `CosmosEnemyLayoutValidation` passed 47
Play Mode checks in the saved COSMOS scene: 441 inset/symmetry samples, live
dimension/scale changes, rectangular fallback, 216 original/mirrored formation
poses, and all 16 selected cues (183 real releases). Warnings and spawns agree;
release groupings and adjacent cadence retain authored values within the existing
scheduler's warning/release tick tolerance. The fixture runs cues individually
with a demonstration scope and pauses spawned enemies; it is placement validation,
not a full match balance test. Timeline, formation and saved scene hashes are
unchanged. Reports and visually inspected rows/mounts captures are in
`Library/CosmosEnemyLayoutValidation`. No standalone build was requested or run.
The pre-existing optional side-turret variant references unavailable
`SideLeftTop`/`SideRightBottom` sockets; these were reported and not installed.
Existing player-spawn kinematic-velocity warnings remain; no new runtime errors.

### COSMOS full-sequence spawn audit (2026-10-06)

Investigated missed encounters without changing gameplay, scene, timeline or
formation settings. `CosmosSpawnAudit` adds MASSIVE > COSMOS > Audit full encounter
spawning. It probes the real pickup prefabs with the Director's MemberClear path,
then records cue/slot rejection transitions through the full sequence. Evidence:
Library/CosmosSpawnAudit/baseline.txt and baseline-summary.txt.

All four current power-up prefabs plus Matter nugget, Orbital Mass Nugget and
Mass Nugglet permitted overlapping enemy arrivals. Their actual colliders are on
Default (0), outside the Director's 2688 mask (PowerUps, NoSpawnZone, Obstacle).
The mask's PowerUps bit does not itself imply that the present pickup prefabs
block. COSMOS retains Resonance/Amplifier overlap permission and has no shared
Amplifier placement region. Positive controls confirmed ordinary obstacles,
NoSpawnZone volumes and player clearance still reject arrivals.

The unattended 2x-time replay finished all 16 cues: 100 of 183 authored enemies
spawned, with 3 complete, 5 partial and 8 skipped cues. Failures were existing
enemy/player occupancy, occupied turret sockets and the 80-total population cap;
there were no collider, warped-footprint or full-query rejection reasons.
Seeker at 27s and Dyson at 39s were blocked by enemies; Carrier at 63s by a player;
the 99s turret cue reused TopLeft/BottomRight still occupied by the 51s turrets.
The 150.5s Ranged rows requested 20 against 53 alive + 19 reserved; the 159s
Ranged phalanxes requested 20 against 62 alive. Both exceeded the total cap.
Seeker, Dyson, Carrier and turret formations still inherit Strict integrity and
zero adjustment; their cue overrides are disabled and allowed lateness is 3s.
Thus a blocked member can reject an entire strict formation. Flexible Drone
formations instead skipped individual occupied slots. Initial population
reservation still requests the entire formation, even for Flexible integrity.

This was a diagnostic replay with normal AI, stationary unassisted players,
normal pickup spawning, no scripted kills/heals and no occupancy overrides; its
counts do not reconstruct the user's earlier playthrough. Unity compiled and
reported zero runtime errors. The authored timeline and saved scene hash stayed
unchanged, and COSMOS returned to Edit Mode. Existing player-spawn warnings remain.

### Flexible formations and wall quadrants (2026-10-06)

The follow-up replaces the strict Seeker, Dyson, Carrier and turret formation
defaults referenced in the audit above. All formations used by the shared Enemy
Lab timeline, including its fallbacks, now use Flexible integrity with independent
slot releases and 3s blocked-slot grace. Existing Drone/Ranged formation tuning is
preserved. Seeker/Dyson can adjust up to 1.5 world units before warning; both Carrier
formations can adjust up to 2. These are shared formation asset settings, with the
existing per-cue overrides still available in the Composer.

`EnemyArenaLayout.Socket` supports opt-in searchable wall ranges while preserving
legacy fixed mounts. COSMOS's TopLeft/TopRight/BottomLeft/BottomRight mounts cover
their entire half of the top/bottom boundary. Endpoints run from wall center to
corner so equal fractions yield mirrored positions. `EnemyDirector.WallPlacement`
searches a bounded 64-step grid nearest the preferred position, first attempting
equal fractions across the unannounced pair, then independent available positions.
Oval positions follow the curved wall and update the inward normal at each candidate.
Once announced, warning positions remain immutable; blocked arrivals retry/expire
under the existing flexible policy. A live turret reserves its inward-offset body
footprint rather than monopolizing the whole quadrant, allowing later waves in
remaining space. Destruction/restart clears its footprint reservation.

The Composer highlights selected wall ranges in yellow and offers **Inspect wall
ranges**. The arena layout Inspector exposes **Search wall range**, endpoints,
preferred position, inward direction, aim arc, clearance offset and support collider.
Mobile displacement does not constrain wall searches. New Lab/ORBITAL setups use
the same quadrant defaults; existing other scenes retain their saved mount modes.
Population limits, exclusion volumes, player/enemy clearance and environmental
overlap permissions still apply. Flexibility does not override the population cap.

`MASSIVE > Encounters > Validate flexible COSMOS placement` runs temporary Play Mode
fixtures with real prefabs; evidence is written to
`Library/EnemyFlexiblePlacement/validation.txt`. The setup utility is at
`MASSIVE > Encounters > Use flexible formations and wall quadrants in current scene`.

Validation passed 31 checks with zero runtime Console errors: rectangular/oval
geometry, inward orientation, repeated live turret waves, symmetric relocation,
asymmetric fallback, fully blocked quadrant isolation, retry after clearing,
immutable warnings, mobile partial releases/local corrections, legacy fixed-mount
occupancy, population limits, and unchanged saved assets during fixtures. COSMOS
returned to Edit Mode. This is targeted placement validation, not a new unattended
full-sequence audit or a standalone build.

### Turret orientation controls and four-quadrant formation (2026-10-06)

Turret formation assets now have a **Flip orientation** Inspector button with
Undo/Redo and multi-selection support. It swaps the standard left/right mount
IDs, including the side-wall fallback IDs; mount geometry and inward facing still
come from the level's layout. This edits the shared formation and therefore all
encounters that use it.

The Composer's selected-encounter panel also has **Flip orientation**, backed by
`EnemyEncounterTimeline.Cue.flipWallOrientation` (default false). This flips only
that encounter's wall mounts, including variants/fallbacks, and is independent of
seeded mobile mirroring. Duplication and Unity serialization preserve the setting;
runtime, authoring validation, scene handles and arena previews resolve the same
flipped mounts. Missing or unrecognized opposite mounts reject placement explicitly.

`05c Turret Four Quadrants.asset` adds a Composer library formation containing one
turret in each of TopLeft, TopRight, BottomLeft and BottomRight, with simultaneous
arrivals, 2s warning, Flexible integrity and 3s per-slot grace. It uses the existing
wall-range search and population accounting. It is available to drag onto the
timeline; existing encounter timing and orientation were preserved.

Validation: 12 Editor control checks passed (Undo/Redo, independent cue edits,
duplication and serialization), followed by 38 COSMOS Play Mode placement checks
with zero runtime errors. Added scenarios verify both diagonals using one shared
asset and all four turrets relocating symmetrically around an obstruction. Reports:
`Library/EnemyTurretFormations/validation.txt` and
`Library/EnemyFlexiblePlacement/validation.txt`. Unity returned to Edit Mode;
the new Inspector/Composer buttons and Four Quadrants library entry were reviewed.

### Draggable Composer spawn previews (2026-10-06)

In **ARENA PLACEMENT**, drag a spawn circle to set its preferred location.
With a timeline encounter selected, this creates a per-slot override on that cue;
with a library formation selected, it edits the shared formation. Mobile positions
stay within their authored region and invert the level's oval mapping/inset and
seeded mirroring. Turrets project onto their resolved wall range (including a
flipped quadrant), maintaining wall contact and inward facing. Fixed wall mounts
remain read-only in the preview. Runtime conflict handling can still adjust a
blocked preferred location before its warning begins.

Drag proposals are temporary until release; Escape, focus loss and entering Play
Mode cancel them. A release creates one Undo operation. **Reset placements to
formation** clears the selected cue's overrides for the displayed formation, with
Undo. Hover/drag feedback identifies the slot and checks its footprint against
the arena, exclusions and other slots in that formation. Authoring remains locked
during Play Mode. The preview cannot predict future moving blockers.

`Cue.placementOverrides` is scoped by formation reference and slot index, so
variants/fallbacks do not inherit unrelated overrides. Duplication deep-copies the
entries. Reordering a shared formation's slots also changes which slot an index
refers to; reset/review affected cue placements after structural template edits.
The runtime reservation path, radial adjustment center, Composer, authoring
validation and scene handles all resolve effective cue slots. Shared wall slots
can override their preferred range fraction without modifying the scene layout.

Validation: `MASSIVE > Encounters > Validate spawn placement editing` passed 14
Editor checks, then 40 COSMOS Play Mode placement checks with zero runtime errors.
Reports are in `Library/EnemySpawnPlacement/validation.txt` and
`Library/EnemyFlexiblePlacement/validation.txt`. The runtime cases verify exact
mobile and flipped-wall arrivals at edited positions, alongside existing blocker,
warning-lock, symmetry and population checks.
Native Editor interaction also verified Carrier drag/Undo and flipped turret
drag/reset along the curved wall. A hover-dependent IMGUI control-count error was
fixed by reserving a stable hint row and reset control; repeated native drags then
produced no new Console errors. Test edits were restored to template positions.

### Regulation-relative encounter timing (2026-10-06)

`EnemyEncounterTimeline.fitRegulation` maps each authored cue's first arrival by
`regulation duration / authored timeline duration`. The shared timeline enables
this option; its saved 181-second authoring span and cue values are preserved.
At the current 120-second regulation, a cue halfway across that span starts at
60 seconds; at 240-second regulation it starts at 120 seconds. This fits the
existing full schedule into the match instead of retaining late cues outside
regulation. Turning **Fit regulation** off restores fixed authored timing.

The Director reads its scene's GameManager duration (same score-profile precedence
as match startup), captures the scale at timeline start/restart, and scales its
completion time. It does not rewrite assets or move an active run's warnings when
settings change. Telegraph lengths, within-formation stagger, allowed lateness,
blocked-slot grace and enemy behavior continue using their existing durations.
Extremely early/late cues can still need adjustment for a complete warning or
stagger; the Composer reports those timing constraints. Play-cue mode keeps its
short preview lead, and ordinary pause/conflict rules remain in force.

The Composer ruler, blocks, playhead, arrival fields, drag/drop and checks use
effective match seconds; edits convert back to the proportional authored time.
**Authored span (s)** defines the reference span; the displayed mapping shows the
current level's regulation duration and ratio. The Lab has a separate
**Preview regulation (s)** setting (120 by default) so its long Player Actions
test-scene clock does not stretch encounter previews to 20 minutes. A scene
without match timing falls back to authored seconds.

Timing validation is available through
`MASSIVE > Encounters > Validate regulation timeline scaling`, with editor results
in `Library/EnemyTimelineTiming/validation.txt` and runtime results in
`Library/EnemyFlexiblePlacement/validation.txt`.
Validation passed 11 Editor checks and 57 Play Mode checks, with zero runtime
Console errors. Real Seeker waves were verified at 60-, 120- and 240-second
regulation lengths, preserving full warnings, within-batch stagger and pause
behavior. The same fixtures verify captured run timing, prompt single-cue
previews, placement/blocker handling and preservation of saved assets.

### One-timeline 2v2 scaling (2026-10-06)

The shared Enemy Lab Timeline now has an enabled **2v2 scaling profile**. The
Director captures `GameFlowContext.IsTwoVTwo` and effective population limits at
start/restart; it never selects difficulty from the number of living players.
One-v-one keeps every authored slot, position, timing and population limit.

Initial 2v2 defaults:
- Drones: 50% extra, rounded down to balanced pairs. Paired Rows and Goal
  Phalanxes become 30 total; Center Ring becomes 14.
- Ranged Drones: 25% extra, rounded down to pairs: 24 for rows/phalanxes and 12
  for the ring.
- From halfway through the authored timeline: one extra Seeker pair, one extra
  Dyson pair, and paired turret encounters complete the opposite diagonal.
  Existing Four Quadrants and Carrier counts remain unchanged.
- Total, per-definition and pressure caps multiply by 1.5; zero remains
  unlimited. These are ceilings, not additional spawn requests. Carrier child
  reservations use the same effective budget.

Each cue supports **Inherit / Disable / Override**; Override supplies an explicit
extra-pair count and bypasses the inherited late-timeline gate. Expansion respects
supported formation geometry: row gaps, ring gaps, an additional phalanx rank,
opposing local flanks or the unoccupied turret diagonal. Unsupported/mixed
formations are left unchanged. Generated slots never rewrite base slots or
per-cue drag overrides, and keep the selected variant, mirror and wall flip.
Health, damage, AI, attack cadence and telegraphs are unchanged.

All due base batches reserve before optional pairs. Each pair is admitted only
if both members fit remaining budget and placement; otherwise both are skipped
with a visible reason. Extras follow the original sequence at its real-time
stagger, rather than compressing original timings. Accepted warnings lock their
positions. A later obstruction can hold the pair until its short deadline
(one second of grace by default), then skip both. Originals and extras have
separate release state, including for Strict formations; restart cancels both.

The Composer's **1v1 preview / 2v2 preview** selector shows effective budgets,
purple reinforcement ticks in timeline blocks and purple `+` icons in arena
placement. The blue authored slots remain draggable. The 2v2 profile and cue
overrides are editable in the details panel and normal Inspector. **Play all**
and **Play cue** request the selected actual player roster before scene Awake,
using an Editor-only, one-shot request; this does not serialize a match-mode
override into the level. The selected mode is locked during playback. The Lab
creates two actors per team in 2v2 and retains manual P1 control.

Validation entry: `MASSIVE > Encounters > Validate 2v2 encounter scaling`.
Authoring results: `Library/EnemyEncounterScaling/validation.txt`.
Runtime geometry, regression and mode results:
`Library/EnemyFlexiblePlacement/validation.txt`.

Validation passed 26 authoring checks and 78 Play Mode checks with zero runtime
Console errors. Coverage includes 30-Drone rows, complete turret quadrants,
original-batch priority, partial budget admission by pairs, pre/post-warning
obstructions, locked warning positions, Strict formations, captured mode/caps,
restart cleanup, four-player match startup, and a four-actor Lab at Y=0 with
manual P1 control and its existing background-input lock. Native Composer
interaction verified the mode switch, effective limits, per-cue controls and
distinct reinforcement icons. Balance values remain initial playtest settings.
