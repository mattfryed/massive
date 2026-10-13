# Resonance and COSMOS optimizations — 11 October 2026

Both requested optimizations are implemented and validated. The comparison build improves the three tested levels, although it does not yet sustain 60 FPS throughout their matches.

[Launch optimized development build](C:/Users/mattf/massive/Builds/Cabinet-Development-20261011-Optimized/Launch-Cabinet-Development.cmd).
[Original diagnostic report](C:/Users/mattf/massive/Docs/AI/Performance-20261011.md).

**What changed**

Resonance contact processing now caches clipped curve-span geometry, uses conservative bounds to skip irrelevant spans, and limits initial actor queries to the maximum possible interaction range. The magnetic range includes the actor radius and velocity lookahead; ongoing magnetic surface following retains its global-nearest query. Swept player passage queries similarly reject only paths provably outside the effect's band. Existing wake/pulse decay remains independent of new contact queries. Actor registries now use dictionaries, and collider bounds are read once per collider rather than twice.

Width/taper values and transforms remain live. Span caches refresh on initialization and collision-extent changes; pattern rebuilds replace visual path caches. Physics timestep, curve resolution, response parameters, shaders, colors, particle counts and quality settings were not altered.

COSMOS now loads an exact binary topology bake for its existing 56,000-particle background. The shader animation, timeline evolution, palette and supernova sampling remain live and use the same topology data. The saved scene changed by one serialized bake-reference line.

The Inspector has **Bake / Refresh Background Topology**. Refresh after changing Seed, Particle Count or Organic Strength for fast Editor Play Mode startup. Missing or mismatched bakes fall back to generation, so parameter changes do not silently display old settings. Player builds automatically create/refresh matching bakes for enabled scenes and prefab dependencies, with source/Unity-version fingerprint validation, and assign them to the built scene copies. The current bake adds approximately 7.2 MB before player compression.

**Standalone comparison**

Same cabinet and settings as the baseline: i5-11400F, RTX 2060, 3840×2160 at 120 Hz, Very High, 4× MSAA, VSync 1, DX11 borderless with BitBlt override. Unity was closed during captures. Four scripted players used the same movement/attack/shield harness. Each level completed a full natural match and reached results; NOVA also completed its bonus/finale flow.

| Level | Average FPS before → after | Early FPS before → after | Late FPS before → after | Regulation P95 before → after |
|---|---:|---:|---:|---:|
| COSMOS | 47.9 → 57.3 | 60.4 → 68.0 | 30.9 → 44.9 | 37.8 → 27.8 ms |
| ORBITAL | 36.5 → 41.6 | 44.2 → 48.4 | 25.9 → 29.0 | 56.5 → 39.4 ms |
| NOVA | 37.7 → 46.0 | 49.7 → 56.6 | 23.0 → 30.8 | 55.4 → 39.3 ms |

“Early” is scene age 10–60 seconds; “late” is 120–180 seconds. Average FPS includes included gameplay hitches. Detailed profiling, snapshot/screenshot intervals, their cooldowns and initial scene warmup are excluded. The optimized analysis also excludes unfocused frames and one second after focus returns: COSMOS briefly lost focus around scene age 74.9–78.1 seconds; neither early nor late comparison windows was affected. ORBITAL and NOVA remained focused throughout their included samples.

These are one full match per build/level, not deterministic replay trials. Encounter details, power-ups and fixed-update cadence vary. They demonstrate improvement in these runs, not a guaranteed percentage in every match. The other five levels were not recaptured after this change.

NOVA bonus averaged 49.9 FPS versus 41.0 previously. Its instructions screen remained below target (49.2 FPS in this run). ORBITAL instructions averaged 58.0 FPS; these previews were not optimized in this pass.

**Resonance CPU evidence**

Late-capture inclusive time for the whole ResonanceInteractionDriver.FixedUpdate callback:

| Level | Before | After |
|---|---:|---:|
| COSMOS | 11.785 ms/rendered frame | 1.536 ms/rendered frame |
| ORBITAL | 12.968 ms/rendered frame | 2.429 ms/rendered frame |
| NOVA | 9.553 ms/rendered frame | 1.751 ms/rendered frame |

These are inclusive timings, including the new child markers; comparing the new parent self time to the old parent self time would overstate the benefit. Detailed capture overhead is included, and physics can run zero or multiple steps per rendered frame.

Per FixedUpdate invocation, COSMOS fell from 5.772 to 1.373 ms and ORBITAL from 5.894 to 1.771 ms in the late samples. NOVA's late sample had a different update cadence and pattern re-enabling/filter work: its per-invocation cost was 3.057 → 3.378 ms, so its lower per-rendered-frame total must not be presented as a per-step speedup. NOVA's early per-invocation cost did fall from 3.260 to 0.230 ms.

The added markers isolate remaining work: late collision-filter maintenance averaged 1.29 ms/frame in COSMOS, 1.27 in ORBITAL and 1.62 in NOVA. Exact actor contact queries averaged only 0.11, 0.16 and 0.06 ms/frame respectively. Future resonance work can therefore focus on filter maintenance and creation/destruction rather than repeating the same curve-query optimization.

**COSMOS startup**

- Original standalone startup had a 5,608 ms frame during first background generation.
- Optimized standalone's largest frame within the first ten scene seconds was 490 ms, including the scene transition. The bake was confirmed in all three gameplay snapshots: 56,000 particles, one generation.
- Gameplay activation request-to-sceneLoaded was 472 ms versus 181 ms before; the separate multi-second generation stall is gone.
- Regulation began at scene age 3.319 seconds versus 8.628 seconds before.
- Isolated validation: generation took 5,714 ms; cached lookup 0.318 ms; background rebuild/GPU upload from the bake 3.402 ms. The upload timing is not the entire player scene-load time.

**Validation**

- 16,384 exact-contact, 16,384 influence-limited-contact and 16,384 swept-visual comparisons passed against frozen pre-optimization reference calculations: **49,152 comparisons**, with additional conservative-bound rejection assertions. Numeric tolerances are 0.00001 for scalar results and 0.000001 squared for vectors.
- Coverage includes clipped/tapered arcs, degenerate spans, reflected and nonuniform scales, changed transforms and contact extents after cache warmup, stationary queries and fast sweeps.
- The existing 105-check resonance prototype suite passed, including local-contact checks, hard-wall collision, magnetic braking/glide, overlapping slows and cleanup. Its 31 local-contact checks also passed when run separately; they are not an additional 31 unique checks.
- Isolated 4,096-query benchmark: original 265.641 ms, optimized global-nearest 147.884 ms, optimized influence-limited 8.737 ms. This benchmark is not a whole-game FPS forecast.
- All **1,792,000 floats** in the 56,000 baked particles and all topology statistics round-tripped exactly after asset reimport. Changed count, seed and organic-strength settings were rejected as stale.
- Final Windows x64 Mono Development build succeeded: 0 errors, 106 existing-category warnings, GUID `b5d51229146c41449dfe19c083d7f031`, 473,979,539 bytes. No Deep Profiling or script debugging.
- All three player processes exited with code 0; no captured gameplay exceptions or new runtime warnings. The known Level Select convex Sphere warning remains.
- Twelve raw CPU/Rendering captures decoded successfully. Late-match screenshots from all three scenes were inspected; backgrounds, resonance presentation, players and HUD rendered. These are representative visual checks, not a pixel-identical replay comparison.
- PlayerSettings and QualitySettings match their pre-change hashes. Temporary build/probe assets and the batch validation wrapper were archived outside Assets and removed. Unity was reopened to COSMOS afterward.

**Remaining performance limits**

ORBITAL's cloud update still consumed about 10.6 ms/frame in its late detailed sample. NOVA retains its expensive noise/trail/particle workload and below-target instructions preview. Other enemy/rendering work remains significant.

Large isolated hitches are unresolved: COSMOS reached 1.89 seconds, ORBITAL 2.64 seconds and NOVA 0.81 seconds outside detailed capture intervals. ORBITAL's worst captured hitch was larger than the baseline's 1.10 seconds; these short traces do not isolate its cause or establish whether that difference is run-to-run variation. Capture the actual stall with spawn, filter-restoration, mesh-generation and GC attribution before treating the cabinet as consistently smooth. These changes do not resolve the earlier transient-mesh cleanup finding.

**Files and evidence**

Implementation:
- [ResonanceInteractionDriver.cs](C:/Users/mattf/massive/Assets/Resonance/Runtime/ResonanceInteractionDriver.cs), [ResonanceSegment.cs](C:/Users/mattf/massive/Assets/Resonance/Runtime/ResonanceSegment.cs), [ResonanceContactVisuals.cs](C:/Users/mattf/massive/Assets/Resonance/Runtime/ResonanceContactVisuals.cs), [ResonancePatternController.cs](C:/Users/mattf/massive/Assets/Resonance/Runtime/ResonancePatternController.cs).
- [CosmicWebBake.cs](C:/Users/mattf/massive/Assets/Scripts/Anomalies/COSMOS/CosmicWebBake.cs), [CosmicWebBaking.cs](C:/Users/mattf/massive/Assets/Editor/CosmicWebBaking.cs), [CosmicWebBackground.cs](C:/Users/mattf/massive/Assets/Scripts/Anomalies/COSMOS/CosmicWebBackground.cs); serializable topology statistics, the generated binary bake and the COSMOS scene reference.
- [ResonanceQueryValidation.cs](C:/Users/mattf/massive/Assets/Editor/ResonanceQueryValidation.cs) and the updated resonance/COSMOS READMEs.

Evidence root:
`C:/Users/mattf/.codex/visualizations/2026/09/26/01a0df00-fc1e-7373-be83-d53f2fe84bda/resonance-cosmos-20261011`

Key evidence: `validation.json`, `build-result.json`, `summary.json`, `resonance-comparison.json`, `profile-summary.json`, and each level's frames, events, snapshots, player log, screenshots and raw profiles. `before/` preserves the touched source/settings baseline. The original development build remains available for comparison.

