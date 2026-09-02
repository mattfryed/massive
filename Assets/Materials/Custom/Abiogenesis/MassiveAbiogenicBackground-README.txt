MASSIVE Abiogenic Background - first pass

Files
- MassiveAbiogenicFieldProfile.cs
- MassiveAbiogenicBackgroundController.cs
- MassiveAbiogenicFieldCommon.hlsl
- MassiveAbiogenicBackground.shader

Intent
This is a world-space, arena-anchored background volume rendered through a quad.
It is inspired by the abiogenic TSL sketch, but rebuilt for MASSIVE as a monochrome HLSL effect.

Why this version first
- It fits the existing VectorGridGPU/ArenaBounds workflow.
- It respects scene depth naturally because the quad sits behind the arena.
- It avoids committing immediately to a fullscreen renderer-feature path.
- The raymarch math is isolated in MassiveAbiogenicFieldCommon.hlsl so a later fullscreen depth composite can reuse it.

Suggested scene hookup
1. Create a new material using shader MASSIVE/AbiogenicBackground.
2. Create a new empty GameObject near the arena root.
3. Add MeshFilter + MeshRenderer + MassiveAbiogenicBackgroundController.
4. Assign:
   - ArenaBoundsFromVectorGrid reference
   - the new material
   - a MassiveAbiogenicFieldProfile asset
5. Keep the background object unparented or under a non-scaled parent if possible.
6. Let the controller auto-drive the transform so the quad sits under the arena.

Suggested starting values
- planeOffsetWorld = 0.35
- overscan = 1.35
- profile.maxSteps = 48
- profile.maxDistance = 12
- profile.cellScale = 3
- profile.layerDepth = 0.5
- profile.sphereRadius = 0.1
- profile.scrollSpeed = 0.15
- profile.warpStrength = 0.14
- profile.baseLuma = 0.10
- profile.glowStrength = 0.90

Likely next steps
- Tie phaseOffset / scrollSpeed / glowStrength to stage or anomaly state.
- Add a second profile for brighter "event" states like NOVA.
- Reuse MassiveAbiogenicFieldCommon.hlsl in a later fullscreen/depth-composite pass if you want the field to truly intersect scene depth.
