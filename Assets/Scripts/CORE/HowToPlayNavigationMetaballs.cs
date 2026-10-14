using UnityEngine;

/// <summary>Ambient SDF motion with a directional pulse for manual pane navigation.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class HowToPlayNavigationMetaballs : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private HowToPlayCarousel carousel;
    [SerializeField] private MetaballSDFInstance surface;

    [Header("Metaballs")]
    [Tooltip("Total number of balls, including the central ball.")]
    [Range(1, MetaballSDFInstance.MaxBalls)] [SerializeField] private int metaballCount = 7;
    [Tooltip("Base ball radius in local units, independent of the cluster's spread.")]
    [Min(.01f)] [SerializeField] private float metaballSize = .58f;
    [Tooltip("Maximum proportional size difference. Zero makes every ball the same size. The central ball is largest; surrounding balls gently vary. A single ball uses the base size.")]
    [Range(0f, 1f)] [SerializeField] private float sizeVariation = .65f;

    [Header("Idle shape")]
    [Tooltip("Spread of the ball centers in this object's local XY plane. Individual ball size is controlled separately above.")]
    [Min(.1f)] [SerializeField] private float radius = 2f;
    [Min(0f)] [SerializeField] private float idleSpeed = .32f;
    [Range(0f, .3f)] [SerializeField] private float idleDrift = .12f;

    [Header("Navigation push")]
    [Tooltip("Distance beyond the idle radius to the point the leading metaballs move toward.")]
    [Min(0f)] [SerializeField] private float pushDistance = 1.8f;
    [Min(.05f)] [SerializeField] private float pushSeconds = .45f;
    [Min(.05f)] [SerializeField] private float settleSeconds = .85f;

    private float pulseStart;
    private float pulseFrom;
    private int pulseDirection;
    private bool pulsing;
    private HowToPlayCarousel subscribedCarousel;

    private void OnEnable()
    {
        pulsing = false;
        if (Application.isPlaying && carousel)
        {
            subscribedCarousel = carousel;
            subscribedCarousel.Navigated += Push;
        }
        Draw(0f, 0f);
    }

    private void OnDisable()
    {
        if (subscribedCarousel) subscribedCarousel.Navigated -= Push;
        subscribedCarousel = null;
        pulsing = false;
        if (surface)
        {
            surface.Clear();
            surface.Apply();
        }
    }

    private void Update()
    {
        float now = Application.isPlaying ? Time.unscaledTime : 0f;
        Draw(now * idleSpeed, EvaluatePush(now));
    }

    private void Push(int direction)
    {
        // Start from the current deformation so quick reversals never snap the shape.
        pulseFrom = EvaluatePush(Time.unscaledTime);
        pulseDirection = direction > 0 ? 1 : -1;
        pulseStart = Time.unscaledTime;
        pulsing = true;
    }

    private float EvaluatePush(float now)
    {
        if (!pulsing) return 0f;
        float age = Mathf.Max(0f, now - pulseStart);
        float attack = Mathf.Max(.05f, pushSeconds);
        float release = Mathf.Max(.05f, settleSeconds);
        if (age < attack)
            return Mathf.Lerp(pulseFrom, pulseDirection, EaseInOut(age / attack));
        if (age < attack + release)
            return pulseDirection * (1f - EaseInOut((age - attack) / release));
        pulsing = false;
        return 0f;
    }

    private void Draw(float time, float push)
    {
        if (!surface || !surface.TargetRenderer) return;
        float size = Mathf.Max(.1f, radius);
        int count = Mathf.Clamp(metaballCount, 1, MetaballSDFInstance.MaxBalls);
        float baseBallRadius = Mathf.Max(.01f, metaballSize);
        float variation = count == 1 ? 0f : Mathf.Clamp01(sizeVariation);
        float maxBallRadius = baseBallRadius * (1f + variation);
        // Keep the ray-marched volume uniform to avoid precision artifacts.
        // Recess its center behind the placeholder plane so it stays behind UI.
        float extent = (size + Mathf.Max(0f, pushDistance)) * 2f + size;
        float idleReach = size * (.57f + Mathf.Clamp(idleDrift, 0f, .3f) * 1.3f);
        float pushReach = Mathf.Lerp(idleReach, size + Mathf.Max(0f, pushDistance), .8f);
        // Expand for larger balls, including room for the material's smooth union.
        extent = Mathf.Max(extent, (Mathf.Max(idleReach, pushReach) + maxBallRadius) * 2.2f);
        Transform volume = surface.TargetRenderer.transform;
        volume.localScale = Vector3.one * extent;
        volume.localPosition = new Vector3(0f, 0f, Mathf.Max(size, maxBallRadius + extent * .05f));
        surface.Clear();
        for (int i = 0; i < count; i++)
        {
            float phase = i * 2.399963f;
            Vector2 center;
            float ballRadius;
            if (i == 0)
            {
                center = new Vector2(Mathf.Sin(time * .73f), Mathf.Cos(time * .61f)) * (size * .035f);
                ballRadius = baseBallRadius * (1f + variation);
            }
            else
            {
                float angle = (i - 1) * Mathf.PI * 2f / (count - 1) + .22f * Mathf.Sin(time * .57f + phase);
                float orbit = size * (.57f + idleDrift * Mathf.Sin(time * .83f + phase));
                center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbit;
                center += new Vector2(Mathf.Sin(time * .67f + phase), Mathf.Cos(time * .79f + phase)) * (size * idleDrift * .3f);
                ballRadius = baseBallRadius * (1f + variation * .3f * Mathf.Sin(time * .71f + phase));
            }

            float direction = push >= 0f ? 1f : -1f;
            float leading = Mathf.InverseLerp(-size * .65f, size * .65f, center.x * direction);
            Vector2 focus = new Vector2(direction * (size + pushDistance), 0f);
            // Keep the trailing body anchored while the leading lobes reach out.
            float response = i == 0 ? .05f : Mathf.Lerp(.01f, .8f, leading * leading);
            center = Vector2.Lerp(center, focus, Mathf.Abs(push) * response);
            surface.AddBall(new Vector3(center.x, center.y, 0f) / extent, ballRadius / extent);
        }
        surface.Apply();
    }

    private void OnValidate()
    {
        metaballCount = Mathf.Clamp(metaballCount, 1, MetaballSDFInstance.MaxBalls);
        metaballSize = Mathf.Max(.01f, metaballSize);
        sizeVariation = Mathf.Clamp01(sizeVariation);
    }

    // Zero velocity and acceleration at both ends of each half of the pulse.
    private static float EaseInOut(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
}
