# Amplifier + Resonance spawn cycle

The coordinator owns one gameplay Core and one Resonance pattern at a time. Existing goal scoring, capture absorption, goal treatment effects, Core spawn animation, and multiplier rules remain unchanged.

## Cycle

Match scoring opens → initial delay → Resonance grains form → safe neutral Core appears → Core captured → Resonance dissolves while Core absorption finishes → randomized respawn wait → next ordered pattern.

All times use gameplay time, so pausing also pauses formation and delays. An unclaimed Core remains until captured unless Maximum Active Seconds is greater than zero. Manual retirement/timeout never awards a multiplier. Closing match scoring removes the managed pair and stops further spawns.

## Scene controls

Select **Amplifier + Resonance — Spawn Cycle**:

- **Pattern Order:** drag rows to choose order. Entries require prefab assets with a configured pattern definition. Starting Pattern is zero-based. Loop Pattern Order repeats the list; otherwise it ends after the last entry. Empty/unconfigured entries are skipped.
- **Initial Spawn Delay**, **Respawn Delay**, and **Respawn Delay Variation:** variation is an additive uniform range. A delay of 8 with (-2, 2) waits 6–10 seconds, measured after the old pair has fully disappeared.
- **Placement Retry Delay:** waits before another bounded search when no safe location exists. There is no unsafe fallback.
- **Wait For Scoring:** starts with the real match by default. Turn off only for independent Play Mode testing.
- **Core Prefab:** an independent copy preserves the scene Core's visual overrides and physical size. Minimum Core Radius is a clearance floor; actual collider size can only increase it.
- Play Mode controls restart, skip an existing delay, retire without scoring, select the live pattern, and stop/restore demonstrations.

## Placement and no-go zones

The **Amplifier Spawn Region** component shares its validation between spawning and Scene view dots. Green candidates are clear; red candidates are unavailable. The teal outline bounds safe Core centers, accounting for the entire Core, padding, grid transform, and neutral-width fraction.

Placement avoids solid objects, player proximity, goal attraction, the next pattern's authored collision/magnetic footprint, and existing NoSpawnZone-layer / PowerUpNoSpawnZone volumes. Normal triggers are not automatically blockers. Add a specific trigger to **No Go Colliders** when it represents a hazard. NOVA's star currently needs this explicit assignment (its entry volume is a Default-layer trigger). No enemy spawning code or closed NOVA scene is changed by this integration.

**Blocking Mask** selects solid obstacles; **No Spawn Mask** includes triggers. **Ignored Colliders** is an explicit exception list. Grid-plane colliders themselves are ignored, but obstacles parented beneath the grid still count. Concave mesh no-go volumes conservatively reserve their bounds; primitive/convex volumes give more precise zones.

The inspector preview is a sampled aid, not a continuous navmesh. Runtime rechecks current object positions immediately before instantiating the Core. If the territory is occupied, it retries without changing the pattern.

## Formation tuning

For in-place **Edit Mode** tuning, select **Resonance 345 Hz — Option B (Particles)** and expand **Resonance Manifestation**. The coordinator also has **Edit / Preview Scene Option B Animation**, which selects this object. Use **Form In**, **Dissolve Out**, **Reset to Idle**, or the **Formation Preview** scrubber while watching the Game view. No Play Mode is needed; the editor clock continues even after selecting another object.

Scene tuning is saved with the scene. **Save Animation Tuning to Spawn Prefab** explicitly copies only the formation parameters to the linked runtime prefab; **Load Animation Tuning from Spawn Prefab** reads them back. Neither button copies pattern geometry, particle densities, colors, materials, collision settings, or transient animation progress. A prefab asset selected directly in Project is not a main-scene renderable object; use the scene preview for Game-view tuning.

Each pattern prefab contains **Resonance Manifestation**:

- Spawn / Despawn Seconds
- Size Growth Power (lower grows grains earlier)
- Condensation Power (higher holds the dispersed shape longer)
- Birth Distribution: **Local Band** for nearby formation; **Full Field** retains the original rectangular scatter
- Spawn Spread: maximum additional starting offset from each grain's idle position, in pattern-local units. The current scene and encounter prefab use **0.55**, about half a grid cell at their present scale
- Along Arc Spread: how much of that spread can run along the arc rather than across it (currently **0.25**)
- Field Coherence: blends independent jitter into a shared standing-wave vibration (currently **0.9**)
- Field Wavelength: repeating spatial wave spacing (currently **2**); Birth Pulse adds restrained grain-size breathing during formation (**0.15**)
- Vibration Strength / Frequency
- Dispersal Center / Half Extents in the pattern's local XZ plane (Full Field only)

The preview buttons and normalized scrubber can inspect the transition. At zero, particle size is zero. Local Band grows the existing fixed grain populations close to their resting positions, then vibrates and condenses them into place. Spawn Spread excludes the small additional vibration and existing idle-layer width/wander. A zero spread plus zero vibration gives an in-place appearance. Dissolution reverses into the same nearby band. At one, the shader returns to the exact authored idle rendering. The full-pattern collision ghost remains a contact effect, not a permanently visible spawn layer.

The current local-birth pass uses vibration strength **0.08**, frequency **6**, size growth power **0.5**, and condensation power **1.8**, with the existing **2.4 / 1.2 second** spawn/dissolve timings. This evokes a field resonating locally; it does not simulate a physical Chladni plate or read the vector grid's displacement. Full Field's saved rectangle is retained when switching modes.

Formation/dissolution disables all pattern collisions, magnetic/sludge response, and grid attraction. These become active only once formation has fully settled. The Core is spawned afterward so it cannot be obstructed by appearing collision geometry. Option A remains supported by a simple fade; Option B uses the grain condensation.

## Preserving the standalone prototypes

The setup command creates independent Core, pattern, and pattern-definition snapshots under **Assets/Resonance/Spawn Patterns**. Original Option A/B and scene Core tuning are not overwritten. Only explicitly assigned standalone examples are suspended while the coordinator runs. HUD Core copies, goals, and gallery objects are untouched. Stop/disable restores the prior demonstration state; leaving Play Mode restores the edit-time scene.

Only 345 Hz is provided initially. Add new configured pattern prefabs to the ordered list as they are authored. Edit-mode scene wiring must be saved normally by the designer.

The existing multiplier cap behavior is unchanged: a goal refuses a capture if it cannot advance the team multiplier. This system advances only after a successful capture.
