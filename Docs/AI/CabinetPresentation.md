# Cabinet presentation settings

The Windows cabinet uses Direct3D 11, borderless fullscreen, and VSync at every refresh (`vSyncCount = 1`). All six quality presets enable VSync. On the current 3840 x 2160 / 60 Hz display this targets 60 FPS; changing the display refresh rate also changes the VSync ceiling.

`ProjectSettings/ProjectSettings.asset` sets `useFlipModelSwapchain: 0`, selecting BitBlt for Windows Direct3D 11 players. Keep the following arguments in cabinet launchers to make the intended graphics API and presentation path explicit:

```text
-screen-width 3840 -screen-height 2160 -screen-fullscreen 1 -window-mode borderless -screen-quality "Very High" -force-d3d11 -force-d3d11-bitblt-model
```

This is a cabinet-specific workaround, based on September 27, 2026 measurements on Unity 6000.0.28f1, RTX 2060 / driver 591.86. On the unchanged Begin prompt at 4K and 8x MSAA, borderless Flip/VSync averaged 29.75 FPS; borderless BitBlt/VSync averaged 53.04 FPS. These are player-frame throughput measurements, not scanout or input-latency measurements. A steady 60 FPS still needs additional rendering headroom. Re-test presentation after a Unity or GPU-driver update.

The defaults apply across scenes. No runtime VSync manager is required: the active game code has no VSync override. A VSync-disabling FPS test exists only in an unused vendor demo prefab.

## Current local cabinet build

The current standalone build is `Builds/Cabinet-20260927-LipFlow/MASSIVE.exe`, launched with its adjacent `Launch-Cabinet.cmd`. It is a regular Windows x64 Mono build, with the cached Attract grid, trimmed title-sphere rear and conservative lettering-region shortcut, with 4x MSAA, the full 192-resolution title body and stronger outer-letter lip interaction, and without temporary profiling components or frame-timing instrumentation. The September 26, September 27 BitBlt, cached-grid, rear-shell and lettering-region `Launch-Cabinet.cmd` entry points delegate to this launcher. Build folders and launchers are local ignored artifacts; source and project settings are the durable changes for future builds. Older capture launchers still target their original builds.

Previous build validation: the clean lettering-region build succeeded with 0 errors and 99 warnings in 50.17 seconds. All 93 lettering-region checks passed, including 45 original/optimized render pairs with zero changed pixels. All 36 rear-shell checks passed, including 13 identical full/trimmed image pairs. The earlier cached-grid change passed its 24 checks. VSync=1 remains enabled in all six quality levels; BitBlt remains the project default. Temporary build-helper assets were removed afterward.

After the cabinet driver update to NVIDIA 617.14, the prior live-grid build measured 53.11 FPS at the unchanged Begin prompt (4K, 8x MSAA, BitBlt, VSync=1), versus 53.04 FPS on 591.86. The initial firewall-obstructed captures remain excluded. A subsequent clean paired test in one non-development executable measured 52.31 FPS with the original live grid and 55.65 FPS with the cached grid (+6.38%). Both used 4K, 8x MSAA, BitBlt and VSync=1, with Unity closed; the analyzed window was seconds 30-120 of each 150-second run at Begin. Average GPU time fell from 18.914 ms to 17.747 ms, and 99th-percentile frame time fell from 21.318 ms to 19.994 ms. GPU utilization remained about 99%; the 60 FPS target is not yet met. The animated cached grid baked its mesh and attraction field only once. This measures Begin-screen throughput, not gameplay-wide performance or display latency.

The rear-shell trim reduces title base geometry from 196,608 to 157,706 triangles (19.8%). It did not measurably improve throughput: a same-executable full/trimmed/warm-full comparison averaged 56.26 / 56.04 / 56.09 FPS at matching settings, with mean GPU times 17.487 / 17.552 / 17.539 ms. These captures used the same 30-120 second analysis window and current driver. Keep the geometry saving, but prioritize visible shader cost for the remaining rendering headroom. That earlier rear-shell build has GUID `dfaa6d22ea734634b2067fe5f78e5fe8`.

The lettering-region shortcut skips inverse trig and distance-texture reads outside a conservative boundary. At the same Begin-screen settings, original / optimized / original-repeat runs measured 56.44 / 57.40 / 56.05 FPS, with average GPU times 17.438 / 17.156 / 17.572 ms. This is approximately a 2% throughput gain with no changed pixels in the tested renders; steady 60 FPS remains unmet. That lettering-region build has no measurement probe and uses GUID `78d4458acbed4e5aaf53a5c18bd7ae13`.
## 4x MSAA and restored title detail - September 27, 2026

Select **Black hole > Attract Ferrofluid Study > Game MSAA** in ATTRACT to switch between 4x and 8x. This edits the active Quality preset for the entire game and future builds; it saves even in Play Mode. The cabinet's Very High preset now uses 4x. Other quality presets, VSync=1, 4K resolution and BitBlt are unchanged. The dropdown supports Undo/Redo; the active preset is shown next to it.

Earlier commit 1548a64e capped the body mesh at resolution 128 with Optimize Body Geometry, despite Mesh Resolution being 192. Only lettering retained extra tessellation. Vertex normals are interpolated across triangles, so hard reflection contours expose small angular segments. Frozen native 4K renders showed smoother contours at 192; reinstating the older finite-difference normal calculation at 128 retained the angular contours. This symptom already occurred at 8x MSAA. Hidden-rear trimming and the lettering-region shortcut retain their earlier verified behavior.

ATTRACT now disables **Optimize Body Geometry**, restoring the requested body resolution of 192, with **Trim Hidden Rear** retained. The Inspector shows **Body Resolution on Enable** and explains the cap. The title has 179,283 vertices / 354,346 base triangles after rear trimming, versus 80,267 / 157,706 at body resolution 128. More geometry reduces interpolation faceting; it does not make the finite mesh mathematically continuous.

Same-executable, non-development standalone Begin-screen captures at 4K / VSync=1 / BitBlt averaged 57.03 FPS at 8x/body128, 60.00 at 4x/body128, and 60.00 at 4x/body192. Their 99th-percentile frame times were 19.470 / 17.073 / 17.107 ms. Each analyzed seconds 30-120 of a 150-second run, Unity closed, focused throughout and HasBegun=false. An earlier body192 capture entered the mode menu and was excluded, then repeated. These results cover Begin, not every gameplay scene or mode-menu configuration.

That clean MSAA4 build succeeded with 0 errors and 99 warnings in 49.53 seconds, GUID c831b46880ba4c8b8d1eb61b47067bcb. Its standalone Begin rendering was inspected and its player log had no error/exception matches during smoke testing. The MSAA selector passed switch, Undo, Redo and serialization checks. Shader source and fluid-field code were not changed. Temporary diagnostics were archived outside the project and removed. Detailed captures and measurements are in the Codex attract-msaa-faceting-20260927 audit folder.


## Stronger outer letter lip - September 27, 2026

**Black hole > Attract Ferrofluid Study > Outer Lip Flow** adds stronger surface-to-recess coupling. The saved ATTRACT setting is 1; 0 restores the previous lip appearance. Ridge Undulation remains its motion control, and Type Undulation independently controls the recessed white faces. Save lasting preferences in Edit Mode; Play Mode edits are temporary.

The lip reuses existing mound height and slope, enlarges their bounded effect on the outer recess, and carries the height shift into the opaque black collar. It fades toward the inner wall and blends in near the end of the existing arrival animation. A slightly wider tessellation band resolves the moving lip; base Mesh Resolution remains 192, with 179,283 vertices / 354,346 base triangles after rear trimming. MSAA remains 4x. There is no additional fluid/noise evaluation per fragment, simulation, texture rebake, or CPU mesh rebuild for this motion.

Validation: 8 native 4K comparisons with Outer Lip Flow 0 were pixel-identical to the pre-change shader. Stronger values were visually checked at four frozen times and during arrival. With Type Undulation 0 at the captured time, all seven white letter faces and their three-pixel surrounding edges were unchanged. The existing 93 lettering-region and 36 rear-shell checks passed with the new effect enabled, including extreme layouts, joystick offsets and surface settings. These are representative checks, not a guarantee for every possible authoring combination.

A same-executable standalone comparison at 4K / 4x MSAA / VSync=1 / BitBlt, with Unity closed, averaged 60.00 FPS for both the original and stronger shaders. Mean GPU times were 15.956 and 15.910 ms; P99 frame times were 17.068 and 17.113 ms. The difference is too small to claim a performance improvement; no measurable slowdown occurred in this run. Each analysis used seconds 30-120 of a 150-second focused Begin-screen run. Other gameflow scenes were not reprofiled.

The clean LipFlow build succeeded with 0 errors and 99 warnings in 51.36 seconds, GUID 1c478625d4474ea2b0cf40457e857cfc. Current local executable: Builds/Cabinet-20260927-LipFlow/MASSIVE.exe, with the normal launcher chain updated. Temporary runtime probes and reference assets were removed before this build; FrameTimingStats remains off. Detailed renders, original shader, timing CSVs and test logs are archived in the Codex attract-lip-flow-20260927 folder.

## References

- [Unity swapchain setting](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings-useFlipModelSwapchain.html)
- [Unity standalone command-line arguments](https://docs.unity3d.com/6000.0/Documentation/Manual/PlayerCommandLineArguments.html)