using UnityEngine;

public partial class PlayerVisualController
{
    // Presentation modifiers, deliberately separate from root scale, hitboxes and life-FX radii.
    public float RepulsorVisualScale { get; private set; } = 1f;
    public Vector3 RepulsorVisualOffsetWS { get; private set; }

    public void SetRepulsorVisual(float scale, Vector3 offsetWS)
    {
        RepulsorVisualScale = Mathf.Clamp(scale, .5f, 1.3f);
        RepulsorVisualOffsetWS = offsetWS;
    }

    public void ClearRepulsorVisual()
    {
        RepulsorVisualScale = 1f;
        RepulsorVisualOffsetWS = Vector3.zero;
    }
}
