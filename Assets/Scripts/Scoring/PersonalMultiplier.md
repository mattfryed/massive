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

## HUD controls

Open `Assets/Prefabs/PLAYING FIELD.prefab`, then
`UI / Text Objects / TEAM_01` or `TEAM_02` and a `Player N multiplier` object.
Its **Player Score Chain Presenter / Maximum Multiplier Effects** provides:

- Enabled switch and color.
- Pulse frequency and glow opacity/spread.
- Sweep duration in seconds (lower is faster).
- Shared glow material, `MultiplierMaximumGlow.mat`.

The fixed white number badge reserves space for decimals. The remaining bar grows
from minimum to maximum, without resetting at the former tier boundaries. Its
track now matches the full visible frame. At the cap, one soft glow pulses behind
the bar with a traveling highlight; the number and white fill remain steady.
Effects start once the animated fill reaches maximum and hide immediately on
reset or disable. Each view reuses its glow object and releases its mesh on
destruction; no materials or objects are created each frame.

Validation: `MASSIVE / Scoring / Run Multiplier Integration Smoke Tests` covers
fractional score math, custom caps, continuous progress, all four HUD widgets,
decimal fitting, maximum-effect lifecycle, and Amplifier integration.
`Run Enemy Scoring Smoke Tests` includes the actual Dyson prefab.
