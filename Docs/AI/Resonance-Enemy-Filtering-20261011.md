# Resonance enemy filtering — 2026-10-11

Implemented option 1: enemy bodies ignore resonance contacts efficiently while
retaining the existing geometric sludge slowdown and drag. **Keep sludge for now.**
Measured enemy processing is too small to justify removing that gameplay behavior.

## Change and compatibility

- Added reserved layer 13, `ResonanceEnemyPassThrough`, in `ProjectSettings/TagManager.asset`.
  This is the only ProjectSettings change; the global collision matrix is unchanged.
- Default-layer patterns with Enemy Sludge enabled move only generated contact
  colliders to that layer. Renderers and authored geometry/density remain unchanged.
- `EnemyBase` excludes that layer on its Rigidbody, including attached compound
  colliders. Pool/disable cleanup preserves authored and unrelated exclusion bits.
- The actor driver skips native enemy-wall collision exceptions while still
  computing movement multiplier and velocity drag. Player/Core/pickup filtering
  retains its existing path. Projectile slowdown and beam blocking code is unchanged.
- Steering, Seeker movement/sight, Carrier sidesteps, and Ranged Drone sight checks
  exclude these contacts before collecting hits. Dense resonance cannot fill their
  hit buffers and hide an ordinary wall behind it. Carrier sidesteps now agree
  with the existing enemy sludge policy.
- Custom obstacle layers or unequal global matrix permissions retain the existing
  pair-filter fallback. Disabling Enemy Sludge restores authored wall behavior.
  Rebuild patterns after future global matrix changes. Future non-enemy queries
  intended to hit Default resonance must also include the reserved contact layer.

## Controlled result

Three sequential before/after repetitions per fixture, medians below. The actual
345 Hz encounter supplies 227 solid wall colliders. Every native ignored-pair
value is checked both before and after cleanup. The before implementation includes
the previously completed actor-owned/reverse-order cleanup optimization.
Six additional bodies retain ordinary pair filtering in each row.

| Enemies | Native ignored pairs | Driver setup | Driver shutdown |
|---:|---:|---:|---:|
| 8 | 3,178 → 1,362 | 4.48 → 1.05 ms | 40.47 → 5.88 ms |
| 24 | 6,810 → 1,362 | 18.58 → 1.11 ms | 196.60 → 6.15 ms |
| 48 | 12,258 → 1,362 | 58.44 → 1.20 ms | 676.09 → 6.19 ms |

At 48 enemies plus six other bodies, shutdown is about 99.1% cheaper.
Enemy-only fixtures generate zero ignored pairs (48-enemy shutdown:
522.98 ms → 0.011 ms). These are isolated driver timings, not frame-rate promises;
they exclude pattern construction and enemy body initialization. Benchmark
particles were reduced only inside the temporary fixture, not in production assets.

## Standalone ORBITAL evidence

The development player completed Attract → How To Play → Level Select →
Instructions → ORBITAL regulation → Results → Attract, then exited normally
after 269.73 seconds. Four scripted players; random enemy spawns; maximum
59 simultaneous enemies and 16 slowed enemies.
No runtime errors/exceptions. Only the known Level Select Sphere convex-hull warning.

Environment: Unity 6000.0.28f1 / Mono, i5-11400F, RTX 2060,
3840×2160 at 120 Hz, Very High, 4× MSAA, VSync 1, borderless DX11 with BitBlt.
The Editor was closed during the capture. Lightweight method timers were enabled;
deep profiling was off. Gameplay warmup/capture disturbances were excluded.

| Enemy sludge processing, summed across enemies | Mean | 95th percentile | Maximum |
|---|---:|---:|---:|
| Frames where it runs | 0.114 ms | 0.272 ms | 0.931 ms |
| Busy final minute, frames where it runs | 0.186 ms | 0.405 ms | 0.931 ms |

Across all measured gameplay frames, the enemy sludge mean was
0.075 ms. The active-frame late-game
mean is approximately 1.1% of a 60 FPS frame budget.
The permanent `Resonance.EnemySludge` marker covers enemy Tick work after filtering,
including movement influence removal, contact queries, and drag application.
It excludes enemy registration and ConfigureFilters. Those paths are already cheap
for enemies under the new policy; total driver time also includes other bodies.

### Remaining costs

`RestorePairs` still peaked at 185.40 ms
(29.75 seconds into gameplay, in a 225.23 ms frame). Another restoration took
137.31 ms at 154.50 seconds. There is still a hitch problem to address; this change
does not eliminate all pair restoration. Source inspection confirms current
production patterns use Default, keep Enemy Sludge enabled, and have no runtime
matrix overrides, so they take the enemy fast path. Remaining exceptions belong
to the player/Core/pickup path; custom-layer patterns would also use the fallback.
The capture times RestorePairs collectively, not each remaining actor category,
so the exact player/Core/pickup contribution is not separately measured.

The preceding diagnostic capture reached 953.53 ms in restoration. The new
185.40 ms peak is encouraging, but these are different random matches, not a
controlled comparison. The controlled table above establishes the causal benefit.

Cloud visual updates still averaged 4.39 ms per gameplay
frame (4.76 ms in the final minute), substantially more
than enemy sludge. Next priorities are removing the remaining non-enemy pair
exceptions while preserving their response modes, then cloud/rendering costs.
Making enemies ignore slowdown entirely would not resolve either cost.

### FPS qualification

A Windows Firewall prompt covered the player on launch. User dismissal was
requested; later capture showed desktop occlusion and no confirmed clean display
interval. Application.isFocused nevertheless reported true. Consequently this
run is useful for method CPU timings and lifecycle validation, **not a trustworthy
before/after FPS comparison or 60 FPS certification**. Raw frame data is retained
in `comparison.json`, but its before/after FPS must not be used as an improvement
claim. A clean foreground run after user dismissal remains outstanding.

## Validation and deliverables

- 27 new enemy-filter checks, including actual physics passage and hard-wall
  blocking, unchanged slowdown/drag, overlapping fields, pooling, dense queries,
  custom-layer fallback, and ordinary walls behind resonance.
- Existing 20 collision-cleanup, 105 prototype/contact, and 31 formation lifecycle
  checks pass: **183 checks total**.
- Build succeeded with 0 errors and 106 warnings, matching
  the preceding build count (74 existing build warnings plus 32 compiler warnings).
  Build GUID: `acf3dbc6987a46fca6cbbcc905ccc0d2`.
- `git diff --check` passes. Earlier uncommitted work is preserved.
- Temporary benchmark and instrumentation Assets were archived outside the project;
  production source was restored byte-for-byte after the build, retaining only
  the intended optimization and permanent profiler marker.
- Reusable Editor menu: **MASSIVE > Resonance > Validate Enemy Filtering**.

Build: `Builds/Cabinet-Resonance-Enemy-20261011/MASSIVE-Development.exe`.
Normal play launcher: `Builds/Cabinet-Resonance-Enemy-20261011/Launch-Cabinet-Development.cmd`.
Audit behavior only activates with the explicit `-massive-audit` argument.

Evidence directory: `C:\Users\mattf\.codex\visualizations\2026\09\26\01a0df00-fc1e-7373-be83-d53f2fe84bda\resonance-enemy-filter-20261011`. Includes exact before/clean source backups,
validation output, benchmark fixtures/results, build logs, standalone logs,
frame and method CSVs, lifecycle events, environment, and analysis script.

Final Editor check: reopened S-9_COSMOS in Edit Mode; Console shows 0 errors and 28 warnings, matching the initial Editor baseline. Clean production source compiled after temporary instrumentation was removed.
