#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The oracle is the calculation before oval-wall caching was introduced. Keep
// it independent of the production outline helpers, cached data and fast paths.
public static class ArenaBoundsCacheValidation
{
    static readonly FieldInfo GridField = typeof(ArenaBoundsFromVectorGrid)
        .GetField("grid", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly float[] Paddings = { -.25f, 0, .00001f, .04f, .25f, 1.25f, 20 };

    [MenuItem("MASSIVE/COSMOS/Validate Arena Bounds Cache")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run the arena bounds validation in Edit Mode after compilation.");

        var scene = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        int containsChecks = 0, clampChecks = 0, configurations = 0;
        var random = new System.Random(791443);
        try
        {
            root = new GameObject("Arena bounds cache differential fixture");
            root.SetActive(false);
            root.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(root, scene);
            var first = NewGrid(root.transform, "First grid");
            var second = NewGrid(root.transform, "Replacement grid");
            var bounds = root.AddComponent<ArenaBoundsFromVectorGrid>();
            GridField.SetValue(bounds, first);
            first.size = new Vector2(27.7f, 13);
            bounds.ovalOutline = true;
            bounds.ovalEndHeightRatio = .7f;

            void Validate(string label)
            {
                configurations++;
                var grid = bounds.Grid;
                Vector2 half = grid.size * .5f;
                for (int i = 0; i < 256; i++)
                {
                    var local = new Vector3(Random(random, -half.x * 1.3f, half.x * 1.3f),
                        Random(random, -half.y * 1.3f, half.y * 1.3f), Random(random, -2, 2));
                    Compare(bounds, grid.transform.TransformPoint(local), Paddings[i % Paddings.Length],
                        i % 8 == 0, label + " random " + i, ref containsChecks, ref clampChecks);
                }

                // Every wall vertex and every span midpoint, on both halves.
                // Offset in world-distance coordinates normal to the segment.
                // Include points immediately either side of clearance equality.
                Vector3 scale = grid.transform.lossyScale;
                float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y);
                for (int i = 0; i <= ArenaBoundsFromVectorGrid.OvalSegments * 2; i++)
                {
                    float x = Mathf.Lerp(-half.x, half.x, i / (ArenaBoundsFromVectorGrid.OvalSegments * 2f));
                    int span = Mathf.Min(i / 2, ArenaBoundsFromVectorGrid.OvalSegments - 1);
                    float ax = Mathf.Lerp(-half.x, half.x, span / (float)ArenaBoundsFromVectorGrid.OvalSegments);
                    float bx = Mathf.Lerp(-half.x, half.x, (span + 1f) / ArenaBoundsFromVectorGrid.OvalSegments);
                    Vector2 a = new Vector2(ax * sx, ReferenceHeight(bounds, ax) * sy);
                    Vector2 b = new Vector2(bx * sx, ReferenceHeight(bounds, bx) * sy);
                    Vector2 tangent = b - a;
                    Vector2 inward = new Vector2(tangent.y, -tangent.x).normalized;
                    Vector2 surface = i % 2 == 0 ? new Vector2(x * sx, ReferenceHeight(bounds, x) * sy) : (a + b) * .5f;
                    float padding = Paddings[i % Paddings.Length];
                    float clearance = bounds.insetWorld + Mathf.Max(0, padding);
                    for (int offset = -1; offset <= 1; offset++)
                    {
                        Vector2 sample = surface + inward * (clearance + offset * .00001f);
                        for (int sign = -1; sign <= 1; sign += 2)
                        {
                            var local = new Vector3(sample.x / sx, sign * sample.y / sy, .17f);
                            Compare(bounds, grid.transform.TransformPoint(local), padding, i % 24 == 0,
                                label + " boundary " + i + "/" + offset + "/" + sign,
                                ref containsChecks, ref clampChecks);
                        }
                    }
                }

                // Side/goal separators, centre, and the conservative interior
                // fast-path threshold need explicit checks as well as randoms.
                foreach (float padding in Paddings)
                {
                    float clearance = bounds.insetWorld + Mathf.Max(0, padding);
                    for (int sign = -1; sign <= 1; sign += 2)
                    for (int offset = -1; offset <= 1; offset++)
                    {
                        float delta = offset * .00001f;
                        var side = new Vector3(sign * (half.x - (clearance + delta) / sx), 0, 1.7f);
                        var interior = new Vector3(0, sign * (ReferenceHeight(bounds, half.x) - (clearance + delta) / sy), -.6f);
                        Compare(bounds, grid.transform.TransformPoint(side), padding, true, label + " side",
                            ref containsChecks, ref clampChecks);
                        Compare(bounds, grid.transform.TransformPoint(interior), padding, true, label + " interior threshold",
                            ref containsChecks, ref clampChecks);
                    }
                    Compare(bounds, grid.transform.position, padding, true, label + " centre", ref containsChecks, ref clampChecks);
                }
            }

            // Reuse the same bounds object throughout, and query immediately
            // after each edit without RefreshNow, Update, or a bounds event.
            Validate("COSMOS dimensions");
            first.size = new Vector2(19.2f, 8.8f); Validate("live size");
            first.size.x += .000001f; Validate("sub-epsilon size");
            bounds.ovalEndHeightRatio = .3f; Validate("low ratio");
            bounds.ovalEndHeightRatio = .95f; Validate("high ratio");
            bounds.ovalEndHeightRatio = -.4f; Validate("clamped low ratio");
            bounds.ovalEndHeightRatio = 1.3f; Validate("clamped high ratio");
            bounds.ovalEndHeightRatio = .7000001f; Validate("live ratio");
            bounds.ovalEndHeightRatio = .7000002f; Validate("sub-epsilon ratio");
            bounds.insetWorld = .23f; Validate("live inset");
            first.transform.localScale = new Vector3(1.8f, .42f, 2); Validate("nonuniform scale");
            first.transform.localScale = new Vector3(1.800001f, .42f, 2); Validate("sub-epsilon scale");
            first.transform.localScale = new Vector3(-1.8f, .42f, 2); Validate("negative X scale");
            first.transform.localScale = new Vector3(1.8f, -.42f, -2); Validate("negative Y scale");
            first.transform.localScale = new Vector3(-1.8f, -.42f, 2); Validate("negative XY scale");
            first.transform.SetLocalPositionAndRotation(new Vector3(7, -4, 12), Quaternion.Euler(39, 71, 23));
            Validate("translated rotated grid");
            root.transform.SetPositionAndRotation(new Vector3(-19, 6, 4), Quaternion.Euler(80, -31, 17));
            root.transform.localScale = new Vector3(.61f, 2.4f, -1.3f); Validate("transformed nonuniform parent");
            bounds.ovalOutline = false; Validate("rectangle");
            bounds.insetWorld = 0; Validate("rectangle zero inset");
            bounds.ovalOutline = true; Validate("oval reenabled");

            second.size = first.size;
            second.transform.localScale = new Vector3(.73f, -1.4f, 1);
            second.transform.SetLocalPositionAndRotation(new Vector3(-4, 9, -6), Quaternion.Euler(21, -17, 43));
            GridField.SetValue(bounds, second); Validate("replacement grid reference");
            second.size = new Vector2(.025f, .012f); Validate("small arena");
            second.size = new Vector2(43, 27); Validate("large arena");
            second.transform.localScale = new Vector3(.0001f, .0003f, 1); Validate("small nonzero scale");
            GridField.SetValue(bounds, first); Validate("original grid restored");
            GridField.SetValue(bounds, null);
            Compare(bounds, new Vector3(7, -3, 12), .4f, true, "missing grid", ref containsChecks, ref clampChecks);

            return "PASS arena bounds cache: " + configurations + " live configurations, " + containsChecks +
                " exact containment comparisons and " + clampChecks + " exact clamp comparisons against the original calculation; " +
                "all wall vertices/midpoints, clearance thresholds, insets, rectangle/oval transitions, " +
                "grid replacement, tiny edits, rotation, translation, and signed/nonuniform scales. Preview fixture cleaned up.";
        }
        finally
        {
            if (root) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static VectorGridGPU NewGrid(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        var grid = go.AddComponent<VectorGridGPU>();
        grid.registerAsDefault = false;
        return grid;
    }

    static float Random(System.Random random, float low, float high) => low + (float)random.NextDouble() * (high - low);

    static void Compare(ArenaBoundsFromVectorGrid bounds, Vector3 point, float padding, bool clamp, string label,
        ref int containsChecks, ref int clampChecks)
    {
        bool expected = ReferenceContains(bounds, point, padding);
        bool actual = bounds.ContainsWorldPoint(point, padding);
        if (expected != actual)
            throw new Exception("Arena containment differs: " + label + "; world=" + point.ToString("R") +
                ", padding=" + padding.ToString("R") + ", expected=" + expected + ", actual=" + actual);
        containsChecks++;
        if (!clamp) return;
        Vector3 expectedClamp = ReferenceClamp(bounds, point, padding);
        Vector3 actualClamp = bounds.ClampWorldPointInside(point, padding);
        if (!expectedClamp.Equals(actualClamp))
            throw new Exception("Arena clamp differs: " + label + "; world=" + point.ToString("R") +
                ", padding=" + padding.ToString("R") + ", expected=" + expectedClamp.ToString("R") +
                ", actual=" + actualClamp.ToString("R"));
        clampChecks++;
    }

    static float ReferenceHeight(ArenaBoundsFromVectorGrid bounds, float x)
    {
        if (!bounds.Grid) return 0;
        Vector2 half = bounds.Grid.size * .5f;
        if (!bounds.ovalOutline) return half.y;
        float halfWidth = bounds.Grid.size.x * .5f /
            Mathf.Sqrt(1 - Mathf.Pow(Mathf.Clamp(bounds.ovalEndHeightRatio, .3f, .95f), 2));
        float u = x / halfWidth;
        return half.y * Mathf.Sqrt(Mathf.Max(0, 1 - u * u));
    }

    static bool ReferenceOval(ArenaBoundsFromVectorGrid bounds, Vector3 p, float clearance)
    {
        Vector2 half = bounds.Grid.size * .5f;
        Vector3 scale = bounds.Grid.transform.lossyScale;
        float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y);
        if (Mathf.Abs(p.x) > half.x || Mathf.Abs(p.y) > ReferenceHeight(bounds, p.x) ||
            (half.x - Mathf.Abs(p.x)) * sx < clearance) return false;
        if (clearance <= 0) return true;
        Vector2 point = new Vector2(p.x * sx, Mathf.Abs(p.y) * sy);
        Vector2 a = new Vector2(-half.x * sx, ReferenceHeight(bounds, -half.x) * sy);
        for (int i = 1; i <= ArenaBoundsFromVectorGrid.OvalSegments; i++)
        {
            float x = Mathf.Lerp(-half.x, half.x, (float)i / ArenaBoundsFromVectorGrid.OvalSegments);
            Vector2 b = new Vector2(x * sx, ReferenceHeight(bounds, x) * sy);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(.000001f, ab.sqrMagnitude));
            if ((point - (a + ab * t)).sqrMagnitude < clearance * clearance) return false;
            a = b;
        }
        return true;
    }

    static Vector2 ReferenceHalfInset(ArenaBoundsFromVectorGrid bounds)
    {
        Vector2 half = bounds.Grid.size * .5f;
        Vector2 inset = ReferenceLocalDistance(bounds, bounds.insetWorld);
        return new Vector2(Mathf.Max(0, half.x - inset.x), Mathf.Max(0, half.y - inset.y));
    }

    static Vector2 ReferenceLocalDistance(ArenaBoundsFromVectorGrid bounds, float distance)
    {
        Vector3 scale = bounds.Grid.transform.lossyScale;
        return new Vector2(distance / Mathf.Max(.000001f, Mathf.Abs(scale.x)),
            distance / Mathf.Max(.000001f, Mathf.Abs(scale.y)));
    }

    static bool ReferenceContains(ArenaBoundsFromVectorGrid bounds, Vector3 world, float padding)
    {
        if (bounds.Grid == null) return false;
        Vector3 local = bounds.Grid.transform.InverseTransformPoint(world);
        if (bounds.ovalOutline) return ReferenceOval(bounds, local, bounds.insetWorld + Mathf.Max(0, padding));
        Vector2 inset = ReferenceHalfInset(bounds);
        Vector2 extra = ReferenceLocalDistance(bounds, Mathf.Max(0, padding));
        return Mathf.Abs(local.x) <= Mathf.Max(0, inset.x - extra.x) &&
            Mathf.Abs(local.y) <= Mathf.Max(0, inset.y - extra.y);
    }

    static Vector3 ReferenceClamp(ArenaBoundsFromVectorGrid bounds, Vector3 world, float padding)
    {
        if (bounds.Grid == null) return world;
        Transform transform = bounds.Grid.transform;
        Vector3 local = transform.InverseTransformPoint(world);
        if (bounds.ovalOutline)
        {
            float clearance = bounds.insetWorld + Mathf.Max(0, padding);
            if (ReferenceOval(bounds, local, clearance)) return world;
            float low = 0, high = 1;
            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * .5f;
                if (ReferenceOval(bounds, new Vector3(local.x * mid, local.y * mid, local.z), clearance)) low = mid;
                else high = mid;
            }
            local.x *= low; local.y *= low;
            return transform.TransformPoint(local);
        }
        Vector2 extra = ReferenceLocalDistance(bounds, Mathf.Max(0, padding));
        Vector2 half = ReferenceHalfInset(bounds);
        float halfX = Mathf.Max(0, half.x - extra.x), halfY = Mathf.Max(0, half.y - extra.y);
        local.x = Mathf.Clamp(local.x, -halfX, halfX);
        local.y = Mathf.Clamp(local.y, -halfY, halfY);
        return transform.TransformPoint(local);
    }
}
#endif
