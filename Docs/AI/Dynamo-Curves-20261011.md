# DYNAMO GPU tracing optimization — 2026-10-11

The organic magnetic field now traces its two directions independently on the
GPU. A 32-thread group and a two-row dispatch replace 64-thread groups whose
threads each calculated both directions sequentially. The dispatcher reads the
kernel's actual group width once during initialization. Seed count, maximum
trace steps, step length, envelope math, warble, depth, colors, bloom, solar-wind
density, damage and movement remain authored as before.

## Evidence and choice

The first isolation test found field tracing at roughly 1.3–1.9 ms, solar-wind
path solving at about 0.18 ms, and streak generation at about 0.05 ms. The field
was the stronger target. Reducing group size alone to 32, 16 or 8 threads gave
no meaningful improvement; independent trace directions did. The retained
shader preserves the original two constant-folded integration branches and
selects one by dispatch Y. No adaptive stepping or density reduction was added.

Final cabinet-aspect comparison, original versus retained tracing:

| Held state | Original | Parallel directions | Reduction |
|---|---:|---:|---:|
| calm | 1.750 ms | 0.791 ms | 54.8% |
| onset | 1.309 ms | 0.800 ms | 38.9% |
| mid | 1.331 ms | 0.792 ms | 40.5% |
| diagonal-max | 1.886 ms | 1.125 ms | 40.3% |

These are **CPU submission plus synchronized GPU completion wall times**, not
GPU-only timings or standalone FPS. Graphics-enabled Unity batch, RTX 2060,
actual S-6_DYNAMO sources, camera aspect explicitly 16:9, 12 warmups and 60
samples per operation/state. The timed region generates geometry and reads
back the GPU count, without Camera.Render. Readback-only overhead is recorded
separately in the JSON. An earlier 4:3 preview pass is archived separately;
the final image comparison uses the cabinet's 3840 x 2160 output.

## Visual and functional validation

Compared 48 direction/pressure/motion cases (16 directions, pressure 0/.35/1),
covering 4,136,430 original segments. GPU append order is nondeterministic, so
geometry comparison uses nearest segment endpoints rather than raw buffer order.
Both outputs are finite and connected. 47 cases have the same segment count;
one maximum-pressure case has 49,943 rather than 49,942 segments. The worst
nearest-endpoint displacement is 0.0398 world units at maximum pressure.
This is **not bit-identical geometry**: compiler floating-point rounding can
accumulate along the most strongly compressed traces.

Six before/after HDR renders at 3840 x 2160, 4x MSAA:

| State | Maximum clamped linear RGB difference | Pixels exceeding 1/255 |
|---|---:|---:|
| calm | 0.001465 | 0 (0.0000%) |
| storm | 0.001465 | 0 (0.0000%) |
| diagonal | 0.001465 | 0 (0.0000%) |
| extreme9 | 0.682617 | 4,079 (0.0492%) |
| extreme10 | 0.195312 | 898 (0.0108%) |
| extreme15 | 0.760498 | 3,277 (0.0395%) |

Calm and the representative .35-strength storm captures differ by less than
one 8-bit color step. At maximum pressure, small portions of individual bright
lines move by a few pixels; this is visible in a difference image but does not
reduce field density or alter the broad appearance. Full-scene normal and
maximum-pressure captures were inspected. Gameplay uses the unchanged CPU
envelope, not these rendered segments.

Validation passed:

- Field stability: 12 direction/pressure cases, 1,056,565 connected segments,
  adaptive pole resolution, source settings unchanged. Added persistent coverage
  for seed counts 17/31/32/33/63/64/65/257 to catch missing/duplicated trace
  directions in partially occupied GPU groups.
- Edit preview: 24 direction/time cases, calm/peak/recovery, deterministic held
  states, independent motion, resizing, buffer reuse and complete cleanup.
- Organic field Play Mode: 1,088,132 segments across 12 depth/pressure/direction
  cases, envelope containment, resizing, empty output, disable/re-enable,
  stable buffers and zero managed bytes over 32 updates.
- Flow Play Mode: 48 cases, 2,921,472 finite exterior path samples, full sampled
  exterior coverage, reversal, viewport, pause, clock, resize and stop checks.
- Bloom/style Play Mode: HDR composite, independent channels, camera detach,
  private materials, sharp-core settings and zero managed allocations.

The first combined Play Mode runner called the style test immediately after
the flow test's intentional stop. That violated the style test's visible-source
precondition. Reordering the temporary runner passed all suites; no production
behavior was changed for this test-fixture issue.

The scene, DYNAMO instructions prefab, stage profile, PlayerSettings and build
scene list retain their pre-change SHA-256 hashes. Existing pending work in the
checkout is preserved.

## Standalone build and measurement

Win64 Mono Development build succeeded with **0 errors and 106 warnings**,
matching the prior build's warning count. All 13 enabled scenes and eight levels.
Build GUID: `6172184baa1c47b3a2f7872f5e6760ab`.

`Builds/Cabinet-Dynamo-Curves-20261011/Launch-Cabinet-Development.cmd` launches
normal play at 3840 x 2160, Very High, 4x MSAA, VSync 1, DX11 borderless with
the approved BitBlt override. The same binary contains an opt-in audit harness
and original-shader reference for a full-match comparison; neither activates
during normal launch.

The user accepted the Windows firewall prompt, and the standalone comparison
completed on October 11 (October 12 UTC). Both initial obstructed captures remain
marked INVALID and are excluded. The valid captures are `baseline-clean` and
`optimized`: two complete 264-second gameflow runs, each including an actual
180-second four-player scripted match, results and return to Attract. Both exited
normally with code 0 and recorded no unfocused frames. Unity Editor was closed.

Both runs used the same build GUID, hardware and recorded graphics settings:
3840 x 2160 at 120.001 Hz, Very High, 4x MSAA, VSync 1, DX11 borderless with
BitBlt. Authored field and bloom settings match; the only recorded DYNAMO settings
differences are the selected original/optimized shader and its 64/32 thread width.
No visual density or gameplay setting was reduced.

The baseline window was visually verified unobstructed around gameplay age 60
seconds. To be conservative, the paired statistics below use gameplay age 65
seconds through the end of regulation, approximately 118.6 seconds per run.
They are not whole-match averages. Late-game statistics use scene age 120-180
seconds. Opening scenes are not used for paired FPS claims. Snapshot-disturbed
frames, their one-second recovery, loading and non-regulation phases are excluded.
The optimized player was also inspected unobstructed at Attract and early gameplay.

| Metric | Original tracing | Parallel directions |
|---|---:|---:|
| Matched-window average FPS | 57.69 | 58.35 |
| Mean frame time | 17.33 ms | 17.14 ms |
| P95 frame time | 24.86 ms | 24.51 ms |
| Median GPU time | 16.50 ms | 16.48 ms |
| P95 GPU time | 19.46 ms | 18.69 ms |
| Late-game average FPS | 55.79 | 57.00 |
| Late-game P95 frame time | 25.31 ms | 25.18 ms |
| Late-game median triangles | 2,674,757 | 2,828,986 |
| Late-game median draw calls | 590 | 587 |

Observed throughput increased 1.15% in the paired window and 2.17% late in the
match. This is a small change in one pair of nondeterministic combat/storm runs;
late-game triangle counts differ by 5.8%. It does not establish a reliable
whole-game gain of that size. The controlled held-state measurements above are
the stronger evidence that field generation itself is cheaper. **DYNAMO still
does not sustain the 60 FPS target** in this development-build workload. Median
GPU time barely changes, while the GPU tail improves; the remaining scene-wide
rendering and late enemy/VFX load still matter substantially.

Both runs recorded zero runtime errors/exceptions and the same single warning
in level select: Unity could not build the Sphere convex mesh within its 256-polygon
limit and used a partial hull. No new DYNAMO warning was recorded. This pre-existing
collider warning was not changed as part of the field-tracing optimization.

### Avoiding recurring firewall prompts

Read-only inspection after the user's approval found enabled inbound Public-profile
allow rules for this exact executable path and separate entries for many older
dated builds. Windows application firewall rules match the full executable path;
a new dated build directory can therefore prompt again. The second launch from
this approved path showed no prompt. No firewall settings were changed by Codex.

`CabinetPresentation.md` now records this existing path as the reusable development
profiling location, with historical build snapshots archived separately. A different
path or network profile can still require approval. This preserves the production
watchdog configuration and avoids requiring another approval just to rename the
current development folder.
[Microsoft firewall rules documentation](https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/rules).

Reproducible capture/statistics evidence: `Run-Dynamo.ps1`, `display-validation.json`,
`analyze.py`, `standalone-summary.json`, `comparison-checks.json` and
`firewall-rules.json`, plus both capture directories with logs, settings and CSVs.

Evidence directory:
`C:/Users/mattf/.codex/visualizations/2026/09/26/01a0df00-fc1e-7373-be83-d53f2fe84bda/dynamo-curves-20261011`.

Temporary benchmark and audit scripts are archived outside Assets. A clean
post-cleanup Unity startup compiled and exited successfully (code 0);
final-compile.log contains no C# or shader errors.

