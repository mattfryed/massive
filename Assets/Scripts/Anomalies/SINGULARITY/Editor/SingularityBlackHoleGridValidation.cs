#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Isolated checks for the black hole's presentation-only field.
    /// Never opens, saves, or modifies a production scene or material.</summary>
    public static class SingularityBlackHoleGridValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Black Hole Grid Attraction")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run black-hole grid validation in Edit Mode.");
            int checks = 0;
            Action<bool, string> check = (ok, label) => {
                if (!ok) throw new InvalidOperationException("SINGULARITY black-hole grid: " + label);
                checks++;
            };
            Scene scene = EditorSceneManager.NewPreviewScene();
            Scene otherScene = default(Scene);
            RenderTexture target = null;
            Texture2D readback = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var root = Create("Black-hole grid validation", scene);
                var surface = root.AddComponent<SingularitySurface>();
                surface.Configure(28f, 10f, .875f, 3f, 2.1f);
                float height = surface.FrontHeight;
                Vector2 center = Vector2.zero;
                Func<Vector2, float, float, bool, Vector2> field = (point, pull, feather, rear) =>
                    SingularityGridRenderer.EvaluateBlackHoleOffset(point, center, 2f, pull, feather, rear,
                        surface.Width, height, surface.RearStart, surface.RearScale, surface.LoopLength);
                Func<Vector2, bool, Vector2> chart = (point, rear) => new Vector2(point.x,
                    rear ? surface.RearStart + (height * .5f - point.y) * surface.RearScale : height * .5f + point.y);

                Vector2 a = field(chart(Vector2.right, false), 1.25f, 1f, true);
                Vector2 b = field(chart(Vector2.left, false), 1.25f, 1f, true);
                check(a.x < 0f && Mathf.Abs(a.y) < .000001f, "field attracts inward without a sideways bias");
                check((a + b).sqrMagnitude < .000000001f, "left and right attraction are symmetric");
                Vector2 diagonal = field(chart(new Vector2(.6f, .8f), false), 1.25f, 1f, true);
                check((diagonal - new Vector2(a.x * .6f, a.x * .8f)).sqrMagnitude < .000000001f,
                    "diagonal attraction has the same radial profile");
                check(field(chart(Vector2.zero, false), 1.25f, 1f, true) == Vector2.zero,
                    "exact center is finite and stationary");
                check(field(chart(Vector2.right, false), 0f, 1f, true) == Vector2.zero, "zero pull disables displacement");
                check(field(chart(Vector2.right * 2f, false), 1.25f, 1f, true) == Vector2.zero,
                    "radius boundary is exactly still");
                check(field(chart(Vector2.right * 3f, false), 1.25f, 1f, true) == Vector2.zero,
                    "outside the radius is unchanged");
                check(field(chart(Vector2.right * 1.9999f, false), 1.25f, 1f, true).magnitude < .0001f,
                    "outer feather approaches zero continuously");
                check(field(chart(Vector2.right * .0001f, false), 1.25f, 1f, true).magnitude < .0001f,
                    "toward-vector compression is continuous through the center");
                check(field(chart(Vector2.right, false), 1.25f, .05f, true).magnitude > a.magnitude,
                    "feather independently controls the broad outer falloff");
                Vector2 extreme = field(chart(Vector2.right, false), 1000000f, 1f, true);
                check(Finite(extreme) && extreme.x < 0f && extreme.magnitude <= .85001f,
                    "extreme pull is bounded without crossing the center");
                float lastRadius = -1f;
                for (int i = 0; i <= 64; i++)
                {
                    float radius = i * 2f / 64f;
                    Vector2 offset = field(chart(Vector2.right * radius, false), 1000f, 1f, true);
                    float deformedRadius = radius + offset.x;
                    check(Finite(offset) && deformedRadius >= lastRadius - .000001f && deformedRadius >= -.000001f,
                        "isolated radial field remains ordered at sample " + i);
                    lastRadius = deformedRadius;
                }
                Vector2 point = new Vector2(.6f, .8f);
                Vector2 frontOffset = field(chart(point, false), 1.25f, 1f, true);
                Vector2 rearOffset = field(chart(point, true), 1.25f, 1f, true);
                check((rearOffset - new Vector2(frontOffset.x, -frontOffset.y * surface.RearScale)).sqrMagnitude < .000000001f,
                    "rear displacement reverses Z and respects rear-scale conversion");
                check(field(chart(point, true), 1.25f, 1f, false) == Vector2.zero,
                    "rear toggle removes only the rear field");
                check((field(chart(point, false), 1.25f, 1f, false) - frontOffset).sqrMagnitude < .000000001f,
                    "rear toggle does not change the front field");
                check((field(chart(point, false) + Vector2.up * surface.LoopLength, 1.25f, 1f, true) - frontOffset).sqrMagnitude < .00000001f,
                    "periodic chart coordinates sample the same field");
                foreach (float s in new[] { 0f, surface.TopStart, (surface.TopStart + surface.RearStart) * .5f,
                    surface.RearStart, surface.BottomStart, (surface.BottomStart + surface.LoopLength) * .5f, surface.LoopLength })
                {
                    check(SingularityGridRenderer.EvaluateBlackHoleOffset(new Vector2(0f, s), center, 100f, 100f, 1f, true,
                        surface.Width, height, surface.RearStart, surface.RearScale, surface.LoopLength) == Vector2.zero,
                        "folds and joins stay pinned at " + s);
                }
                check(SingularityGridRenderer.EvaluateBlackHoleOffset(new Vector2(surface.Width * .5f, height * .5f), center,
                    100f, 100f, 1f, true, surface.Width, height, surface.RearStart, surface.RearScale, surface.LoopLength) == Vector2.zero,
                    "hard side border remains pinned with an oversized field");

                var portalObject = Create("Black-hole field source", scene);
                var portal = portalObject.AddComponent<SingularityBlackHolePortal>();
                portal.surface = surface;
                portal.enableGridAttraction = true; portal.gridAttractionOnRear = true;
                portal.gridAttractionRadius = 4.5f; portal.gridAttractionPull = 1.25f; portal.gridAttractionFeather = 1f;
                float[] physicsBefore = PhysicsSettings(portal);
                var body = portalObject.AddComponent<Rigidbody>(); body.useGravity = false;
                body.mass = 3.25f; body.linearDamping = 1.75f;
                var grid = root.AddComponent<SingularityGridRenderer>(); grid.surface = surface; grid.blackHole = portal;
                grid.showPlayerAttraction = false; grid.Rebuild();
                var renderer = grid.GetComponent<MeshRenderer>();
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                check(block.GetVector("_BlackHoleGridField") == new Vector4(0f, 0f, 4.5f, 1.25f),
                    "independent grid field binds despite the player-attraction toggle being off");
                check(block.GetVector("_BlackHoleGridShape") == new Vector4(height, surface.RearStart, surface.RearScale, 1f),
                    "shader receives exact front/rear chart dimensions");
                check(block.GetVector("_BlackHoleGridProfile").x == 1f, "shader receives the authored feather");
                Mesh mesh = grid.GetComponent<MeshFilter>().sharedMesh;
                Texture2D lookup = grid.SurfaceLookup;
                portal.gridAttractionRadius = 3f; portal.gridAttractionPull = .8f;
                portal.gridAttractionFeather = .6f; portal.gridAttractionOnRear = false;
                portalObject.transform.position = new Vector3(.4f, 0f, -.3f);
                grid.RefreshPresentation(); renderer.GetPropertyBlock(block);
                check(block.GetVector("_BlackHoleGridField") == new Vector4(.4f, -.3f, 3f, .8f),
                    "live center, radius and pull edits update shader bindings");
                check(block.GetVector("_BlackHoleGridProfile").x == .6f && block.GetVector("_BlackHoleGridShape").w == 0f,
                    "live feather and rear toggle edits update shader bindings");
                check(grid.GetComponent<MeshFilter>().sharedMesh == mesh && grid.SurfaceLookup == lookup,
                    "field-only tuning never rebuilds the mesh or surface lookup");
                check(Equal(physicsBefore, PhysicsSettings(portal)), "grid tuning leaves all actor force and transit settings unchanged");
                check(body.mass == 3.25f && body.linearDamping == 1.75f && !body.isKinematic && !body.useGravity,
                    "presentation refresh does not change Rigidbody settings");

                Action<string> checkInactive = label => {
                    grid.RefreshPresentation(); renderer.GetPropertyBlock(block);
                    Vector4 value = block.GetVector("_BlackHoleGridField");
                    check(value.z == 0f || value.w == 0f, label);
                };
                portal.enableGridAttraction = false; checkInactive("disabled grid option clears the prior field");
                portal.enableGridAttraction = true; portal.enabled = false; checkInactive("disabled portal clears the prior field");
                portal.enabled = true; portalObject.SetActive(false); checkInactive("inactive portal object clears the prior field");
                portalObject.SetActive(true); grid.blackHole = null; checkInactive("missing explicit source clears the prior field");
                grid.blackHole = portal;
                var otherRoot = Create("Different surface", scene);
                var otherSurface = otherRoot.AddComponent<SingularitySurface>(); portal.surface = otherSurface;
                checkInactive("source on another surface is rejected"); portal.surface = surface;
                otherScene = EditorSceneManager.NewPreviewScene();
                SceneManager.MoveGameObjectToScene(portalObject, otherScene);
                checkInactive("source in another scene is rejected"); SceneManager.MoveGameObjectToScene(portalObject, scene);
                portalObject.transform.position = new Vector3(surface.Width, 0f, 0f);
                checkInactive("out-of-face source is rejected"); portalObject.transform.position = Vector3.zero;
                portal.gridAttractionPull = float.NaN; checkInactive("non-finite pull cannot leak into shader data");
                portal.gridAttractionPull = 1.25f;
                grid.RefreshPresentation(); renderer.GetPropertyBlock(block);
                check(block.GetVector("_BlackHoleGridField").w > 0f, "valid source recovers after all disable and invalid-reference cases");

                grid.frontColor = Color.white; grid.rearColor = Color.clear;
                grid.sideBorderColor = Color.white; grid.applySideBorderStyleToRear = false;
                portal.gridAttractionRadius = 4.5f; portal.gridAttractionFeather = 1f; portal.gridAttractionOnRear = true;
                var cameraObject = Create("Black-hole grid validation camera", scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 7.5f; camera.aspect = 2f;
                camera.transform.SetPositionAndRotation(Vector3.up * 30f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                target = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                target.Create(); camera.targetTexture = target;
                readback = new Texture2D(256, 128, TextureFormat.RGBAFloat, false, true);
                portal.enableGridAttraction = false; grid.RefreshPresentation();
                Color[] neutral = Render(camera, target, readback);
                portal.enableGridAttraction = true; grid.RefreshPresentation();
                Color[] attracted = Render(camera, target, readback);
                check(Energy(neutral) > 50f, "GPU fixture renders a visible lattice");
                check(Difference(neutral, attracted) > 10f, "actual grid shader visibly deforms with the field enabled");
                portal.enableGridAttraction = false; grid.RefreshPresentation();
                check(Difference(neutral, Render(camera, target, readback)) < .001f,
                    "GPU field toggle returns exactly to the neutral lattice without persistent deformation");
                grid.frontColor = Color.clear; grid.sideBorderColor = Color.clear; grid.rearColor = Color.white;
                grid.RefreshPresentation();
                Color[] rearNeutral = Render(camera, target, readback);
                portal.enableGridAttraction = true; portal.gridAttractionOnRear = false; grid.RefreshPresentation();
                check(Difference(rearNeutral, Render(camera, target, readback)) < .001f,
                    "GPU rear toggle leaves the rear lattice unchanged while the front field remains enabled");
                portal.gridAttractionOnRear = true; grid.RefreshPresentation();
                check(Difference(rearNeutral, Render(camera, target, readback)) > 5f,
                    "GPU rear field deforms the actual dashed rear lattice");
            }
            finally
            {
                RenderTexture.active = previous;
                if (otherScene.IsValid()) EditorSceneManager.ClosePreviewScene(otherScene);
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            }
            return "SINGULARITY black-hole grid: " + checks + " radial math, rear mapping, isolation, live binding and GPU checks passed.";
        }

        private static GameObject Create(string name, Scene scene)
        { var value = new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene); return value; }
        private static bool Finite(Vector2 value)
        { return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y); }
        private static float[] PhysicsSettings(SingularityBlackHolePortal portal)
        {
            return new[] { portal.attractionRadius, portal.attractionAcceleration, portal.attractionFeather,
                portal.swirlAcceleration, portal.centrifugalAcceleration, portal.captureRadius, portal.eventHorizonRadius,
                portal.exitPadding, portal.cooldownSeconds, portal.entrySeconds, portal.entryExponent,
                portal.minimumVisualScale, portal.maximumStretch, portal.exitSeconds, portal.exitExponent, portal.exitReleaseSpeed };
        }
        private static bool Equal(float[] a, float[] b)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static Color[] Render(Camera camera, RenderTexture target, Texture2D texture)
        {
            camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            return texture.GetPixels();
        }
        private static float Energy(Color[] pixels)
        { float result = 0f; foreach (Color pixel in pixels) result += pixel.r + pixel.g + pixel.b; return result; }
        private static float Difference(Color[] a, Color[] b)
        {
            float result = 0f;
            for (int i = 0; i < a.Length; i++) result += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
            return result;
        }
    }
}
#endif
