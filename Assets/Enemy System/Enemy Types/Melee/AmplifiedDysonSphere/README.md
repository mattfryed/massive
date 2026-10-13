# AMPLIFIED DYSON SPHERE

A prefab variant of the canonical Repulsor Dyson. It retains pursuit, radial attacks,
panel damage/shatter, physical body, score attribution and the shared spawn warning.
The new definition starts at **40 health**, twice the normal Dyson's current 20.
Its direct kill reward uses `ENEMY_AMPLIFIED_DYSON_DEFEAT`: **7 eV base and 2
personal charge**. Collectibles award separately to whichever player collects
them. Drop amounts, probabilities, mass restoration and pickup multiplier gains
are unchanged by the defeat-score balance pass.

## Find the controls

- **MASSIVE > Enemies > Amplified Dyson > Select Enemy and Loot Settings** selects
  `Enemy_AmplifiedDysonSphere.prefab`. **Amplified Dyson Loot** owns all drop controls.
- **Select Panel Wave Settings** selects its Dyson Sphere Panels component. The
  **Panel flip wave (Amplified Dyson)** group has frequency (cycles/second), amplitude
  (degrees), spatial wave cycles and flip softness. The 180-degree default turns each
  triangle fully over about its tip-to-opposite-edge axis, so the current 80-panel
  shell's reversed faces fit their original outlines; reverse geometry keeps the
  black fill visible. Outlines offset toward the camera on either face so the
  reversed fill cannot hide its own stroke. The wave travels
  from the top-down visible center around the shell. Breathing keeps its own controls.
- **Select Rainbow Outline Settings** opens the **Amplifier energy outlines** group.
  The variant uses continuous 3D noise to distribute the full spectrum across the
  whole shell. Two noise channels map around the color wheel, avoiding the Core
  material's warm-biased color mixture. Color varies smoothly within each line,
  with no per-face color assignment or restarted gradient. Existing vector width,
  black face fills, draw-in and shatter fades are retained. Pattern Scale, Flow Speed,
  Brightness and Saturation tune this variant independently; zero Flow Speed freezes the color
  flow. The enemy's animation clock also freezes color flow when paused. The shared
  source material supplies flow, scale and emission only; it is read only, so these
  controls do not change the team goals. Six gradient samples per edge is the minimum
  default, increasing automatically to resolve finer noise (up to 32 samples).
  Ordinary Dyson outlines remain white.
- **Amplifier Nugget core > Amplified Dyson Core** controls core size and burst growth.
  Its nested visual uses the same white, black and prismatic particle systems/materials
  as `Power-ups/Amplifier Nuggets/Amplifier Nugget Visual.prefab`. The original cores
  are disabled only on this variant. Its shader clock and particles pause with the enemy.
- `ED_AmplifiedDysonSphere.asset` owns health, movement, contact damage and reward key.
  Health was doubled when created; later editing the ordinary Dyson does not silently
  overwrite the variant's tuning.

## Hit and death drops

Starter hit tuning: **25% per eligible surviving hit**, at most one roll per **0.35
active seconds**, and **8 successful hit drops per life**. Failed rolls also consume
the interval, keeping continuous beams independent of their per-frame damage callbacks.
Each successful roll emits one Amplifier Nugglet. Zero chance or a zero limit disables
this path; 100% guarantees eligible rolls. Lethal damage uses the death burst instead.

Defeat emits **6–10** randomly selected pickups, with independent weighted draws:

| Pickup | Weight | Collection reward |
| --- | ---: | --- |
| Mass Nugglet | 40 | Small mass restoration + shared MASS_NUGGLET energy rule |
| Amplifier Nugglet | 30 | Small mass restoration + **0.1×** personal multiplier |
| Mass Nugget | 20 | Full nugget mass restoration + shared MASS_NUGGET energy rule |
| Amplifier Nugget | 10 | Full nugget mass restoration + **1×** personal multiplier |

Weights normalize automatically; zero removes a type. A random burst need not contain
all four types. Nugget/nugglet mass uses the existing 10:1 ratio. Amplifier drops use
the current EMERGENCE rewards, honor the personal cap, and grant no banked score.
Full-health collection still awards eligible energy or multiplier gain. These are
body-collected pickups, distinct from attack-claimed Amplifier Nodes and team Cores.

Ejection defaults to random XZ directions at **2–4 units/second**, a **0.3-unit**
offset and **12 active seconds** of life. Drops survive their source's death, pass
through enemies, and reuse the shared pickup body-contact, wall response, visuals
and receipt system. Source/Director/match pause freezes released loot; isolated
demonstration loot cannot reward real players. Expired or collected drops destroy
their own objects. Enemy expiry, administrative Kill and direct destruction do not
emit a defeat burst. Repeated damage/death callbacks cannot duplicate the burst.

## Encounters and validation

`Amplified Dyson Flanks.asset` is available in Encounter Composer's formation library.
**Open Encounter Preview** opens a dedicated 30-second timeline with two arrivals at
three seconds and the existing Dyson warning. Assign that timeline to Enemy Lab to
test, or add the formation to a chosen level timeline. Production encounter schedules
are not automatically changed. The preview budgets this variant at pressure 6; set
its pressure/population policy explicitly when adding it to another timeline.

**Ensure Prototype Assets** creates missing assets without resetting existing tuning.
**Validate Prototype** runs an isolated Play Mode fixture, restores the previously
open scene and writes its results/captures to `Library/AmplifiedDysonValidation`.
It covers real panel geometry, core activity/pause, hit chance endpoints, sustained-hit
limits, death/cleanup, finite pickup life and actual player-body collection for all
four rewards. A full competitive balance pass and standalone build remain separate.

Validated on 2026-10-13: all 34 Play Mode checks passed, with no runtime Console
errors. The shell/core and released-loot captures were visually reviewed. The
original EMERGENCE scene was restored without scene edits.

The flip-axis regression check compares every generated corner before and after
a full turn-over and confirms that the face normal reverses. Focused checks pass
for both the 20- and 80-panel shells (maximum corner drift below 0.000001 units).

Prismatic outline validation at Pattern Scale 10: 12,000 samples over four clock
positions placed 16.2–17.1% in each of six equal hue bands. Front/back renders,
clock freeze, zero flow speed and alpha preservation were checked with the
original 0.005 line width. The source Core material remained unchanged.
