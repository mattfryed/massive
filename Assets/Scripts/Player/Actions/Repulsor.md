# Repulsor (third melee stage)

In S-8_DYNAMO-PROTOTYPE, select a player directly below **Players**.

- **Player Attack Controller → Combo Window**: both Lunge → Swipe and Swipe → Repulsor use the final 0.15 seconds, with the existing 0.15-second early-input buffer. Each transition needs a fresh press. Turn off Use Shared Combo Window to restore the legacy activation-based policy.
- **Player Melee Plasma → Repulsor — outward energy pulse**: volume/ring/off choice, width, depth, density, turbulence, breakup, flow, linger, and separate dark body, white edge, filaments and wisps. The existing stage preview controls include Repulsor and work outside Play mode.
- **Player Repulsor Feedback**: body pulse and movement recovery switches; contraction, extra expansion, vibration and settling; recovery movement and duration.
- **Player Repulsor Grid Pulse**: independent grid switch, intensity, travel radius, duration, packet width and curl.
- **RepulsorAOE child → Player Repulsor AOE**: opponent filtering and knockback. The shared PlayerAttackProfile controls reach and pulse timing.

Defaults: 0.6-second stage, release at 10/60 seconds, active expansion ending at 25/60 seconds, 10% body contraction, 15% extra peak visual size, then a 0.35-second movement recovery starting at 55%. Body feedback changes rendering only. Collider radius and wave geometry respect Player Scale Adjuster; movement reduction composes with other movement influences.

The ring and grid pulse stay at their release position. The fading volume no longer deals hits. The grid pulse displaces existing connected lines and does not draw a second grid. Repulsor has no authored dash travel. Cancellation clears its collider and body/recovery modifiers.

Validation: **MASSIVE → Player → Validate Repulsor** runs isolated Edit Mode checks. `PlayerRepulsorRecorder.StartRecording(directory)` in Play mode captures the actual three-button input sequence, then repeats at 45% speed; it produces 600 PNG frames at 60 fps for MP4 encoding. The recorder uses P1's current size, temporarily takes scripted input, and restores input/time settings on completion.
