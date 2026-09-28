# Player Tuning

Open **MASSIVE > Player Tuning** (also **MASSIVE > Player > Player Tuning**).

The window edits the existing shared attack profile, melee appearance and P1–P4 size/reversal asset, plus `Resources/PlayerTuningProfile.asset` for movement, combo input, lunge assistance and Repulsor impact. Values persist across scenes and in builds. Edits made during Play Mode are saved. Each participating controller has **Use Shared Project Settings** to restore its unchanged local fallback. How To Play demonstrations explicitly use Shared Gameplay tuning, including slot size and reversal. Legacy pseudo players with Automatic tuning and isolated preview fixtures retain local settings. Local tuning remains an explicit opt-out.

## Attack timeline

- Select Thrust, Sweep or Repulsor.
- Orange is the damage-active window, grey is windup/recovery. Drag white handles to change hit start/end; the upper handle changes full stage duration. Numeric fields remain available. The frame ruler uses the selected stage's authored frame rate; gameplay timing remains in seconds.
- The teal engagement bar is the window for accepting the next attack press. Drag either end or move the bar. Editing it enables that stage's custom window. Disable **Custom engagement window** to use the shared fallback behavior again.
- **Early input buffer** accepts a recent press just before the engagement window opens.
- Enable **Allow early handoff**, then set **Earliest next stage**, or drag the violet handoff handle beneath the engagement bar. A queued press waits until this point; a press accepted later transitions on the next update. An early handoff can cut current movement and damage short. No queued press means the current attack completes normally. Each transition consumes one press, and only one gameplay stage is active at a time.
- Violet tails show harmless visual aftermath. Existing emitted trails continue fading in world space at a handoff. Thrust aftermath starts at handoff/end; Sweep and Repulsor aftermath normally starts when damage closes. These lanes represent the corresponding volumetric/asset treatments; disabling or selecting a different treatment can remove them visually.
- The combo diagram assumes the next press is queued as soon as its engagement window opens; actual input can make a later transition. The dimmed portion after an early handoff is skipped in that example. The input window is not a mandatory delay.

Existing stages initially retain their current timing and sequential handoff. The window warns if the authored hit window extends past the stage; **Fit hit window to stage** makes the authored values match the game's existing clamping behavior. Changing duration does not silently stretch all other timings.

## Preview versus gameplay

**Play preview / Pause / Stop**, **Loop**, the scrubber and playback speed stay above the scrolling controls. Playback speed affects preview only. The diagram is schematic and does not move players or enable damage. Select **Selected stage**, choose a scene player and enable **Show stage VFX** to sample the real effect in Edit Mode, including its tail. It requires that player's effective attack profile to match the edited one. Stop, closing the window and entering Play Mode end this preview. Use normal player controls in Play Mode for gameplay testing.

The top-down diagram shows the selected player's body, weapon reach and authored travel. Without a scene player, it shows a size-based reference body; weapon reach requires a real collider. Collision and aim assistance can shorten travel. **Size & reach > Scene view reach overlay** adds body/reach guides around the chosen player without changing colliders.

## Other panels

- **Movement:** propulsion, speed ceiling, lateral/release/reverse braking, deadzone, movement during attacks/shields, aim assistance, swipe rotation curve. Propulsion remains a force; body mass still affects acceleration. The reversal cone's two handles are draggable, with P1–P4 selection and independent momentum retention.
- **Size & reach:** existing global player scale and its movement/reach relationships; scene overlay and shared/local status.
- **Impact & recovery:** Repulsor size, NPC damage, player knockback/stun/mass loss, body pulse, movement recovery and grid response. Target filters and scene bindings remain local.
- **Appearance:** existing shared melee layer controls.

## Comparison and persistence

**Store A/B** holds a temporary session comparison; **Apply A/B** changes the shared settings and supports Undo. **Save snapshot** creates a reusable asset with embedded copies of all four profiles; assign it to the snapshot field and use **Apply** to restore its values. Saved snapshots preserve referenced materials/prefabs across reloads. Applying a snapshot writes its attack values into the currently selected live attack profile. A/B slots clear when the window closes or scripts reload. Undo/Redo includes timeline edits, numeric controls and snapshot application.

No gameplay scene or player prefab needs new wiring. The new shared profile is seeded once from P1 in the reusable Players prefab; that matches the current Dynamo baseline.

## Validation

**MASSIVE > Player > Validate Player Tuning** runs isolated checks for input/handoff timing, press consumption, full unchained travel, cancellation, cooldown, bounds, shared settings in successive scenes and local overrides. Existing Repulsor/shared-settings checks remain available. `PlayerTuningPlayValidationRunner` is an editor-only runtime fixture for actual coroutine hitbox gating and early combo transitions; it does not control the scene's players.
