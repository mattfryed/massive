using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bounded, unscaled UI effects. Never awards score or changes capture ownership.</summary>
[DisallowMultipleComponent]
public sealed class NovaCaptureFeedback : MonoBehaviour
{
    public RectTransform effectsRoot;
    public TMP_FontAsset font;
    public Material circleMaterial;
    [Min(.1f)] public float toastSeconds = .9f;
    [Min(.1f)] public float travelSeconds = .55f;
    const int Capacity = 12;
    const int DotsPerCapture = 7;
    readonly Capture[] pool = new Capture[Capacity];
    readonly Material[] teamMaterials = new Material[2];
    int next;

    sealed class Capture
    {
        public RectTransform toast;
        public CanvasGroup group;
        public Image border, fill;
        public TMP_Text text;
        public readonly Image[] dots = new Image[DotsPerCapture];
        public bool active;
        public float age;
        public Vector3 origin;
        public Transform goal;
        public Camera camera;
        public int team;
    }

    public int ActiveCount
    {
        get { int count = 0; foreach (var item in pool) if (item != null && item.active) count++; return count; }
    }

    void BuildPool()
    {
        if (pool[0] != null) return;
        for (int team = 0; team < 2; team++)
        {
            teamMaterials[team] = new Material(circleMaterial);
            teamMaterials[team].SetColor("_FillColor", team == 0 ? Color.white : Color.black);
            teamMaterials[team].SetColor("_OutlineColor", team == 0 ? Color.black : Color.white);
            teamMaterials[team].SetFloat("_OutlineWidth", .16f);
        }
        for (int i = 0; i < Capacity; i++)
        {
            var c = pool[i] = new Capture();
            c.border = Image("Capture toast " + i, effectsRoot, new Vector2(220f, 34f));
            c.toast = c.border.rectTransform;
            c.group = c.toast.gameObject.AddComponent<CanvasGroup>();
            c.group.blocksRaycasts = c.group.interactable = false;
            c.fill = Image("Fill", c.toast, new Vector2(216f, 30f));
            var go = new GameObject("Awarded points", typeof(RectTransform), typeof(TextMeshProUGUI));
            c.text = go.GetComponent<TextMeshProUGUI>();
            c.text.rectTransform.SetParent(c.toast, false);
            c.text.rectTransform.sizeDelta = new Vector2(208f, 30f);
            c.text.font = font;
            c.text.fontSize = 18f;
            c.text.enableAutoSizing = true;
            c.text.fontSizeMin = 12f;
            c.text.fontSizeMax = 18f;
            c.text.alignment = TextAlignmentOptions.Center;
            c.text.raycastTarget = false;
            for (int j = 0; j < DotsPerCapture; j++)
                c.dots[j] = Image("Goal stream " + i + ":" + j, effectsRoot, Vector2.one * 9f);
            Hide(c);
        }
    }

    static Image Image(string name, RectTransform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.sizeDelta = size;
        image.raycastTarget = false;
        return image;
    }

    public void Show(int team, long points, Vector3 origin, Transform goal, Camera camera)
    {
        if ((team != 1 && team != 2) || points <= 0 || !goal || !camera || !effectsRoot || !circleMaterial) return;
        BuildPool();
        var c = pool[next];
        next = (next + 1) % Capacity;
        c.active = true;
        c.age = 0f;
        c.team = team;
        c.origin = origin;
        c.goal = goal;
        c.camera = camera;
        bool light = team == 1;
        c.fill.color = light ? Color.white : Color.black;
        c.border.color = c.text.color = light ? Color.black : Color.white;
        c.text.text = (light ? "LIGHT +" : "DARK +") + NovaBonusPresentation.FormatEnergy(points);
        c.toast.gameObject.SetActive(true);
        foreach (var dot in c.dots) dot.material = teamMaterials[team - 1];
        Animate(c);
    }

    void Update()
    {
        foreach (var c in pool)
        {
            if (c == null || !c.active) continue;
            c.age += Time.unscaledDeltaTime;
            if (!c.goal || !c.camera || c.age >= Mathf.Max(toastSeconds, travelSeconds + .18f)) Hide(c);
            else Animate(c);
        }
    }

    Vector2 Project(Vector3 world, Camera camera)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(effectsRoot,
            camera.WorldToScreenPoint(world), null, out var point);
        return point;
    }

    void Animate(Capture c)
    {
        Vector2 start = Project(c.origin, c.camera);
        Vector2 end = Project(c.goal.position, c.camera);
        var toast = start + new Vector2(0f, 27f + 30f * Mathf.Clamp01(c.age / toastSeconds));
        var rect = effectsRoot.rect;
        toast.x = Mathf.Clamp(toast.x, rect.xMin + 116f, rect.xMax - 116f);
        toast.y = Mathf.Clamp(toast.y, rect.yMin + 23f, rect.yMax - 23f);
        c.toast.anchoredPosition = toast;
        c.group.alpha = 1f - Mathf.InverseLerp(toastSeconds * .6f, toastSeconds, c.age);
        Vector2 control = Vector2.Lerp(start, end, .5f) + new Vector2(0f, 48f);
        for (int j = 0; j < c.dots.Length; j++)
        {
            float t = (c.age - j * .025f) / travelSeconds;
            var dot = c.dots[j];
            bool visible = t >= 0f && t < 1f;
            dot.gameObject.SetActive(visible);
            if (!visible) continue;
            t = Mathf.SmoothStep(0f, 1f, t);
            Vector2 spread = new Vector2(Mathf.Sin(j * 2.4f), Mathf.Cos(j * 2.4f)) * 12f * (1f - t);
            dot.rectTransform.anchoredPosition = (1f - t) * (1f - t) * start +
                2f * (1f - t) * t * control + t * t * end + spread;
            dot.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, .4f, t);
        }
    }

    static void Hide(Capture c)
    {
        c.active = false;
        c.toast.gameObject.SetActive(false);
        foreach (var dot in c.dots) dot.gameObject.SetActive(false);
    }

    public void Clear()
    {
        foreach (var c in pool) if (c != null) Hide(c);
    }

    void OnDisable() => Clear();

    void OnDestroy()
    {
        foreach (var material in teamMaterials) if (material) Destroy(material);
    }
}
