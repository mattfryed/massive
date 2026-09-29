# Original Particles stage prefabs

Thrust.prefab, Sweep.prefab and Repulsor.prefab are assigned to the **Original Particles** treatment in MeleeVisualProfile. They are used by normal players and every gallery actor.

## Edit a stage

1. Open **MASSIVE > Shared Settings > Melee** (or **Player Tuning > Appearance**, with Original Particles selected) and click **Open Thrust/Sweep/Repulsor prefab**. You can also open the prefab directly from this folder.
2. Thrust and Sweep contain **GPU Particle Trail**, the actual original custom GPU emitter. Its Inspector edits that stage independently. Use **Loop stage preview in Scene view** on the root or emitter to preview it; select other children while the preview keeps running. This is a custom GPU system, so it has its own controls rather than Unity's Shuriken modules.
3. Add ordinary Particle System children, meshes, lights, Animators or effect components to the prefab. Each prefab includes a disabled starter Particle System. Enable it to use the standard particle-module Inspector, or add your own. Assign a material appropriate for the project's built-in renderer.
4. Save the prefab (Auto Save or Ctrl+S in Prefab Mode). Running demo actors rebuild their visual instances with the saved hierarchy and settings, without restarting the gameplay attack. Edit the prefab asset for persistence; changes to runtime clones require the usual prefab-authoring workflow.

The shared window's Thrust/Sweep GPU foldouts edit these same prefab assets and save immediately, including during Play Mode. GPU fields no longer need to share values between Thrust and Sweep. The previous eleven shared values remain a fallback for actors/stages without assigned prefabs and the player-root external trail.

## Playback and placement

+Z is the attack direction, +Y is up. Use the root component's Position Offset, Rotation Offset and Scale for whole-effect placement; author individual offsets and scales on child transforms. Particle System simulation space, size, colors, materials and other modules remain authored on the particle components.

Active Particle System children are explicitly played when the stage starts; disabled child objects stay disabled. **Start At Activation** delays emission until the attack's activation window (enabled by default for Repulsor). Emission stops when the stage finishes, then **Tail Seconds** bounds the visual aftermath. Each finished stage retains its own final direction, so a later stage cannot rotate its lingering effects. **Follow During Tail** follows player position only; disable it to hold the final world position as well. For particles that should continue independently of player movement, use **Main > Simulation Space = World**. **Velocity over Lifetime > Space = Local** can still provide motion along the attack direction: the emitter axes remain fixed after that stage finishes. While the stage is emitting, its local velocity modules still follow that stage's own aim. Cancellation, elimination, changing visual style and disabling the player clear its visual effects. None of these visual controls change damage or hitboxes.

Repulsor's original look is player-body and grid feedback, configured under **Impact & recovery**. Its optional particle layer starts disabled so creating the prefab preserves that look. Enable **Optional Repulsor Particles** or add new children to extend it.

Player Tuning snapshots preserve the stage-prefab assignments. The prefab contents are separate assets; use normal prefab/version-control history for whole-prefab revisions.

Validation: OriginalAttackStageValidation.RunBatch in an isolated Editor. Covers actual GPU preview/emission, three real combo stages, independently saved particle tuning, adding a new Particle System during Play Mode, Undo/Redo, enabling Repulsor particles, style/cancellation cleanup, all seven demo loops and persistence after Play Mode exit.
