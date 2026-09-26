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
| Arena_Planar_Surface | Grid, bounds provider and goal treatment renderer |
| Arena_Planar_Bounds | Existing ground and mask collision geometry |
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

## NOVA scoring and phase policy

NOVA now runs four uninterrupted regulation stages at 0%, 25%, 50% and 75% of the
shared regulation duration. Stage four is the authored star scale and particle
baseline. NovaStarRumble scales only Star Visuals; entry, collision, attraction
and exclusion geometry stay independent. The three transitions eject bounded,
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

Production regulation is still 120 seconds, entry is 6 seconds, Core Collapse is
16 seconds, and the late firing penalty is 2 seconds. Balance and controller feel
still need human playtesting; automated fixtures do not establish competitive balance.
