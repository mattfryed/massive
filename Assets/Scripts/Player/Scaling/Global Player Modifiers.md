# Global player modifiers

**MASSIVE → Player → Player Size Controls** and **Joystick Reversal Controls** edit one project asset: `Resources/MassivePlayerModifiers`. The P1–P4 rows map to controller IDs 0–3 in every scene and in builds. These menus work without an open gameplay scene. Menu edits are saved immediately, including during Play Mode, and support Undo.

The initial profile was migrated from the Dynamo roster: P1/P3 size 66%, P2/P4 100%; reversal enabled with a 100° cone for all players; P1 retains 75% momentum and P2–P4 retain 0%.

Player Size owns the body, colliders and connected effects through the existing scale adjuster. Its optional attack-travel/targeting, movement-speed and projectile-range switches now also belong to each global player slot. Size is relative to the player's authored reference scale; it does not repeatedly multiply the current transform or resize the arena. Damage, timing and rigidbody mass are independent.

Each player component has **Use Global Player Size** or **Use Global Joystick Settings**. Turn that off for a local exception. Turning off the corresponding master toggle in the menu restores local values for every player. Existing scene values are preserved; the profile does not copy its settings into scene components.

Players with existing components preview size in Edit Mode. Older prefabs without those components receive them automatically in their controller's Awake during gameplay, including later spawns. Local fallback for an older player without reversal assistance remains disabled. Pseudo players, prefab asset previews and isolated preview-scene test fixtures do not receive global modifiers.

Use **MASSIVE → Player → Validate Global Player Modifiers** for slot mapping, collider scale, repeated application, local fallback, master toggles and pseudo-player exclusion. Existing Player Scaling and Joystick Reversal validation menus still test the original local mechanics.

## Menu scopes

- **Global:** these two player menus, using the single resource profile.
- **Shared asset:** enemy spawn indicators, scoring/economy profiles, attack profiles and text-animation preset assets. Changes affect every consumer of the selected asset; they are not universal overrides of other profiles.
- **Scene/component:** Melee Visual Preview and Amplifier Treatments currently edit the selected scene's visual components. Their reusable appearance settings are good candidates for future shared profiles, with scene geometry bindings kept local.
- **Actions/tests:** validation, scene setup, prefab gallery generation and ORBITAL/Dynamo construction commands operate on their stated targets. Their presence in the top menu does not make them global settings.

Arena dimensions, goal positions, spawn exclusion zones and level-specific Resonance layouts should stay scene-owned. Shared gameplay defaults can coexist with those local spatial settings.
