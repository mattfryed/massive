# Level select scale

The footer represents characteristic length on a logarithmic ruler. Level Definition's **Scale Exponent** supplies both the tick position and `10<sup>x</sup>` label; meters are implicit. To edit it in Unity, select `Assets/Scripts/Level Select/LevelDefinition-<LEVEL>.asset` in the Project window, then find **Characteristic Scale → Scale Exponent** in the Inspector. These values belong to the level assets, not the scene's scale-number text object.

Current anchors from the agreed scale design:

| Stage | Level | Exponent |
| ---: | --- | ---: |
| 001 | HIGGS | -18 |
| 002 | ORBITAL | -10 |
| 003 | PULSAR | 4 |
| 004 | DYNAMO | 8 |
| 005 | NOVA | 12 |
| 006 | SINGULARITY | 14 |

`LevelScaleRuler` uses fixed exponent bounds of **-35 through 27**, framing the rounded Planck scale and observable-universe diameter. Planck length is not an experimentally established minimum distance, and the observable universe is not the entire universe. The ruler does not rescale when catalog entries change. Level positions are `(exponent + 35) / 62` along the line. Stage numbers and the `LevelCatalog.asset` carousel order now follow increasing scale (2026-09-29). Keep each Level Definition's **Level Number** and the catalog list in this order when adding or rescaling stages; the catalog's Populate tool sorts by Level Number.

Each integer exponent gets a minor tick, and each unselected level gets one major tick. The selected level has nine notches sampled from a Gaussian: `height = selectedHeight × exp(-0.5 × (sample / sigma)^2)`, with nine evenly spaced samples from -3 through 3 and default sigma 1.25. From center outward the heights are approximately 100%, 83.5%, 48.7%, 19.8%, and 5.6%. **Peak Standard Deviation** adjusts the shape on the ruler component. The denser samples preserve the previous peak's overall width; its outermost notches stay within three quarters of a decade on either side so background ticks remain unchanged. There is no triangle. Every line and notch uses pure white at full opacity, including the interrupted continuation marks at both ends.

In `S-0_LEVEL-SELECT NEW`, `Canvas/Level Scale/Scale Value` is the second target of the title's existing `TMPTextTransition`. Both values dissolve out, swap together in `LevelSelectUIController`, then grow in with the same SDF settings. The scale value is centered above the selected notch at font size 36 (25% smaller than the initial 48). Its position updates on selection and ruler resize. The footer does not intercept input.

The eight surrounding selected notches ease down to zero height during OUT (0.25 seconds) and grow to full height during IN (0.5 seconds), using unscaled time and smoothstep easing. The selected center eases between the ordinary 28-unit notch height and selected 44-unit height using the same reveal value; it never disappears. Unselected level ticks, minor ticks, endpoint ticks, the baseline, and continuation marks stay steady. A replacement transition starts from the current height, avoiding jumps during rapid input. The new selection and number move together while the text is hidden. Durations, peak shape and spacing, heights, bounds, and label offset are serialized on the ruler.

The canvas uses a 3840 × 2160 reference resolution, preserving its original authored layout while scaling to smaller displays. The footer stretches horizontally with the canvas and is anchored to its bottom edge.

If this scene is later migrated to the preset animation system, give Scale Value its own `TMPTextAnimator` and a `SCALE` group binding, then duplicate the `TITLE` track as `SCALE` in both selection sequences. Remove it from the legacy transition's targets as part of that migration, so only one system owns its material.

## Validation (2026-09-18)

Unity 6000.0.28f1 compiled the nine-notch revision and subsequent center-height clarification. Isolated preview-scene mesh checks covered all five selections at full, half, quarter, and zero reveal: the fully selected heights match the Gaussian profile, the center blends from 28 to 44 units, unselected and endpoint notches retain their full height, minor ticks retain identical positions and heights across selections, and all vertices remain opaque white. All 8,285 assertions passed for the latest revision.

The latest center-height revision also passed a live OUT/IN cycle check across 27 rendered-frame samples, reaching both 28 and 44 units with intermediate heights and never dropping below the ordinary notch. Earlier ruler revisions passed 145 Play Mode checks across all five levels, both wrap directions, rapid selection replacement, and transitions at time scale zero. Both 3840 × 2160 and 1920 × 1080 layouts were visually inspected then. The title's existing SDF animation remains shared with the value. No standalone player build was run.

## Play Mode performance investigation (2026-09-18)

Measured the scene at 3840 × 2160 in Unity 6000.0.28f1 on an i3-12100F / RTX 4090, with the existing Editor windows open. Each temporary comparison warmed up for 100 frames and sampled the next 100; figures below are median frame times, including Editor overhead, with CPU profiling enabled and deep profiling disabled. Conditions were restored between experiments. These are diagnostic Editor measurements, not standalone-build FPS predictions.

| Temporary condition | Frame time | Finding |
| --- | ---: | --- |
| All effects enabled | 42–47 ms | Repeated baselines |
| Ruler hidden | 46.38 ms vs 46.33 ms baseline | No meaningful idle-frame impact |
| NOVA icon hidden | 15.11 ms | Largest icon contributor |
| NOVA flare system hidden | 15.24 ms | Flare dominates NOVA's cost |
| Only NOVA flare trails disabled | 16.03 ms vs 41.80 ms baseline | Trails account for most of the slowdown |
| Only NOVA flare noise disabled | 28.74 ms | Noise also contributes; trails are the larger target |
| All icons hidden | 6.41 ms | Remaining scene/UI and Editor baseline |

The NOVA icon has approximately 28,000 live particles across its corona and flare systems. Its flare configuration allows 21,000 particles, trails on every particle, minimum trail vertex distance 0.03, and high-quality noise. Reducing trail geometry is the first optimization target. ORBITAL's 7,000-point CPU cloud update contributes another roughly 3 ms per frame. Hiding HIGGS or DYNAMO produced no comparable improvement; PULSAR was a smaller contributor. Much of the wall time appeared in Editor repaint/presentation waits and fell with NOVA's trails disabled, so it should not be treated as unrelated Editor overhead. No particle settings were changed permanently during this investigation.

### Preserving the flare appearance

Follow-up inspection found that shadow casting, receiving shadows, light probes, particle sorting, and trail lighting-data generation are already off. Culling is already Automatic. The original flare trails used `NVIDIA/Flex/Resources/Materials/SurfaceTest.mat` with the `Custom/SurfaceShader` Standard Specular surface shader (opaque, with emission), while particle heads used additive blending. A same-frame 3840 × 2160 comparison with plain white `Unlit/Color` changed 61,528 pixels by more than 2/255, so that direct swap did not satisfy visual equivalence and was restored immediately.

Temporarily omitting only the flare renderer from Scene-view cameras, while preserving the complete Game-view effect and simulation, measured 41.93 ms versus 43.86/45.64 ms surrounding baselines. This modest improvement does not explain away the main trail cost. Camera callbacks and original profiler settings were restored after the experiment. Reducing open Editor views may help iteration, but a standalone capture is still needed to quantify shipping performance.

At the selected icon's current 4K projection, a 0.03-unit local trail step spans approximately 1–4 pixels depending on direction. Increasing Minimum Vertex Distance preserves particle and trail counts but can alter curves, so it is a visual tradeoff requiring comparison rather than a guaranteed invisible optimization. GPU particle instancing is for mesh particles and does not directly accelerate this billboard-and-trail setup. Larger work could move trail generation/rendering to a custom GPU implementation, with appearance validation. Optimizing ORBITAL's CPU update is an independent way to save scene time without changing NOVA.

### Matched opaque flare materials

`LS_NOVA icon.prefab` now assigns `NovaFlareParticle.mat` and `NovaFlareTrail.mat` to **Flare particle system → Renderer → Material / Trail Material**. Both materials and shaders live beside this document. These are dedicated to the level-select flare; the shared third-party materials, corona, particle counts, motion, noise, trail lifetime, and trail geometry are unchanged.

`MASSIVE/NOVA/Opaque Flare Trail` retains the old diffuse-plus-emission response with constant zero specular and smoothness. It removes unused texture sampling and unnecessary generated passes/variants. It compiles to one available forward pass instead of the source material's five available passes; this does not imply that the source drew all five every frame. Blending is off, depth writing is on, and output alpha is always one. In a same-frame 4K RGB comparison, only 25 of 8,294,400 pixels changed, by at most 1/255 per channel. A control clone of the original material changed 13 pixels by the same maximum. This matches the current scene, not every possible lighting configuration.

`MASSIVE/NOVA/Opaque Flare Particle` uses the original circle texture as a binary shape mask, with cutoff 0.5. Surviving pixels are fully opaque: no additive blending, soft-particle fading, or material/vertex-alpha fading. This intentionally replaces partially covered edges and depth intersections with solid coverage; full-effect screenshots were visually checked, but only the trail-only swap is near pixel-identical. Keep the cutoff at 0.5 to preserve the authored circle boundary.

Two reversible Play Mode comparisons sampled 100 frames per condition after 100 warmup frames. Median wall times were:

| Condition | Run 1 | Run 2 |
| --- | ---: | ---: |
| Original, initial baseline | 55.70 ms | 54.35 ms |
| Matched trail only | 40.14 ms | 47.79 ms |
| Matched trail and opaque heads | 41.80 ms | 44.47 ms |
| Original materials restored | 41.41 ms | 42.30 ms |

The restored original was as fast as or faster than the new materials. The initial-to-candidate drop is therefore not evidence of a shader speedup; Editor/load variation affected these runs. **No reliable performance improvement was demonstrated.** The matched shader and opaque heads fulfill the rendering/opacity goal, while the main trail performance problem remains. Both shaders compiled and rendered successfully. Profiling settings and temporary material substitutions were restored, and the Editor was returned to Edit Mode. No standalone build was run.

### Subsequent standalone benchmark (2026-09-18)

A Windows x86-64 Mono release build containing this scene succeeded and was measured at 3840 × 2160, Very High, 8× MSAA, with NOVA selected and all icons active. The player launch explicitly selected Very High because the project's standalone default is lower. VSync was temporarily disabled for both standalone and a fresh Editor comparison; the CPU profiler was off. Two 30-second full-effect samples bracketed a 20-second trails-disabled sample, with warmups longer than the flare particle lifetime. The same temporary frame-interval probe and process sampling were used, and screenshots were visually checked.

| Environment | Full effect average FPS | Mean frame time | Only flare trails disabled |
| --- | ---: | ---: | ---: |
| Fresh Editor Play Mode | 27.19 | 36.77 ms | 91.94 FPS |
| Standalone, usual apps open | 44.02 | 22.72 ms | 129.59 FPS |
| Standalone, Unity and GitHub Desktop closed | 55.66 | 17.96 ms | 152.98 FPS |

The quiet player used about half the time per frame of the fresh Editor. Closing the approved apps coincided with another 26% FPS improvement over the normal player pass, but Chrome also became quieter, so this is not an isolated measurement of Unity/GitHub Desktop overhead. Normal-run background CPU samples attributed 16.5% of whole-CPU capacity to Unity, 7.5% to Chrome, and 2.1% to Windows Defender. Before building, Unity averaged 20.7% and Defender 7.4%; these workloads vary. The full-effect quiet samples were consistent at 55.86 and 55.47 FPS, with roughly 25 ms p95 frame time, rather than a locked 60 FPS.

The trails still cost substantial time outside the Editor: the quiet player's mean frame time dropped from about 18 ms to 6.54 ms when only trails were disabled. Particle counts, motion, noise, lengths, geometry, and materials were preserved in every full-effect sample. These measurements supersede any assumption that the earlier ~24 FPS represented standalone performance.

The temporary probe was removed from Assets afterward, both closed apps were reopened, and Play Mode/frame-limit settings were restored. The build reported zero errors and 117 existing-code/asset warnings; the player logged a convex-mesh polygon-limit warning. This was a level-select benchmark, not a full-game release validation. Detailed reports and raw captures are in `C:/Users/mattf/.codex/visualizations/2026/09/18/01a0b602-901f-7342-b920-ffba5389c071/benchmark-report.md`.
