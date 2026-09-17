# Joystick reversal test

Open **MASSIVE → Player → Joystick Reversal Controls**, or select a player root
and edit **Player Movement Reversal**. It is installed on all four Dynamo prototype
roster players, enabled with a **100° backward cone** and **0% retained momentum**.
Other scenes keep their existing movement until this component is added.

- **Backward Cone** is the full angular width, centered opposite the current
  horizontal velocity. 100° means ±50° around directly backward. It is unrelated
  to the attack's aim direction. Select the component to see the cone in Scene view;
  when stationary/in Edit mode the guide uses the root's right direction.
- **Momentum Retained** is the fraction of horizontal velocity kept when a fresh
  stick change enters the cone. 0% removes the old drift and lets ordinary joystick
  acceleration start the return immediately. 50% keeps half the drift once;
  100% is the existing movement behavior. Holding reverse does not repeatedly
  multiply the velocity. Ordinary traction/braking continues afterward.
- **Enable Joystick Reversal** switches the test off without changing your existing
  movement settings. Partial stick strength still controls normal acceleration.

Vertical velocity, speed limits, acceleration, attack travel and timing remain
unchanged. This is an immediate reduction of opposing drift, not an instantaneous
teleport or snap to full reverse speed. It works with the player size adjuster.

The controller excludes attack input/stages, shields, accelerator charge/telegraph,
stun, enemy-hit recoil, spawning and locked/suppressed input. There is a 0.2-second
recovery after those states. Accelerator recoil, repulsor knockback, NOVA ejection
and active storm currents explicitly protect their motion too. A fresh eligible
stick sample is needed after protection ends. An unchanged held stick cannot
trigger assistance merely because a collision reverses velocity.

Future abilities that inject momentum should call
`player.ProtectActionMomentum(seconds)` before their impulse and expose any
sustained movement-action state through the existing action gating. Rigidbody
velocity alone cannot identify the source of arbitrary future external forces.

Edit-mode settings save with the scene; Play-mode changes are temporary.
Run **MASSIVE → Player → Validate Joystick Reversal** for isolated checks of cone
boundaries, retention, input transitions and momentum protection.

Validation: 67 isolated editor checks passed. In Dynamo through the normal input
path, a 5.44-unit/s forward drift became 0 at 0% retention and 2.72 at 50%, with
one adjustment per reversal. 100% applied no adjustment. An observed attack stage
with a reverse-stick input also applied no adjustment. No compile/runtime errors
were reported during these checks.
