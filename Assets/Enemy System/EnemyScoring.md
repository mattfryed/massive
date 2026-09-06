# Enemy scoring

`ScoreEconomyProfile` owns energy values, multiplier eligibility, chain effects,
repeat policy, and balance projections. `EnemyDefinition.defeatRewardKey` selects
one row; blank means no defeat reward. `EnemySpawnProfile` still controls stage
availability, timing, and population caps.

## Authoring an enemy

1. Add a reward row to the active match economy, such as `ENEMY_DYSON_DEFEAT`.
2. Set its value/unit and use **OncePerSourceToken** for defeats.
3. Select that key on the enemy definition. **Preview Economy** is an Inspector
   preview only; `GameManagerScript` / `MatchScoreService` selects the runtime asset.
4. Spawn and initialize through `EnemyDirector` / `EnemyBase.Init`. Init installs
   the shared `EnemyScoreReward` adapter on existing prefabs automatically.
5. Use `TakeDamage(amount, source, creditedPlayer)` for attributed combat damage.
   The sword hurtbox already passes its owner. Enemy projectiles retain the last
   reflecting player and damage their firing enemy when reflected back into it.

The existing Dyson definition selects `ENEMY_DYSON_DEFEAT`: initially 1 eV, using
the earning player's current multiplier, then advancing their chain. The generic
`ENEMY_DEFEAT` row remains available; its expected count is zero to avoid counting
the same planned Dyson encounters twice. Reward values are placeholders.

## Award contract

- Final blow receives credit. Earlier damage does not earn assists automatically.
- One life token is generated per `Init`, not per enable/disable or collider hit.
- Only a lethal attributed Sword, Projectile, or ShieldReflect hit can award.
- `Kill`, lifetime expiration, scene cleanup, anonymous/environmental damage,
  friendly ownership, and pseudo-player credit produce no defeat energy.
- The score service rejects closed scoring, disabled/missing rewards and duplicate
  tokens. No delayed award is queued after the match closes.
- `Died` remains available for existing cleanup consumers. `Defeated` carries the
  credited player, damage category, source-life token, and world position.
- `Init` starts a new life. Do not call it to heal an existing life. Destruction is
  still the existing lifetime behavior; this change does not add object pooling.

## Other enemy events

Future delivery ticks and dust collection should use separate event keys through
`ScoreRewardEmitter` / `ContinuousScoreRewardSource`. Keep collection quantities
independent of rendered particle counts. Explicitly supply a responsible player
only when their multiplier should apply; `BeginForTeam(teamID)` gives unmultiplied
team ticks. Stopping or transferring objective ownership must stop/rebind the source.
Reward multiplier eligibility and chain effect are independent configuration.

Do not derive rewards from the team's displayed energy tier, panel count, or
current health. Cosmetic variants can share keys; distinct difficulty variants can
have independent rows. Carrier and drone rewards, defeat rewards, and collectible
drops all contribute to an encounter's total payout and should be budgeted together.

## Validation and tests

- The economy Inspector lists enemy mappings and expected counts per reward row.
- `MASSIVE > Scoring > Validate Enemy Rewards` checks the selected economy (or the
  Inspector preview economy) for missing/duplicate keys and invalid defeat rules.
- `MASSIVE > Scoring > Run Enemy Scoring Smoke Tests` creates a temporary isolated
  Play Mode scene, exercises score outcomes and the actual Dyson prefab, then
  restores the previous play-start scene and removes the fixture. It does not save
  the open gameplay scene. Run with the Editor stopped and compilation complete.
- Balance projections are estimates, not spawn guarantees or maximum round scores.

Anomaly migration, enemy damage-to-player chain penalties, new enemy behaviors,
and the full gameplay-clock migration are separate work.
