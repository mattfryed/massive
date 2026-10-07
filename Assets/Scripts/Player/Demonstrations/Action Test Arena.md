# Player action test arena

Open `Assets/Scenes/EXPERIMENTS/S-T_PLAYER-ACTIONS.unity` in Unity 6000.0.28f1, or use **MASSIVE > Demonstrations > Open Player Action Arena**, then press Play. No front-end navigation or Build Settings change is needed. On a fresh checkout, use the Open command once: it imports the bundled TMP Essential Resources from the installed Unity package when missing (the repository ignores that directory).

This is a copy of the composed planar arena, with its connected Players_Gameplay roster (nested canonical PlayerActor instances), grid, goals, HUD, MatchScoreService and Core / Resonance encounter. It uses the same PlayerActor as How To Play. Manual / Combo / Attack + shield use the scoring match roster. All demos uses independent non-scoring demonstration actors on the same arena.

- **All demos** (default): seven labeled demos loop simultaneously using real player inputs and abilities. Each lane shows its current phase and completed loop count.
- **Manual**: normal Rewired P1 / P3 inputs, with attack, shield, damage, death, respawn and scoring.
- **Combo**: input-driven Thrust / Sweep / Repulsor using the live engagement windows.
- **Attack + shield**: a repeatable opposing-player trial. Set center-to-center spacing and shield delay relative to the attack press (negative raises shield first). **Demo spacing / timing** derives a starting point from the live values used by How To Play.
- **Run / Stop / Repeat** control trials. **F6** switches manual / shield mode (from All demos it returns to Manual); **F1** hides the panel. The Inspector also exposes exact numeric spacing, delay, pause, timeout and center values.

Use **MASSIVE > Player Tuning** for shared attack timings, travel/reach, player size, movement and melee appearance. Shared tuning edits can persist during Play Mode and affect other scenes, as in the existing tool. The arena sliders change choreography only; they do not change gameplay stats. Slider changes apply to the next trial.

Actors move back to markers through ordinary input. They are not teleported, healed or forcibly recharged between trials. For the match-roster trials, Stop and mode switching clear injected input without cancelling an already accepted action or resetting score. Leaving All demos removes its owned actors and effects, then restores the match roster, timer and encounter. Wait for normal cooldowns/respawn. A missed block is reported as **No shield contact**, rather than a successful block. A failed movement or combo waits up to the timeout, reports a diagnostic and stops. This is a staged fixture, not an AI opponent or deterministic replay.

The isolated Action Test Economy copies the existing economy with a 1,200-second test round; production remains 120 seconds. Idle return is disabled in this scene. Random power-up spawning is disabled to avoid replacing test attacks; the normal Core / Resonance encounter remains active in match-roster modes and can affect those trials. All demos temporarily pauses the match clock and encounter and hides the match roster; demonstration actors do not score. The lab starts in 1v1. Stop and restart Play Mode for a fresh match. This scene is deliberately absent from the player-facing catalog and build list.

## Validation

**MASSIVE > Demonstrations > Validate Player Action Arena** checks scene composition, P1ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Å“P4 bindings, goals, respawn anchors, shared tuning, grid, HUD, input bootstrap and isolated scoring profile without saving the scene.

`PlayerActionTestArenaValidation.RunBatch` runs the authored scene in Play Mode and verifies normal startup, mode switching, actual shield contact, combo execution and combat energy awards. Invoke only in a disposable batch Editor (`-batchmode -projectPath ... -executeMethod PlayerActionTestArenaValidation.RunBatch -logFile ...`, without `-quit`); it exits the Editor when complete. It does not change the saved scene or profiles. A captured image is written to `Library/ActionArenaValidation.png`.


Verified locally on 2026-09-28 in Unity 6000.0.28f1: compilation, authored bindings, 1v1 startup with scoring open, manual/scripted/manual switching, real shield contact, full combo progression, and repeated normal attacks through a defeat with a banked energy award. The validator does not lower health or call score-award APIs.

Fresh-checkout dependency: the initial planar-template baseline and arena both lacked TMP Essential Resources because Assets/TextMesh Pro is ignored by Git. Importing those bundled resources is required for the existing HUD. The Open command handles this locally; no dependency versions or production prefabs change. Existing kinematic-body spawn warnings remain. Hardware/controller acceptance still needs a normal interactive Editor/cabinet pass.

Final validation after importing the bundled text resources: the same Play Mode checks passed, with no runtime exceptions in Library/ActionArenaFinalVerified.log. The rendered arena, goals, HUD text and scored energy were visually inspected. The title was shortened to ACTION LAB to keep the match timer clear. The developer control panel is centered between the team HUDs and can be hidden with F1.


## Shield experiments

- Tap: the existing opening parry window (Player Shield Ability > Duration Seconds).
- Hold: the same opening parry, followed by block-only protection against melee,
  projectiles and environmental damage, up to **3 seconds total** from the press.
  Release after the opening window ends protection immediately; a quick tap still
  completes its original window. Expiry requires release and another press to re-arm.
- In **Attack + shield**, enable **Hold shield** to test the full duration. Set a
  negative shield delay to raise the shield before the attack; around -1s tests a late,
  block-only contact. Use the normal Rewired shield input in Manual mode.
- On Player Shield Ability, **Contact Advance Fraction** (.5) and **Max Contact Delay**
  (.08s) let the existing lunge close part of the body gap before a registered parry
  resolves. The original contact reserves its strength; stun duration and knockback
  tuning are unchanged. World collisions still constrain the lunge.
- Shield Rings VFX exposes parry burst count (3), expansion (1.3), duration (.35s),
  and spacing (.035s). Holding drains ordinary rings from outside inward.
- Player Nuggets GPU exposes stun shudder amplitude (.035), parry rim padding (.055),
  and vibration amplitude (.018). Feedback is visual only and settles back to the
  continuing nugget simulation by the end of the parry/stun sequence.


## Simultaneous gallery

### Live power-up icon row

The bottom of **Simultaneous Player Demos** contains four pairs: Time Dilation,
Particle Accelerator, Decoherence and Mass Node. Each references the canonical
definition and pickup prefab, so subsequent power-up edits appear here too.

- **Natural:** ordinary spawn, idle and timeout/despawn, using the global settings'
  effective world lifetime (currently 12 seconds), followed by a one-second repeat pause.
- **Hit:** a persistent canonical PlayerActor attacks the second icon through
  scripted Sword input. The actual pickup applies the effect, shatters and shows
  the shared two-line pickup toast. The player walks back through normal input
  and waits for the equipped effect to expire before repeating. Mass Node uses
  its instant effect. Initial hit delays stagger the four toasts for readability.

The three Accelerator combat lanes sit above this row. Actors, pickup claims,
collisions, toasts and Mass Node effects are isolated/owned by their demonstration.
These non-match players do not award match score. Gallery Stop, mode changes and
scene exit remove the row's runtime objects; Run creates a fresh set.

Each pair exposes markers, hit delay and repeat pause in its **Power Up Icon Demo**
component. Gameplay values remain on the shared definitions and player tuning.
Use **MASSIVE > Demonstrations > Set Up Power-up Icon Row** to install missing
content (idempotent), and **Validate Power-up Icon Row** for repeated real claims,
natural expiration, shatter/toast observations, score/time preservation and
Stop/Run cleanup. Reports and rendered captures go to
`Library/PowerUpIconDemoValidation`.

The shared pickup animation preserves the full 3D shell throughout its lifetime.
Edges grow inward from both vertices, with stable per-edge timing/speed variation.
The inner icon starts at 50% of the shell's draw duration. Natural expiration
plays that same timeline backward, including the inner icon's size envelope.
On a confirmed hit, the inner icon immediately shrinks from its current size;
the shell's 20 triangular face outlines separate and rotate as rigid pieces,
then erase around each perimeter from a randomly chosen vertex. Per-face timing
and speed vary slightly. Hits during spawn preserve the partial drawing.

Tune shell duration, scale, acquisition distance/spin/durations and edge/face
variation in **MASSIVE > Power-up Settings > Global > Visuals**. This shared
profile drives the same canonical pickups in all scenes and in this gallery.
The legacy wire-out/fold/delay fields are retained only for prefab compatibility.
Mass Node's particle icon shares the timeline and scales already-live particles.
The row validation also exercises all four canonical prefabs' geometry, reverse
timeline, midpoint start, early acquisition, immediate inner shrink and cleanup.

Validated on 2026-10-05 in Unity 6000.0.28f1 / DX11: all 118 checks passed,
including two complete natural and hit loops per definition, real effect
application, all four wire shatters and visible toasts, continued action lanes,
unchanged match score/clock, and Stop/Run cleanup. Overview, activation, each
toast, natural despawn, later-loop and close-up animation renders were captured
and inspected. The additional checks cover all four pickup animation timelines,
fixed 3D vertices, 20 rigid face outlines, varied drawing/erasure, early hits,
and immediate inner-icon despawn inside the real melee claim callback.
No runtime Console errors; the scene's pre-existing match-spawn kinematic
velocity warnings remain. No standalone build was run.

Global power-up settings validation on 2026-10-05 passed all **157 checks** in
Unity 6000.0.28f1 / DX11. This includes the above demo/animation regression plus
percentage mixing, zero-chance exclusion, Undo/Redo, missing-system installation
and repair, shared limits and lifecycle timing, and live visual changes in two
additive scene fixtures. Ordinary toasts stay in their owning scene; toast
settings and the global cleanup delay were exercised on actual instances. Test
profiles and definitions were cloned and discarded; the saved defaults were
preserved. No runtime Console errors. The window's visuals, lifecycle, spawn
mixer, definition and prefab-link layouts were also inspected in the Editor.

Collider follow-up on 2026-10-05 passed **189 checks**, including 32 new checks
for all four pickups. The claim trigger and solid sphere now fit the intact
shell's radius and follow global Shell Scale live. Tested physics surfaces at
0.25x, 2x, 0.75x and 1x, independent inner scaling, immediate sizing on re-enable,
and colliders remaining disabled after acquisition. Removed the unused disabled
colliders from Mass Node's decorative inner particle prefab instance. The full
melee/demo regression passed with no runtime Console errors; saved user tuning
was preserved.

### Action lanes

The scene opens in **All demos** with six paired scenarios and one solo attacker:

1. Melee attack into a tap parry.
2. Melee attack into held shield after the opening parry window.
3. A solo attacker chaining Thrust, Sweep and Repulsor, then returning to repeat; no defender.
4. Melee attack into a Decoherence parry.
5. Particle Accelerator shot into a tap parry.
6. Particle Accelerator shot into held shield after the opening parry window.
7. Particle Accelerator shot into a Decoherence parry.

All actors instantiate the canonical PlayerActor and use shared live tuning. Abilities are equipped indefinitely for these fixtures through the normal power-up API. Demos wait for actual recharge, health and respawn; they do not force successful outcomes. The Decoherence cases use its existing melee/projectile behavior. Attackers return around defenders after passing through them. Each demo owns a simulation scope; melee, Repulsor, lock-on and projectile queries reject other scopes, and cross-demo body collisions are ignored. Normal match players share the null scope and retain their normal targeting.

Adjust choreography in the seven scenario assets under `Assets/Scripts/Player/Demonstrations/Action Gallery`. Gameplay values still come from shared Player Tuning and the canonical ability definitions. **Stop** removes the gallery actors; **Run** restarts all seven. **Manual**, **Combo**, and **Attack + shield** remain available in the same panel.

`PlayerDemoGalleryValidation.RunBatch` authors missing gallery content and validates the scene in an isolated batch Editor. On 2026-09-28 it passed repeated real contacts in all seven lanes, late held-shield contacts, natural death/respawn, pair isolation, score/time preservation, manual restoration and gallery restart. The held melee/projectile contacts occurred at shield ages 0.862s / 1.836s, beyond the 0.65s opening parry. Rendered overview and later-loop frames were inspected. Evidence is saved locally in `Library/DemoGalleryValidation`.

Final regression: all 28 shield checks passed. The original arena suite passed both a diagnostic and a clean rerun (shield contact, combo progression and real combat scoring). Its first run had an intermittent "Combo window was not reached" timeout; the cause was not reproduced or confirmed, and logs are retained in `Library/DemoGalleryValidation`. Final runtime compilation also passed after the gallery panel layout adjustment.

Solo-demo update validated on 2026-09-28: lane 3 has no defender and repeatedly starts Thrust, Sweep and Repulsor, including actual Repulsor pulses. All seven lanes completed at least three loops, with isolation, score/time preservation, manual restoration and a clean restart with 13 actors. Unity compiled successfully; the rendered gallery was inspected. Current evidence: `Library/SoloComboValidation`.

Original Particles now uses three editable stage prefabs in `Assets/Prefabs/Player/Attack Stages/Original Particles`. Open them from **MASSIVE > Shared Settings > Melee** or **Player Tuning > Appearance**. Thrust and Sweep have independent GPU emitters; each prefab can contain additional Particle Systems and other effects. Save prefab edits while the gallery runs to refresh all actors. Repulsor includes a disabled optional particle layer so its existing body/grid appearance is preserved. See **Editing these effects.md** beside the prefabs for preview and authoring instructions.
