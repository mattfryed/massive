# ATTRACT sphere controls

## Mode selector alternate

In ATTRACT, select **Ferrofluid Mode Select → Attract Mode Selector → Use Ferrofluid Mode Select**. On uses the new horizontal menu; off restores the original vertical 1 vs 1 / 2 vs 2 menu and its pseudo-player effects. The toggle is independent of the large sphere and embedded-title toggles. Set it before Play to save a default; it also switches live in Play Mode.

The horizontal row contains **MASSIVE SCORE ATTACK**, **1 vs 1**, and **2 vs 2**. Left/right navigation uses the existing Rewired EventSystem and wraps through the three options. Only the selected mode uses the bold font face; other labels use InputMono Regular. There are no text backgrounds or underlines. The centered preview group shows one, two, or four small ferrofluid spheres above the labels, below the title. Team one uses white mounds with black valleys; team two uses the large sphere's black body, white reflections and rear rim. Each sample remains opaque pure black or white, with the existing MSAA providing edge coverage.

**Score attack is a menu preview only**, as requested. Selecting or submitting it does not change GameFlowContext, clear the selected level, or load a scene. The two versus choices invoke their original serialized button callbacks, preserving the existing match/scene flow. No new GameMode values, gameplay rules or player rosters were added.

Layout controls adjust the label row height, sphere row height, sphere diameter, option spacing and label size. These and the following controls update live:

| Control | Behavior |
| --- | --- |
| Sphere Spacing | Clear space between neighboring spheres, in sphere diameters. Default 0.16. |
| Team Spacing | Additional gap separating the white and black teams. Default 0.30. |
| White Sphere Texture Scale | Independent mound-pattern size for white player spheres. 1 retains the original detail; 2 makes broader mounds; 0.5 makes finer detail. |
| Black Sphere Texture Scale | The same independent control for black player spheres. Neither control changes the title sphere. |
| Coalescence Duration | Seconds for liquid to gather or separate. Default 1.1 seconds. |

Eight small liquid droplets emerge at staggered times, curl inward and merge through rounded liquid junctions into the final mound field. Despawn reverses this process. An interrupted transition resumes from its current formation state. The object scale stays fixed throughout; the implicit liquid surface changes shape. Team members keep their color when partners join or leave. The formation shader and settled shell share the same surface-field calculations, and only the brief formation phase uses ray marching. Frame stalls are capped at a 0.05-second animation step so startup shader work cannot skip the visible formation.

Relief, motion, reflection coverage and rim settings come from the large-sphere component. Player density is independently computed as 9 divided by the corresponding texture-scale control. The previews share one 64-subdivision cube-sphere mesh, one settled material and one formation material. They add no colliders, PlayerControllerScript components or scoring behavior. All generated resources are released when the alternate is disabled; original active states and EventSystem selection are restored.

The original Mode Select and Pseudo players roots stay in the scene. The new controller must be on its own root outside both. Its camera, bold/regular fonts, coalescence shader, sphere style and original button references are assigned. The Screen Space Camera canvas scales with screen height. The regular face is a static SDF asset baked from the project's existing InputMono-Regular.ttf. There is no generated Edit Mode preview.

## Sphere and lettering

In `Assets/Scenes/S-0_ATTRACT.unity`, select **Black hole → Attract Ferrofluid Study**.

| Inspector control | Behavior |
| --- | --- |
| Use Ferrofluid Sphere | On: dense moving black fluid sphere. Off: original sphere and original floating particle title. |
| Embed MASSIVE Lettering | On: pure-white lettering carved into the active ferrofluid surface. Off: original floating title over the ferrofluid sphere. |
| Lettering Scale | Uniform size on the sphere; 1 is the approved layout, 0.5 is half size, with a range of 0.25–1.3. Both dimensions and the black letter border scale together. |
| Embed Depth | Recess below the reference surface, from 0 to 0.03 sphere-local units. |
| Vertical Position | Moves the lettering vertically around the sphere. |
| Ridge Undulation | How strongly the mounds move the outer junction of the ridge and liquid. Default 1. |
| Type Undulation | White-face response to the mound heights, from 0 (steady, the default) to 1 (full surface motion). Independent of Ridge Undulation. Values such as 0.35 provide a lighter response. |

The main scene retains your saved sphere and lettering values. The component's code defaults are scale 1 and depth 0.016. Switching the sphere off retains the lettering preferences for the next activation. Turning the component off also restores the original visuals.

Set controls before Play to save scene defaults. All four primary controls also update live during Play Mode; Unity discards those Play Mode adjustments when you stop. This effect does not generate an Edit Mode preview.

The collapsible **Surface and lighting** section contains density, relief, motion, highlight coverage, rear rim, and mesh resolution. Resolution is applied on the next activation. **Joystick response** retains shared-player averaging, the 0.18 drift threshold, and 0.7-second smoothing. **Asset references and advanced layout** contains the wired source logo, shader, distance texture, optional camera, and base angular width.

The original sphere mesh/material, title objects, particle systems, grid behavior and colliders remain in place. The alternate adds one transient mesh/material/renderer while active, restoring original renderer states and releasing generated resources when switched off. Missing lettering references restore the original title. Runtime code uses the project's existing Rewired movement actions.

The imported runtime files retain their study GUIDs so the validated assets transfer without rebinding. Study scene creation, recording utilities, and resonance assets are not required in the main project. The 2048 x 423 signed-distance texture uses the original title outlines, with the curved S sections reconstructed as tangent-continuous cubic curves. Their four terminal corners and straight end faces remain crisp. Straight letters retain their original outlines; the floating source title meshes are untouched. Distance is packed at 16-bit precision into RG with a blue encoding marker. Its data is linear, bilinear-filtered and uncompressed. These channels control shape and depth; they are never displayed as colors.

The shader evaluates lettering, recess borders and reflection edges at each MSAA sample. The ATTRACT camera already uses 8× MSAA, so internal edges now receive the same smoothing as the sphere silhouette. Each sample is still opaque pure black or pure white; intermediate screen pixels come only from resolving edge coverage. There are no gray lighting gradients or transparent letter faces. Disable camera MSAA as well as render-target MSAA when inspecting the unfiltered binary palette. Sample-frequency shading requires Shader Model 5 / Direct3D 11-class hardware; the current Windows target is validated.

At Type Undulation 0, the white lettering uses fixed spherical coordinates and a stable recessed radius. Increasing the slider blends that radius toward the actual mound heights while retaining the recess depth. The outer shoulder keeps its existing, independent tangential motion; its shape blends into the inner wall and letter floor. At zero response, a geometry margin and wall clearance keep neighboring mounds from tugging at or covering the white face in the tested front view. Negative distance continues beyond the texture padding to keep the outer M/E junctions continuous.

The shader adds local GPU tessellation around the lettering, up to four subdivisions per base edge, to resolve the curved walls and their highlights. Geometry density elsewhere is unchanged. This uses the same Shader Model 5 / Direct3D 11 hardware requirement as sample-frequency antialiasing. Standalone-player performance has not been benchmarked.

The current scene copy passed 21 input checks and 75 runtime/capture checks, plus the new slider's serialized default and Inspector Undo checks. At 2560 x 960, the white-face mask changed in zero pixels with Type Undulation 0, 45,500 pixels at 0.35, and 104,632 pixels at 1 across the tested time interval. The outer junction still changes with the mound field, while pixels outside the lettering band remain unchanged when adjusting Ridge Undulation. The saved main scene, camera, quality settings, scale and embed depth are preserved.
