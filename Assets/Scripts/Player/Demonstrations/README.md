# Player demonstrations

`Assets/Prefabs/PlayerActor.prefab` is the shared, single-player source. The four actors in `Players.prefab` are nested instances of it; `Players_Gameplay.prefab` continues to supply the gameplay roster. How To Play spawns the same source at runtime. Make common component, collider, visual and ability changes on **PlayerActor**, and balance changes in **MASSIVE > Player Tuning**. Changes made only as overrides on one scene/roster instance remain local to that instance.

The old `Player.prefab` and `Pseudo Player 1/3.prefab` are legacy assets. The old How To Play pseudo instances are retained inactive for reference. Do not use these copies as the source for new demonstrations.

Gameplay spawn anchors belong to each nested slot in `Players.prefab`; the standalone Actor has no roster anchor. Scene overrides bind goals and grids, and each `PlayerRosterController` explicitly references its four scene instances. Converting the roster to nested Actors changes prefab-local object IDs: preserve the per-slot anchors and remap consumer references, including variant IDs, rather than relying on runtime discovery. `PlayerActorReferenceMigration` captures those identities before conversion. Unity missing-object references must be checked with Unity's null semantics, not C# null-coalescing.

Match startup validates the selected mode's required slots (normally P1/P3 for 1v1, all four for 2v2). Each needs a unique scene-local match player with the correct ID, an enabled controller, a roster spawn anchor and a goal. Required players must be active after roster activation. Automatic discovery only repairs unambiguous missing slots. A failed check logs one error naming the scene, roster, slot and failed binding; `StartupFailureReason` exposes the diagnostic on the roster and match manager. The match stays in Preparing with scoring and input locked, including when spawn waiting is disabled. Fix the binding and reload the scene; rejection is latched to prevent a partially initialized match from resuming.

## Policy and input

`ConfigureDemonstration` runs before the actor becomes active. It selects scripted input, shared gameplay tuning and a non-match role. This preserves ordinary movement, combat, shield, size/reversal and power-up behavior while excluding match scoring, HUD and spawn presentation. `IsPseudoPlayer` remains the compatibility flag for existing match filters; tuning is now a separate `PlayerTuningMode` policy. Automatic keeps legacy fixtures local; SharedGameplay opts demonstrations into live values; Local is an explicit fixture override.

Every action goes through `PlayerInputFrame`, including attack press, hold and release. Directional aim is explicit, so stationary actors can face each other. Control graphics observe applied input and cannot delay actions.

## Scenarios

Create a **MASSIVE > Demonstrations > Player Scenario** asset and assign PlayerActor. Attach `PlayerDemoDirector` to a stage, then assign the scenario. Optional camera and control graphics only affect presentation.

- Movement and combo moves using actual propulsion and advances through the live combo engagement windows.
- Block shares one director for both actors. It derives spacing and shield timing from the live attack/shield data, and waits for real shield contact before counting success.
- Power-up claims the normal pickup with melee. Time Dilation moves the actor, Accelerator repeatedly charges and fires at a normal-health opponent until the real damage/death system resolves a kill, then waits for the opponent's normal respawn; Decoherence activates the normal shield action. Shared damage values determine how many shots are needed.

Scenario data contains choreography/readability settings, not duplicate gameplay stats. If tuning makes a scenario impossible, it times out with a visible diagnostic in the Console and `LastFailure`; it does not count a successful loop or change the gameplay rules. New power-up types need a use sequence in the director.

The How To Play cameras stay fixed through each loop and show spatially separated stages in the normal physics scene. Each has an explicitly bound VectorGridGPU strip, with responsive attraction from the same shared Player Interaction profile. The strips do not claim the global default grid. The force collector routes each interactor to its own grid. Power-up regions use a 1:2:1 layout, with permanent instances of the existing pickup toast below them. This preserves the existing gameplay physics queries. `PlayerDemoView` maps each camera into the original instructional layout. Adjust viewport bounds/padding to fit the screen, rather than scaling the Player for presentation. If reusing this for a playable tutorial, place the stage in the intended arena and supply suitable targets; these remote stages are for noninteractive demonstrations.

## Lifetime and reset

Each director owns one persistent actor group, initialized while inactive. Normal loops preserve the actor instances, health, mass, physics and cooldowns. Actors steer back through PlayerInputFrame; the director never teleports or replaces a visible actor to reset a loop. Accelerator's opponent uses the normal death/dissolve/respawn lifecycle at its original anchor. Pickups are reclaimed when effects expire, and receive their definition before OnEnable computes lifetime. Projectiles and impacts join the simulation group. Explicit Stop/restart or scene unload clears effects and inputs, deactivates the group and destroys it at frame end. Spawned pickup destruction is limited to the pickup, never the outer stage.

Simulation waits use scaled game time, matching Player actions. The existing visual effects retain their own time settings. This is state-driven choreography, not deterministic input replay; recorded inputs alone do not guarantee the same physics result across machines.

## Validation

- **MASSIVE > Demonstrations > Validate Prefabs and Tuning** checks canonical/nested references, components, ownership, roster slots, and shared tuning after temporary value changes; original values are restored.
- With How To Play open and saved, **Run How To Play** enters Play Mode. **Report Runtime Results** reports loop failures, confirmed blocks, pickup claims and fired projectiles.
- **MASSIVE > Player > Validate Player Tuning** exercises existing timing, handoff and shared/local tuning regression checks.
- **MASSIVE > Player > Validate Actor Scene Bindings** checks every level in the live Level Catalog, the development roster scenes and planar template, plus the missing-object fallback. Catalog levels must have enabled build scenes and exactly one valid roster, with spawn anchors visible through the arena masks. `PlayerActorMigrationValidation.StartPlayableCatalogRun(screenshotDirectory)` runs normal 1v1/2v2 startup and death/respawn for every catalog level in Play Mode and saves per-case screenshots and results. `StartRun` can target an individual scene (NOVA by default). Grid bindings are required only when a scene uses VectorGridGPU. These fixtures reload scenes and change only runtime timer/idle settings; exit Play Mode afterward.
- `RosterStartupValidation.StartRun(resultsPath)` injects missing/invalid bindings into disposable NOVA Play Mode instances, verifies actionable single-error rejection and closed scoring/input, and confirms unused 1v1 slots do not block startup. Expected error logs are included in the results file. No authored scene or prefab is saved.

`MassiveDemoMigration` contains the one-time prefab/scene migration and reference-preserving backups under `Library/DemoMigrationBackup`. Migration is already applied; ordinary authoring does not require rerunning it.
