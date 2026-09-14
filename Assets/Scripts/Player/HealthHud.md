# Player health HUD prototype

Each player's health bar sits directly above their personal multiplier in
`Assets/Prefabs/PLAYING FIELD.prefab`, under `UI / Text Objects / TEAM_01` or
`TEAM_02`, named `Player N health`.

The bar reads the same `massScore`, minimum, and maximum used for the internal
nuggets. It shows a percentage: full white is 100%, and zero health/death has no
white fill. The percentage uses a white text outline to remain readable over both
the white fill and the empty black track. There is no permanent white number badge.

Current player prefabs spawn and respawn at 50% health. This HUD does not change
health, damage, healing, nugget behavior, or respawn balance. It stays at 0% while
the player is eliminated, returning to their restored health when respawn completes.

Below 15% actual health, the fill, frame, and percentage blink red at 1.5 flashes
per second. The dim phase remains visible. Healing to 15% or above restores the
normal colors immediately. Death leaves an empty, steady bar. Adjust the threshold,
color, frequency, and dim brightness under **Low Health Warning** on the presenter.

Select a `Player N health` object to adjust **Fill Smooth Seconds** on
**Player Health Presenter**. Its child **Health frame**, **Health track**, and
**Health fill** use the same Shapes components as the multiplier UI. Number styling
uses `Assets/Scripts/Player/Health Number.mat`; it is separate from multiplier text.
Disable the health GameObject to compare the prototype with nuggets alone.

## 1v1 and 2v2 layout

Both team roots have a **Team Player Hud Layout** component. **Automatic** follows
the enabled roster player roots: two participants use two columns, and a single
participant uses the whole team score column. Normal 1v1 uses P1 versus P3; unused
P2/P4 health and multiplier panels are hidden. Player labels align to the team's
score text box on the left; full bars align to its right edge. The team captions
share that same text-box width. Font sizes, number badges, and bar heights stay fixed.

Death and temporarily hiding an ancestor gameplay rig do not change the layout.
Switching back to 2v2 restores both columns, including any existing fill or maximum
multiplier effect. **Layout Mode** also offers explicit 1v1/2v2 presentation overrides
for testing; these change the HUD only. **Two Player Gap** adjusts the column gap.

Unbound players display `--` with an empty bar. Player identity comes from the
adjacent multiplier or the existing player registry. Lightweight state comparisons
also catch legacy direct health edits; text only changes when the percentage changes.

`MASSIVE / Players / Run Health HUD Validation` checks all four bindings, layout,
damage, healing, zero/full states, real death/respawn, re-enabling, custom health
bounds, and replacement players in an isolated Play Mode scene. The temporary scene
is removed afterward and the previous gameplay scene is restored.
