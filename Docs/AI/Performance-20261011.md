# MASSIVE cabinet performance diagnostic — 11 October 2026

**Result: the new development build works, but the tested four-player matches do not sustain the cabinet's 60 FPS target.** ORBITAL and NOVA are the largest sustained problems. Shared resonance processing is expensive across levels. COSMOS also has a confirmed 5.6-second startup calculation that scene preloading does not hide.

[Launch the development build](C:/Users/mattf/massive/Builds/Cabinet-Development-20261011/Launch-Cabinet-Development.cmd). [Executable](C:/Users/mattf/massive/Builds/Cabinet-Development-20261011/MASSIVE-Development.exe).

**Build and test conditions**

- Source: clean commit `29ff79bcf27a0d23f7468de088ca3e47686b6118` in `C:/Users/mattf/massive`.
- Unity 6000.0.28f1, Windows x64, Mono, Development build; no Deep Profiling, script debugging, or automatic profiler connection. Build GUID `1d158cc7f46748baa38877a89f14d909`.
- Build succeeded: 0 errors, 106 warnings, 203 seconds for the build stage, 466,802,275 bytes reported by Unity.
- i5-11400F, RTX 2060 6 GB, 32 GB RAM; driver 617.14. 3840×2160, current display refresh 120 Hz, Very High, 4× MSAA, VSync 1, DX11, borderless, BitBlt presentation override.
- All eight enabled catalog levels ran full natural matches and reached results; all player processes exited successfully. Four scripted players used real movement, center-directed attacks, partial charges and shields. NOVA's bonus phase also completed.
- The Unity editor was closed throughout player captures, then restored to COSMOS after offline analysis. No gameplay, scene, prefab, package, or quality-setting changes were retained. The audit harness was removed from project Assets after building and archived with the evidence. Its behavior in this binary requires `-massive-audit`; the normal launcher uses real controls.

**Gameplay results**

Average FPS is frames divided by elapsed measured time, including gameplay hitches. “Early” is scene age 10–60 seconds; “late” is 120–180 seconds. P95 is the frame time that 95% of measured frames were at or below; the 60 FPS budget is **16.67 ms**.

| Level | Regulation average FPS | Early FPS | Late FPS | Regulation P95 frame time | Median GPU time |
|---|---:|---:|---:|---:|---:|
| LATTICE | 50.3 | 52.5 | 41.0 | 34.3 ms | 13.1 ms |
| COSMOS | 47.9 | 60.4 | 30.9 | 37.8 ms | 15.7 ms |
| HIGGS | 56.1 | 58.9 | 49.4 | 24.5 ms | 16.2 ms |
| ORBITAL | 36.5 | 44.2 | 25.9 | 56.5 ms | 12.4 ms |
| PULSAR | 50.8 | 58.1 | 38.2 | 35.3 ms | 16.2 ms |
| DYNAMO | 43.6 | 45.9 | 37.8 | 35.0 ms | 21.1 ms |
| NOVA | 37.7 | 49.7 | 23.0 | 55.4 ms | 13.4 ms |
| SINGULARITY | 54.5 | 56.1 | 48.8 | 26.5 ms | 16.2 ms |

Every level worsened late in its match. That makes shared interactions, enemies, spawning and cleanup higher priorities than only simplifying each level's background. The average GPU column does not explain every slow frame; ORBITAL and NOVA have substantial CPU/job work despite GPU medians below budget.

**Menus, instructions and results**

| Screen | Measured average FPS | Interpretation |
|---|---:|---|
| Attract Begin, longer settled window | 60.0 | Meets target in this window |
| Attract menu, longer settled window | 60.2 | Meets target in this window |
| How To Play | 60.3 | Meets target in this window |
| Level Select | 73.4 | Above target on average; P95 16.1 ms |
| Instructions: LATTICE, COSMOS, HIGGS, PULSAR, DYNAMO, SINGULARITY | 119.8–120.0 | Ample measured headroom |
| Instructions: ORBITAL | 60.0 | CPU-heavy preview, little headroom |
| Instructions: NOVA | 46.6 | Clear optimization target |
| Results, all levels | 120.0 | Ample measured headroom |
| NOVA bonus phase | 41.0 | Also below target; median GPU 20.4 ms |

Short Attract visits in subsequent runs averaged about 56–59 FPS, including the retained portion of their settling/intro period. The longer settled 60 FPS samples do not establish that every entry to Attract is perfectly paced.

The display is now 120 Hz. VSync 1 permits up to 120 FPS; it does not enforce 60. A consistent 60 FPS presentation policy at this refresh would use VSync 2, subject to cabinet testing, but that would not fix frames already exceeding 16.67 ms. No pacing settings were changed. [Unity VSync documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/QualitySettings-vSyncCount.html).

**Recommended work, in priority order**

1. **P1 — Reduce shared resonance CPU work. Confirmed expensive callback; inner-function attribution still needs finer markers.** In short late-game CPU captures, `ResonanceInteractionDriver.FixedUpdate` averaged 13.0 ms/frame in ORBITAL, 11.8 in COSMOS, 9.6 in NOVA, 5.4 in HIGGS, 4.7 in PULSAR, 4.2 in DYNAMO, 3.5 in LATTICE and 3.4 in SINGULARITY. These are callback self times including uninstrumented callees; fixed-step catch-up can run the callback several times in one rendered frame.

   The [driver](C:/Users/mattf/massive/Assets/Resonance/Runtime/ResonanceInteractionDriver.cs:66) processes active actors each physics step, and its contact loop tests every interactive segment. [NearestContact](C:/Users/mattf/massive/Assets/Resonance/Runtime/ResonanceSegment.cs:46) traverses all polyline spans, evaluating envelopes and distances. Player-passage visuals also search segments. Add markers around filter maintenance, passage queries and contact queries; then add conservative spatial bounds to reject distant segments and cache invariant path/transform data. Preserve exact nearby contact behavior. Validate fast crossings, tapered ends, sludge and magnetic Core responses before accepting the optimization.

2. **P1 — Bake COSMOS topology before gameplay. Confirmed startup cause.** Scene activation itself took 181 ms, followed by a 5,608 ms gameplay frame. Running the exact topology source and scene settings in an isolated Unity process took **5,615 and 5,565 ms** for 56,000 particles, reproducing the stall. [CosmicWebBackground](C:/Users/mattf/massive/Assets/Scripts/Anomalies/COSMOS/CosmicWebBackground.cs:190) builds the missing particle data in its first LateUpdate after activation.

   Serialize/bake topology at authoring or build time and load/upload that data at runtime, with rebaking when seed/count/organic-strength inputs change. This can preserve the current appearance; reducing the particle count is not necessary to remove the generation cost. Plain scene preloading cannot execute this deferred first-frame calculation while activation is held. Reprofile the remaining buffer upload after baking. Evidence: `cosmos-topology-benchmark.csv`; the benchmark's managed-allocation field is unavailable and must not be interpreted as zero allocation.

3. **P1 — Move ORBITAL cloud animation off its current full CPU update. Confirmed.** [OrbitalCloudVisual.Update](C:/Users/mattf/massive/Assets/Scripts/Anomalies/ORBITAL/OrbitalCloudVisual.cs:120) averaged 10.2 ms/frame early and 11.9 ms late; its instructions preview averaged 12.5 ms. The code resamples short-lived points, updates color/size/lifetime across approximately 23,000 particles and uploads the full array every frame.

   Start with cached color evaluation and avoiding unnecessary full-array work, then evaluate GPU animation or a reduced-rate CPU update with visual interpolation. Preserve the formation transition and density distribution. This is a large cost even before enemy combat grows.

4. **P1 — Reduce NOVA particle trail and noise work, including its instructions preview. Confirmed workload; gains require an A/B test.** The preview snapshot contains 28,144 particles, mainly Flare (19,175, noise and trails, 8-second lifetime) and Corona (8,969, noise and trails, 3-second lifetime). Its detailed capture shows render-thread waiting on jobs and substantial particle trail geometry work. Late gameplay has at least 62,449 live particles among the 35 largest sampled systems. Noise, trail geometry and particle geometry dominate named worker activity.

   Test trail vertex spacing/lifetime, particle lifetime/emission, noise cost, and a lighter dedicated instructions preview. Worker times run in parallel and must not be added directly to main-thread or GPU frame time. NOVA also shares the resonance bottleneck and has heavy trigger traffic, so particle reductions alone may not reach 60 FPS.

5. **P2 — Reduce LATTICE field evaluation and uploads. Confirmed expensive callback.** [LatticeDisruptionField.Update](C:/Users/mattf/massive/Assets/Scripts/Anomalies/LATTICE/LatticeDisruptionField.cs:110) averaged 6.3 ms/frame early and 5.7 ms late. Its Advance method reevaluates edge connections/noise and uploads state plus the node texture. Profile ConnectionAt/noise separately, reuse shared samples, and test less frequent visual updates with interpolation while retaining the required gameplay disruption data. Rendering is already partly on the GPU; the remaining CPU field simulation still matters.

6. **P2 — Reduce DYNAMO GPU cost. Confirmed GPU budget problem; individual effect attribution unconfirmed.** Median GPU time was 21.1 ms, above the 16.67 ms budget. [DynamoSelectiveBloom](C:/Users/mattf/massive/Assets/Scripts/Anomalies/Magnetosphere/DynamoSelectiveBloom.cs:89) creates full-resolution HDR/MSAA sources for separate field and flow bloom channels, plus downsample/upsample passes. A/B test each bloom channel, source resolution and flow/field detail independently. The code identifies plausible targets; this audit does not establish how much of the 21.1 ms belongs to bloom.

7. **P2 — Investigate late-match hitches, allocations and transient mesh ownership. Hitches and cleanup gap confirmed; exact hitch causes unresolved.** Outside detailed captures, every level had an isolated regulation frame above 700 ms; COSMOS reached 1.96 seconds. The largest stalls lie outside the short CPU capture windows, so they cannot responsibly be attributed to a particular script or garbage collection from these traces.

   Shared late-game work includes beam turrets, Dyson repulsors, rendering callbacks and enemy updates. COSMOS's late turret FixedUpdate marker alone averaged 5.8 ms/frame. Target a capture around 120–190 seconds with allocation call stacks or explicit spawn/rebuild markers to separate expensive instantiation, procedural generation, destruction and GC.

   Unity allocated memory grew roughly 228–302 MiB between the 8- and 148-second gameplay snapshots across the runs. For example, LATTICE grew 502→759 MiB and COSMOS 485→787 MiB; much was reclaimed at results. Profiler buffers, caches and growing encounters contribute, so these snapshots do not prove a persistent leak. A concrete ownership gap exists in [DysonSpherePanels](<C:/Users/mattf/massive/Assets/Enemy System/Enemy Types/Melee/DysonSphere/DysonSpherePanels.cs:1256>): runtime-generated panel meshes have a cleanup routine for rebuilds, but OnDestroy only unregisters the component. Dispose owned runtime meshes when the owning object is destroyed, and validate repeated spawn/despawn cycles before claiming the amount recovered.

   COSMOS and ORBITAL also show many mass-nugget particle systems with 900 particles apiece. Their recorded top-35 systems total 31,500 at late snapshots; this is a lower bound, not the whole scene count. Pooling and cheaper pickup visuals are reasonable follow-up experiments.

**Loading**

Instructions preloading reported ready in 0.073–0.446 seconds after entering the screen. Request-to-sceneLoaded activation times were:

| Level | Activation |
|---|---:|
| LATTICE | 179 ms |
| COSMOS | 181 ms, plus the separate 5.6-second first-frame generation |
| HIGGS | 95 ms |
| ORBITAL | 227 ms |
| PULSAR | 98 ms |
| DYNAMO | 177 ms |
| NOVA | 424 ms |
| SINGULARITY | 208 ms |

These are measurements from a warm local test sequence, not guaranteed cold-boot disk times. Fixing deferred initialization is more promising than additional generic preloading for COSMOS. Event-log request_ms values on automatically loaded results screens refer to an old gameplay request and are **not** valid results load durations.

**Warnings and reliability**

- No captured gameplay exceptions or graphics-device failures in the eight valid runs.
- Every Level Select entry warns that a convex Sphere collider exceeds the 256-polygon limit. [LiveParticleIcon.Initialize](<C:/Users/mattf/massive/Assets/Scripts/Level Select/LiveParticleIcon.cs:65>) instantiates the active preview before disabling colliders; cooking has already occurred. Use a visual-only preview or a suitable simpler collider. Its FPS impact is not established.
- HIGGS instructions emits two Player 1 warnings: missing Rigidbody on the preview root and a massBlobPrefab resembling a Player blob. Check the visual preview's attached gameplay components/references rather than adding physics merely to silence it.
- Final build warnings: 32 C# warnings, 73 vendor shader macro redefinitions, one convex Sphere warning. Preserve the full build messages for cleanup; warnings do not imply 106 distinct runtime failures.
- Reopening the editor completed compilation and scene loading. It also logged an Entities asmref without a target and two vendor Terrain Rain shader dependencies missing SimpleTerrainBase; these did not prevent the standalone build or tested gameflow.
- NVIDIA samples reached 76°C and 2,387 MiB VRAM, with normal boost clocks observed. These samples do not suggest a VRAM-capacity problem; this was not a dedicated thermal or long-duration reliability test.

**Method and limits**

Baseline CSV analysis excludes detailed profiler recording, its one-second cooldown, screenshot/snapshot disturbance intervals, and the first three seconds of a scene. Focus remained on the player during included samples. Separate CPU/Rendering raw files cover approximately gameplay ages 30–35 and 150–155 seconds; the summaries use every fifth frame, including zeros when a marker is absent. Slow frames additionally extracted for inspection are not added to those regular-sample means. There are 35 valid raw captures, all decoded successfully.

CPU profile timings include profiling overhead and explain relative costs; they are not substitutes for the unprofiled FPS table. FrameTimingManager values can lag the wall-clock delta, so individual CSV rows must not be used to assign a single hitch to that row's GPU/main time. Particle snapshots keep only the largest 35 Unity ParticleSystems and omit procedural GPU particles.

This was one full match per level, not a long cabinet soak or a controlled regression benchmark. Scripted players exercised real combat and partial charges, but not every human strategy, every power-up state or full three-second charging. A non-development release build may be faster and must be measured separately. September results used different refresh/AA and player activity, so percentage improvements or regressions against them would be misleading.

**Evidence and follow-up**

All raw data, code used to collect it, screenshots and logs are in:

`C:/Users/mattf/.codex/visualizations/2026/09/26/01a0df00-fc1e-7373-be83-d53f2fe84bda/performance-20261011`

Key files: `summary.json`, `profile-summary.json`, `gpu.csv`, `build-result.json`, `build-messages.txt`, `cosmos-topology-benchmark.csv`, and each level's `frames.csv`, `events.tsv`, `Player.log`, `snapshots.json`, screenshots and raw profiles. The first firewall-obstructed run is preserved under `excluded-firewall-startup` and excluded from all reported results.

The next implementation pass should start with shared resonance queries and COSMOS topology baking, then ORBITAL's CPU cloud and NOVA's particle workload. Re-run the same four-player capture after each isolated change, and validate visuals/gameplay before accepting it. No optimization changes were applied during this diagnostic.

