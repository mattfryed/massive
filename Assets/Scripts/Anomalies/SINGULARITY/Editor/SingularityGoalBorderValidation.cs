using System;
using Massive.Singularity;
using Shapes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SingularityGoalBorderValidation
{
    [MenuItem("MASSIVE/SINGULARITY/Validate Goal Border Extensions")]
    public static string Run()
    {
        int count = 0;
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Isolated goal border test");
            SceneManager.MoveGameObjectToScene(root, scene);
            var surface = root.AddComponent<SingularitySurface>();
            surface.ConfigureProjectedBounds(28f, 12f, .875f, 3f, 2.1f);
            var gridObject = new GameObject("Grid"); gridObject.transform.SetParent(root.transform);
            var grid = gridObject.AddComponent<SingularityGridRenderer>(); grid.surface = surface;
            var joins = root.AddComponent<SingularityGoalBorderExtensions>(); joins.grid = grid;
            joins.followAmplifierEffects = false;
            var left = MakeGoal(root.transform, "Left", -14f, 270f);
            var right = MakeGoal(root.transform, "Right", 14.01f, 90f);
            joins.leftGoalOutline = left; joins.rightGoalOutline = right;
            for (int team = 1; team <= 2; team++)
            for (int which = 0; which < 2; which++)
            {
                bool top = which == 0;
                Require(joins.TryGetSegment(team, top, out Vector3 a, out Vector3 b, out Color color), "Segment exists", ref count);
                Require(Mathf.Abs(a.z - (top ? 6f : -6f)) < .00001f && Mathf.Abs(b.z - a.z) < .00001f,
                    "Straight join reaches exact top/bottom Z extremum", ref count);
                Require(Mathf.Abs(a.y - b.y) < .00001f && Mathf.Abs(a.y - 26.93f) < .00001f,
                    "Join preserves authored goal overlay plane", ref count);
                Vector3 crest = surface.WorldPosition(team == 1 ? -14f : 14f,
                    top ? surface.TopCrestDistance : surface.BottomCrestDistance);
                Require(Mathf.Abs(b.x - crest.x) < .00001f && Mathf.Abs(b.z - crest.z) < .00001f,
                    "Joined endpoint follows the actual curved side border", ref count);
                Require((team == 1 ? b.x > a.x : b.x < a.x) && color == Color.white,
                    "White extension travels inward from unchanged goal", ref count);
            }
            Require(left.Radius == 6f && right.Radius == 6f && left.transform.position.x == -14f && right.transform.position.x == 14.01f,
                "No goal resizing or relocation", ref count);
            ValidateVisibleJoins(scene, joins, ref count);
            int revision = surface.Revision;
            float crestBefore = surface.TopCrestDistance;
            surface.ConfigureProjectedBounds(28f, 12f, .8f, 4f, 1.7f);
            Require(surface.Revision != revision && surface.TopCrestDistance != crestBefore,
                "Crests update with surface geometry", ref count);
            Require(Mathf.Abs(surface.Evaluate(0f, surface.TopCrestDistance).z - 6f) < .00001f &&
                Mathf.Abs(surface.Evaluate(0f, surface.BottomCrestDistance).z + 6f) < .00001f,
                "Updated crests still meet exact requested extrema", ref count);
            joins.leftGoalOutline = null;
            Require(!joins.TryGetSegment(1, true, out _, out _, out _), "Missing goal safely hides its joins", ref count);
            joins.enabled = false;
            Require(!joins.TryGetSegment(2, true, out _, out _, out _), "Disabled join component emits no segments", ref count);
            string report = "SINGULARITY goal border: PASS " + count + " checks (exact four joins, GPU-visible strokes, standard goals preserved, geometry updates and lifecycle gates).";
            Debug.Log(report); return report;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static Disc MakeGoal(Transform parent, string name, float x, float yAngle)
    {
        var go = new GameObject(name); go.transform.SetParent(parent);
        go.transform.SetPositionAndRotation(new Vector3(x, 26.93f, 0f), Quaternion.Euler(90f, yAngle, 0f));
        var disc = go.AddComponent<Disc>(); disc.Type = DiscType.Arc; disc.Radius = 6f;
        disc.AngRadiansStart = 0f; disc.AngRadiansEnd = Mathf.PI;
        disc.Thickness = .05f; disc.Color = Color.white; return disc;
    }

    private static void ValidateVisibleJoins(Scene scene, SingularityGoalBorderExtensions joins, ref int count)
    {
        RenderTexture target = null;
        Texture2D baseline = null, rendered = null;
        RenderTexture previous = RenderTexture.active;
        bool wasEnabled = joins.enabled;
        try
        {
            var cameraObject = new GameObject("isolated goal join GPU camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.scene = scene;
            // Shapes' automatic camera callback explicitly skips Preview cameras.
            // This avoids every active scene's ImmediateModeShapeDrawer submitting
            // into our render. Only the component under test is submitted below.
            camera.cameraType = CameraType.Preview;
            camera.orthographic = true; camera.orthographicSize = 8f; camera.aspect = 2f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 50f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
            target = new RenderTexture(1024, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            target.Create(); camera.targetTexture = target;
            baseline = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
            rendered = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
            var midpoints = new Vector3[4];
            for (int team = 1; team <= 2; team++)
            for (int side = 0; side < 2; side++)
            {
                if (!joins.TryGetSegment(team, side == 0, out Vector3 start, out Vector3 end, out _))
                    throw new InvalidOperationException("Missing authored join for GPU regression.");
                midpoints[(team - 1) * 2 + side] = Vector3.Lerp(start, end, .5f);
            }

            joins.enabled = false;
            ReadCamera(camera, target, baseline);
            joins.enabled = true;
            joins.DrawShapes(camera);
            ReadCamera(camera, target, rendered);
            for (int i = 0; i < midpoints.Length; i++)
            {
                float before = MidpointBrightness(camera, baseline, midpoints[i]);
                float after = MidpointBrightness(camera, rendered, midpoints[i]);
                Require(after > .5f && after - before > .4f,
                    "Goal " + (i / 2 + 1) + (i % 2 == 0 ? " upper" : " lower")
                    + " join visibly bridges the gap (enabled " + after.ToString("0.###")
                    + ", disabled " + before.ToString("0.###") + ")", ref count);
            }
        }
        finally
        {
            joins.enabled = wasEnabled;
            RenderTexture.active = previous;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (baseline != null) UnityEngine.Object.DestroyImmediate(baseline);
            if (rendered != null) UnityEngine.Object.DestroyImmediate(rendered);
        }
    }

    private static void ReadCamera(Camera camera, RenderTexture target, Texture2D image)
    {
        camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
    }
    private static float MidpointBrightness(Camera camera, Texture2D image, Vector3 world)
    {
        Vector3 pixel = camera.WorldToScreenPoint(world);
        int x = Mathf.RoundToInt(pixel.x - .5f), y = Mathf.RoundToInt(pixel.y - .5f);
        float value = 0f;
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            Color color = image.GetPixel(Mathf.Clamp(x + dx, 0, image.width - 1), Mathf.Clamp(y + dy, 0, image.height - 1));
            value = Mathf.Max(value, Mathf.Min(color.r, Mathf.Min(color.g, color.b)));
        }
        return value;
    }
    private static void Require(bool condition, string label, ref int count)
    { if (!condition) throw new InvalidOperationException("SINGULARITY goal border: " + label); count++; }
}
