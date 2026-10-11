# MASSIVE level composition

NOVA is the first composed level. `Assets/Scenes/Templates/Level-Planar.unity`
is a starter scene for conventional planar arenas; duplicate it, assign a new
LevelDefinition on LevelSceneContext, and add the new gameplay scene to the
project's existing level-selection/build workflow.

The template is intentionally not an enabled build scene. Its empty level identity
is an authoring requirement, not a fallback to another level's identity.

## Ownership and hierarchy

- `00_Systems`: LevelSceneContext, MatchRuntime, optional AnomalyRuntime.
- `01_Presentation`: CameraRig, Lighting, Audio, MatchEndFX.
- `02_Arena`: Surface, BoundsAndCollision, Goals, SpawnAnchors, NoSpawnZones.
- `GameplayObjects`: Players, Encounters, Spawners, LevelContent, Runtime.
- `04_UI`: MatchHUD, Feedback, optional AnomalyUI and LevelUI, EventSystem.
- `90_EditorOnly`: authoring fixtures, tagged EditorOnly and stripped from builds.

Retain the GameplayObjects tag, player child names, and existing gameplay tags.
GameManager uses that tag to shut gameplay down at match end. Score authority,
HUD, and end effects stay outside that root. Keep only GameFlowContext and the
single Rewired manager persistent between scenes.

LevelSceneContext provides scene identity, explicit common references, direct
Editor-launch input bootstrap, spawner phase gating, and optional bonus world
pause. GameManager owns countdown, regulation, bonus phase, results and PostGame.
MatchScoreService owns every banked energy award and both multiplier systems.

## Prefab modules

| Asset | Purpose |
| --- | --- |
| MatchRuntime | GameManager, explicit score authority, roster and VFX warmup |
| Players_Gameplay | Four existing players plus common Repulsor, feedback, size and reversal components |
| Arena_Planar_Surface | Grid, bounds provider, goal treatment renderer and generated exterior mask |
| Arena_Planar_Bounds | Existing ground and collision geometry; fixed mask renderers retired |
| TeamGoals_Planar | Light/dark captures and score presentation |
| MatchHUD | Existing world-space HUD with explicit timer bindings |
| AmplifierResonanceEncounter | Shared coordinator, spawn region and pattern anchor |
| AnomalyUI | Optional warning, minigame and transition UI |
| NOVA_Feature | NOVA star, entry and anomaly adapter; optional level content |
| NOVA_BonusHUD | Bonus-only footer and pooled capture feedback; scene binds the HUD, camera, bounds and goals |

Cross-module references are scene overrides in NOVA and the template. Instantiating
a module alone requires wiring its external references. Use the composed template
to retain those connections. Shared settings profiles hold tuning; scene objects
and geometry remain in the scene. Do not apply a scene's external object references
back to a prefab asset.

The planar goal attraction radius remains NOVA's symmetric 4.5 / 4.5. Spawn regions
must reference the target level's bounds and stable exclusion colliders. NOVA uses
a separate 3.5-unit star entry exclusion on NoSpawnZone, independent of the entry
trigger's enabled state. The optional PowerUpSpawner uses that layer too.

For a folded level, retain the common systems/HUD hierarchy and substitute the
existing SINGULARITY surface, player and Core adapters and its front-face spawn
domain. This planar starter does not implement folded placement or replace those
adapters.

## Automatic outside mask

`ArenaOutsideMask` on the Surface's `VectorGridGPU` creates one transient black
cutout using the shared COSMOS mask shader. Its opening follows the bounds/grid
dimensions, transform and scale in Edit Mode and Play Mode. Rectangular fields
include half-ellipse goal caps; oval bounds use their full ellipse. The mask is
presentation only and does not change walls, scoring, goals or player movement.

Inspector controls: **Include Team Goal Half Circles** defaults on; off closes
the mask opening at both goal mouths while preserving the field shape (including
oval fields) and the saved cap depth. **Mask Y Offset** is world height relative
to the grid (default 2); **Goal Cap Depth Ratio** is cap depth relative to half the field
height (1 for round caps, 0 for a plain rectangle); **Edge Padding** keeps the
border stroke visible (minimum margin also follows grid border thickness).
Keep the mask above background effects and below foreground HUD. Disabling the
component releases its generated mesh/material and removes coverage.

HIGGS, NOVA, PULSAR, Level-Planar and S-T_PLAYER-ACTIONS inherit the component and its
local arena/shader bindings from the prefab. Their matching Bounds prefab keeps
the four original mask colliders, but disables those fixed MeshRenderers so they
cannot clip a resized arena. The ground remains enabled. COSMOS keeps its existing
oval layout/depth controls; the shared shader preserves its original parameters.
Legacy scenes without the Surface prefab are not automatically migrated.

PULSAR now inherits the Surface and Bounds modules at `02_Arena/Surface` and
`02_Arena/BoundsAndCollision`. The mask remains on `GRID/VectorGridGPU`, with
PULSAR's local bounds and **Include Team Goal Half Circles** enabled. Its authored
field, goals and collision geometry are retained as module overrides.

`MASSIVE > Levels > Validate Shared Surface Outside Mask` starts from a saved
supported arena, checks all five Surface consumers, then runs temporary
GPU and Play Mode fixtures in the active level.
Reports and comparison captures are written to `Library/ArenaOutsideMask`; the
fixture returns to Edit Mode without saving runtime changes. PULSAR reports and
captures are in the `S-5_PULSAR` subfolder.

October 7 validation (Unity 6000.0.28f1, DX11): 13 authored checks across all four
consumers and 17 Play Mode/GPU checks passed, with no runtime errors. Tests covered
both goal-toggle states on rectangular and oval fields, field/goal openings,
outside corners, foreground depth, live resizing, oval
compatibility, the original COSMOS shader parameters and disable/re-enable cleanup.
All 26,124 sampled visible HIGGS HUD/title pixels remained visible. The masked
Game-view and synthetic cutout captures were inspected. No scene files changed
during installation/validation; the Surface and Bounds prefab changes provide
inheritance. A standalone build and cabinet performance were not tested.

## PULSAR composition

`S-5_PULSAR` uses the six standard roots and eight common modules. LevelSceneContext
assigns PULSAR/STAGE_004, bootstraps the shared Rewired manager on direct Editor
launch, and gates power-up spawning by match phase. MatchRuntime explicitly binds
all four roster slots, the shared economy profile and the single MatchScoreService.
The HUD timer, score presenters, Amplifier goals, Repulsor grid effects and feedback
use those scene-local authorities. The shared economy profile controls match length.

`GameplayObjects/Encounters` contains AmplifierResonance and PULSAR Enemy Timeline.
The enemy setup reuses LATTICE's shared timeline/profile with PULSAR's own bounds,
wall mounts, exclusions and enemy container. `02_Arena/NoSpawnZones/PULSAR exclusion`
is a stable trigger around the solid core, shared by enemy and pickup placement;
it does not use transient particle bounds or rotate with the visual effects.

PULSAR's original geometry, gravity, grid forces, particles and authored poses are
preserved under LevelContent. The existing `PULSAR SYSTEM` rhythm prototype remains
inactive beside the PULSAR object; its session now references its existing director,
playable director and star center. It still needs an IPulsarInputSource implementation
and scoring/session integration before it can be enabled as a playable rhythm mode.
The optional anomaly manager and anomaly UI remain inactive as authored.

GameplayObjects shuts down at match end. The existing death-sphere effect and HUD
remain outside that root, and GameManager sends the exact result to shared PostGame.
The obsolete inactive Rewired Initializer was removed; only the normal session/input
objects persist between scenes. Legacy inactive title/nugget authoring objects are
under `90_EditorOnly`.

`MASSIVE > Levels > Apply PULSAR Universal Structure` performs the one-time migration
from the legacy layout and refuses to duplicate an existing composition. A scene
backup is retained in `Library/PulsarLevelValidation`. `MASSIVE > Levels > Validate
PULSAR Integration (1v1 and 2v2)` validates modules/references, then runs disposable
fixtures through startup, enemies/scoring, pause, shared encounters and PostGame.
The outside-mask validator now uses PULSAR's real input bootstrap, without supplying
a temporary manager.

October 7 composition validation (Unity 6000.0.28f1, DX11): 40 composition checks,
7 enemy wiring checks and 7,800 serialized reference slots passed. Both 1v1 and
2v2 passed 41 runtime checks, including six enemy families, mode scaling, one-time
defeat awards, pause/resume, coexistence with the Core/Resonance pattern, match-end
shutdown, exact result handoff, PostGame and return to Attract. Runtime errors were
zero. The composed scene also passed 16 authored mask checks across all five
consumers and 18 Play Mode/GPU checks; all 39,861 sampled visible PULSAR HUD/title
pixels remained visible. Captures were inspected. Saved scene/encounter assets
were unchanged during fixtures. No standalone build or cabinet performance test.

## SINGULARITY composition

`S-8_SINGULARITY` uses the six standard roots and five common modules:
MatchRuntime, Players_Gameplay, TeamGoals_Planar, AmplifierResonanceEncounter and
MatchHUD. LevelSceneContext binds the catalog's STAGE_007 identity, shared economy,
roster, input bootstrap and phase-gated pickups. The original SingularityLevelContext
remains for portal cancellation. Match end retires gameplay, preserves the HUD,
and hands exact totals to shared PostGame and then Attract.

The original SingularitySurface, folded grid, player/Core adapters, black-hole
travel, rear-face dimming, periodic seam and authored poses remain intact. Shared
Repulsor feedback emits into the folded grid. No planar top/bottom walls or second
grid renderer replace the folded surface. The inactive sandbox remains under
`90_EditorOnly`.

`SingularityArenaDomains` on `02_Arena/Surface` adapts surface dimensions to two
disabled, data-only VectorGridGPU components. `02_Arena/BoundsAndCollision/Front
face placement` supplies the flat front rectangle for enemies and power-ups.
`02_Arena/Surface/Projected arena mask` covers the full projected extent, including
both folds, and hosts ArenaOutsideMask with **Include Team Goal Half Circles**.
These domains do not register as the global grid or allocate a second simulation.

`GameplayObjects/Encounters/SINGULARITY Enemy Timeline` reuses LATTICE's shared
timeline/profile with local bounds, wall mounts, exclusion and enemy container.
Enemies and pickups remain on the front face; enemies do not pursue rear-face or
portal-transiting players. Players and the Core still cross faces. The existing
black-hole exclusion is shared by enemy, pickup and Core-pattern placement.
The level's empty optional anomaly pool remains unchanged.

`MASSIVE > Levels > Apply SINGULARITY Universal Structure` performs the one-time
migration and refuses to duplicate an existing composition. `Validate SINGULARITY
Integration (1v1 and 2v2)` runs saved-scene and disposable match fixtures; `Validate
SINGULARITY Outside Mask` exercises the projected cutout and goal toggle on the GPU.
Backups, reports and inspected captures are in `Library/SingularityLevelValidation`.

October 7 validation (Unity 6000.0.28f1, DX11): 35 composition checks, 7 enemy wiring
checks and 6,241 serialized reference slots passed. Both 1v1 and 2v2 passed 52
integration checks plus the existing 50-check live Core portal suite. Coverage
includes all six enemy families, scoring, pause/resume, pickups, player portal
travel, folded Repulsor feedback, Core travel/capture/cancellation and results flow.
The mask passed 4 authored and 14 Play Mode/GPU checks; all 29,124 sampled visible
HUD/title pixels remained visible. Runtime errors were zero. The shared player
spawn routine still logs kinematic-velocity warnings. Runtime fixtures preserved
saved scenes and shared encounter assets. No standalone build or physical-controller
play session was run.

## NOVA scoring and phase policy

NOVA now runs four uninterrupted regulation stages at 0%, 25%, 50% and 75% of the
shared regulation duration. Stage four is the authored star scale and particle
baseline. NovaStarRumble scales Star Visuals and its solid colliders together;
entry, attraction and spawn-exclusion geometry stay independent. The three transitions eject bounded,
oppositely paired nuggets and nugglets using the existing shared pickup behavior.
Timing, visual profiles, launch speed and pickup lifetime are tunable on NOVA_Feature.

At timeout GameManager snapshots regulation totals, closes ordinary awards and
enters FinaleEntry through an optional IMatchFinale scene reference. Levels without
that reference keep their existing timed ending. Players can still move during the
entry window. All rostered players enter Core Collapse, including respawning players;
only players inside the circle at cutoff avoid the two-second firing penalty.
An empty on-time snapshot means everyone is late, never that the bonus is skipped.

Bonus uses realtime while LevelSceneContext pauses the world. Regulation spawners
remain retired. NOVA_CORE_CAPTURE banks 25 meV per original subparticle through
MatchScoreService, with Bonus Only and Ignore All Multipliers enabled and no chain
charge. Both personal and team Amplifier multipliers are bypassed. Ordinary rewards
are rejected during the bonus and bonus rewards are rejected during regulation.
The ruleset version is 4; existing ordinary reward behavior is retained.

Future minigame interactions call NovaMinigameScoring.TryAwardInteraction with a
registered bonus reward key, rostered player, stable event ID and quantity. Configure
those rows to ignore all multipliers and leave Chain Effect at None. Never bank raw
score or apply multipliers in minigame code. Duplicate/invalid interactions and
instruction previews cannot bank energy.

Core Collapse hides both regulation score/player HUDs, the regulation timer and
the stellar-stage display. Its footer contains team capture counts and exact
awarded bonus meV, with instructions between them. Counts represent completed
particle clusters; energy reflects their original subparticle quantity. The footer
projects the actual grid footprint and minigame bounds to stay below the field,
with a 12-reference-pixel gap. The shared AnomalyUI prefab remains unchanged;
NOVA overrides its lower-third references to NOVA_BonusHUD and keeps the bonus
countdown above the field.

Only accepted capture awards create a small team-styled points toast and seven
particles traveling to that team's goal capture point. Effects run in unscaled time,
reuse a fixed pool of twelve captures, and never award score themselves. Rejected
or duplicate awards do not increment counters or produce feedback. Cancellation
restores previous HUD visibility; the terminal outro keeps it hidden until results.

After the last capture, scoring closes; the white field contracts over 1.15 seconds,
holds for .35 seconds, then the supernova runs during match resolution. There is no
arena return bounce, shielding payout or intermediate result panel. MatchResult stores
regulation, bonus and total energy separately; PostGame chooses the winner from totals
and displays the breakdown. Cancellation/unload restores the previous timescale.
The legacy Death Sphere remains inactive. GPU reveal uses per-instance material
properties and does not edit the shared material.

## Validation

NovaLevelValidation.Authored checks the saved scene/template and references.
NovaFinaleValidation.StartRun runs disposable Play Mode fixtures for natural stage
timing, matter ejection, all-late/mixed entry, flat and duplicate-safe awards, real
particle capture callbacks, contraction, explosion and combined results. Its timing
and scores are runtime fixtures, never saved to the central economy asset.
NovaBonusPresentationValidation.StartRun checks footer containment, text overflow,
flat-score receipts, duplicate/rejected capture feedback, goal attribution, pool
limits, unscaled expiry and cancellation cleanup in Play Mode.

Production regulation follows the shared economy (currently 180 seconds), entry is 6 seconds, Core Collapse is
16 seconds, and the late firing penalty is 2 seconds. Balance and controller feel
still need human playtesting; automated fixtures do not establish competitive balance.


## Catalog-wide integration validation (2026-10-07)

All eight playable catalog scenes now satisfy the universal hierarchy and explicit
system bindings. ORBITAL retains its existing cloud within the standard structure;
NOVA's enemy timeline uses local bounds and star exclusions. Stage identity and
serialized scene names match the catalog. Each authored player has exactly one
Repulsor damage area, and all levels shut down gameplay at match resolution.
Automatic planar masks also cover unpacked LATTICE, ORBITAL and DYNAMO surfaces.
The shared Surface and catalog scenes hide Amplifier authoring controls by default;
DYNAMO's debug overlay starts hidden.

Use MASSIVE > Levels > Audit All Playable Levels for saved composition/reference
checks, or Validate All Playable Levels (1v1 and 2v2) for the disposable runtime
sweep. This checks startup, scoring, enemy families, pickups, Core/Resonance,
amplification, pause, results and Attract cleanup. Runtime time/score fixtures are
never saved to production scenes or settings. Save the active scene before running.

All 16 cases passed 536 runtime checks with zero runtime errors or warnings in
Unity 6000.0.28f1 DX11. NOVA's unique terminal bonus was included. Saved assets
retained their hashes through the sweep. Reports, original backups and captures
are in Library/UniversalLevelValidation. Standalone builds, physical-controller
feel and full competitive balance remain outside this validation.
