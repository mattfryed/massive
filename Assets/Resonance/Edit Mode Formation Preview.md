# Edit-mode formation preview repair

The scene Option B originally lacked ResonanceManifestation; only the runtime spawn prefab had it. The component is now on scene Option B with an explicit link to its spawn prefab for animation-only Save/Load.

Two additional causes were reproduced in Unity 6000.0.28f1:

- Change-driven ExecuteAlways Update did not advance the preview reliably. A 1.40-second Edit Mode observation produced progress 0 on a 10-second formation. An editor callback now advances the animation independently of selection and repaints Scene/Game views. After correction, 1.471 seconds produced progress .147, matching elapsed time exactly. Runtime still uses scaled gameplay time; held scrubs do not advance.
- Newly created paused particle pools had billboard geometry but uninitialized native custom vertex data in Edit Mode. A plain magenta material rendered the geometry, while the production shader did not show the grains. Zero-time simulation followed by restoring the exact authored particles/custom streams initialized the native buffers; the actual Game camera then rendered the expected grains. This initialization runs once per pool build, only in the Editor outside Play Mode. It does not age/move grains, upload particles each frame, or alter runtime rendering.

The GPU validation now renders actual ParticleSystems directly, replacing the previous test-only baked-mesh workaround. Additional edit-preview tests cover settings-only transfer, held poses, no double advancement, disable/re-enable, Play-entry reset without domain reload, and invalid prefab destinations. A scoped real-prefab save check verified the animation field reached disk while the controller remained unchanged, then restored the original values.

Inspector workflow: select scene Option B → Resonance Manifestation → Form In / Dissolve Out / scrub → Save Animation Tuning to Spawn Prefab when satisfied. Save the scene to retain its component and link. Preview progress is never serialized.

## Local birth / field vibration

The scene preview and linked 345 Hz encounter now use **Local Band**. **Spawn Spread** controls how far grains begin from their own idle positions, instead of redistributing them across the arena. **Along Arc Spread** shapes this into a narrow band; **Field Coherence**, **Field Wavelength**, **Vibration Strength / Frequency**, and **Birth Pulse** control the settling vibration. These are visual standing waves about the pattern origin, not a simulation of the grid's physics. The exact settled look, layer populations, contacts, and pattern geometry remain independent of formation tuning. The original **Full Field** option and each object's saved rectangle remain available.

Local formation starts with a 0.55-unit band, 0.25 along-arc spread, 0.9 coherence, 2-unit wavelength, and 0.15 pulse. Vibration is reduced to 0.08 at frequency 6. Size grows slightly earlier (power 0.5) while settling lasts longer (power 1.8); duration is unchanged. The same controls affect the reverse dissolve. The animation-only Save/Load buttons include all six new local-birth settings.

Local-birth validation: 16 new native-GPU/behavior checks, 6 existing formation GPU checks, 22 particle checks, 31 lifecycle checks, 20 edit-authoring checks, and 39 spawn-rule checks passed (134 total). Exact idle pixels and zero-spread/zero-vibration translation parity are tested with deterministic transparent-layer ordering in the disposable test scenes; production draw order is unchanged. The real Game camera also showed nearby grains at held intermediate poses with Play Mode off. Option A/B controller, pattern definition, and spawner serialized snapshots matched the pre-change state exactly. Scene and encounter prefab received only the formation tuning; the scene remains unsaved for normal authoring workflow.

Validation: 20 edit-authoring checks, 6 native GPU checks, 31 formation lifecycle checks, and 22 particle regressions passed (79 total). A fresh-build .72 scrub pose was visibly rendered by the main Game camera with Play Mode off. Both Option A/B controller settings matched the turn's starting snapshot exactly; the user's initial spawn delay of 6 seconds was preserved. Final Console had zero errors. Scene left unsaved with Option B at idle and the Manifestation Inspector expanded.
