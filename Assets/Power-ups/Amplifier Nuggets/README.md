# Amplifier pickup visual study

- **Amplifier Nugget Visual.prefab**: the existing black and white mass-pickup particle clouds plus a third prismatic cloud.
- **Amplifier Nugglet Visual.prefab**: a variant at one-third linear scale, matching the existing mass nugglet ratio.

These are visual-only prefabs. They have no pickup/reward script, colliders, rigidbody or automatic spawning. Mass + personal-multiplier rewards are intentionally left for a later gameplay pass. They do not inherit the mass pickups' mass + score reward behavior.

## Preview and tuning

Open **MASSIVE > Amplifier > Preview Amplifier Nuggets**. This animates both sizes in an isolated editor preview, without Play Mode or changes to the current scene. Pause, restart, adjust the viewing distance/background, or select either prefab.

**Select Prismatic Material** opens the shared material controls:

- **Rainbow Cycle (seconds)**: four seconds for a complete loop by default.
- **Starting Hue**, **Saturation**, **Brightness**, and **Opacity**.

Inside the prefab, **White Swirl**, **Black Swirl**, and **Prismatic Swirl** are independent native Particle Systems. Disable Prismatic Swirl to compare the monochrome layers; its Emission, Start Size, Noise, and Velocity over Lifetime modules tune density, particle size and swirling movement. All three use hierarchy scaling, so the smaller variant scales both motion and particle size consistently.

The prismatic color cycles on the GPU across existing and newly emitted particles, independently of their short lifetimes. It uses alpha compositing to preserve saturated color at crossings. It neither changes the original mass pickup materials nor allocates per-particle scripts/materials. The editor preview overrides only its own renderers' animation clock for pause/restart and deterministic captures.

Prismatic particles retain their opacity through the end of their lifetime, with no death fade. The short birth fade and shared material Opacity control remain independently available.

The live population is approximately 900 white + 900 black + 630 prismatic particles per visual at the default emission/lifetime, with hard limits of 1400 / 1400 / 1000. Large and small variants share the same materials.
