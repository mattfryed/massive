# Pattern-library validation — 2026-10-14

## Synth-impact replacement and 345 Hz addition

This later pass supersedes the original 0.75-second audio and silent-345 statements in the historical results below.

- Replaced the 404/776/1660/4129 WAV contents with the approved synth-impact auditions. SHA-256 comparisons confirm exact copies; all four existing clip `.meta` files are byte-identical to their pre-replacement versions, retaining GUIDs and import settings. Existing cue settings/references were not changed.
- Added the matching 345 Hz **Field Pulse**, its PCM importer and `AC_Resonance_345Hz_Impact` cue. Assigned it to the 345 spawn definition and the original Option A/B definitions. Unity serialized the already-default `untaperedStart`/`untaperedEnd` flags as false; no authored geometry, collision or taper values changed.
- All five clips import as stereo 48 kHz, 0.72-second audio (138,284 bytes each). The new cue uses the existing screen-space SFX routing, volume 0.8, fixed pitch, 0.12-second cooldown and three-voice limit. The four approved clips remain unprocessed copies of the audition masters.
- Updated validation expectations for the new duration and required 345 cue. **222 Edit Mode library checks passed**, including all five sound assignments, both render modes, geometry/interaction regressions and cycle order.
- **63 Play Mode checks passed** using the existing disposable fixture: real Core physics/contact for every frequency, exactly one correct pooled sound per impact, duplicate suppression, silent player crossings, shared scene mute, disabled-pattern gating, five actual captures and a loop back to 345.
- Unity compiled the editor validation changes; the final Console query returned **zero warnings/errors**. No production runtime scripts, audio database, event IDs, scene assets or prefabs were edited by this audio replacement.
- Returned to Edit Mode in `Assets/Scenes/S-0_ATTRACT.unity`, clean as before the check, with `runInBackground` false and no fixture scene remaining. No runtime changes were saved.

**Ready with limitations:** exact asset identity/imports and runtime routing are verified. The user approved the four synth auditions; the new 345 audition is supplied for listening. Full-match mix balance, physical speaker output and a player build were not checked. Pre-replacement audio backups and audition MP4s are outside the Unity project in the conversation's `resonance-audio/synth-impact-v2` output folder.

## Completed

- Unity 6000.0.28f1 compiled the runtime additions, installer and final validation helpers successfully, targeting StandaloneWindows64 in the Editor.
- **217 new Edit Mode checks passed:** approved 8/12/8/14 arc counts; both render modes; particle budgets; collision generation; non-colliding full-pattern ghosts; open endpoints; retained closed-path behavior; wall anchoring; 776 half-length segments; five-entry order; repeated append safety; sound references and 0.75-second durations.
- Existing suites passed: **105** prototype checks, **22** particle checks, **31** contact-visual checks, **31** manifestation checks, **16** local-birth checks, **51** spawn-rule checks. Total completed: **473**.
- Unity Console query after these checks returned zero warnings/errors. Compilation logs included existing warnings in unrelated NOVA/gallery tooling; those files were not changed here.
- SHA-256 checks confirm all four imported WAV files are byte-identical to the approved short impact versions. Each is 144,044 bytes; total new audio is approximately 563 KiB.
- The shared encounter prefab and seven standalone scene coordinators contain 345 → 404 → 776 → 1660 → 4129. Scene edits are limited to appending four entries; unrelated existing scene changes were preserved.
- The existing 345 definition, encounter prefab and original Option A/B assets were not modified. The AudioEventId enum and audio database already had unrelated user changes and were not edited by this feature.

## Play Mode validation completed

- **54 runtime checks passed** in `ResonancePatternLibraryPlayValidation`: ordered 345 → 404 → 776 → 1660 → 4129 → 345; six safely formed pairs and five real goal captures. Combined with the earlier suites: **527 checks passed**.
- Every pattern formed before enabling interactions and spawning its Core. Each actual spawn point passed the neutral-territory/clearance policy.
- Real Core movement and physics produced a contact/turnaround on each pattern. Each new pattern played its correct 0.75-second clip on exactly one pooled AudioSource. Duplicate contact callbacks were suppressed, player-passage callbacks added no Core sound, shared scene silence was honored, and disabled interaction blocked both impact counts and audio.
- Original 345 retained its previous silent-impact behavior.
- Goal capture disabled interactions and began dissolution; the fifth capture advanced the order back to 345.
- The final Console query returned **zero warnings/errors**. No production runtime code, scene asset or prefab was changed during the validation handoff.

### Test setup and initial fixture failures

The temporary runtime area used the real shared encounter prefab, pattern prefabs, gameplay Core, score service, goals and AudioSystem, with no GameManager or match-history session. Only fixture delays/formation times were shortened on runtime copies. Core physics was released via its existing possession-mode physics path before prescribing an incoming trajectory; player input/possession aiming itself was not tested here.

Initial runs exposed test-fixture assumptions, not changes required in gameplay: the instructions screen retained its scene-wide silence while disabled; Unity did not advance game frames in the background; the newer possession system parks unclaimed Cores kinematically; and a magnetic release continuing after the first impact caused a second collision during the player-only audio assertion. The helper now releases the inactive instructions-screen mute, temporarily enables background simulation, wakes Core physics, and ends the residual magnetic turn before parking it. Those setup failures were preserved and followed by the complete successful 54-check run; no failing gameplay check was disabled.

### Cleanup and handoff

After the user authorized coordination, **Create cycling how-to-play screen** yielded the editor and was notified when validation finished. Unity was confirmed back in Edit Mode in `Assets/Scenes/S-0_HOW-TO-PLAY-PANES.unity`, with its original unsaved state intact, `runInBackground` restored to false, and the temporary runtime scene no longer loaded. No authoring scene was replaced or saved.

## Status and limitations

**Ready with limitations for gameplay review.** Implementation, asset checks and focused runtime integration passed. Visual inspection and subjective listening in a full gameplay scene remain for tuning; correct source playback was verified, not the sound heard through the physical speakers. No player build, full-match balance test, performance profiling or every-level runtime traversal has been run.
