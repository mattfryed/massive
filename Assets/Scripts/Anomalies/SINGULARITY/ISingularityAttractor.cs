using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>A scene-local source expressed in the periodic surface chart, never projected screen space.</summary>
    public interface ISingularityAttractor
    {
        SingularitySurface Surface { get; }
        Vector2 SmoothedAttractionPosition { get; }
        float AttractionRadius { get; }
        float AttractionPull { get; }
        bool IsAttractionActive { get; }
    }
}
