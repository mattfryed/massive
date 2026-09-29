# Player action test arena

Open `Assets/Scenes/S-T_PLAYER-ACTIONS.unity` in Unity 6000.0.28f1, or use **MASSIVE > Demonstrations > Open Player Action Arena**, then press Play. No front-end navigation or Build Settings change is needed. On a fresh checkout, use the Open command once: it imports the bundled TMP Essential Resources from the installed Unity package when missing (the repository ignores that directory).

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
