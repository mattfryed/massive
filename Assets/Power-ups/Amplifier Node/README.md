# Amplifier Node

Instant, attack-activated power-up based on Mass Node. Grants the same mass, then advances the collector to the next authored personal multiplier milestone. With the default economy this is ×1 → ×2 → ×4 → ×8 → ×16. Fractional values complete their current interval (×1.5 → ×2). At the cap, the mass grant still applies.

- Tune spawn chance, lifetime and description in **MASSIVE > Power-up Settings > Amplifier Node** / **Global > Spawning**. It starts with the same spawn weight as Mass Node.
- **Mass Node** is a shared source reference: its grow count, extra mass, sound and collection effects also apply here. Use **Edit shared Mass Node grant** to tune it.
- The equipped power-up and its timer remain intact. The node does not grant score, change team amplification or add extra chain charge. Normal death/reset rules for personal multipliers still apply.
- `Assets/Power-ups/PU_AmplifierNode.prefab` is a variant of Mass Node. It retains the shell, physical body, attack trigger, spawn animation and acquire/timeout outro. The inner icon adds the Amplifier Nuggets prismatic swirl; all three layers follow the icon and shrink together on pickup. Particles do not fade opacity on death.
- The shared Resources catalog makes it available in every scene using the standard power-up spawner. Existing scenes do not need new local references.

Validation: **MASSIVE > Power-ups > Validate Amplifier Node** runs canonical prefab contacts for all three attack stages and checks mass parity, fractional/custom multiplier milestones, cap handling, one-time collection, shared tuning and equipped ability preservation in a disposable Play Mode scene.
