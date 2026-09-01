using UnityEngine;

public interface IVectorGrid
{
    // Replace/append forces this frame (cheapest: call once per frame).
    void SetForces(System.ReadOnlySpan<VectorGridGPU.Force> forces);

    // Convenience: add a single force quickly (will reupload this frame).
    void AddForce(in VectorGridGPU.Force f);

    // Reset grid to original positions
    void ResetGrid();

    // Change global spring/damping at runtime (feel tuning)
    float SpringK { get; set; }
    float Damping { get; set; }
}
