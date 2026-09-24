# Amplifier gameplay controls

In **S-8_DYNAMO-PROTOTYPE**, select **Amplifier + Resonance — Spawn Cycle**. Its Inspector links directly to the Core prefab, both goals, and capture toast.

## Core size and motion

Use **Edit Core Size / Physics (spawn prefab)**. Changing the prefab affects subsequent managed spawns. During Play Mode, select the live Core to tune that instance.

- **Overall scale** multiplies the existing authored size. It scales the shell, body collider, spawn clearance and grid influence radius together; mass remains an independent dial.
- **Mass**, **Linear drag**, **Angular drag**, **Maximum speed**, **Wall bounce** and **Attack impulse** control motion. Maximum speed is the planar speed limit; zero means unlimited. Attack impulse and mass together determine launch speed.
- Defaults preserve the previous mass 3, linear drag 0.42 and angular drag 0.6. Maximum speed starts at 25, and wall restitution at 0.85.
- Wall response is frictionless and reflects incoming motion even below Unity's normal bounce threshold, preserving travel along the wall.

## Sequence lifetime

The scene starts with **Time-out = 30 seconds** and **Warning before expiry = 5 seconds**. Time begins after the Core finishes appearing, uses gameplay time, and pauses with the game. Zero time-out disables expiry.

During the final five seconds, the existing surface pattern progressively distorts with irregular motion. **Amplifier Core Visual → Time-out warning → Timeout Instability** adjusts its strength independently of attraction excitement. The physics body does not shake.

Expiry shrinks the Core in place and dissolves the Resonance without awarding a capture. The usual respawn delay and pattern order then apply. When both teams are at their configured maximum multiplier, new sequences wait; an existing unused pair retires. Resetting team multipliers allows spawning again. Explicit Stop remains stopped.

## Capped goals and capture feedback

**Light Goal Forces / Dark Goal Forces** opens the goal's attraction and **Goal at maximum multiplier** controls. A capped goal stops accepting or attracting Cores. With **Repel When Maxed** enabled, **Repulsion Acceleration** pushes them back toward the arena; **Repulsion Minimum Speed** clears a Core from the capture aperture. The attraction radius also defines the repulsion range.

**Capture Toast** selects the score service's **Team Amplifier Toast Presenter**. Controls cover placement, font sizes, lifetime, rainbow, vibration and ghost trails. It shows the small team label above the large multiplier. ×2 is white without tier effects; higher tiers progressively intensify toward the configured cap. Toasts follow accepted multiplier increases, never resets or blocked captures.

Under **Placement and type**, **Light Anchor / Dark Anchor** use screen coordinates from 0 to 1: X moves left/right and Y moves bottom/top. **Overall Scale**, **Team Label Size** and **Multiplier Size** control the capture toast's size. These controls update live in Play Mode; edit outside Play Mode to retain changes.

Under **Max amplification notice**, a separate small white **MAX AMPLIFICATION** message explains a capped goal's rejection. It appears once when a Core enters the repulsion area (or touches the capture aperture when repulsion is disabled). Leaving the area re-arms that Core. The notice has its own toggle, font size, scale, lifetime and repeat delay, plus a pixel offset from each team's anchor. It can coexist with a capture toast without replacing it. Resetting the multiplier clears both types of message.

Power-up pickups pass through Resonance colliders and ignore its membrane/deflector forces. Player, enemy and Amplifier Core responses retain their own settings. No global collision-layer changes are made.

## Validation

Edit Mode: **AmplifierCoreTuningValidation.RunChecks**, **AmplifierResonanceSpawnValidation.Run**, **TeamAmplifierToastValidation.Run**.

Play Mode: **AmplifierCorePhysicsPlayValidation.Begin**, **AmplifierLifecycleBatchPlayValidation.Begin**, **AmplifierPickupInteractionPlayValidation.RunBegin**. Run separately in a disposable Play session; each helper reports results and cleans its fixtures. The lifecycle helper leaves the managed cycle stopped after restoring configuration.
