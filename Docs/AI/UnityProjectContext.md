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
