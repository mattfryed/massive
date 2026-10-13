# Personal multiplier

Personal multipliers now increase continuously with each accepted action. The HUD
shows up to two decimal places (`1`, `1.19`, `2.5`, … `16`); scoring retains the
underlying precision and rounds the final award once to the nearest meV.
The award uses the multiplier earned before that action. Its charge then increases
the multiplier for subsequent awards.

## Balance controls

Select `Assets/Scripts/Scoring/ScoreEconomyProfile.asset`.

- **Chain Settings / Multiplier Steps** are growth control points, not hard tiers.
  Values interpolate continuously between them. The last value is the cap.
- **Charge Required Per Tier** retains its serialized name for existing assets.
  It sets the charge cost between consecutive control points.
- Each reward's **Chain Charge** controls its contribution to growth.

Existing tuning is preserved: control points 1, 2, 4, 8, 16; costs 4, 6, 8, 10.
Reaching 16 requires 28 total charge. A Drone grants 0.25 charge: the first three
kills leave the player at 1.0625, 1.125, and 1.1875 (displayed 1.06, 1.13, 1.19).
The current base reward is 100 meV, so those kills award 100, 106, and 113 meV.
Growing continuously also increases scores earned on the way to the cap compared
with the previous stepped implementation; match score pacing should be reviewed
in gameplay before retuning reward weights.

Team Amplifiers retain their existing shared values and multiply the personal
factor before score rounding. Personal multipliers still reset on death and match
reset. The profile's optional hit penalty applies to fractional progress as well;
charge loss can cross control points, including leaving the cap.

## Player and HUD shimmer

Open **MASSIVE / Player Tuning / Appearance / Personal multiplier shimmer**.
The player's outline and personal multiplier bar begin shimmering at **8x** with
25% strength, then grow continuously to full strength at **16x**. The effect uses
that player's actual fractional multiplier, independently of team Amplifiers.

Controls include the enabled switch, onset/full-strength multipliers, starting
intensity, outline opacity/reach, bar intensity, pattern scale/contrast/curl, and
drift speed. Changes save globally, including during Play Mode. Colors come from
the shared Amplifier goal corona's cyan/magenta/gold palette.

The **Live preview · world scale** foldout in that section renders the actual body
and HUD shimmer shaders in Edit Mode and Play Mode. Scrub the preview multiplier,
choose a player slot, pause the animation, or use Zoom / Fit. PlayerActor's resting
outline uses that slot's global size (or the prefab's local size when globals are
disabled); the PLAYING FIELD bar uses its authored dimensions and continuous
multiplier progress. Both share one world-unit scale, with a one-unit grid and
ruler. Samples have separate clipping panes so zoom cannot overlap them. The
preview does not include nuggets, movement deformation or camera post-processing.
Preview controls never alter live scores, scenes or prefab assets. Shimmer tuning
below the preview still saves globally, and changes appear immediately.

The outline adds drifting filaments outside the actual deformed player silhouette.
It follows attacks, charge vibration, Decoherence copies and the existing folded
surface projection. Fill, nuggets, collision geometry and scoring are unchanged.
The HUD replaces its old white maximum-only glow with the same multicolor shimmer
starting at 8x. Text and bar fill stay steady. Reset, death, disable and falling
below the threshold remove the effect; respawning does not retain it.

Per-widget controls remain on **Player Score Chain Presenter / Multiplier Shimmer
Glow** in `Assets/Prefabs/PLAYING FIELD.prefab`, under
`UI / Text Objects / TEAM_01` or `TEAM_02` and a `Player N multiplier` object.
These retain the material/tint, pulse frequency, glow opacity/spread and sweep
duration. Glow objects and meshes are reused; no materials are created per frame.

The fixed white number badge reserves space for decimals. The remaining bar grows
from minimum to maximum, without resetting at the former tier boundaries.

Validation: `MASSIVE / Scoring / Run Multiplier Integration Smoke Tests` covers
fractional score math, custom caps, continuous progress, all four HUD widgets,
decimal fitting, maximum-effect lifecycle, and Amplifier integration.
`Run Enemy Scoring Smoke Tests` includes the actual Dyson prefab.
