# ORBITAL cloud update optimization

11 October 2026

Electron strikes already use the analytical probability field at a body's sampled collider contact. They do not count or collide with visible particles. The previous cabinet capture measured this gameplay work at 0.36 ms per late gameplay frame, versus 10.27 ms for the visual particle update. Replacing the probability field with coarse zones would change the encounter distribution while leaving the larger visual cost in place.

The implementation caches each stationary particle's final RGB color until it respawns or the formation changes. Settled formations update opacity from the original blink envelope and use the cached RGB bytes. Entrances and formation transitions keep the original nonlinear density-to-color calculation. Formation changes no longer evaluate an expired particle's old position immediately before replacing it.

All 23,000 gameplay particles, the 7,000-particle rotating menu volume, sampling distributions, random-number sequence, lifetimes, sizes, backing hemisphere, intro, six-state cycle, collision probabilities and impulses are retained. The renderer, shader, materials, scenes, prefabs and quality settings are unchanged. The cache adds about 92 KB for the gameplay cloud and 28 KB for the icon, and is released with the existing particle pool.

## Differential validation

A temporary copy of the original component was run beside the optimized version with the same seed and timesteps. 1,129,232 particle states matched exactly in position, RGBA bytes and size. The check also compared presentation rotation and native particle counts. It covered cut-face and full-volume modes, all formations and transitions, the intro, zero-time updates, a 700 ms slow frame, changes to opacity/hemisphere fraction/point size, rebuild and disable/re-enable cleanup.

Alternating 120-update CPU benchmarks on the cabinet used 23,000 particles at 1/60 s simulated steps for each settled formation. The original took 9.57â€“10.76 ms/update; the optimized component took 3.37â€“3.99 ms/update. Each formation's separate 120-update allocation check recorded zero managed allocations. These are isolated Editor CPU measurements, not whole-game FPS results.

The temporary comparison source and measurements are retained under the local `orbital-cloud-20261011` evidence directory. They are removed from `Assets` after testing.

## Standalone capture

Same i5-11400F / RTX 2060 cabinet, 3840 × 2160 at 120 Hz, Very High quality, 4× MSAA, VSync 1, DX11 borderless and the BitBlt override. Unity was closed. Both builds include the preceding shared-resonance and COSMOS-bake changes. The same four-player scripted movement/attack/shield harness ran a full natural match through results and returned to Attract. The new run lasted 269.65 seconds and exited with code 0.

| Measurement | Before | Optimized |
|---|---:|---:|
| Mean cloud update, regulation | 9.84 ms | 4.21 ms |
| Average regulation FPS | 41.1 | 50.0 |
| Early FPS, scene age 10–60 s | 47.4 | 53.6 |
| Late FPS, scene age 120–180 s | 31.1 | 40.8 |
| Regulation P95 frame time | 39.7 ms | 30.6 ms |
| Late P95 frame time | 63.7 ms | 46.7 ms |
| Worst regulation frame | 1,566 ms | 1,419 ms |
| Late mean / peak enemy count | 38.3 / 60 | 32.7 / 57 |

The cloud's measured whole-match update cost fell 57.2%. The isolated same-input tests establish the improvement independently of changes in enemy population. The observed 21.7% whole-match FPS improvement is one run per version, not a deterministic replay or a guaranteed uplift in every match. Faster frames and independently randomized gameplay change encounter and enemy outcomes. Late-game comparisons have different average enemy counts.

Metrics use regulation after ten scene seconds and exclude diagnostic disturbances, profiling intervals and loss of focus with a one-second cooldown. The new run had zero unfocused gameplay frames; the previous run's brief loss of focus was excluded. Temporary stopwatch measurement surrounds cloud Tick; the earlier audit also timed other systems. No detailed profiler recording ran during these captures.

The exact particle-state comparisons include entrances and transitions, but those paths deliberately keep the full palette calculation. The new cloud P95 update was 9.09 ms; caching chiefly benefits settled formations. Visible counts and short blink lifetimes were not reduced.

The instructions icon/close-up and gameplay cloud were visually inspected in the actual player. All six formation identifiers occurred during gameplay. Runtime logs contain only the pre-existing level-select convex Sphere hull warning; there are no runtime errors or exceptions.

The 60 FPS target is still unmet. The worst new frame occurred at scene age 176.15 s with 37 enemies: 1,419.09 ms total, of which cloud Tick was only 6.74 ms. This change does not resolve the separate resonance collision-pair restoration stalls identified in `Orbital-Targeted-20261011.md`. This capture did not re-profile their native/internal breakdown.

## Build and project state

Final Windows x64 Mono Development build: **Succeeded, zero errors, 74 warnings**, GUID `3776987d056349068f484b307b124db1`, Unity 6000.0.28f1. Every warning message was present in the previous build; there were no new warning messages. The normal launcher preserves the cabinet display settings and does not enable the scripted audit.

- Build: `Builds/Cabinet-Orbital-Cloud-20261011-Validated/MASSIVE-Development.exe`
- Launcher: `Builds/Cabinet-Orbital-Cloud-20261011-Validated/Launch-Cabinet-Development.cmd`
- Production changes: `Assets/Scripts/Anomalies/ORBITAL/OrbitalCloudVisual.cs` and its README.
- Evidence: `C:/Users/mattf/.codex/visualizations/2026/09/26/01a0df00-fc1e-7373-be83-d53f2fe84bda/orbital-cloud-20261011/`, including `cloud-validation.json`, `comparison.json`, `optimized/`, original/optimized source copies and the archived test harness.

An earlier graphics-disabled build reported 82 compute-kernel initialization errors while loading existing scene components. That artifact was not used for profiling or handoff. Rebuilding with a graphics device produced the zero-error result above. The temporary baseline comparison component also needed to be moved out of the Editor assembly before it could be attached; the corrected test then passed. Both setup-failure logs are retained as evidence.

Temporary audit assets and source timing hooks were removed after the standalone capture. ProjectSettings and QualitySettings match their pre-test hashes. Prior resonance/COSMOS work was preserved; no scenes, prefabs, shader/material assets, packages, or settings were changed for this optimization.

Final Editor compilation passed after removing the diagnostic harness. Unity was restored in Edit Mode in S-9_COSMOS with zero Console errors and the same 28 baseline warnings. The restored source contains no audit references; the changed-file whitespace check passed. No Inspector or prefab setup is required.
