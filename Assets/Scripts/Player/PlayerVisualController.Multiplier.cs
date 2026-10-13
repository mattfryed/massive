using Massive.Scoring;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PlayerVisualController
{
    private PlayerScoreChain _multiplierChain;
    public float MultiplierShimmerStrength => PlayerMultiplierShimmer.Strength(_multiplierChain);

    private void DrawMultiplierShimmer(CommandBuffer buffer, MaterialPropertyBlock properties)
    {
        // Child visual rigs must read the owning player's chain without changing team-color lookup.
        if (!_multiplierChain) _multiplierChain = GetComponentInParent<PlayerScoreChain>();
        float strength = MultiplierShimmerStrength;
        if (strength <= 0f || blobMat.passCount < 4) return;
        var settings = PlayerMultiplierShimmer.Settings;
        PlayerMultiplierShimmer.Apply(properties, _multiplierChain,
            strength * settings.outlineOpacity, baseRadius * settings.outlineReach);
        // The shader expands the existing deformed silhouette and uses its folded-surface projection.
        buffer.DrawProcedural(Matrix4x4.identity, blobMat, 3, MeshTopology.Triangles, 128 * 3, 1, properties);
    }
}
