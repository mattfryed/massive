using Shapes;
using UnityEngine;

/// <summary>Gently varies an authored stat fill without changing its left edge or saved value.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rectangle))]
public sealed class HowToPlayStatBarIdle : MonoBehaviour
{
    [Tooltip("Random change as a fraction of the authored fill length.")]
    [SerializeField] private Vector2 lengthVariation = new Vector2(.025f, .075f);
    [Tooltip("Random seconds for each eased growth or shrink.")]
    [SerializeField] private Vector2 transitionSeconds = new Vector2(.8f, 1.9f);
    [Tooltip("Inset from the right edge of the surrounding stat bar.")]
    [Min(0f), SerializeField] private float edgePadding = .03f;

    private Rectangle fill, track;
    private System.Random random;
    private float authoredWidth, from, target, elapsed, duration;
    private bool grow, captured;

    public Rectangle Fill { get { CacheShapes(); return fill; } }
    public float MaximumWidth
    {
        get
        {
            CacheShapes();
            return track ? Mathf.Max(0f, track.Width * .5f - edgePadding - transform.localPosition.x) : fill.Width;
        }
    }

    private void CacheShapes()
    {
        if (!fill) fill = GetComponent<Rectangle>();
        if (!track && transform.parent) track = transform.parent.GetComponent<Rectangle>();
    }

    /// <summary>Update the resting length, including while its idle animation is running.</summary>
    public void SetAuthoredWidth(float width)
    {
        CacheShapes();
        authoredWidth = Mathf.Max(0f, width);
        fill.Width = authoredWidth;
        if (captured && random != null) NextTarget();
    }

    private void OnEnable()
    {
        CacheShapes();
        authoredWidth = fill.Width;
        captured = true;
        random = new System.Random(GetInstanceID());
        grow = random.Next(2) == 0;
        NextTarget();
    }

    private float Sample(Vector2 range, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        float high = Mathf.Max(low, Mathf.Max(range.x, range.y));
        return Mathf.Lerp(low, high, (float)random.NextDouble());
    }

    private void NextTarget()
    {
        from = fill.Width;
        float variation = Mathf.Clamp01(Sample(lengthVariation, 0f));
        float maximum = track ? MaximumWidth : authoredWidth * 2f;
        target = Mathf.Clamp(authoredWidth * (1f + (grow ? variation : -variation)), 0f, maximum);
        grow = !grow;
        elapsed = 0f;
        duration = Sample(transitionSeconds, .05f);
    }

    private void Update()
    {
        if (!fill || authoredWidth <= 0f) return;
        elapsed += Time.unscaledDeltaTime;
        fill.Width = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
        if (elapsed >= duration) NextTarget();
    }

    private void OnDisable()
    {
        if (captured && fill) fill.Width = authoredWidth;
        captured = false;
    }
}
