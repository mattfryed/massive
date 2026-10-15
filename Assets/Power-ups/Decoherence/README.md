# Decoherence

Hold Shield and move to paint a lingering disruption field on the arena. The
Particle Accelerator's shared energy-ring prefab displays remaining charge.
While active, the ring splits with the player's two body copies, using the same
activation shake, separation, phase pulse and independent jitter. Each copy shows
the same remaining charge; both rejoin with the body on release or exhaustion.
Each fresh acquisition starts full. The original melee phase-through and
attacker stun remain available while the player is actively holding Shield.

## Charge and brush

`PU_Decoherence.asset` owns the settings, also exposed in MASSIVE > Power-ups >
Power-up Settings:

- Full Meter Paint Seconds: **6 seconds** of continuous painting.
- Empty To Full Refill Seconds: **4 seconds**, starting immediately when idle.
- Minimum Activation Charge: **15%**. Release and press again at or above this
  threshold; neither full refill nor the ordinary Shield cooldown is required.
- Full-charge radius: **2.2** at player size 1. New brush radius is this value
  multiplied by player size and remaining charge. Radius shrinks continuously
  while painting; damage rate remains constant inside the footprint.
- Activation Ease Seconds: **0.24 seconds**. Each fresh activation starts at
  the original player body outline and eases out to the live charge radius.
  Damage follows the growing paint, so it cannot reach the full area early.
  A low-charge target smaller than the body eases inward instead. Previously
  deposited paint keeps its original radius until its final contraction.
- Paint Lifetime Seconds: **4 seconds per deposited area**.
- Paint Recede Seconds: **0.24 seconds**, within that lifetime. Every patch
  smoothly shrinks to zero, including the last remaining circle.
- Movement while painting: **90%** of normal.

Charge replaces the ordinary Shield maximum hold and recovery for this power-up.
Release immediately stops painting and active protection. Exhaustion also stops
painting and requires a release/new press; holding an empty meter never creates
repeating micro-bursts. The ordinary Shield is unchanged when Decoherence is not
equipped. Refill continues while existing paint remains dangerous.

## Painted ground

Brush deposits record world position, radius and expiry. Moving drags that
changing radius across the arena. Existing deposits retain their deposited size until their final contraction;
repainting refreshes only the new brush footprint. The visible boundary is the
union of exposed circular arcs, with interior overlap edges removed. Two faint,
displaced copies of the real arena grid show the occupied area. Main and ghost outlines retain their opacity
through the final contraction; the boundary recedes through geometry alone.
Damage and grid echoes use the same shrinking footprint. Very small outlines
also shrink their line thickness and ghost displacement to converge to zero.

The exterior edge undulates with smooth noise (**0.045** displacement at size 1).
**Three ghost outlines** use independent seeded patterns, **0.13** displacement
and **24%** of the main outline's opacity. Noise frequency (**1.35**), speed
(**0.45**), displacement, copy count and opacity are adjustable on the definition.
Noise samples world positions so the merged arcs remain joined in every copy.
It is cosmetic: it does not make the damage footprint jitter. Animation follows
the field's simulation clock, and visual randomness does not consume gameplay
random state.

The grid echoes borrow the arena's live mesh, GPU position buffer and curve,
attraction/pulse, oval and presentation settings. They do not change simulation
or grid physics. A per-field mask clips both copies to the union of surviving
paint deposits, with a small inset below the noisy border. They remain on old
paint after release and disappear as that paint expires. Defaults: **0.16 opacity**,
**0.18 displacement** at player size 1, **0.4 animation speed**. These settings
and the explicitly referenced echo material live on the Decoherence definition.
Its renderer and mask buffer are released when the last paint expires or the
owner is cleared/disabled, even if the pickup ended earlier.

## Sustained-damage audio

All enemies share the same feedback policy in `EnemyBase` and `AudioSystem`.
Damage delivery is separate from source/scoring: ordinary impacts use the
existing `TakeDamage` default, while continuous producers pass
`EnemyDamageDelivery.Sustained`. Both Decoherence and Particle Accelerator use
that mode. New enemies inherit it automatically, using their own hit cue.
Future sustained attacks opt into the same mode rather than implementing a
per-enemy sound exception.

First sustained contact can play immediately. Shared AudioSystem defaults limit
repeats to **0.4 seconds per enemy**, and **0.12 seconds between sustained hit
sounds across the entire crowd**. Overlapping sources share the target's timer.
Actual damage and damage events continue every tick. Ordinary impacts and death
bypass the sustained gate and retain the normal cue/mixer/pool rules. Missing
cues and muted/rejected playback do not consume the sustained budget. Enemy
initialization resets per-life timing; scene silence clears the global gate.

Leaving or releasing does not move or erase existing paint. An enemy or opposing
player can enter it afterward and still take damage. All overlapping deposits
and strokes from one owner deal damage once per target per frame. Different
players remain independent damage sources. Solid scenery blocks contact from
each deposit; long teleport jumps do not paint a connecting bridge. Geometry is
bounded to 512 deposits per owner, with adaptive spacing and cached boundaries.

Pickup expiry/replacement stops new painting and hides the charge meter, but
existing paint keeps damaging and recedes over its remaining lifetime. Acquiring
Decoherence again resumes the same painted ground without stacking damage.
Death, disabling the actor, match input lock, or explicit demo cleanup removes
that owner's paint. Stun stops new painting; existing ground remains until its
normal expiry. Simulation scopes, scene ownership, team filtering and
target pause/spawn/transport exclusions still apply.

## Damage and timing

Seconds Per Sword Hit defaults to **1 second**:

`enemy damage / second = ordinary sword-hit damage / seconds per sword hit`

`remaining exposure = remaining health / ordinary sword-hit damage × seconds per sword hit`

Hurtbox sword-damage overrides are respected. Multiple hurtboxes and overlapping
paint do not multiply the rate. The highest eligible hurtbox damage is used once
per enemy. Charged attack and combo modifiers do not change this rate.

| Canonical enemy | Full-health exposure at the initial setting |
| --- | --- |
| Drone, Ranged Drone, Seeker | 1 second |
| Particle Beam Turret | 3 seconds |
| Carrier | 6 seconds |
| Repulsor Dyson | 20 seconds |

These are accumulated contact times, excluding time outside the painted ground.
Damage does not heal when a target leaves. The pickup still lasts 16 seconds.
Defeats use the ordinary scoring service with source and owner attribution.

Entry disruption lasts **0.25 seconds**, at most once per enemy per deposited
stroke, with a shared **2-second** recovery across owners. Enemy mesh fragments
settle after exposure ends. Opponents take **0.04 mass/second**, independently
of enemy erosion, without aura stun or mass transfer. Ordinary shields protect
them; friendly actors are excluded.

## Demonstration and validation

The Player Action Lab's **Decoherence** mode shows moving brush strokes, lingering
damage, charge/refill readouts, normal-health Drones and a Carrier, and opponent
shield protection. It uses canonical definitions and ordinary input.

Run **MASSIVE > Power-ups > Validate Decoherence Field** from Edit Mode for the
disposable-scene suite: charge drain/refill, minimum activation, held exhaustion,
shared ring, activation growth/contact, partial-charge restarts, shrinking brush,
independent expiry, merged outlines, noise and each ghost's rendered contribution, persistent
and non-stacking damage, real health ratios, walls, scopes, defenses, scoring,
ordinary Shield regression and cleanup. It also renders all six enemy prefabs.
Reports and images: `Library/DecoherenceFieldValidation/`.

Charge-ring follow-up: 124 field/audio checks passed, including split-body pose
alignment, matching fractional charge on both copies, 32-dot capacity, release,
exhaustion, unequip and switching to Particle Beam. Close-up renders are
`charge-ring-split.png` and `charge-ring-rejoined.png` in that report folder.

Run **MASSIVE > Demonstrations > Validate Decoherence Demo** for the saved lab's
repeated loops, lingering damage, mode switching, score/time isolation and cleanup.
Reports and images: `Library/DecoherenceDemoValidation/`.
Full-match balance, dense four-player effects and cabinet performance still need
playtesting.

Validated on 2026-10-14 in Unity 6000.0.28f1: 115 field/audio checks and 28 lab checks passed. No runtime errors were recorded. Coverage includes overlapping sustained sources, different enemy types/cues, damage/event preservation, ordinary impacts/death, mute, new-life reset, missing audio, real grid borrowing and echo cleanup. The merged outline, shared ring and displaced grid echoes were inspected in the Game view. No standalone build was run.

A targeted GPU render check found zero visible pixels outside surviving painted
ground in its close-up capture; 21,858 pixels changed when only the grid echo's
noise time advanced. The capture also includes the player outline inside the paint.
Evidence: `Library/DecoherenceDemoValidation/grid-echo-clipping-check.txt` and
`grid-echo-isolated.png`.

Lifetime/core follow-up (2026-10-15): Unity compiled and **154 field/audio checks
passed**, with no runtime errors. Paint survives natural/forced expiry and pickup
replacement, continues damaging at its original location, and releases its
presentation after its last patch expires. Reacquisition shares the same field
without stacking damage or interfering with the replacement beam's ring. Death,
actor disable and explicit cleanup still remove lingering paint. A canonical
Dyson at 0.12 health remains hittable through its core after all panels shatter;
core-only paint contact finishes it and credits exactly one defeat. Its attack
radius continues to follow the panels.

The final contraction was checked at full, half, near-zero and expired radius,
including the matching damage footprint. Render captures `recede-full.png`,
`recede-half.png`, `recede-tip.png` and `recede-empty.png` were visually inspected:
the last main/ghost outlines contract to zero without fading. The disposable
fixture emitted missing-AudioListener warnings outside its audio-specific check;
no standalone build was run.
