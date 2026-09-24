# PULSAR live particle icon

`LevelDefinition-PULSAR.asset` now uses `LS_PULSAR-LiveIcon.prefab`. This display prefab creates a stationary instance of **`LS_PULSAR-Icon.prefab`** and renders it into a live texture. Keep editing the particle settings, trails and Rotator on that original source prefab. Its settings are unchanged by this implementation.

The carousel moves the display. The source camera follows the inverse carousel transform, so the icon retains its changing perspective while old world-space trail vertices stay with the effect. Only the source's own motion contributes to the corkscrew trails. The capture camera renders before the main camera each frame; the image is neither prerecorded nor refreshed at a reduced frame rate.

## Inspector controls

Open `LS_PULSAR-LiveIcon.prefab` and select its root:

- **Source Prefab:** the original particle effect to edit.
- **Texture Size:** maximum resolution, currently 3840 × 2160. The texture uses the game camera's resolution and aspect up to this limit when enabled. Lower values save memory but soften fine trails. Re-enable the component or restart Play Mode after changing capture settings.
- **Anti Aliasing:** currently 2 samples. Use 1, 2, 4 or 8; 0 follows the project quality setting. More samples use more GPU memory.
- **Capture Size:** a conservative world-space area used only to skip captures completely outside the view, currently 6 × 6 at the source prefab's scale. It does not crop the rendered image.
- **View Camera:** leave empty to use the main camera.

At 4K/2× MSAA Unity reports approximately 316 MiB for the HDR render texture including its rendering buffers. The image matches the screen's pixel grid to avoid an extra resampling blur. It adds a camera and a display draw; it is a visual isolation technique, not a particle-count optimization.

## Rendering and lifetime

The display adds the source's captured RGB with no additional opacity multiplier or fade. The original PULSAR materials remain additive. This is intended for the current black menu background; it is not a general opaque compositing solution. The capture flattens internal depth onto the icon's display plane, so interleaving with other 3D geometry can differ from direct rendering.

This component targets the Built-in render pipeline, a standard perspective or orthographic camera, and uniformly scaled menu icons. Each enabled icon owns its capture stage, camera, render texture, display mesh and material. Those resources are released on disable/destruction. The source continues simulating when its capture is outside the view, avoiding a frozen effect on return. Source colliders and grid force emitters are disabled in the runtime clone. No project layers or main-camera settings are changed.

To restore direct rendering, set **Icon Prefab** on `LevelDefinition-PULSAR.asset` back to `LS_PULSAR-Icon.prefab`.

## Validation — 2026-09-18

Unity 6000.0.28f1 compiled the component and display shader with no Console errors or warnings. The live 4K scene was visually checked both stationary and during carousel rotation. A complete carousel loop and rapid direction reversal left the capture stage stationary and retained the same render texture. Disable/re-enable checks confirmed destruction of the old stage, texture, material and mesh, followed by exactly one replacement capture camera and display.

A focused, uncapped 3840 × 2160 Editor comparison kept PULSAR selected throughout, with five seconds warm-up and twenty seconds sampling per phase. Live capture / original direct rendering / live capture measured **44.33 / 50.81 / 42.64 FPS** respectively. Median frame times were **21.911 / 19.191 / 22.734 ms**. This is approximately 3 ms additional frame time for the live capture under these conditions. All other level icons remained active. VSync and frame-rate settings were restored afterward. These are Editor measurements; no standalone build was tested for this change.
