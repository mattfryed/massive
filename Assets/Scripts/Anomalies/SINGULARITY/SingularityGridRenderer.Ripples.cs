using UnityEngine;

namespace Massive.Singularity
{
    public sealed partial class SingularityGridRenderer
    {
        private struct Ripple
        {
            public Vector2 center;
            public float began, duration, startRadius, reach, amplitude, width;
        }
        private const int RippleCapacity = 8;
        private readonly Ripple[] ripples = new Ripple[RippleCapacity];
        private readonly Vector4[] rippleCenters = new Vector4[RippleCapacity];
        private readonly Vector4[] rippleShapes = new Vector4[RippleCapacity];
        private int nextRipple;
        private static readonly int RippleCentersId = Shader.PropertyToID("_SurfaceRippleCenters");
        private static readonly int RippleShapesId = Shader.PropertyToID("_SurfaceRippleShapes");
        private static readonly int RippleCountId = Shader.PropertyToID("_SurfaceRippleCount");
        public int ActiveRippleCount { get; private set; }

        public void EmitRipple(Vector2 center, float duration, float reach, float amplitude, float width, float startRadius = 0f)
        {
            if (!surface || !isActiveAndEnabled || !Finite(center.x) || !Finite(center.y) ||
                !Finite(duration) || !Finite(reach) || !Finite(amplitude) || !Finite(width) || !Finite(startRadius)) return;
            ripples[nextRipple] = new Ripple { center = center, began = Time.time,
                duration = Mathf.Clamp(duration, .05f, 3f), reach = Mathf.Clamp(reach, .1f, 10f),
                startRadius = Mathf.Clamp(startRadius, 0f, Mathf.Clamp(reach, .1f, 10f)),
                amplitude = Mathf.Clamp(amplitude, 0f, .4f), width = Mathf.Clamp(width, .05f, 2f) };
            nextRipple = (nextRipple + 1) % RippleCapacity;
        }

        public void ClearRipples()
        {
            for (int i = 0; i < RippleCapacity; i++) ripples[i].duration = 0f;
            ActiveRippleCount = 0;
        }

        private void BindRipples()
        {
            int count = 0;
            for (int i = 0; i < RippleCapacity; i++)
            {
                Ripple r = ripples[i];
                float elapsed = Time.time - r.began;
                if (r.duration <= 0f || elapsed < 0f || elapsed >= r.duration) continue;
                float u = elapsed / r.duration;
                rippleCenters[count] = new Vector4(r.center.x, r.center.y,
                    Mathf.Lerp(r.startRadius, r.reach, 1f - (1f - u) * (1f - u)), r.amplitude * (1f - u) * (1f - u));
                rippleShapes[count] = new Vector4(r.width, 0f, 0f, 0f);
                count++;
            }
            ActiveRippleCount = count;
            properties.SetInt(RippleCountId, count);
            properties.SetVectorArray(RippleCentersId, rippleCenters);
            properties.SetVectorArray(RippleShapesId, rippleShapes);
        }
    }
}
