# Match history validation — October 13, 2026

Unity 6000.0.28f1, Windows DX11. All new runtime/editor scripts compiled. The only existing gameplay script changed is `GameManagerScript`; no gameplay scene, prefab, scoring economy, package or project setting was changed for this feature.

## Automated checks

- **48 storage/report checks passed in the connected Editor.** Exact 64-bit score serialization, player/bonus/reward data, multiplier integrals, atomic replacement/backup, malformed primary and temporary-only recovery, unsupported future schemas, repeated finish callbacks, independent clocks, write failures, grouping, eligibility, CSV formatting and all nine level identities.
- **45 gameplay checks passed in a separate temporary project copy.** Real HIGGS 1v1/2v2, NOVA 2v2 bonus, inactive-cabinet abandonment and unexpected scene exit. Five starts yielded exactly three completed matches, one abandoned match and one interrupted match. Final persisted scores and statistics matched PostGame. No runtime errors/exceptions occurred during these scenarios.
- Play validation restored the copy's original edit-mode scene/dirty flag and exited. The user's live HOW-TO-PLAY-PANES scene was not replaced or saved; its unsaved flag was retained.
- All 11 runtime/editor C# files matched the validation copy when the gameplay checks finished. A subsequent editor-only change aligned the viewer's column headings with its cells; runtime code was unchanged.
- The updated viewer compiled without Console errors and its summary layout was inspected with the five real validation records loaded.
- The scoped gameplay diff passed whitespace checks using the existing Windows line-ending convention.

The gameplay fixtures expire regulation early and shorten NOVA's runtime bonus for testing. They exercise actual lifecycle, score-service, result handoff and file-writing paths without modifying authored assets. Development/test records are excluded from production high scores and the validation uses disposable save directories.

The clean project import reported existing vendor example shader include/fallback problems and an import exception in an existing generated Wireframe Shader prefab. These happened before gameplay validation; none originated from the statistics feature. They were not repaired or suppressed.

## Evidence

Storage report: `C:/Users/mattf/.codex/visualizations/2026/10/14/01a13827-277f-74a1-9c12-f0675e2d8b79/storage-validation.txt`

Gameplay report: `C:/Users/mattf/.codex/visualizations/2026/10/14/01a13827-277f-74a1-9c12-f0675e2d8b79/gameplay-validation.txt`

Example export from the isolated test run: `C:/Users/mattf/.codex/visualizations/2026/10/14/01a13827-277f-74a1-9c12-f0675e2d8b79/validation-export/`

The Unity menu commands in README.md reproduce the checks with new temporary data. A standalone player build, full-length competitive playtests and physical cabinet validation were not run.
