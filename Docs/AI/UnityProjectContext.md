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
