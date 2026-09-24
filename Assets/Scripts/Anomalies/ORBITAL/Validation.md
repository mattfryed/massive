# ORBITAL validation — September 18, 2026

Status: **Ready with limitations** for Editor playtesting. No standalone player build or platform performance benchmark was run.

## Environment and baseline

- Unity 6000.0.28f1, Windows Editor, Built-in render pipeline, connected Unity MCP.
- Starting commit: `b6ad72155410d94d6bd8dc4d18d053062a4ac732`. Existing repulsor/enemy/prototype changes were already present and preserved.
- Initial Console: zero errors/warnings. First ORBITAL Play Mode inspection exposed two inherited camera audio listeners; the duplicate was removed only in the new scene. Final live run: zero errors/warnings.
- Subsequent composited menu screenshots produced five tooling errors: `PlayerLoop internal function has been called recursively`. Their stack traces point to MCPForUnity `Runtime/Helpers/ScreenshotUtility.cs:196` (`CaptureCompositedAfterFrame`), not ORBITAL gameplay. Camera-specific captures and native deferred screenshots avoid that path. This is a screenshot-tool limitation, separate from the clean interaction run.
- Native full Game view captures exposed missing particle bounds in the paused manually populated system. Explicit local bounds and AlwaysSimulate culling corrected it. Native captures then confirmed both the real gameplay cloud and instructions preview, with zero Console errors/warnings in the final session.

## Results

| Criterion | Evidence | Result |
|---|---|---|
| New playable ORBITAL scene | Scene loads; normal roster, countdown, score service, goals, power-ups, audio and drone director present | Passed |
| Full cut face and blinking probability samples | Visually inspected Editor and Play Mode; 20,000 live particles; no removed quadrant | Passed |
| Shared density and unbiased random direction | 20,000 density/sampler/direction trials, normalized peak/support/symmetry, handedness and timestep-invariant encounter math | Passed |
| Player, core and enemy strikes | Real scene player/core and actual drone prefab hit and displaced with steering active | Passed |
| Electron position, enclosing vector line and direction | Live flash accounting and visual inspection of the last verified strike | Passed |
| One collectible nugget per hit | Strike/nugget/flash counts agree; moving nugget collected once for mass, then expiry/reuse checked | Passed |
| Lifecycle and gating | Closed scoring, time pause, outside-cloud immunity, visual disable/re-enable, effect expiry and bounded pool checks | Passed |
| Serialization and registration | No missing scripts; all required references set; one audio listener; one catalog entry and one enabled build scene | Passed |
| Shader and script compilation | Unity imports with no compilation errors; both shaders have no compiler errors | Passed |
| Level-select and instructions | Runtime screens visually inspected and sized to the existing layout | Passed |
| Scene flow and match result | Carousel confirmation opens ORBITAL instructions; selected-gameplay route loads ORBITAL into Regulation; accelerated existing match-end routine reaches POSTGAME with ORBITAL result identity | Passed |
| Standalone target build | Not requested; not run | Not run |
| Frame-time/device benchmark | No target budget specified; not measured | Not run |

`OrbitalValidation.Run()` passed 60,015 assertions, including 20,000 samples with three assertions per iteration. These are a dependency-free editor check, not 60,015 independently authored tests.

`OrbitalValidation.RunLive()` passed **18 interaction checks**:

1. Natural strikes release one nugget and one flash.
2. Closed scoring stops strikes.
3. Time pause stops strikes.
4. Player receives an electron strike.
5. Player moves after its impulse.
6. Amplifier core receives an electron strike.
7. Amplifier core moves after its impulse.
8. Real drone prefab receives an electron strike.
9. Drone recoils while its steering is active.
10. All three actor types produce matching nugget/flash counts.
11. Nugget floats away from the contact.
12. Nugget is collectible and grants mass once.
13. Nugget expires for reuse.
14. Core outside the cloud receives no strikes.
15. Disabling the density display disables its hazard.
16. Visual pool rebuilds after disable/re-enable.
17. Nugget population remains bounded.
18. Orbital lines disappear after the flash.

High encounter rates were temporarily used for repeatable interaction checks and restored afterward. The impact preview image replays the last verified contact/direction with the Editor paused so the brief line and flash can be inspected. Runtime fixture state was not saved.

## Level-select 3D icon follow-up

- The saved icon now contains 7,000 samples throughout the complete volume. Its generated child is tilted and turns at 12 degrees per second without interfering with the carousel's root rotation. Gameplay and instructions prefabs retain `fullVolume = false`.
- A 20,000-sample volume check measured means `(0.0010, 0.0013, -0.0015)` and second moments `(0.0971, 0.0982, 0.2074)`, with samples on both sides of all three axes and maximum normalized radius `0.9938`.
- Fixed blink timing under long frames: expired samples retain elapsed time within their new lifetime instead of all restarting at zero opacity. An explicit 0.5-second step left 6,996 of 7,000 particles with nonzero opacity. This also improves the shared cross-section visual during frame stalls.
- A fresh Play session loaded the saved icon through the normal carousel. Native Game view screenshots at two different orientations verified the volume and ongoing presentation turn. The final version retains the original ORBITAL particle material; temporary rendering experiments were discarded.
- Unity compilation and final Console check: zero errors/warnings. No standalone build or performance benchmark was run for this visual change.
- Screenshots: `Assets/Screenshots/ORBITAL/ORBITAL-level-select-3D.png` and `ORBITAL-level-select-3D-angle2.png`. Restored the user's starting `S-0_ATTRACT` scene outside Play Mode.

## Impact, outer-tail and mass nugglet refinement

- Stemless, sharp three-vertex arrowhead; orbit and arrow use a narrow bright core with a soft additive halo. The point flash keeps its original size. Native Game view overview and close-up inspected.
- Gameplay tail extends by 25% (4.9 to 6.125 world units). Inner densities through normalized radius 0.78 remain unchanged. 23,000 particles approximately preserve the previous inner concentration; hazard queries and sampling share the extended support. Menu/instructions cloud dimensions remain unchanged.
- New `Mass Nugglet.prefab` is a variant of the existing Orbital Mass Nugget. Root scale is 1/3; both particle systems use hierarchy scaling. With identical particle seeds and simulation times, the original renderer bounds were `(0.65, 0.47, 0.59)` and the nugglet `(0.22, 0.16, 0.20)`.
- Nugglet reward multiplier is 0.1. Actual player pickup comparison confirmed the original nugget grants ten times the nugglet reward. These pickups restore mass and do not directly award match points.
- Updated density/asset checks passed **60,021 assertions**, plus 5,000 additional samples to verify visible occupancy in the extended tail. Updated live fixture passed **19 interaction checks**, adding the exact standard/nugglet reward comparison to the original checks.
- The first fixture run exposed its assumption that the scene's drone spawner is active. The fixture now reads its configured prefab even while the spawner is disabled. The scene's disabled spawner setting was preserved; final live run had zero Console errors/warnings. A temporary validation-script access error was corrected before the final compilation/run.
- `ORBITAL-refined-gameplay.png` includes a temporary side-by-side pickup comparison at lower left (standard on left, nugglet on right) and a paused representative impact. `ORBITAL-arrowhead-glow-detail.png` magnifies that impact. Preview actors/camera/time changes were not saved. ORBITAL remains open outside Play Mode.
- No standalone build or target-device benchmark was run.

## Opaque impacts, pickup lifecycle and cloud entrance

- Impact colors and shader fragment alpha remain one. Electron and arrow geometry grow/shrink; orbit thickness goes from zero to full to zero. The soft halo adds RGB light. The scene's existing 0.5-second impact duration is preserved.
- Cloud particles use `ZTest LEqual`. Native Game view inspection confirms the black player/core surfaces occlude the cloud. The 1.4-second entrance starts every sample at size zero and the same deepest purple, then reaches the normal density palette and sizes.
- Standard Matter nuggets, Orbital Mass Nuggets and Mass Nugglets share the same lifecycle: 0.18-second particle-size entrance, 0.16-second exit on collection/expiry, immediate collection lockout, reset on reuse, and an animated retirement before a full pool relocates an occupied slot.
- All three prefabs passed real right-arena-wall tests with velocity `(0.12, 0, 0.07)` becoming `(-0.102, 0, 0.07)`, and `(3, 0, 1.2)` becoming `(-2.55, 0, 1.2)`. Wall restitution is 0.85; tangent momentum is preserved. Their frictionless material and zero sleep threshold prevent low-speed sticking without a minimum-speed boost or project-wide physics changes.
- The toast uses the existing bounded toast pool and reports **actual mass restored** as `+X% MASS`. Collection at 0.005 below capacity restored exactly 0.005, displayed the corresponding amount, and granted no duplicate reward during the exit. A zero-gain pickup produces no positive toast. These pickups still do not add match points.
- `OrbitalPolishValidation` passed **37 runtime checks**, covering all three prefabs, six oblique wall collisions, entrances/exits, expiry, capped collection/toast, duplicate protection, pooled reuse/retirement, cloud entrance and opaque impact lifetime. The initial impact-expiry timing assertion assumed the script's default duration; it now honors the scene's actual duration.
- The 60,021 density/scene/shader assertions passed. The existing interaction fixture also confirmed player/core/drone impulses, pickup reward ratio, expiry and gating. Its two lifetime-total accounting assertions encountered the deliberately added polish-preview flash, so they are not counted as passing in this follow-up. A fresh complete rerun was interrupted by changes to the shared Editor scene; it ended on level select. The fixture now supports a temporary real core prefab when the edited scene has no gameplay core. No other session's scene edits were reverted or saved over.
- Latest Console: zero errors/warnings. Final script compilation completed. Earlier fixture-only diagnostics (missing gameplay core and invoking scene assertions after the Editor switched away from ORBITAL) are separate from runtime gameplay. No standalone build or target-device benchmark was run.
- Native captures: `ORBITAL-cloud-entrance.png`, `ORBITAL-opaque-occlusion.png`, and `ORBITAL-opaque-impact.png` in `Assets/Screenshots/ORBITAL`. Capture actors/settings were temporary.

## Six formations and amplifier overlap

- The gameplay cloud cycles Original → (3,0,0) → (3,2,2) → (4,1,1) → (4,2,0) → (4,2,1) → Original. Saved Inspector values are 15 seconds between transition starts and 2 seconds of smooth redistribution. Physics advances the formation before evaluating encounters; visible samples and hits use the same blended density. The original formation, outer taper, intro, impact opacity and pickups remain intact.
- Added cached radial profiles and six sampling tables. Radial nodes and angular lobe directions follow the requested reference; radial probability weighting and contrast compression are explicit game adaptations. Sample count stays bounded at 23,000 during changes. No new runtime object creation occurs when switching between the preloaded formations.
- ORBITAL now contains DYNAMO PROTOTYPE's existing amplifier/resonance coordinator configuration with explicit local arena/score/anchor references, the `345 Hz Particle Encounter` and `Amplifier Core - Spawn Cycle` prefabs, six-second initial delay, and 8 ± 2 second respawn delay. The same score profile, team goals, HUD presenters and capture authority are used. Old standalone gameplay cores are inactive; HUD cores and the user's disabled drone-spawner state are preserved.
- `OrbitalFormationValidation.Run()` passed **6,034 assertions**, including 6,000 bounded/symmetric sample trials, concentration checks, radial/angular nodes, timing, sequence wrap, pause, large timestep behavior, and scene references. The existing density/scene/shader validation also passed **60,021 assertions**.
- `OrbitalFormationPlayValidation` passed **21 runtime checks** in a fresh ORBITAL match. All six formations rendered with the full particle pool. The resonance pattern formed alongside the cloud; safe placement produced exactly one core. Time pause and match-clock pause froze the cloud. A real managed core took an electron impulse during a transition, then physically entered a team goal. Capture advanced the team tier once, disabled/dissolved the paired pattern, rejected duplicate capture, updated the HUD, and multiplied an accepted existing score award. The randomized 6–10-second respawn delay completed, producing one replacement core while cloud transitions continued independently.
- Native Game view screenshots `ORBITAL-formation-0.png` through `ORBITAL-formation-5.png` were inspected, including the original, concentric shells, horizontal/nested lobes, polar/belt structure and nested diagonal lobes. `ORBITAL-amplifier-overlap.png` captures the active overlap. Temporary accelerated formation timing was restored; no runtime test score/capture state was saved.
- Instructions now explain six changing formations and goal capture for team amplification. Loaded through the normal selected-level route and visually verified: all paragraphs fit inside the instructions panel. Native capture: `ORBITAL-six-formations-instructions.png`. Menu diorama and instructions preview retain their existing static starting formation and 3D/cross-section modes.
- Unity compilation and runtime Console: zero errors/warnings. This pass completed the previously interrupted overlap validation. No standalone build or device benchmark was run.

## Scope of file changes

New ORBITAL runtime/editor scripts, shaders, materials, prefabs, stage/level definitions, scene and documentation. Shared integration includes scaled player mass gain, Matter nugget lifecycle/prefab, the toast pool's actual-mass display, one LevelCatalog entry and the appended build scene. A pickup-specific frictionless PhysicsMaterial was added; global physics, graphics, input and package settings are unchanged. Other in-progress changes were preserved.
