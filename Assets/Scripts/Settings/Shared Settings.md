# Shared project tuning

Open **MASSIVE → Shared Settings**. The same appearance controls in **Player → Melee Visual Preview** and **Amplifier Treatments** now edit these shared assets by default. **Resonance → Shared Formation & Timing** and **Text Animation → Shared Settings** open their respective tabs directly.

Changes made through these menus save immediately, including during Play Mode. Undo is supported. The assets are in `Assets/Scripts/Settings/Resources`, so scenes and builds resolve the same defaults without depending on a previously opened scene.

| Profile | Shared across scenes | Kept local |
| --- | --- | --- |
| Melee | Treatment selection, thrust/sweep/Repulsor layers, materials, colors, density, motion, lingering trails, Repulsor body feedback and grid pulse | Attack-controller and collider bindings, player-specific reference dimensions, preview stage, playback speed, looping and scrub position |
| Amplifier | Goal treatment layers and wave settings, Core scale/physics/lifecycle/surface motion, pull and capped-goal repulsion strength, capture/max toast appearance | Goal team and capture point, capture/attraction radii, grid and mass-surface references, canvas, preview state |
| Resonance | Formation/dissolution animation, grain condensation and vibration, encounter delays/variation, time-out and warning duration | Pattern order, geometry definitions, neutral/no-go regions, anchors, field dimensions and standalone example selection |
| Text | Score-digit preset and intensity, shared tier-style profile and its promotion/loop presets | Text targets, layout references, sample text, test score/multiplier, playback and preview range |

Each participating component has **Use Shared Project Settings**. Disable it for a local exception: its original serialized scene/prefab values are retained. Each profile also has **Shared Enabled**, which restores local values for the whole group when disabled. A missing shared asset falls back to local tuning. Blank material/font or text-preset assignments retain the component's local resource fallback.

The initial profiles were seeded from the current Dynamo setup, including P1's melee treatment, the encounter's Core prefab, the scene's Resonance animation, and current HUD tuning. Existing scene and prefab data were not overwritten. Shared defaults do not install missing gameplay systems into a level.

Gameplay attack timing, reach and damage remain in the existing **Player Attack Profile**. Text animation modules remain reusable **Text Animation Preset** assets; the Text tab assigns them and provides direct access to the score preset and tier-style library.

Developer note: public/local serialized fields retain their original names for compatibility. Rendering and gameplay consumers resolve shared values through effective accessors. Code that deliberately needs an instance variation should set `UseSharedSettings = false` before changing its local fields. Editor preview-scene fixtures retain local values; normal loaded scenes and runtime spawns resolve the shared Resources assets. No scene object references or live preview state are stored in those assets.


## Original Particles

**MASSIVE > Shared Settings > Melee** and **Player Tuning > Appearance** expose three Original Particles prefab assignments and Open buttons. The assets live in `Assets/Prefabs/Player/Attack Stages/Original Particles`.

Thrust and Sweep each contain the actual **GPU Particle Trail** emitter. Their particle count, length, widths, speed, drag, lifetimes, sizes and emission rate are now independently owned by each prefab. The window's GPU foldouts edit and save those same prefab fields during Play Mode. Open the whole prefab to add Particle Systems or other effects. Saved prefab changes refresh running actors, including new children. The root/emitter Inspector provides a looping Scene-view preview.

Repulsor keeps its existing body/grid feedback and has a disabled optional particle layer. Enable it in the prefab to add particles. Each stage has independent placement, activation gating and a bounded visual tail; gameplay timing, hitboxes and damage remain in the attack profile.

The old `legacyParticles` group remains under **Legacy fallback (actors without stage prefabs)**. Player-root local fields and external/Time Dilation configuration are retained. Stage-prefab GPU emitters always use their own authored values. Player Tuning snapshots capture prefab assignments; whole prefab contents use their own asset history.

See **Editing these effects.md** beside the three prefabs for the full workflow.
