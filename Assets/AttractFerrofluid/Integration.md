# ATTRACT sphere controls

## Mode selector

The menu starts with only **PRESS ANY BUTTON TO BEGIN** below MASSIVE. A fresh controller, keyboard or mouse button press reveals the mode labels and all four player slots, with **1 vs 1** selected. P1 and P3 fill from their globs; P2 and P4 remain small. Joystick movement alone does not dismiss the prompt. The opening press is consumed: all held buttons must be released before navigation or confirmation is enabled. Returning to ATTRACT, or switching the menu back on, starts at the prompt again. The prompt has independent controls under Begin prompt on Ferrofluid Mode Select: Prompt Size, Prompt Horizontal Position and Prompt Vertical Position. Position is normalized to the screen (0–1); horizontal 0.5 centers it. Current size and position are preserved when installing these controls.

In ATTRACT, **Ferrofluid Mode Select → Attract Mode Selector → Show Mode Select** enables or hides the horizontal menu. The legacy vertical menu, pseudo-player roots and particle title have been removed from this scene. The sphere and embedded-lettering controls remain independent. Set defaults before Play; controls also update live.

The horizontal row contains **MASSIVE SCORE ATTACK**, **1 vs 1**, and **2 vs 2**. Left/right navigation uses the existing Rewired EventSystem and wraps through the three options. Only the selected mode uses the bold font face; other labels use InputMono Regular. There are no text backgrounds or underlines. The centered preview group shows one, two, or four small ferrofluid spheres above the labels, below the title. Team one uses white mounds with black valleys; team two uses the large sphere's black body, white reflections and rear rim. Each sample remains opaque pure black or white, with the existing MSAA providing edge coverage.

**Score attack is a menu preview only**, as requested. Selecting or submitting it does not change GameFlowContext, clear the selected level, or load a scene. The two versus choices call the existing ChooseModeScript.MoveToNextScene handler directly: false for 1v1, true for 2v2. This sets GameFlowContext, clears the level selection and opens HOW-TO-PLAY. No new GameMode values, gameplay rules or player rosters were added.

The assigned BeginPromptSDF prefab contains the existing TMP Text Transition component. Open Prompt Animation Prefab to edit its intro/outro timing, easing and SDF Grow settings. Defaults are a 0.35-second intro and 0.25-second outro, growing from no visible stroke to the original full lettering and back. Only SDF Grow is enabled; there is no alpha fade or transform-scale animation. Timing uses unscaled time. Auto Play In On Enable is enabled on the prompt prefab. Its Maximum Animation Step is 1/30 second, so a long loading/render frame cannot skip the short intro or outro; ordinary frames retain their normal timing. The menu does not restart an already-running auto-play animation. The new step limit defaults to zero on existing TMP Text Transition components, preserving their elapsed-time behavior. The first press plays the outro before revealing the modes; a press during the intro is remembered and plays the outro as soon as the intro finishes. Held buttons remain guarded until release. Re-entering ATTRACT or re-enabling the menu starts a fresh intro. Layout controls update live in Play Mode; save defaults in Edit Mode. The animation material belongs to the generated prompt instance and does not alter the shared font material.

Layout controls adjust the label row height, sphere row height, sphere diameter, option spacing and label size. These and the following controls update live:

| Control | Behavior |
| --- | --- |
| Sphere Spacing | Clear space between neighboring spheres, in sphere diameters. Default 0.16. |
| Team Spacing | Additional gap separating the white and black teams. Default 0.30. |
| White Sphere Texture Scale | Independent mound-pattern size for white player spheres. 1 retains the original detail; 2 makes broader mounds; 0.5 makes finer detail. |
| Black Sphere Texture Scale | The same independent control for black player spheres. Neither control changes the title sphere. |
| Coalescence Duration | Seconds for liquid to gather or separate. Default 1.1 seconds. |

Eight small liquid droplets emerge at staggered times, curl inward and merge through rounded liquid junctions into the final mound field. Despawn reverses this process. An interrupted transition resumes from its current formation state. The object scale stays fixed throughout; the implicit liquid surface changes shape. Team members keep their color when partners join or leave. The formation shader and settled shell share the same surface-field calculations, and only the brief formation phase uses ray marching. Frame stalls are capped at a 0.05-second animation step so startup shader work cannot skip the visible formation.

Player density, relief, highlight coverage, pattern scale and motion speed have independent controls. Rim width and angle retain the title sphere's styling. The player pattern follows a predetermined 20-second loop (at Player Motion Speed 1), with a different phase per slot, and does not respond to joystick movement. Player Density is divided by the corresponding texture-scale control. The previews share a 32-subdivision cube-sphere mesh, one player surface material and one formation material. They add no colliders, PlayerControllerScript components or scoring behavior. All generated resources are released when the menu is disabled; prior EventSystem selection is restored if it still exists.

Both the full player spheres and permanent idle globs use the dedicated player surface shader. It has no lettering, attraction or tessellation work. Ray marching is used only between these endpoints, on a 12-triangle cube whose conservative bounds track the reservoir and attached drops. Its surface field matches the endpoint mesh, so fills, drains and reversals retain the same liquid behavior. Player Mesh Resolution defaults to 32 (12,288 triangles, down from 49,152) and applies on enable; Player Motion Speed updates live. Existing density, relief, coverage and separate white/black texture-scale controls remain available.

## Rendering optimizations

The title evaluates the fluid field and its analytic derivatives together on the broad body. The transported letter shoulder, wall and floor retain their existing normal calculation. Surface heights, joystick mapping, pure white/black shading, 8x MSAA and per-sample evaluation of internal edges are retained.

Optimize Body Geometry is enabled in ATTRACT. It caps the broad body mesh at resolution 128 while retaining the selected Mesh Resolution as the lettering detail target and clearance reference. At the scene's resolution 192, this reduces the base mesh from 442,368 to 196,608 triangles and increases local lettering refinement to compensate. Disable Optimize Body Geometry, then re-enable the sphere, to compare with the original base geometry. Geometry controls apply on enable; they do not rebuild meshes every frame.

Local 4K/8x-MSAA Editor comparisons on an RTX 4090 measured approximately 6.7 to 5.6 ms GPU time for the title view, 7.7 to 5.5 ms for 1v1, and 9.8 to 6.2 ms for four spheres halfway through formation. These are whole-frame results from three paired runs per state, with 120 samples per run, not standalone target-hardware guarantees. Validation also covered shader compilation, numerical surface derivatives, conservative bounds, native-resolution lettering and arrival captures, endpoint swaps, rapid mode reversals, joystick independence, loop wrapping and legacy toggles. Target-machine standalone profiling remains to be done.

The controller is on the Ferrofluid Mode Select root. Its camera, bold/regular fonts, shaders, sphere style and mode-flow handler are assigned. The Screen Space Camera canvas scales with screen height. The regular face is a static SDF asset baked from the project's existing InputMono-Regular.ttf. There is no generated Edit Mode preview.

## Sphere and lettering

**Lettering arrival:** on entering ATTRACT, the embedded title starts covered by the continuous ferrofluid surface. Liquid withdraws from the interiors of the letter strokes, leaving a rounded, moving edge that settles into the existing recess. There is no text fade or opacity animation. In the Black hole object's Attract Ferrofluid Inspector, Animate Lettering Arrival switches this on or off; Arrival Delay defaults to 0.25 seconds and Reveal Duration to 3 seconds. Replay Lettering Arrival previews it again in Play Mode. The arrival runs independently of Motion Speed and gameplay time scale. Re-entering the scene or re-enabling the embedded sphere starts it again. The begin prompt and mode controls remain usable during the arrival.

During the opening, a narrow liquid lip tracks the emerging white letter face. The wider resting ridge develops over the final 35% of the reveal, avoiding an early letter-shaped depression before the type is visible. Arrival timing and the finished lettering remain unchanged.

The initial opening briefly holds its width while sinking to the full letter-floor depth. Only then can the white face appear, and the opening resumes expanding outward. White coverage also checks the actual tessellated surface at each antialiasing sample, keeping partially lowered surface triangles black.

In `Assets/Scenes/S-0_ATTRACT.unity`, select **Black hole → Attract Ferrofluid Study**.

| Inspector control | Behavior |
| --- | --- |
| Use Ferrofluid Sphere | On: dense moving black fluid sphere. Off: original sphere. |
| Embed MASSIVE Lettering | On: pure-white lettering carved into the active ferrofluid surface. Off: hide the embedded title. |
| Lettering Scale | Uniform size on the sphere; 1 is the approved layout, 0.5 is half size, with a range of 0.25–1.3. Both dimensions and the black letter border scale together. |
| Embed Depth | Recess below the reference surface, from 0 to 0.03 sphere-local units. |
| Vertical Position | Moves the lettering vertically around the sphere. |
| Ridge Undulation | How strongly the mounds move the outer junction of the ridge and liquid. Default 1. |
| Type Undulation | White-face response to the mound heights, from 0 (steady, the default) to 1 (full surface motion). Independent of Ridge Undulation. Values such as 0.35 provide a lighter response. |

The main scene retains your saved sphere and lettering values. The component's code defaults are scale 1 and depth 0.016. Switching the sphere off retains the lettering preferences for the next activation. Turning the component off restores the original sphere renderer.

Set controls before Play to save scene defaults. All four primary controls also update live during Play Mode; Unity discards those Play Mode adjustments when you stop. This effect does not generate an Edit Mode preview.

The collapsible **Surface and lighting** section contains density, relief, motion, highlight coverage, rear rim, and mesh resolution. Resolution is applied on the next activation. **Joystick response** retains shared-player averaging, the 0.18 drift threshold, and 0.7-second smoothing. **Asset references and advanced layout** contains the source shape, isolated letter fields, field composer, surface shader, optional camera, and base angular width.

The original sphere mesh/material, surrounding sphere particles and colliders remain in place. The old particle lettering has been removed. The alternate adds a transient mesh/material/renderer and a cached letter field, restoring the original sphere renderer and releasing generated resources when switched off. Disabling embedded lettering leaves a continuous liquid surface. Runtime code uses the project's existing Rewired movement actions.

The imported runtime files retain their study GUIDs so the validated assets transfer without rebinding. Study scene creation, recording utilities, and resonance assets are not required in the main project. The 2048 x 423 signed-distance texture uses the original title outlines, with the curved S sections reconstructed as tangent-continuous cubic curves. Their four terminal corners and straight end faces remain crisp. Straight letters retain their original outlines. The original mesh assets remain available, but the floating title objects are no longer in ATTRACT. Distance is packed at 16-bit precision into RG with a blue encoding marker. Its data is linear, bilinear-filtered and uncompressed. These channels control shape and depth; they are never displayed as colors.

The shader evaluates lettering, recess borders and reflection edges at each MSAA sample. The ATTRACT camera already uses 8× MSAA, so internal edges now receive the same smoothing as the sphere silhouette. Each sample is still opaque pure black or pure white; intermediate screen pixels come only from resolving edge coverage. There are no gray lighting gradients or transparent letter faces. Disable camera MSAA as well as render-target MSAA when inspecting the unfiltered binary palette. Sample-frequency shading requires Shader Model 5 / Direct3D 11-class hardware; the current Windows target is validated.

At Type Undulation 0, the white lettering uses fixed spherical coordinates and a stable recessed radius. Increasing the slider blends that radius toward the actual mound heights while retaining the recess depth. The outer shoulder keeps its existing, independent tangential motion; its shape blends into the inner wall and letter floor. At zero response, a geometry margin and wall clearance keep neighboring mounds from tugging at or covering the white face in the tested front view. Negative distance continues beyond the texture padding to keep the outer M/E junctions continuous.

The shader adds local GPU tessellation around the lettering, up to four subdivisions per base edge, to resolve the curved walls and their highlights. Geometry density elsewhere is unchanged. This uses the same Shader Model 5 / Direct3D 11 hardware requirement as sample-frequency antialiasing. Standalone-player performance has not been benchmarked.

The current scene copy passed 21 input checks and 75 runtime/capture checks, plus the new slider's serialized default and Inspector Undo checks. At 2560 x 960, the white-face mask changed in zero pixels with Type Undulation 0, 45,500 pixels at 0.35, and 104,632 pixels at 1 across the tested time interval. The outer junction still changes with the mound field, while pixels outside the lettering band remain unchanged when adjusting Ridge Undulation. The saved main scene, camera, quality settings, scale and embed depth are preserved.

## Gameplay vector grid in ATTRACT — 2026-09-15

The Vector Grid root retains its original Animator, Rotator, transform and animation clips. Its Gameplay Vector Grid child uses the current VectorGridGPU, material and compute shader from DYNAMO PROTOTYPE's PLAYING FIELD reference: 1-unit squares, 8 simulation subdivisions, 8 render subdivisions, smooth curves with tension 0.1 and overshoot protection, and the scene's line alpha 0.4745098. No gameplay assets are modified.

The child starts with a 40 x 40 simulation area and 2x render overscan. AttractVectorGrid extends that area in 8-unit increments if the camera requires more, keeping square spacing constant and the animated edges outside the view. The arena border overlay is off. The original 40x parent scale is compensated by the child's 0.025 scale, so the original rotation, tilt and vertical motion remain unchanged. The old CPU grid, its renderer and the old Black hole force are disabled; their settings remain serialized for recovery.

AttractVectorGrid connects Black hole to the GPU grid, with Attraction Radius 20 and Attraction Strength 16. These are world-space controls. Positive strength attracts. The existing sphere visuals, mode selector and their toggles are independent of this backdrop.

Validation: isolated native Unity capture passed 12 checks with zero runtime errors. Twenty animation cycles covered all viewport corners at 4:3, 16:9, 21:9 and 32:9; widest case grew the simulation area to 48. Maximum measured displacement was 0.723 units. Main ATTRACT compiled and rendered successfully with one active GPU grid and no Console errors, then returned to a saved, clean Edit Mode. Standalone performance was not benchmarked.


# Permanent player globs and lettering controls

In ATTRACT, the Black hole object's Attract Ferrofluid component exposes Letter Spacing alongside Lettering Scale. Zero retains the approved lettering; negative values tighten the gaps and positive values open them. It translates the seven embedded letter outlines and their recesses, without stretching them or changing their height.

Ferrofluid Mode Select now reserves four permanent positions. Left to right they are P2, P1, P3, P4. P1/P2 are white dominant; P3/P4 are black dominant. SCORE ATTACK fills P1; 1 vs 1 fills P1 and P3; 2 vs 2 fills all four. Unselected players remain as small living globs. The existing spacing controls adjust the slot layout consistently across modes.

Fill / Drain Duration controls how long a glob takes to fill or empty. Idle Glob Size controls the diameter of the remaining glob relative to the full sphere. Growth emits rounded, attached lobes from inside the glob and folds them into the growing body. Despawn reverses the same path; interrupted transitions retain their phase. Every slot's transform scale is constant throughout formation.

Player Density, Player Relief, and Player Highlight Coverage affect only the player spheres and their globs. Existing White Sphere Texture Scale and Black Sphere Texture Scale remain independent multipliers on pattern size. Higher density makes more fine mounds; larger texture scale makes broader mounds. Highlight coverage changes the area of the white shapes, maintaining binary black/white shading. The title retains its own surface settings.

The horizontal menu uses the existing versus game handler directly. SCORE ATTACK remains a menu preview. Controls update in Play Mode; save scene defaults in Edit Mode.

## Independent letter recesses — 2026-09-27

Letter spacing previously moved slices of the whole-word signed-distance field. Because that field also describes the nearest neighboring outline, slices brought unrelated recess halos into the gaps. Each letter now has an independent R16 field derived from the approved subpixel contours. Their continuous union follows the actual spaced outlines, including around the M/A, A/S, S/I and V gaps.

MassiveLogoDistance.png remains the authoring source. **MASSIVE → Attract Ferrofluid → Rebuild isolated letter fields** regenerates MassiveLogoGlyphs.asset without requiring the deleted scene lettering. The seven 768-square slices cover 1024 source pixels each. Runtime composition creates a padded 3072 × 1447 half-float field only on initialization, a spacing/source change, or render-texture recovery. Normal animation samples that single field; arrival, scale, depth, ridge flow and type flow do not rebake it. The field contains shape data, not displayed gray colors. Existing 8× MSAA and opaque white/black shading are unchanged.

## Cached Attract grid — September 27, 2026

In **S-0_ATTRACT**, select **Vector Grid > Gameplay Vector Grid**, then edit **Attract Cached Grid**. The original Rotator, Animator, animation clips and transforms remain active and unchanged.

- **Line Width** is now a real pixel-width control. The previous gameplay line shader did not use its width value.
- **Grid Size** controls total coverage, including the area revealed by tilt. The 80 x 80 default replaces the old 40 x 40 layout with 2x render overscan.
- **Grid Spacing** controls the size of the squares. Layout changes automatically rebuild the line mesh.
- **Base Color** and **Attraction Color** independently control the line colors, including alpha and HDR intensity. They update without a rebake. **Color Displacement** sets how much pull reaches the full attraction color.
- **Attraction Radius/Strength**, **Spring Strength**, and the falloff settings rebuild a small radial lookup. **Rebake Grid** is also available in the Inspector and component context menu. Save lasting settings in Edit Mode; Play Mode edits remain temporary.

The shape is the settled radial force field, cached once rather than simulated every frame. Because the grid still rotates, tilts and moves vertically, a cheap vertex shader samples that fixed world-space field as the grid passes through it. The attraction therefore stays centered on the sphere. Mesh topology and the radial lookup do not rebake for animation. The former spring wobble/settling delay is intentionally absent.

The renderer uses one pass and has no simulation buffers, compute dispatches, gameplay border pass, or per-vertex gameplay curve reconstruction. The default mesh has 207,684 vertices versus 850,086 in the former overscanned grid. Generated mesh, texture and material resources are transient, recreated on enable, and released on disable. A 500,000-vertex budget limits extreme authoring combinations; the Inspector reports when requested curve sampling is reduced.

The old **Vector Grid GPU** and **Attract Vector Grid** components are disabled only on this Attract object, with their settings retained. Gameplay scenes and shared grid shaders/scripts are unchanged. To manually restore the old renderer, disable Attract Cached Grid and enable those two components.

Validation is available in Edit Mode at **MASSIVE > Attract > Validate Cached Grid**. It checks the frozen field against the existing GPU simulation at three heights, rendered line width, editable colors/layout, cache invalidation, animation without rebaking, resource cleanup, and bounded authoring settings.

## Hidden rear shell — September 27, 2026

**Black hole > Attract Ferrofluid Study > Surface and lighting > Trim Hidden Rear** is enabled in ATTRACT. The full original source sphere and player-preview spheres remain available. This setting applies on the next activation; disabling it restores the full generated title mesh.

The title retains its visible surface plus a conservative rear skirt, based on the minimum and maximum radius of the current fluid/recess settings. Only triangles entirely behind that cutoff are removed. Vertex positions, normals, winding and lettering refinement stay unchanged. At the current settings, base triangles fall from 196,608 to 157,706 (19.8%), and vertices from 99,846 to 80,267. Cutting exactly at the hemisphere would risk changing the moving profile.

Flow and joystick movement do not regenerate the mesh. Increasing relief or recess expands coverage when necessary. Changing the relative viewing direction restores the full shell once; a perspective camera starts with a full shell. This optimization targets the fixed orthographic title view; it does not provide a complete rear surface for additional viewpoints. If the radial shader's displacement formula or exposed parameter ranges change, update its conservative bounds in SafeRearCutoff as well.

Open ATTRACT in Edit Mode and use **MASSIVE > Attract > Validate Rear Shell**. All 36 checks passed, including 13 full/trimmed image pairs covering arrival, flow, four maximum joystick offsets, relief/recess extremes and a native 4K render. Every pair had zero changed pixels. Camera fallback, lettering density, cleanup, player-preview geometry and no per-frame rebuilding also passed.

Standalone measurements at Begin (4K, 8x MSAA, VSync=1, BitBlt, driver 617.14) were effectively tied: full sphere 56.26 FPS, trimmed 56.04 FPS, warm full-sphere repeat 56.09 FPS. Each capture used seconds 30–120 of a 150-second run in the same non-development executable. This verifies a geometry reduction, not a measurable FPS gain; the scene still needs rendering headroom for steady 60 FPS. The next candidate is early rejection of lettering calculations outside the affected region. Further geometry/AA changes need separate visual and timing validation.

## Lettering region shortcut — September 27, 2026

The title shader now skips inverse trigonometry and distance-texture reads at points too far from the lettering to affect its recess, normals, tessellation or shading. This is automatic: existing scale, spacing and vertical-position controls update a cached conservative region. The region includes 0.25 distance units beyond the letter extents, plus filtering padding, exceeding the largest current influence band. Each moving shoulder sample is checked at its own position, so ridge flow is preserved. Arrival can only shrink the original outlines. The visible calculations, 8x MSAA and all authoring controls retain their behavior.

**MASSIVE > Attract > Validate Lettering Region** compares unrestricted and bounded rendering in an isolated preview scene. During implementation, the original pre-change shader was retained as a separate reference: all 93 checks passed, including 45 before/after image pairs with zero changed pixels. Cases cover arrival, flow, maximum joystick offsets, scale/spacing/elevation extremes, maximum relief and recess, minimum mesh resolution, disabled lettering, and two native 4K renders.

The bounds depend on FerrofluidLogoFieldBaker keeping contours inside GlyphRects and the source image height, and on the current shader influence ranges. Review ConservativeRegion and rerun the image comparisons when changing those contracts. No source distance texture rebake or scene migration is required.

Standalone Begin-screen testing at 4K/8x MSAA/VSync=1/BitBlt measured 56.44 FPS original, 57.40 FPS bounded, and 56.05 FPS original warm repeat. Average GPU time was 17.438 / 17.156 / 17.572 ms. The shortcut saves approximately 0.28-0.42 ms of GPU time (1.7-2.4% throughput improvement) with the tested appearance preserved. Steady 60 FPS remains unmet.

## Repeated lettering surface evaluations experiment - September 27, 2026

Two alternatives to the four neighboring surface evaluations were tested and rejected. Sharing mound-placement work across neighboring samples first triggered a shader-compiler failure; a smaller version compiled but changed three pixels during arrival. Skipping normal probes inside fully revealed white letter faces passed 117 checks, including 57 image comparisons with zero changed pixels, but slowed the standalone Begin screen.

At 3840 x 2160, 8x MSAA, VSync=1 and BitBlt, the existing shader measured 57.38 FPS, the interior-probe shortcut 55.26 FPS, and a warmed repeat of the existing shader 57.25 FPS. Average GPU times were 17.152 / 17.816 / 17.194 ms. All runs used the same non-development comparison executable with Unity closed, analyzing seconds 30-120 of each 150-second capture. The exact GPU cause of the regression was not isolated.

The shader and lettering-region validator were restored to their exact pre-experiment versions, and temporary comparison assets were removed. The existing Cabinet-20260927-LetterRegion build and launchers remain current. No runtime optimization from this experiment is retained; previous cached-grid, rear-shell and lettering-region work is preserved. Measurements, experimental sources and image comparisons are archived in the Codex attract-letter-normals-20260927 diagnostic folder.


## MSAA control and full body detail - September 27, 2026

**Black hole > Attract Ferrofluid Study > Game MSAA** switches the current project Quality preset between 4x and 8x; its choice is saved even in Play Mode and applies across the game and future builds. Very High now uses 4x. VSync and BitBlt are unchanged. The selector supports Undo/Redo and shows the active quality preset.

The earlier Optimize Body Geometry setting capped the broad body at 128 despite the authored Mesh Resolution of 192. Hard reflection contours revealed the coarser interpolation of vertex normals. Same-time 4K renders at both sample counts reproduced the angular contour; restoring full 192 body detail smoothed it. Substituting the older four-sample normal method at 128 retained the contour corners. MSAA resolves pixel coverage and cannot restore the missing curvature samples.

The saved Attract title now has Optimize Body Geometry disabled, Mesh Resolution 192, and Trim Hidden Rear enabled. Body Resolution on Enable in the Inspector makes the effective density explicit. Shader calculations, flow, relief and lettering parameters are unchanged. The full-detail rear-trimmed title contains 179,283 vertices and 354,346 base triangles.

At the standalone Begin screen, 8x/body128 averaged 57.03 FPS; 4x/body128 and 4x/body192 both averaged 60.00 FPS. Their 99th-percentile frame times were 19.470, 17.073 and 17.107 ms. Measurements used the same non-development executable at 3840x2160, VSync=1 and BitBlt, with Unity closed and matching Begin-screen state. These results do not establish 60 FPS across all scenes. See Docs/AI/CabinetPresentation.md for the clean build and detailed validation.


## Stronger outer letter lip - September 27, 2026

**Black hole > Attract Ferrofluid Study > Outer Lip Flow** adds stronger surface-to-recess coupling. The saved ATTRACT setting is 1; 0 restores the previous lip appearance. Ridge Undulation remains its motion control, and Type Undulation independently controls the recessed white faces. Save lasting preferences in Edit Mode; Play Mode edits are temporary.

The lip reuses existing mound height and slope, enlarges their bounded effect on the outer recess, and carries the height shift into the opaque black collar. It fades toward the inner wall and blends in near the end of the existing arrival animation. A slightly wider tessellation band resolves the moving lip; base Mesh Resolution remains 192, with 179,283 vertices / 354,346 base triangles after rear trimming. MSAA remains 4x. There is no additional fluid/noise evaluation per fragment, simulation, texture rebake, or CPU mesh rebuild for this motion.

Validation: 8 native 4K comparisons with Outer Lip Flow 0 were pixel-identical to the pre-change shader. Stronger values were visually checked at four frozen times and during arrival. With Type Undulation 0 at the captured time, all seven white letter faces and their three-pixel surrounding edges were unchanged. The existing 93 lettering-region and 36 rear-shell checks passed with the new effect enabled, including extreme layouts, joystick offsets and surface settings. These are representative checks, not a guarantee for every possible authoring combination.

A same-executable standalone comparison at 4K / 4x MSAA / VSync=1 / BitBlt, with Unity closed, averaged 60.00 FPS for both the original and stronger shaders. Mean GPU times were 15.956 and 15.910 ms; P99 frame times were 17.068 and 17.113 ms. The difference is too small to claim a performance improvement; no measurable slowdown occurred in this run. Each analysis used seconds 30-120 of a 150-second focused Begin-screen run. Other gameflow scenes were not reprofiled.

The clean LipFlow build succeeded with 0 errors and 99 warnings in 51.36 seconds, GUID 1c478625d4474ea2b0cf40457e857cfc. Current local executable: Builds/Cabinet-20260927-LipFlow/MASSIVE.exe, with the normal launcher chain updated. Temporary runtime probes and reference assets were removed before this build; FrameTimingStats remains off. Detailed renders, original shader, timing CSVs and test logs are archived in the Codex attract-lip-flow-20260927 folder.
