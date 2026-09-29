# MASSIVE project context â€” resonance task, 2026-09-06

Confirmed live project: `C:/Users/mattf/Documents/GitHub/massive`; inspected commit `02f5e1872c8d7cbd3784a17c1d74ebe3dcdeee5f`. This ChatGPT workspace is a separate incomplete reference mirror; `sources/` remains read-only.

- Unity 6000.0.28f1, Windows standalone target. Built-in render pipeline confirmed in Editor and GraphicsSettings, despite installed URP 17.0.3. Legacy input handler is active.
- Coplay Unity MCP v10.1.2 is installed and the editor is connected. Scene inspection, compilation, code execution, and isolated physics checks are available.
- Relevant first-party code uses MonoBehaviours, ScriptableObjects, private serialized references plus some public authoring fields, and feature namespaces (Massive.Player, Massive.Multiplier). No first-party gameplay asmdef was found; vendor assemblies have their own asmdefs.
- `Assets/VectorGridNu/ArenaBoundsFromVectorGrid.cs` provides an optional grid reference, BoundsSnapshot, world containment, and explicit RefreshNow. Grid coordinates are local XY, rotated into gameplay XZ. Current grid: position (0,-0.09,0), rotation (270,0,0), size 28x12, unit scale.
- `Assets/VectorGridNu/ArenaBoundaryCollidersFromVectorGrid.cs` generates physical bounds, deferring inspector rebuilds. Resonance does not modify that builder.
- `Assets/Power-ups/Amplifier Core/AmplifierCoreGameplay.cs` owns core lifecycle and attack impulses, with scoring owned elsewhere. It uses Rigidbody.linearVelocity and XZ thrust; prefab has mass 3 and continuous collision detection. Scoreboard copies are presentation-only.
- `Assets/Scripts/Player/Actions/PlayerAttackController.cs` provides StopAtSolidImpact and line-of-sight masks. Resonance does not alter attacks.
- Active scene was `Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity`, already dirty in Editor and modified on disk. Existing gallery files under Assets/Editor and EditorUserSettings were also changed; preserve them.
- Build settings start with S-0_ATTRACT, then onboarding/menu scenes, gameplay stages, and POSTGAME. The prototype is not an enabled build scene.
- No first-party test assembly or direct Unity Test Framework dependency was found in inspected files. Resonance adds a dependency-free editor validation menu; it does not install packages.
- No current GDD was located in the available mirror or targeted repository search. The user's current 120-second match and scoring description supplied design context. Network implementation and whole-project test/build health were not audited.

New feature: `Assets/Resonance/README.md` describes structure, integration, controls, defaults, tests, and limitations. Scope: additive obstacle prototype, no changes to existing scoring, player, core, arena, package, or project settings files. The task leaves the scene unsaved and saves a reusable prefab plus pattern/material assets.

## Player action arena worktree â€” 2026-09-28

Active Unity project: `C:/Users/mattf/massive`, branch `v5`, commit `b505e7c7`. Verified against running Unity Editor process 12484 and the updated Codex project configuration. The arena work was transferred here from the temporary worktree with byte-identical assets and .meta GUIDs; the temporary branch/worktree is retired. The historical live-path/MCP statements above describe an earlier environment, not this session.

Confirmed from this checkout: Unity 6000.0.28f1, built-in rendering (`GraphicsSettings.m_CustomRenderPipeline` is empty), Rewired input through PlayerInputFrame, Coplay MCP package present but no callable Unity MCP in this session. Local Unity batch execution is available. Current design context was read from the user's MASSIVE Notion GDD on 2026-09-28.

The planar template composes the shared Players_Gameplay roster, canonical nested PlayerActor prefab, goals, grid, MatchRuntime and HUD. PlayerDemoDirector creates non-match actors for How To Play; a scoring arena must retain normal match participation. `S-T_PLAYER-ACTIONS` reuses the composed template and adds an input-only experiment driver for manual / combo / attack-and-shield modes. It stays outside the level catalog and build list. Runtime score authority remains MatchScoreService. The separate test economy changes round length only; shared Player Tuning remains authoritative for actions and visuals.

See `Assets/Scripts/Player/Demonstrations/Action Test Arena.md` for controls and validation entry points. No package, rendering, production economy or common PlayerActor changes are required.


### Shield tap / hold experiment (2026-09-28)

The live PlayerShieldAbility now takes shieldHeld from the common scripted/Rewired
input path. Tap lifetime remains .65s; holding retains that opening parry and then
blocks all damage until release or 3s total. ApplyMassLoss centrally rejects damage
while held, including external hazard/projectile drains. Melee reserves a parry on
first shield contact and allows up to .08s for the original lunge to close half the
remaining body gap before applying the original stun/knockback. Extra parry rings
have separate lifetimes. Nugget shudder and padded-rim response are render-only
displacements shared by both nugget shader passes, leaving the simulation intact.
The action arena has a Hold shield toggle and supports shield lead times up to 3s.
Regression runner: MASSIVE > Demonstrations > Validate Shield Actions (temporary
play-mode fixture; preserves the editor's open scenes).


### Simultaneous action gallery (2026-09-28)

`S-T_PLAYER-ACTIONS` now defaults to AllDemos: seven independent PlayerDemoDirector
CombatPair scenarios cover melee tap/hold/exposed/Decoherence and Particle Accelerator
tap/hold/Decoherence. PlayerDemoGallery pauses the real match clock and encounter,
suppresses the match roster, and owns cleanup on mode changes. The non-scoring demo
actors use ConfigureDemonstration and shared gameplay tuning; equipped fixture power-ups
have unlimited lifetime. Manual / Combo / Attack + shield retain the scoring roster.
PlayerControllerScript.SharesSimulationWith scopes melee, lock-on and projectile
queries; normal match actors still share the null scope. Cross-pair physics collisions
are ignored, including after respawn. Real combat events verify loop outcomes.
Scenario assets and label material live in Demonstrations/Action Gallery. Authoring:
PlayerDemoGallerySetup. Batch regression: PlayerDemoGalleryValidation, including
repeated loops, natural death/respawn, late blocks, cleanup and manual restoration.


### Solo third gallery demo (2026-09-28)

Gallery lane 3 now uses PlayerDemoKind.SoloCombo: one canonical attacker runs the
real Thrust / Sweep / Repulsor input sequence, then returns to its centered start
position. There is no defender (13 gallery actors total). MovementAndCombo retains
the How To movement lead-in; SoloCombo skips it. Gallery physics isolation supports
single actors, and Repulsor hit queries now use the same simulation-scope check as
melee/projectiles. Normal match actors continue sharing the null scope.


### Persistent legacy GPU particles (2026-09-28)

AttackTrailGPU implements ISharedSettingsConsumer against
MeleeVisualProfile.legacyParticles. Its eleven effective numeric parameters are
read live; particle capacity changes rebuild buffers, and dispatch uses the
allocated capacity. Existing local fields and scene/compute/material bindings
are retained for opt-out and preview fixtures. Shared Settings, the component
Inspector, and Player Tuning > Appearance expose the same persistent group.
Canonical defaults seed the new fields; Thrust and Sweep share this profile.
Validation entry point: LegacyParticleSettingsValidation.RunBatch (isolated
batch Editor only), including GPU readback and persistence across Play Mode exit.

Verified in Unity 6000.0.28f1: 17 Play Mode checks plus saved-asset reload after
Play Mode exit passed. Covers all 13 gallery consumers, actual GPU readback,
capacity changes, shared/local toggles, Undo/Redo, new spawns, and seven demo
loops. Evidence: Library/LegacyParticleSettingsValidation/report.txt and
unity-verified.log. Tested source hashes match the live project.


### Original Particles stage prefabs (2026-09-28)

The current Original Particles treatment has separate Thrust, Sweep and Repulsor prefabs under Assets/Prefabs/Player/Attack Stages/Original Particles, referenced by MeleeVisualProfile. Each is an OriginalAttackStageVfx hierarchy. Thrust/Sweep contain the original AttackTrailGPU with stage-local tuning; all three include disabled optional Shuriken layers. Repulsor retains its pre-existing body/grid feedback. No gameplay collider or PlayerActor prefab modification was needed.

PlayerMeleePlasma.OriginalParticles owns cached stage instances, current-stage sampling, tails and cancellation/style/disable cleanup. It suppresses the player-root melee trail while prefab emitters replace it; the existing external trail and missing-prefab fallback remain. Stage GPU sampling preserves the original compute/material and uses the prefab transform for placement. Stage initialization arrays are reused across activations.

The shared window edits prefab-owned GPU fields and opens the complete prefab; saved prefab imports and shared-editor Undo/Redo invalidate the runtime copies. OriginalAttackStageVfxEditor provides an explicit Scene-view loop usable in Prefab Mode, including during Play Mode. The custom GPU emitter is not a Shuriken conversion. Added active Particle Systems play with the stage and stop emission before the bounded tail. Prefab contents are separate from Player Tuning snapshot data; snapshots preserve assignments.

OriginalAttackStagePrefabSetup.Install creates missing assets without overwriting existing prefabs. OriginalAttackStageValidation.RunBatch passed 16 checks in Unity 6000.0.28f1: three assigned prefabs, GPU Prefab Mode preview and real combo emission, separate Thrust/Sweep values, live prefab hierarchy additions, Undo/Redo, optional Repulsor emission, all seven demo loops, Off/cancellation/gallery cleanup, and persistence after Play Mode exit/reimport. Rendered frames were inspected. Evidence: Library/OriginalStagePrefabValidation/report.txt, unity-final.log and gallery PNGs. LegacyParticleSettingsValidation now explicitly exercises the unassigned-prefab fallback path.

### Stage particle tail direction (2026-09-28)

Finished Original Particles stage effects retain their own last direction and world rotation instead of inheriting the next attack stage's aim. Follow During Tail controls following player position; disabling it preserves the last sampled world position too. Particle modules remain prefab-owned. A World-simulation Particle System with local Velocity over Lifetime can still steer when its emitter rotates; retaining the finished stage's frame prevents this through combo transitions. During emission, local velocity still follows the active stage's own aim.

Verified against the user's saved Thrust/Center line in Unity 6000.0.28f1, using an isolated project copy: baseline Thrust tail rotated 60 degrees with Sweep, fixed tail rotated 0 degrees, Sweep retained its 60-degree arc, particles survived the transition, all seven demos passed at least two loops, and cancellation, tail expiry, frozen-position tails and gallery cleanup passed. Thirteen targeted checks passed. The Thrust prefab was byte-identical before/after; no authored particle values changed. Evidence and reproduction harness: Library/ThrustTrajectoryValidation. OriginalAttackStageValidation also contains a combo-tail rotation regression assertion.
