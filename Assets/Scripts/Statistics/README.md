# Match history and playcounts

Open **MASSIVE > Statistics > Match History** to inspect saved matches. The window defaults to **Production**. Choose **Development** for ordinary Editor playtests or **Choose folder** to inspect files copied from a cabinet. Filter by level/mode, expand a match for scores and player statistics, and use **Export CSVs** to export all filtered records into a new timestamped folder.

## What is recorded

A play starts when the real match reaches regulation after roster spawning and countdown. Only the nine currently cataloged main gameplay levels, in their own gameplay scenes, are recorded. Instructions, attract demos, action labs, and failed startup do not count. Every new match gets a unique ID. Multiple matches in one application launch share an application-run ID; this does not identify individual people.

The record includes mode, permanent level identity, level title/number/scene at play time, UTC timestamps, application/build version, score rules/version/settings fingerprint, status, exact integer meV team and individual scores, regulation/bonus breakdowns, kills, deaths, enemy kills, distance, average multiplier integrals, and the existing reward-source telemetry. No new Core-capture or multiplier-cap measurements are collected.

- **Started** includes every recorded play, including the following outcomes.
- **Completed** means final scoring resolved; the final snapshot is saved before gameplay teardown and PostGame navigation.
- **Abandoned** means the inactive-cabinet path returned to Attract.
- **Interrupted** means application quit, unexpected scene exit, or recovery of an unfinished previous run.
- **In progress** is a currently unfinished record. Partial scores never contribute to win/tie totals or best completed scores.

**Elapsed time** runs from regulation start through final resolution. **Active regulation time** excludes stopped world time, scoring-closed transitions and application suspension. **Active bonus time** counts scoring-active bonus play even while NOVA deliberately freezes the arena. Active times measure real play seconds, independent of slow motion. Configured regulation length and the game's existing bonus-clock reading are retained separately.

## Storage and recovery

Files live beneath `Application.persistentDataPath/MatchHistory/`:

- `Production`: non-development standalone player matches.
- `Development`: Editor and development-player matches.
- `Validation`: ordinary batch-mode runs. The dedicated validation tools use unique temporary directories instead.

Each match is one schema-versioned, checksummed JSON envelope containing a value-only result snapshot. Start and completion are flushed synchronously. Checkpoints serialize on the main thread and write in the background approximately every 15 elapsed seconds. A flushed temporary file replaces the committed file atomically, retaining one backup. A final write waits for its checkpoint writer before committing.

Reading chooses the newest valid revision across the primary, temporary file and backup, counting the match once. Unsupported future schemas are reported and preserved. On application startup, unfinished previous-run records become interrupted; original candidates are retained with `.recovered-…` suffixes. Recovery uses the last checkpoint's time/stats, so an abrupt power loss can omit the final interval (normally up to 15 seconds, longer if storage failed). It does not claim the restart time was the match's end time. Write failures issue a warning and allow gameplay to continue.

There is no automatic deletion or history reset. JSON retains exact 64-bit scores. CSV exports also retain all score digits; when importing very large scores into spreadsheet software, choose **Text** for meV columns to avoid that software's numeric rounding. A checksum detects accidental corruption; it is not anti-cheat protection.

## Reports and high-score preparation

The four CSV tables are `level-summary.csv`, `matches.csv`, `players.csv`, and `rewards.csv`. The last three share `match_id`. Player slots are labeled 1–4 in exports, while stored runtime IDs remain 0–3. Team totals remain authoritative and may include unattributed team awards. The summary groups by permanent level ID and mode, retaining lifetime counts across renamed/reordered levels and scoring revisions; it is not a cross-version leaderboard.

Only completed production matches with standard settings are eligible for later high scores. Development/test, partial, altered-duration and changed-settings matches remain in history but are ineligible. A leaderboard key includes level ID, mode, scoring rules/version, full scoring-settings fingerprint and configured round length. No public high-score screen or initials entry is included yet.

`Resources/MatchHistoryConfiguration.asset` references the canonical catalog and standard economy. **MASSIVE > Statistics > Refresh Configuration** bakes level asset GUIDs and the standard economy fingerprint. This also runs before player builds. Refresh after adding levels or changing the canonical economy when inspecting eligibility in the Editor. Renames/reordering preserve GUIDs; recreating a level asset creates a new identity. Bump the scoring rules version when gameplay/balance changes outside the economy asset should start a new leaderboard era.

## Validation

- **Validate Storage and Reports** exercises precision, atomic replacement, corrupt/temporary/backup recovery, future-version preservation, duplicate completion, independent timing, errors, grouping, eligibility and CSV export using disposable files.
- **Validate Gameplay Recording** starts from Edit Mode and exercises real HIGGS 1v1/2v2, NOVA 2v2 bonus, inactive-cabinet abandonment and unexpected scene exit. It shortens runtime fixtures without saving scene/profile changes, redirects records into a temporary folder, exits Play Mode and verifies restoration of the original scene and dirty flag. The report path is returned in `MatchHistoryPlayValidation.Status`.

Runtime integration is owned by the existing `GameManagerScript`; no extra persistent GameObject or per-scene wiring is required.
