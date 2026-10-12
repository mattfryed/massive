# Amplifier gameplay controls

In **S-8_DYNAMO-PROTOTYPE**, select **Amplifier + Resonance — Spawn Cycle**. Its Inspector links directly to the Core prefab, both goals, and capture toast.

## Alternate interaction: Possession Shot

Open **MASSIVE → Amplifier → Core Interaction**. These controls edit the shared Amplifier profile across scenes. **Mode → Possession Shot** enables the alternate interaction; **Physical** restores body, melee and Particle Accelerator pushing. A Core that opts out of shared settings has the same **Possession** controls in its Inspector. Presentation-only scoreboard Cores never participate.

- Thrust with the player's body into an available Core to enter it. The distant tip of an attack cannot claim it. Sweep, Repulsor and beams do not move or enter the Core in this mode.
- Aim with the movement stick. Hold Attack, then release to fire. Charge begins after the entry animation; a held entry button can continue charging, while a released entry Thrust requires another press. Full charge defaults to **2 seconds**, approximate unobstructed coast distance **5–26 world units**, initial speed **16 units/second**. Charge adjusts drag, with the authored Core drag as the minimum; high body drag can shorten even a full shot. Damage and initial launch speed do not change with charge.
- A player has **5 seconds** after entry to shoot before being ejected. Entry uses SINGULARITY's tidal stretch/collapse (**0.38 seconds**); exit uses its fast outward re-forming curve (**0.32 seconds**). Both deform the existing body and nuggets without changing authored player/collider size. The shooter exits behind the shot where clearance permits. Controls return at the end of exit; a contest loser's stun starts then. Occupants are immune to outside damage/stun and keep identity, inventory and score. Match locks, disabling the Core/player and scene teardown restore input/physics/rendering, including an exit in progress.
- A camera-facing circle in the center of the shell identifies possession: **Light = white fill / black outline; Dark = black fill / white outline**. Its diameter is **33%** of the shell diameter. During a contest, both occupants appear side by side, **Light left / Dark right**, matching the contest bar regardless of entry order. Each circle has its own opening through all shell layers. The incoming indicator grows with entry, and the winner's indicator returns to the center when the loser is ejected. Oversized marker settings fit within the shell during contests. **Player Marker Scale**, **Player Marker Outline** and **Shell Gap Blending** remain independent dials.
- One opponent can enter with Thrust to contest control. The meter starts white/black **50/50**. Alternate Attack and Shield presses; held buttons, repeated identical presses and simultaneous presses do not count. Credited cadence defaults to **6 presses/second**, progress **0.085 per press**, contest deadline **3 seconds**. Filling a side wins immediately; otherwise the leader wins at the deadline, and a tie ejects both.
- The loser is ejected with **0.8 seconds** stun. The winner has **2.5 seconds** to aim and **0.8 seconds** of re-entry protection. Release both contest buttons before starting a fresh charge. Teammates cannot join an occupied Core.
- A launched Core uses its ordinary solid Rigidbody, mass, wall restitution, angular drag and Resonance interactions. It coasts and spins instead of following a constant-speed path, and becomes enterable only after remaining below **Settled Speed (0.15)** for **0.3 seconds**. Temporary zero-speed magnetic turns do not end flight. **Rolling Spin** adds travel-driven rotation and redirects it on wall impacts. The opening shield window redirects the shot and transfers NPC kill attribution. **Parry Range Retention (80%)** now retains that fraction of velocity and approximate coast distance. A later held block combines **35%** coast-distance retention with **50%** speed retention without transferring ownership. Neither replenishes energy.
- Only NPC hurtboxes take shot damage: **one ordinary sword hit × NPC Damage Multiplier**, once per NPC per shot, even across ricochets/parries. Players take no damage, but their bodies remain solid: a moving Core physically collides with either team, including its shooter once the exit animation finishes. Contact can push or block the Core/player according to their normal physics; it does not permit entry during flight. Solid player bodies also block the shot's forward NPC damage sweep. Surviving NPCs receive a short push, including NPCs with kinematic movement. Team-owned friendly NPCs are excluded.
- Dotted aiming is a **straight general direction**, capped at **14 units**, updating with aim and charge. It predicts no rebounds or stopping point. In SINGULARITY it follows the same straight chart direction over the folded presentation. Actual shot physics respects walls, Resonance, goal fields and SINGULARITY's chart side walls/seam; portal entry remains unavailable during a shot or possession.
- The accepting goal's team receives amplification regardless of the shooter. A maxed goal repels the shot. Encounter time continues accumulating while occupied, contested or launched, but expiry waits until that interaction finishes. **Maximum Flight Seconds (15)** starts a strong smooth brake if a flight lasts too long; it never cuts velocity to zero at a distance/time boundary.

Charge is shown by the aim dots growing in sequence from nearest to farthest while Attack is held. The entire guide stays at its maximum preview length throughout charge, giving the progression a fixed endpoint. **Charged Dot Scale** sets the fully charged size (default 2.5× each dot's resting radius); **Dot Charge Spread** controls the overlap between neighboring dots' growth (default 12% of the charge). The final dot finishes growing at full charge. All dots retain their normal distance taper, spacing and opacity. **Show Dot Charge** switches off size growth while retaining the resting aim line; **Show Trajectory** hides the whole guide. The old charge-guide toggle migrates to Show Dot Charge. Player Thrust's chevrons are independent and unchanged; Core charge uses only its own **Full Charge Seconds**. There is no charge bar or Core chevron layer.

The white-left / black-right bar appears only for a two-team contest, initially 50/50. Each credited alternating press moves the split: Light expands from the left, Dark from the right. A white border keeps the black portion visible against the field. **Meter Width / Height / Offset** apply to this contest display.

**Marker Clearance** controls the minimum opening around each possession circle. **Shell Gap Blending** (formerly Marker Flow Deflection; saved values are retained) merges these openings into the animated noise's empty regions, forming broad organic joins as the pattern moves. It does not push or swirl the noise coordinates radially around each circle. Both openings are combined symmetrically before joining the noise, so neither player takes priority. All seven Neutral Shell slices respect the same projected clearance. The circles are rendered within the shell shader, with no separate badge objects or overlapping ribbons.

The white Light possession indicator is a symbol, not a recoloring of the player. The actual Light actor retains its black body, white outline and white floating particles. Dark retains its black body and outlined internal particles. The contest recorder uses the unmodified actor palettes and checks both before entry, plus Light after ejection.

Aim-line dots progressively shrink from **Dot Radius** at the Core to **Final Dot Scale** at the far end (default 25% of the first dot). These shared controls are under **MASSIVE → Amplifier → Core Interaction → Aim guide**. A final scale of 1 restores uniform dots; 0 tapers to zero. The charge-size multiplier applies on top of this distance taper. Release, ejection and contests hide the guide; a new possession begins with uncharged dots.

The window also exposes dot spacing/size, ejection recoil, winner timeout and all contest/defence values. Playback previews remain separate from this gameplay mode.

Validation: **MASSIVE → Amplifier → Validate Possession and Shots** runs disposable Play Mode fixtures using normal scripted player input, real physics contacts, goals, hurtboxes and Resonance geometry. It restores the previous Play Mode start scene and removes its temporary scene. It does not save runtime changes into authored levels.

## Core size and motion

Use **Edit Core Size / Physics (spawn prefab)**. Changing the prefab affects subsequent managed spawns. During Play Mode, select the live Core to tune that instance.

- **Overall scale** multiplies the existing authored size. It scales the shell, body collider, spawn clearance and grid influence radius together; mass remains an independent dial.
- **Mass**, **Linear drag**, **Angular drag**, **Maximum speed**, **Wall bounce** and **Attack impulse** control motion. Maximum speed is the planar speed limit; zero means unlimited. Attack impulse and mass together determine launch speed.
- Defaults preserve the previous mass 3, linear drag 0.42 and angular drag 0.6. Maximum speed starts at 25, and wall restitution at 0.85.
- Wall response is frictionless and reflects incoming motion even below Unity's normal bounce threshold, preserving travel along the wall.

## Attack and Particle Accelerator contact

- Thrust and Sweep move the Core through their live melee hitboxes. Thrust also retains its body-contact launch and solid-impact stop. Sweep uses the current arcing weapon direction and continues its attack after contact.
- Repulsor pushes outward from its released pulse origin, including Cores behind the player. Each Sweep or Repulsor can launch a given Core once; lingering visuals do not keep applying force. **Attack impulse** and Core **Mass** determine the melee launch.
- The sustained Particle Accelerator pushes along the local beam direction while its collision trace touches the Core. Walls and nearer blockers stop that force; the fading/retracting beam does not push. Tune **Amplifier Push Force** in **MASSIVE → Power-up Settings → Particle Accelerator** (or its shared definition). This is independent of damage; zero disables beam pushing. Core mass, drag and maximum speed still apply.
- Spawn/despawn, captured, disabled, presentation-only and external-transit Cores reject attack force.
- **MASSIVE → Amplifier → Validate Attack and Beam Contacts** runs isolated Play Mode contact/force regression checks and returns to Edit Mode. It uses temporary fixture profiles and direct stage entry to exercise hit delivery; it is not a normal-input competitive match test.

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
