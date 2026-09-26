# HIGGS field and Symmetry Knots

Design baseline: [MASSIVE GDD, section 4.2.3](https://app.notion.com/p/11e617c6d8ae4eeda7f0424645db5203). Implemented prototype, September 25, 2026. Balance and cabinet visual/performance acceptance remain open.

## Gameplay contract

The field provides evolving ambient topography. A reserved local excitation announces one temporary territory-control Knot. Knots bank immediate team energy, Amplifier Cores improve future earnings, and Resonance changes approach terrain. HIGGS does not change player body mass or movement physics.

| Parameter | Authored value |
| --- | --- |
| Concurrent Knots | 1 in 1v1 and 2v2 |
| First opportunity delay | 8 s after scoring opens, subject to Core introduction |
| Field telegraph / Knot formation | 1.5 s / 1 s, no awards |
| Unclaimed active window | 6 s |
| First uncontested claim | Extends deadline to at least 10 s from that claim, once only |
| Lock ramp | 2 s uncontested; no income during ramp |
| Capture radius | 1.15 world units |
| Reward | HiggsControlTick: 25 meV every 0.25 productive seconds |
| Multiplier / charge | Team Amplifier applies; no personal multiplier or charge |
| Outro / excitation tail | 1 s / 0.75 s |
| Next opportunity cooldown | 10 s plus random -2 to +2 s after tail |
| Core introduction clearance | Wait at least 3 s after its introduction ends |
| Regulation | 120 s; HIGGS reward introduced in version 2 and retained in shared ruleset version 4 |

The player centre must be inside the capture ring; extending a weapon or shield into it cannot claim from outside. Any opposing eligible presence contests, even two allies against one opponent. Additional allies do not increase income. Contest pauses the lock ramp and clears a partial reward tick; leaving the zone or a change of controlling team resets the ramp. Re-entry never extends lifetime again. Eligibility excludes inactive, disabled, eliminated and pseudo players, and actors in other scenes. Pause freezes capture and encounter clocks. Closed scoring cannot award, and match end retires the encounter.

The full claimed window can yield 800 meV at team x1 if held continuously from first claim: two seconds locking, eight seconds productive. Team x8 raises this to 6.4 eV. This is a prototype tuning comparison, not a final economy target.

## Ownership and integration

- `HiggsExcitationKnotManager`: finite opportunity schedule, safe reservation, one Knot, excitation envelope, Core introduction gate and encounter telemetry.
- `HiggsFieldGPU` / `HiggsFieldSim.compute`: reserve bubble zero at the safe world/UV position; retain ambient drift elsewhere. Gameplay random excitation rate is zero. Keep the reserved source fixed through formation, scoring and outro, fade its tail, then release it.
- `SymmetryKnotCaptureState`: frame-rate independent lifecycle/capture rules. Double-precision clock accumulation avoids dropped ticks at common frame rates.
- `SymmetryKnotController`: eligible presence query, state advancement and authoritative `MatchScoreService.TryAwardToTeam` calls. A team stream has no player attribution.
- `SymmetryKnotGoalMouth`: endpoint and aggregated receipt feedback only. Receipts use accepted energy amounts and the existing pooled toast system, at most once per second. The endpoint never awards score or fakes Core capture.
- `SymmetryKnotVisual`: capture ring, forming/locking/contested state, tube caps, spring motion and outro. `hold01` now drives visual locking.
- `SymmetryKnotMetaballFlow`: stream follows `IsFlowing`; no stream during warmup, contest, vacancy or retirement. Presentation may fade out after flow stops.
- `AmplifierResonanceSpawner`: shared Core/pattern lifecycle. HIGGS supplies only an optional introduction gate; other stages keep their existing behavior.
- `AmplifierSpawnRegion`: shared neutral region, bounds, player/goal/blocker/pattern exclusions and retries. At the end of a telegraph, players may have approached; geometry and Core clearance are still rechecked.

The HIGGS scene contains a real shared Amplifier/Resonance spawner using the existing 345 Hz encounter and Core prefabs. Shared settings remain authoritative for that encounter. HIGGS timing, field appearance and capture radius remain stage-local. Goal HUD Cores are presentation, not duplicate gameplay objectives.

The field extent matches the current 28 x 12 grid. Local excitation glow compounds on high contours; underlay opacity is 0.65, glow strength 1.25 and power 2. Tune contrast against players, capture rings, Core and Resonance together.


## Universal level composition — September 26, 2026

HIGGS uses the same six ownership roots as NOVA: `00_Systems`, `01_Presentation`, `02_Arena`, `GameplayObjects`, `04_UI`, and `90_EditorOnly`. Eight common prefab modules supply MatchRuntime, Players_Gameplay, Arena_Planar_Surface, Arena_Planar_Bounds, TeamGoals_Planar, MatchHUD, AmplifierResonanceEncounter and the existing optional AnomalyUI. Scene overrides preserve HIGGS configuration and connect references between modules.

The HIGGS field, Knot manager and two goal mouths remain under `GameplayObjects/LevelContent/HIGGS`. The Core/Resonance coordinator lives under `GameplayObjects/Encounters` and retains HIGGS placement settings and its Knot introduction gate. No NOVA star, star exclusion, Core Collapse bonus or supernova finale is installed.

LevelSceneContext binds HIGGS identity, the roster, match, persistent input prefab, optional anomaly manager and spawner group. Direct scene Play Mode now initializes input; the normal Attract/level-select route reuses its existing manager. A single explicit MatchScoreService serves the HUD, goals and encounter. MatchHUD includes the timer and Feedback includes team Amplifier announcements. Players inherit common Repulsor, scaling, reversal and feedback components, with references wired to this scene.

Existing world transforms, HIGGS field/Knot parameters, goal receipts and both placement regions are checked during migration. The disabled Knot spawn test, disabled legacy input initializer and inactive sample nugget are retained under EditorOnly. The generic legacy Death Sphere remains inactive, matching the common structure; HIGGS retains the ordinary timed ending through GameManager and PostGame.

`MASSIVE/Levels/Apply HIGGS Universal Structure` is a one-time, explicit migration for the saved HIGGS scene in Edit Mode. It retains existing objects while converting common groups to shared prefab instances with overrides; it does not rewrite shared prefab assets. Subsequent edits belong in prefab/scene overrides. `HiggsLevelValidation.Authored()` checks module links, unique systems and serialized references; `HiggsLevelValidation.Runtime()` checks direct-launch setup after the first Knot and Core/pattern opportunity.


## Authoring and validation

Launch HIGGS directly in the Editor or select it through Attract. LevelSceneContext creates the persistent Rewired input manager when needed and reuses it across gameplay reloads.

`MASSIVE/HIGGS/Apply Accepted Stage Setup` is an explicit migration utility for the open HIGGS scene outside Play Mode. It reapplies the accepted prototype values and saves affected assets. Do not run it to preserve subsequent bespoke tuning.

`MASSIVE/HIGGS/Validate Capture Rules` runs deterministic lifecycle and authored-configuration checks. `HiggsValidation.RunPlayChecks()` runs a destructive runtime-only fixture in an already-started HIGGS match, using actual players, colliders and the score service; exit Play Mode immediately afterward. Neither validation function saves runtime fixture state.

Telemetry on the manager: opportunities, placement retries, first arrival, uncontested/contested time, ownership changes and Knot energy. Active-Knot metrics are captured on retirement; these are local runtime diagnostics, not persisted match-result telemetry.

## Universal composition validation — September 26, 2026

- Unity 6000.0.28f1 compiled the migration and validator. The saved scene passed 37 composition checks and an inspection of 6,848 serialized reference slots, with no missing scripts, broken references or cross-scene references.
- Migration checks confirmed unchanged serialized field/Knot/goal-mouth settings, both placement regions and all original HIGGS world transforms. Eight prefab links are present; NOVA's scene, shared modules, planar template and shared economy asset were unchanged by this migration.
- All 22 capture/configuration checks and 17 staged physics/scoring checks passed after composition, including real player colliders, 2v1 contest, team amplification, exact goal receipts and scoring closure.
- Nine runtime checks passed in each of 1v1 and 2v2. Direct launch reached regulation; a gameplay reload retained exactly one ready Rewired manager and selected all four 2v2 players. The title, timer, spawner gating, score service, Core/Resonance and Knot scheduler were active. One 2v2 placement became invalid during its telegraph and safely retried before manifesting.
- The 2v2 run completed its ordinary 120-second regulation and produced a HIGGS/STAGE_002 PostGame result using ruleset 4, with no bonus breakdown or bonus duration. A separate runtime-only clock-expiry fixture ended a match with an active Knot: scoring closed, the Knot retired, its reserved excitation released and the ordinary PostGame handoff passed.
- Screenshots of both rosters were inspected for arena, field, timer and HUD presentation. The final cleanup run reported no Console warnings or errors. An earlier temporary test callback used serialized access for a nonserialized clock and threw; the callback was corrected to reflection and the clean run passed. This was a test-harness error, with no saved runtime state.

Idle-return suppression and player activity refresh were confined to Play Mode. All fixtures were discarded on exit; HIGGS was left saved in Edit Mode. This structural integration has not repeated cabinet-input balance, standalone-build or target-hardware performance acceptance.

## Validation record — September 25, 2026

- Unity 6000.0.28f1 recompiled without errors.
- 22 capture/configuration checks passed, including 30/60/120 fps award equivalence, bounded lifetime, pause, interruption, the authored economy, and versioned fallback/new profiles.
- 17 staged Play Mode integration checks passed with the actual player colliders and MatchScoreService: centre-based capture boundary, one/two allies, 2v1 contest, personal multiplier exclusion, no charge, capped team x8 income, exact accepted goal receipt, inactive/eliminated exclusion and scoring closure.
- Both full 120-second idle cadence runs completed:

| Mode | Knots | Maximum live Knots | Core/pattern pairs | Placement retries | Overlapping introduction samples | Excitation at match end |
| --- | --- | --- | --- | --- | --- | --- |
| 1v1 | 5 | 1 | 3 | 0 | 0 | Released |
| 2v2 (four active actors) | 5 | 1 | 3 | 0 | 0 | Released |

These runs used normal authored timing and a runtime-only idle-return suppression/last-activity refresh, with no simulated combat. Monitoring sampled at approximately 10 Hz. The results establish scheduling and cleanup for those two runs, not fairness or a fixed opportunity count for every random seed.

A readback of the active GPU source showed zero UV deviation from its reserved position and excitation envelope 1. Screenshots were inspected for the active Knot alongside Core/Resonance and for productive flow/goal receipts. The productive-flow preview used staged, frozen player positions and the real capture clock/score service; all fixture changes were discarded on leaving Play Mode.

The resumed validation runs reported no Console warnings/errors. Earlier scene-transition attempts in this work encountered Attract UI Selectable IndexOutOfRangeException messages and a persistent-allocation warning; their origin was not established and they did not recur in the resumed checks. These September 25 runs predate the universal composition and used the Attract route to initialize input.

Remaining acceptance: ordinary-input contested rounds; team travel/access fairness; contour, stream and receipt readability at cabinet distance; reward balance against combat/pickups and Core investment; target-hardware GPU/CPU profiling and a standalone build. Current evidence makes no competitive-balance or target-performance claim.


## Visual implementation reference

The following visual descriptions and ranges were relocated from the earlier GDD. They are historical tuning suggestions, not current serialized defaults. The gameplay contract above supersedes the old floating-point mass stream, SendMessage endpoint, 12-18 second generic lifetime and 2.75-3.5 radius suggestions.

### 3) `SymmetryKnotVisual` (Tube, Rings, Motion)
**Responsibilities**
- Owns all Shapes-based visuals:
	- dashed capture ring (control boundary)
	- inner concentric “core” rings
	- source tube slices (`SourcesSlicesParent`)
	- end tube slices (runtime `EndKnotRoot`)
- Handles all visual state transitions:
	- forming (unclaimed)
	- locked (claimed)
	- contested
	- retract and despawn collapse
**Key Methods**
- `Initialize(Vector3 sourceWorldPos, float captureRadiusWorld)`
	- Positions the knot at the source.
	- Sets capture ring radius and core ring radii.
	- Ensures slice pools exist for the source cap.
	- Forces capture/core rings onto the XZ plane (Shapes rings default to XY).
- `SetOwner(int teamID, Transform goalMouth, bool contested, float hold01)`
	- Stores current owner/goal reference.
	- Ensures the runtime end knot root exists when a goal mouth is present.
	- Keeps end knot alive during retract so it can animate out instead of popping.
- `Update()`
	- Drives fade/despawn timing.
	- Drives a **spring** for aim direction and lock state to create overshoot + settle (“bounce”).
	- Adds subtle continuous motion:
		- wiggle across tube length (side-to-side)
		- breathing (radius/thickness expansion)
	- Updates both caps:
		- `UpdateSourceCap()`
		- `UpdateEndCap()`
**Shared “Meeting Point” Aim**
- Both caps aim at a shared meeting point `Meet` so they point at each other accurately.
- `Meet` is computed as a blend between:
	- vertical “forming” top point above the source
	- leaned “locked” top point shifted toward the controlling goal
- A small `meetGap` keeps the mid section “ghosted” (not fully connected).
**Retraction / Despawn**
- When the knot unlocks, the end cap **retracts back toward the goal mouth**, while:
	- radius shrinks
	- line thickness shrinks to 0
	- alpha fades
- On despawn, the entry side performs a “collapse” rather than uniform fade:
	- per-slice thickness/radius collapse
	- rotation wiggle while collapsing
	- optional ring collapse on capture/core rings
---
### 4) `VolumetricRingSlice` (Solid Tube Slice Rendering)
**Purpose**
- Renders each tube slice as a **volumetric 3D line** ring, producing a “solid tube” outline.
**How it works**
- Inherits `Shapes.ImmediateModeShapeDrawer`.
- Draws a circle polyline (segments around 360°) using:
	- `Draw.LineGeometry = Volumetric3D`
	- `Draw.ThicknessSpace = Meters`
- Exposes properties named `Radius`, `Thickness`, `Color` so existing visual controllers can set them consistently.
This replaces flat disc rings for slices while keeping the same slice placement/orientation logic.
---
### 5) `SymmetryKnotMetaballFlow` (Matter Stream via Metaball SDF)
**Design**
No guide arc is drawn. The “vector line” is communicated by:
- the two knot tube caps aiming at each other
- a stream of metaball “matter” that appears to travel *inside* the tube
**Implementation**
- Uses two SDF volumes:
	- `SourceMatterSDF` (near source cap)
	- `EndMatterSDF` (near end cap)
- Each volume has a `MetaballSDFInstance`, which supports up to 32 balls (array pushed to shader).
**Flow Model**
- When the knot is locked (end knot exists + slices are valid), the script:
	- samples points along the source cap slices (polyline sample)
	- spawns metaballs that **shrink to 0** as they approach the cap tip (toward the ghosted middle)
- Simultaneously on the goal side:
	- samples points along the end cap slices (reversed)
	- spawns metaballs that **grow from 0** as they approach the goal mouth
**Key Behaviors**
- `flowMul` tween:
	- smooth in/out of matter stream when knot locks/unlocks
- Auto-fit volumes:
	- dynamically centers/scales each SDF cube to contain the slice positions
	- prevents “invisible” balls caused by positions outside the SDF unit cube bounds
**Depth / “Inside the tube”**
- Because the metaballs are rendered via the SDF shader inside a cube volume and the tube is drawn as volumetric line geometry, the stream reads as contained inside the slice tunnel (and can be tuned further via volume scaling and ball radius).
---


### `SymmetryKnotVisual` (Tube, Motion, Despawn)
**General**
- `Y Lift Above Grid`: **0.02–0.05** (prevents z-fight)
**Claim Tween**
- `Lock Tween Seconds`: **0.30–0.55**
	(snappier feels more “arcade”; slower feels more “cosmic”)
**Bounce / Spring**
- `Aim Spring Stiffness`: **16–22**
- `Aim Spring Damping`: **8–12**
	(lower damping = more overshoot; increase if too jittery)
- `Lock Spring Stiffness`: **18–26**
- `Lock Spring Damping`: **10–14**
**Source Cap**
- `Source Slice Count`: **14–20**
- `Source Cap Rise`: **1.2–1.8**
- `Source Cap Len`: **1.8–2.6**
**End Cap**
- `End Slice Count`: **12–18**
- `End Emit Len`: **0.8–1.4**
- `End Curve Pull`: **1.2–2.2**
**Meeting / Ghost**
- `Meet Gap`: **0.18–0.35**
	(smaller = feels more “connected”; larger = more ghosted/teleport)
**Slice Shrink / Ghost**
- `Slice Radius Start`: **0.45–0.65**
- `Slice Radius End`: **0.14–0.25**
- `Slice Thickness Start`: **0.06–0.10**
- `Slice Thickness End`: **0.015–0.03**
- `Slice Alpha Start`: **0.9–1.0**
- `Slice Alpha End`: **0.10–0.22**
**Wiggle (subtle!)**
- `Wiggle Amplitude`: **0.04–0.10**
- `Wiggle Frequency`: **1.1–1.8**
- `Wiggle Speed`: **0.9–1.6**
- `Wiggle End Influence`: **0.05–0.18** (keep low so ends stay “anchored”)
**Breathing**
- `Breathe Amplitude`: **0.03–0.08**
- `Breathe Speed`: **0.9–1.4**
**End Knot Retract**
- `End Knot Remove Delay`: **0.15–0.35**
	(lets the retract fully read before object cleanup)
**Colors**
- Start with all white; later you can tint “claimed” subtly by team hue.
---
### `VolumetricRingSlice` (Tube Slice Renderer)
- `segments`: **48** (default)
	- bump to **64** if you see faceting close-up
- Thickness is driven by `SymmetryKnotVisual` per slice, so the prefab thickness is mostly a fallback.
- Keep `LineGeometry`: **Volumetric3D**
- `ThicknessSpace`: **Meters**
---
### `SymmetryKnotMetaballFlow` (Matter Stream)
**Ball Counts**
- `Source Ball Count`: **12–18**
- `End Ball Count`: **10–16**
	(more balls = denser stream, but can get “muddy”)
**Motion**
- `Cycles Per Second`: **1.1–1.8**
	(higher = more energetic stream)
**Sizing**
- `Base Radius OS`: **0.10–0.16**
	(object-space radius depends on your SDF cube scale; tune by eye)
- `End Radius Mul`: **1.05–1.25**
	(slightly larger at the goal reads as “delivery/condensation”)
**Envelope**
- `Taper Pow`: **1.3–2.0**
	(higher = stays big longer then collapses quickly)
- `Wrap Feather`: **0.12–0.22**
	(prevents popping at wrap)
**Flow In/Out**
- `Flow In Seconds`: **0.20–0.35**
- `Flow Out Seconds`: **0.18–0.30**
**Auto-fit Volumes**
- `Auto Fit Volumes`: ✅ (recommended)
- `Fit Padding World`: **0.6–1.0**
- `Fit Min Size World`: **0.8–1.5**
---


## Visual tuning heuristics

- Too noisy: reduce wiggle, flow speed or metaball count.
- Too rigid: reduce spring damping slightly and add modest breathing.
- Unclear direction: increase slice taper and source-cap length; inspect both endpoints together.
- Matter outside the tube: reduce ball radius and inspect SDF bounds/fit padding.
- Excessive income: tune the shared HiggsControlTick reward or stage lock duration, then compare ordinary 120-second rounds. Do not restore endpoint-owned floating-point scoring.
