# Repulsor (third melee stage)

In S-8_DYNAMO-PROTOTYPE, select a player directly below **Players**.

- **Player Attack Controller → Combo Window**: both Lunge → Swipe and Swipe → Repulsor use the final 0.15 seconds, with the existing 0.15-second early-input buffer. Each transition needs a fresh press. Turn off Use Shared Combo Window to restore the legacy activation-based policy.
- **MASSIVE → Player → Melee Visual Preview → 3 — Repulsor**: defaults to **Seismic detonation**, with the previous filament burst and plasma ring retained for comparison. The controls directly below the stage selector separate inward charge, source flash, seismic light streak, pressure front, curved blast shell, internal discharges, vapor wake, fine ejecta, and branching discharges. Radiance is independent of gas opacity. Depth, turbulence, internal motion, ejecta density/intensity/length and aftermath are adjustable. The preview works outside Play mode; Stop clears it.
- **Player Repulsor Feedback**: body pulse and movement recovery switches; contraction, extra expansion, vibration and settling; recovery movement and duration.
- **Player Repulsor Grid Pulse**: independent grid switch, intensity, travel radius, duration, packet width and curl.
- **RepulsorAOE child → Player Repulsor AOE**: opponent filtering and knockback. The shared PlayerAttackProfile controls reach and pulse timing.

## Size and enemy damage

Open **MASSIVE → Player → Melee Visual Preview → 3 — Repulsor → Repulsor — size & damage (shared profile)**. These controls remain visible with the artwork switched off.

- **Overall scale** (default 1): scales the maximum hit radius, shockwave dimensions, ejecta and grid response together. It composes with Player Size; it does not resize the player, change attack timing or multiply damage. The expanding wave still begins on the player's outline.
- **Base hit radius** (default 3): radius at Player Size 1 and Overall Scale 1. Maximum radius = base radius × overall scale × player size, with the release outline as a minimum. P1 at size 0.5 therefore reaches 1.5 world units with default settings.
- **Enemy damage** (default 1): flat NPC health damage once per enemy per pulse. Zero disables NPC damage. Player-versus-player knockback, stun and optional mass loss remain separate settings on RepulsorAOE.
- **Show hit area**: with Scene-view Gizmos enabled, selecting the player or RepulsorAOE shows a cyan maximum-radius guide and an amber live-radius guide during activation.

Scale, base radius and enemy damage belong to the shared attack profile, so edits affect every player using that profile. Edit preview is visual only. Released pulses retain their captured reach, damage and dimensions.

NPC contacts resolve through EnemyHurtbox, including child hurtboxes. Weapon and sensor colliders cannot count as enemy bodies. Dead, paused, disabled and same-team enemies are skipped; multiple hurtboxes cannot multiply damage. Lethal hits use the normal attributed enemy-defeat/scoring path with the Repulsor damage source. The active sphere switches off at the end of the damage window; lingering light has no hitbox.

Defaults: 0.6-second stage, release at 10/60 seconds, active expansion ending at 25/60 seconds, 10% body contraction, 15% extra peak visual size, then a 0.35-second movement recovery starting at 55%. Body feedback changes rendering only. Collider radius and wave geometry respect Player Scale Adjuster; movement reduction composes with other movement influences.

The charge follows the player until release. The blast and grid pulse then stay centered on their release position. The active pressure crest follows the actual collider radius. After the active phase, residue drifts outward by up to 6.5% while eroding; it no longer deals hits. Fine ejecta have independent ages and velocities and remain within about 1.1 times the blast footprint. The new treatment uses a compact source discharge, a fast planar front and a slower ellipsoidal gas shell with large turbulent openings. Light emission is separate from optical attenuation, preserving transparent gas instead of making every lit layer opaque. The grid pulse displaces existing connected lines and does not draw a second grid. Repulsor has no authored dash travel. Cancellation clears its collider and body/recovery modifiers.

The seismic shader is a procedural 64-step bounded volume for the Built-in renderer (shader model 4.0); it requires no external VFX assets or full-screen post-processing. Its visual metaphor is cinematic, not a physical simulation of an EMP or supernova. Multi-player target-hardware performance has not been benchmarked.

Validation: **MASSIVE → Player → Validate Repulsor** runs isolated Edit Mode checks. `PlayerRepulsorRecorder.StartRecording(directory)` in Play mode captures the actual three-button input sequence, then repeats at 45% speed; it produces 600 PNG frames at 60 fps for MP4 encoding. The recorder uses P1's current size, temporarily takes scripted input, and restores input/time settings on completion.
