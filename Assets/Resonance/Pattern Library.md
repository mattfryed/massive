# Resonance pattern library

## Design intent

One encounter cycles through five recognizable configurations. The faint, complete pattern still appears only on a Core impact; only the exposed portions have the existing collision, sludge/magnetic response, local contact effects and grid attraction. Frequencies are artistic reference labels, not a claim that these are physically simulated plate modes.

The four additions reproduce the approved outline study in a 28 × 12 arena. SINGULARITY's existing encounter adapter fits them to its front face; the Core's wrapping behavior is unchanged.

| Order | Pattern | Exposed interaction arcs |
| --- | --- | --- |
| 1 | 345 Hz — original | Existing eight arcs; unchanged |
| 2 | 404 Hz — Crossroads | Eight: both opposing sides of all four diagonal ovals |
| 3 | 776 Hz — Orbit | Twelve: four center-ring arcs, plus both wall intersections on all four outer corner curves; each added segment is half a center segment's length |
| 4 | 1660 Hz — Petals | Eight: four inner-facing petal arcs and the inward-rounded middle of each outer corner curve |
| 5 | 4129 Hz — Channels | Fourteen: six original middle-band segments and eight complementary segments on the outermost top/bottom bands |

## Authoring controls

- **Spawn Cycle → Pattern Order:** reorder encounter prefabs. **Starting Pattern** is zero-based; **Loop Pattern Order** repeats after 4129. Captures still drive advancement; existing timing, capacity gates and spawn exclusions are retained.
- **Spawn Patterns/[frequency] Hz Spawn Pattern.asset:** exposed arc locations, independent visual/collision tapers and the **Core Impact Sound** reference. Full curves remain separate from exposed arcs.
- **[frequency] Hz Particle Encounter.prefab:** existing particle, color, grid, magnetic/sludge and manifestation controls. New prefabs copy the 345 encounter's presentation/interaction tuning, but use their own un-stretched arena-space traces.
- **Untapered Start / End:** only enabled where an approved segment attaches to the arena border, keeping that end full-width. The inward end still tapers normally. Global thickness/taper controls continue to work.
- **Open Polyline:** arc degrees mean normalized path length (0–360). An open curve clamps at its ends rather than wrapping and never adds a closing chord. Closed polylines still wrap, including arcs crossing 360 degrees. Each trace is stored as editable ordered XZ points.
- Option A remains available on every new controller via **Rendering**; Option B is the saved default. No shader fork or second interaction system was introduced.

## Core impact sounds

All five patterns use **0.72-second synth impacts** under `Assets/Audio/Assets/SFX/Resonance`, alongside one `AudioCue` per frequency. The approved 404/776/1660/4129 Hz synth auditions replace the earlier bell-like sounds in place, preserving their clip GUIDs and cue tuning. The new 345 Hz **Field Pulse** uses the same synthesis family: low pressure, a warm filter sweep and short electrical texture. It is assigned to the spawn pattern and both original Option A/B definitions; their geometry and interaction tuning are unchanged. Source WAVs are stereo 48 kHz PCM; the audition MP4s and longer drone demonstrations are not imported into gameplay.

Only a gameplay Amplifier Core's hard impact or magnetic turnaround plays the pattern's cue. Approach, player passage, visual preview buttons and formation/dissolution do not trigger it. A missing cue or missing AudioSystem is safely silent.

The existing AudioSystem now accepts a direct cue reference through `PlayCue`; its same pool, mixer fallback, scene-silence policy and concurrency checks still apply. Defaults are volume 0.8, slight volume variation, **pitch fixed at 1**, a 0.12-second cue cooldown and at most three overlapping voices. The controller's existing Impact Cooldown additionally deduplicates compound collider/turnaround notifications. Tune sound volume/cooldown on the cue; changing pitch also changes the nominal frequency.

## Maintenance and checks

**MASSIVE → Resonance → Install Missing Pattern Library** creates only missing assets, copies existing 345 prefab tuning for newly created prefabs, and appends missing entries to the shared encounter prefab. It does not overwrite existing library tuning, reset 345, or edit scene assets. The four approved WAVs must already be imported.

**Add Missing Patterns to Selected Spawn Cycle** appends missing entries to a selected scene coordinator, with Undo, without saving the scene or replacing custom order/timing. Existing standalone scene coordinators have also been expanded to five entries; prefab-based coordinators inherit the shared list.

**Validate Pattern Library** runs isolated Edit Mode geometry, audio-reference, dual-renderer, collision, endpoint, budget, ordering and idempotency checks. `ResonancePatternLibraryPlayValidation` provides an explicit disposable Play Mode fixture with real encounter/Core/goal/audio components. After entering Play Mode, call `CreateFixture` then `Begin`; stop Play Mode afterward. It temporarily deactivates the original scene's runtime objects, never replaces or saves the authoring scene, and does not start a match-history session. Its formation/delay acceleration affects runtime copies only. Stopping Play removes the test area and restores the original scene, including unsaved work.
