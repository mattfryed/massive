using UnityEngine;

public partial class PlayerVisualController
{
    private Massive.Singularity.SingularityPlayerAdapter singularityPresentation;
    private bool singularityPresentationResolved;
    private static readonly int SingularityEnabledId = Shader.PropertyToID("_SingularityEnabled");

    private void ApplySingularityProjection(MaterialPropertyBlock properties, Matrix4x4 sourceToWorld, Matrix4x4 viewProjection)
    {
        // Property blocks survive component disable/removal; never let the last
        // mapped draw silently opt a now-unbound player back into the surface.
        properties.SetFloat(SingularityEnabledId, 0f);
        if (!singularityPresentationResolved)
        {
            singularityPresentation = GetComponentInParent<Massive.Singularity.SingularityPlayerAdapter>();
            singularityPresentationResolved = true;
        }
        if (singularityPresentation != null && singularityPresentation.IsRenderingOnSurface)
            singularityPresentation.ApplyRenderProperties(properties, sourceToWorld, viewProjection);
    }
}
