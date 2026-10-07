# Mass pickup scoring

Mass Nuggets and Mass Nugglets grant two independent rewards on player-body collection:

- The existing mass restoration, capped at the player's available capacity.
- Fixed banked energy, credited once to the collecting player's contribution and their team's total through `MatchScoreService`.

The score does not depend on mass restored. A full-mass player still earns the full score. With a personal x2 and team Amplifier x4, a 100 meV nugget earns 800 meV. Neither multiplier increases restored mass.

## Shared tuning

Open **MASSIVE > Scoring > Score Economy Settings**, then expand **Rewards**:

| Reward key | Initial value | Personal multiplier | Team Amplifier | Personal charge |
| --- | --- | --- | --- | --- |
| `MASS_NUGGET` | 100 meV | Yes | Yes | None |
| `MASS_NUGGLET` | 10 meV | Yes | Yes | None |

- **Amount / Unit** sets the fixed score. Values are independent of the pickup's mass reward.
- **Enabled** disables only the score reward; mass restoration remains available.
- **Multiplier Eligible** controls the personal multiplier. **Ignore All Multipliers** bypasses both personal and team factors.
- **Chain Effect = None** and **Chain Charge = 0** preserve the initial no-growth behavior. To test growth, select an advancing effect and a positive charge. The current multiplier pays this award; new charge affects subsequent awards.
- Keep **Repeat Policy = Once Per Source Token**. The pickup also consumes itself before reward callbacks, so repeated trigger contacts cannot claim it twice.
- **Expected Occurrences Per Round** is a balance-planning input, initially zero because supply varies by level. Existing score telemetry reports collection counts and base/final energy separately for each reward key and team.

`MatterNuggetScript.scoreRewardKey` selects the row. The standard and Orbital nugget use `MASS_NUGGET`; the Mass Nugglet prefab overrides this to `MASS_NUGGLET`. Blank means mass-only. Changing `rewardMultiplier` changes mass restoration only. Runtime defaults and the Action Test Economy include the same new reward rows without replacing their other tuning.

## Collection and feedback

Only the eligible player's physical body collects these pickups. Attack hitboxes, shields, Repulsor triggers, pseudo players, eliminated players and match-input-locked players do not collect them. The score service retains its normal match-phase and attribution rules. Missing, disabled or closed score rules do not block otherwise eligible mass restoration, and rejected awards are never deferred.

Each `BeginLife`, including active pooled reuse through `Eject`, creates a new source token. Collection awards once for that life. Expiration, retirement, replacement and scene cleanup award nothing.

The existing pooled world-space toast shows **+energy SCORE** above the actual **+percentage MASS** restored. At full mass it shows only score. If no score is accepted, it shows only actual restored mass; neither reward produces a false positive label.

## Validation

**MASSIVE > Scoring > Validate Mass Pickup Rewards** uses a disposable Play Mode scene and the three real pickup prefabs. It tests body-trigger delivery, score attribution, 10:1 mass/score defaults, full and near-full mass, multipliers, duplicate rejection, pooled lifetimes, optional charge, collection exclusions and receipt text. It returns to Edit Mode and removes its temporary scene without saving the open gameplay scene. These are isolated integration checks, not a balance playtest.
