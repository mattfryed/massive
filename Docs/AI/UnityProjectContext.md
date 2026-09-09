# MASSIVE project context — resonance task, 2026-09-06

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
