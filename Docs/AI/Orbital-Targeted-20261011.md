# ORBITAL performance investigation

11 October 2026

Enemy population is a substantial contributor to ORBITAL's slowdown, especially through shared resonance collision management. The largest reproduced gameplay stalls came from restoring collision-ignore pairs when an Amplifier core was captured and its resonance field shut down. The cloud's particle animation is a separate, steady CPU cost. Reducing enemies alone did not reach the cabinet's 60 FPS target.

## Standalone comparisons

Same cabinet: i5-11400F, RTX 2060, 3840 × 2160 at 120 Hz, Very High, 4× MSAA, VSync 1, DX11 borderless and the BitBlt override. Unity was closed during captures. All matches used the same four-player scripted movement, attack and shield harness.

| Diagnostic condition | Average FPS | Late FPS | Regulation P95 frame | Worst measured regulation frame |
|---|---:|---:|---:|---:|
| Normal ORBITAL | 41.1 | 31.1 | 39.7 ms | 1,566 ms |
| Enemy spawning suppressed | 52.0 | 45.4 | 25.9 ms | 82 ms |
| Cloud visual updates frozen | 53.1 | 48.0 | 26.6 ms | 1,522 ms |

Late means scene age 120–180 seconds. The averages cover regulation after ten scene seconds, include gameplay hitches, and exclude diagnostic disturbances and loss of focus with a one-second cooldown. Normal ORBITAL briefly lost focus at scene age 9.209–10.274 seconds; those frames and the cooldown are excluded. The two controls stayed focused throughout gameplay.

The enemy control suppressed the director's Update in ORBITAL, producing zero enemies and zero spawns. The cloud control skipped its particle Tick after eight scene seconds, retaining the drawn cloud and the live formation, strike, recoil and pickup mechanics. Neither control is retained in the game.

These are one full match per condition, not deterministic replays. Removing enemies also changes combat, projectiles, power-ups and player trajectories. The differences show the contribution of the enemy ecosystem, not a precise additive percentage attributable to AI alone. The cloud control had 35.9 enemies on average late in its match, compared with 38.3 normally. Its measured cloud Tick cost was exactly zero. FPS gains from the two controls must not be added together as a prediction.

## Enemy density and ongoing work

Normal ORBITAL averaged 12.1 active enemies and 47.4 FPS at scene age 10–60 seconds. At 120–180 seconds it averaged 38.3 enemies and 31.1 FPS, peaking at 60 active enemies. Approximately 28 of the late population were drones or ranged drones, with four turrets, two carriers and smaller Seeker/Dyson populations.

About 5.5 enemies per frame were receiving a movement slowdown late in the match. Resonance work therefore includes actual gameplay interaction, plus collision bookkeeping for actors that may not currently touch a curve.

Selected late CPU measurements from the normal run:

| Timed operation | Mean per rendered frame |
|---|---:|
| Cloud particle Tick | 10.27 ms |
| Resonance RestorePairs | 4.27 ms |
| Whole resonance FixedUpdate callback | 3.71 ms |
| DroneController FixedUpdate callbacks | 1.37 ms |
| DysonSpherePanels Update callbacks | 0.97 ms |
| Turret FixedUpdate callbacks | 0.71 ms |
| OrbitalProbabilityCloud FixedUpdate | 0.36 ms |
| Whole enemy director Update | 0.17 ms |

Scopes can overlap and must not be summed: some RestorePairs calls occur inside resonance FixedUpdate, while the largest occur during OnDisable outside that callback. Physics can also execute multiple times per rendered frame. Component totals are not isolated costs per enemy type.

The spawn-placement scan initially looked suspicious in code, but this scene leaves the director's placementRegion reference empty. The director's measured cost is a lower priority here. Its largest late Update was 24.35 ms, not the source of the multi-second stalls.

The authored cloud prefab uses 23,000 particles; the initial 20,000 estimate was the script default. The current implementation updates their ages, resampling, density colors, sizes and particle upload on the CPU every frame. The cloud hazard excludes enemies from strike eligibility, although its broad overlap query still encounters and rejects their colliders.

## Confirmed stall mechanism

The normal run's 1,566 ms frame at scene age 176.285 seconds contained 1,531.8 ms in RestorePairs, with 47 restoration calls and 40 enemies active. An 883 ms frame at age 130.839 seconds spent 844.4 ms in the same function. With cloud animation frozen, the worst frame was still 1,522 ms, of which 1,497.6 ms was restoration. With no enemies, the largest late restoration cost fell to 27.2 ms.

A fourth normal match captured detailed CPU traces around the late gameplay window. Its largest captured gameplay frame was 1,618.6 ms:

Physics trigger events → AmplifierGoalCapture.OnTriggerEnter → ResonanceInteractionDriver.OnDisable → RestorePairs.

The shutdown callback took 1,558.1 ms; restoration samples of at least one millisecond accounted for 1,557.6 ms. The same path appeared under AmplifierResonanceSpawner.Update at a pattern transition (686 ms frame, 660 ms restoration), and under GameManagerScript.Update during match shutdown (790 ms frame, 749 ms restoration).

RestorePairs scans one shared list for every released actor, calls Physics.IgnoreCollision to restore previous state, and removes matching entries with List.RemoveAt. Removing entries shifts the remaining list. OnDisable releases every actor in sequence. This is a poor scaling path when many actors have relationships with many resonance colliders.

The measured cause is the restoration function and its synchronous call path. The captures do not separately measure managed list removal versus the native IgnoreCollision calls, so the benefit of a particular rewrite is not yet established. The earlier unprofiled 2.64-second frame cannot be attributed retrospectively with certainty, but this reproduces and identifies the same class of large stall.

A separate results-transition frame spent about 501 ms destroying scene objects. That is a load/unload issue, distinct from the in-match capture stall.

## Recommended order

1. **Optimize collision-pair restoration first.** Measure list maintenance and native restoration separately; keep each actor's pairs together, avoid repeated global scans and shifting removals, and restore each owned pair once. Preserve existing collision state, sludge cleanup, disabled/destroyed actors and overlapping pattern behavior. Verify heavy core capture, pattern timeout, actor replacement and match shutdown.
2. **Optimize the cloud's CPU animation.** It consumes roughly ten milliseconds of a 16.7 ms frame budget even with no enemies. Move suitable per-particle work to a GPU or job path, or precompute reusable samples and color data while retaining the six formations and independent probability gameplay. A completely static cloud was only a diagnostic control.
3. **Reassess enemy budgets and expensive presentations after those changes.** Enemy density matters, but lowering it first would change encounter design while leaving avoidable shared overhead. Profile the remaining drone movement, turret traces, panel presentation, particles and rendering under the intended late-match population.

Neither individual control sustained 60 FPS throughout the match. This test does not claim that fixing only one system will do so.

## Validation and reproducibility

The diagnostic Windows x64 Mono development build succeeded with zero errors and 106 existing-category warnings. Build GUID: b2d850482707404aab59f448a6f9a44a. No Deep Profiling was used. All four matches reached natural results and exited with code zero. The only captured warning was the known level-select convex Sphere warning.

Seventy-six temporary timing scopes recorded operations throughout the three comparison matches. The census collector itself averaged 0.041 ms per frame normally, 0.009 ms without enemies and 0.037 ms with cloud visuals frozen; this does not include every scope's instrumentation overhead. The normal 41.1 FPS run was close to the previous optimized build's 41.6 FPS, but that is not a formal overhead calibration.

Seven raw captures from the fourth match decoded successfully. Detailed traces sample every fifth frame plus all slow frames, so their aggregate averages overrepresent stalls; they are used for call-path attribution, not the FPS comparison table.

All 35 temporarily instrumented production source files were restored byte-for-byte from the pre-test backups. PlayerSettings and QualitySettings remained byte-identical. Temporary probe assets were archived outside Assets. Enemy budgets, cloud settings and gameplay behavior are unchanged. The existing optimized development build remains available separately, and Unity was reopened.

Evidence directory:
C:/Users/mattf/.codex/visualizations/2026/09/26/01a0df00-fc1e-7373-be83-d53f2fe84bda/orbital-targeted-20261011

Key files: targeted-summary.json, hitch-profile-summary.json, build-result.json, restored.json, per-condition targeted.csv, frames.csv, targeted-events.tsv, environment.json, process.json and player logs. The baseline-raw directory contains raw profiles and decoded slow-frame call trees. prepare.py, archived instrumentation-assets and Run-Targeted.ps1 preserve the experiment.

Primary source locations:
- [Collision-pair restoration](C:/Users/mattf/massive/Assets/Resonance/Runtime/ResonanceInteractionDriver.cs:285)
- [Resonance interaction gating](C:/Users/mattf/massive/Assets/Resonance/Runtime/ResonancePatternController.cs:239)
- [Cloud particle update](C:/Users/mattf/massive/Assets/Scripts/Anomalies/ORBITAL/OrbitalCloudVisual.cs:178)
