# Dyson Sphere — Repulsor

`ED_DysonSphere.asset` is the canonical gameplay definition and now spawns
`Enemy_DysonSphere_Repulsor.prefab`. Stage and prototype spawn profiles, Enemy Lab,
and the gameplay prefab gallery use this radial attack. Health and score tuning
are unchanged. The former lunge prefab remains only as the structural base of the
Unity variant and for legacy regression fixtures; it is not selected by gameplay
spawn profiles. `ED_DysonSphere_Repulsor.asset` is a compatibility asset, not a second
active spawn type.

`Dyson Repulsor Test` remains available in Player Actions. The active **Enemy Lab**
presents the repulsor in its own column and suppresses the standalone encounters
while it runs. Disable Enemy Lab to return to standalone testing.

## Attack controls

The root's **Dyson Sphere Repulsor Controller** exposes:

| Setting | Default | Meaning |
| --- | --- | --- |
| Attack Distance | 1.5 units | Centre-to-centre range to begin charging |
| Charge Seconds | .75 s | Stationary contraction/telegraph |
| Charge Scale | .85 | Whole visual body contracts to 85% |
| Charge Panel Vibration / Frequency | .008 units / 32 Hz | Panel jitter as they close onto the base outline |
| Expansion Seconds | .08 s | Fast outward burst |
| Burst Scale | 1.15 | Whole visual body reaches 115% |
| Panel Radius Multiplier | 1.3 | Panels' outer vertex radius reaches 130% of the base shell, before body scaling |
| Panel Hold Seconds | .1 s | Hold the expanded pose, with vibration |
| Attack Panel Vibration / Frequency | .015 units / 38 Hz | Expanded-panel jitter |
| Return Seconds | .4 s | Return to ordinary breathing |
| Bounce Cycles / Damping | 1.5 / 3 | Body's damped rubber-band rebound |
| Cooldown Seconds | 1.65 s | Recovery before chasing again |
| Damage Multiplier | 1.25 | Multiplies the existing .08 Dyson damage to .10 player mass |
| Pushback Speed | 4.25 | Initial recoil speed, scaled by player movement tuning |
| Pushback Ease Seconds | .65 s | Quadratic speed decay for a smooth stop |

Charge removes idle lift/inset as it closes the panels. Expansion translates the
panels along their outward normals without scaling each individual face. At the
default .75 shell radius, peak outer radius is approximately 1.121 units plus
panel vibration (`.75 × 1.3 × 1.15`). The physical central hull follows body scale;
the outgoing damage sphere and incoming sword hurtbox fit the live panel envelope.

Damage is active during expansion, hold and return, at most once per eligible
player per burst. Shields block it. Solid obstacles block the damage ray. Charging
and cooldown do not damage. A lost charge target cancels safely into cooldown.
The attack stays in place and never lunges.

## Ripple and presentation

The `Repulsor grid ripple` child uses **Player Repulsor Grid Pulse**, with local
tuning independent of player settings. Intensity, duration, packet width and curl
remain on that component. **Ripple Travel Multiplier** on the controller sets
travel relative to peak shell size (2.5 by default). This visual-only ripple does
not extend damage range.

The **Black and white energy core** child uses two opaque SDF metaball volumes
based on the Turret core. **Dyson Repulsor Core** exposes Core Size, Explosion
Growth (25%), Explosion Vibration (.018), Vibration Frequency (38 Hz), and idle
breathing. Core growth is independent of shell growth, reaches its peak with the
panels, and eases back during recovery. Core motion uses the same paused enemy
clock as the shell and ripple. The original particle core is disabled in this
variant; the original lunge prefab retains it.
Death immediately disables damage, then the existing panels shatter before cleanup.

## Validation and authoring

`MASSIVE > Enemies > Dyson Repulsor` provides ensure-prefab, place and validation commands.
Creation preserves existing variant tuning. Validation uses scripted real players
in Player Actions, discards its runtime fixtures, and writes its report and pose
captures under `Library/DysonRepulsorValidation`.

The existing `DysonSpherePanels` receives an optional radial pose/clock API; its
original path is unchanged when that API is not used. The shared grid pulse gained
an explicit world-origin entry point and optional external clock; player pulses
retain their normal clock and activation path.
