#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Scene-isolated numerical and GPU depth checks; never saves authoring assets.</summary>
    public static class SingularityRenderingValidation
    {
        private sealed class TestAttractor : ISingularityAttractor
        {
            public SingularitySurface Surface { get; set; }
            public Vector2 SmoothedAttractionPosition { get; set; }
            public float AttractionRadius { get; set; } = 2f;
            public float AttractionPull { get; set; } = .8f;
            public bool IsAttractionActive { get; set; } = true;
        }

        [MenuItem("MASSIVE/SINGULARITY/Validate Surface Rendering")]
        public static void Run()
        {
            int checks = 0;
            Action<bool, string> check = (passed, message) => {
                if (!passed) throw new InvalidOperationException("SINGULARITY rendering: " + message);
                checks++;
            };
            var point = new Vector2(.2f, .1f);
            var source = new Vector4(.8f, .4f, 3f, .8f);
            var pair = new[] { source, new Vector4(-.6f, 1.1f, 2f, .5f) };
            Vector2 single = SingularityGridRenderer.EvaluateAttraction(point, new Vector2(source.x, source.y), source.z, source.w, 1f, 32f, 28f);
            Vector2 combined = SingularityGridRenderer.EvaluateCombinedAttraction(point, pair, 1, 1f, 32f, 28f);
            check(Vector2.Distance(single, combined) < .000001f, "single-source behavior retained");
            Vector2 forward = SingularityGridRenderer.EvaluateCombinedAttraction(point, pair, 2, 1f, 32f, 28f);
            Array.Reverse(pair);
            Vector2 reverse = SingularityGridRenderer.EvaluateCombinedAttraction(point, pair, 2, 1f, 32f, 28f);
            check(Vector2.Distance(forward, reverse) < .000001f, "multi-source field must not depend on registration order");
            check(Vector2.Distance(forward, point) > .01f, "both active sources produce attraction");
            check(SingularityGridRenderer.EvaluateCombinedAttraction(point, pair, 0, 1f, 32f, 28f) == point, "zero count disables field");
            check(SingularityGridRenderer.EvaluateCombinedAttraction(new Vector2(14f, 0f), pair, 2, 1f, 32f, 28f) == new Vector2(14f, 0f), "physical side edge remains fixed");
            var rear = new[] { new Vector4(.2f, 16f, 3f, .8f) };
            check(SingularityGridRenderer.EvaluateCombinedAttraction(point, rear, 1, 1f, 32f, 28f) == point, "opposite face cannot attract through screen overlap");
            var seam = new[] { new Vector4(0f, 31.9f, 3f, .8f) };
            check(SingularityGridRenderer.EvaluateCombinedAttraction(new Vector2(0f, .1f), seam, 1, 1f, 32f, 28f).y < .1f, "attraction crosses periodic seam by shortest distance");
            var invalid = new[] { new Vector4(float.NaN, 0f, 3f, .8f), new Vector4(0f, 0f, -1f, .8f) };
            check(SingularityGridRenderer.EvaluateCombinedAttraction(point, invalid, 2, 1f, 32f, 28f) == point, "invalid sources excluded");
            var crowd = new Vector4[16];
            for (int i = 0; i < crowd.Length; i++) crowd[i] = new Vector4(1f, .1f, 3f, 100f);
            check(SingularityGridRenderer.EvaluateCombinedAttraction(point, crowd, 16, 1f, 32f, 28f).x < 1f, "overlapping attraction stays softly bounded");

            Scene preview = EditorSceneManager.NewPreviewScene();
            Mesh actorMesh = null;
            Material actorMaterial = null;
            RenderTexture target = null;
            Texture2D readback = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var root = new GameObject("SINGULARITY rendering validation");
                SceneManager.MoveGameObjectToScene(root, preview);
                var surface = root.AddComponent<SingularitySurface>();
                var grid = new GameObject("lattice").AddComponent<SingularityGridRenderer>();
                SceneManager.MoveGameObjectToScene(grid.gameObject, preview);
                grid.transform.SetParent(root.transform, false);
                grid.surface = surface; grid.frontColor = Color.white; grid.rearColor = new Color(.1f, .15f, .2f, .2f);
                grid.lineWidthPixels = 3f; grid.showPlayerAttraction = false;
                grid.Rebuild();
                check(Mathf.Approximately(grid.rearDashLength, grid.EffectiveRearGap), "equal dash/gap enabled by default");
                grid.rearDashLength = .4f;
                check(Mathf.Approximately(grid.EffectiveRearGap, .4f), "gap tracks changed dash length");
                grid.equalRearDashesAndGaps = false; grid.rearGapLength = .17f;
                check(Mathf.Approximately(grid.EffectiveRearGap, .17f), "independent authoring remains available");
                var actorSource = new TestAttractor { Surface = surface };
                check(grid.RegisterAttractor(actorSource) && grid.RegisterAttractor(actorSource), "registration is duplicate safe");
                grid.showPlayerAttraction = true; grid.RefreshPresentation();
                check(grid.ActiveAttractorCount == 1, "duplicate source uploaded once");
                actorSource.IsAttractionActive = false; grid.RefreshPresentation();
                check(grid.ActiveAttractorCount == 0, "inactive source excluded");
                actorSource.IsAttractionActive = true; actorSource.Surface = null; grid.RefreshPresentation();
                check(grid.ActiveAttractorCount == 0, "wrong surface excluded");
                actorSource.Surface = surface; grid.showPlayerAttraction = false; grid.RefreshPresentation();
                check(grid.ActiveAttractorCount == 0, "attraction switch disables all registered players");
                grid.showPlayerAttraction = true; grid.UnregisterAttractor(actorSource); grid.RefreshPresentation();
                check(grid.ActiveAttractorCount == 0, "unregistered source removed");
                var registered = new TestAttractor[SingularityGridRenderer.MaximumAttractors];
                bool capacityAccepted = true;
                for (int i = 0; i < registered.Length; i++)
                {
                    registered[i] = new TestAttractor { Surface = surface };
                    capacityAccepted &= grid.RegisterAttractor(registered[i]);
                }
                check(capacityAccepted && !grid.RegisterAttractor(actorSource), "source capacity bounded to shader array length");
                grid.RefreshPresentation();
                check(grid.ActiveAttractorCount == registered.Length, "full source array uploads safely");
                foreach (TestAttractor registeredSource in registered) grid.UnregisterAttractor(registeredSource);
                grid.showPlayerAttraction = false; grid.RefreshPresentation();

                var cameraObject = new GameObject("render test camera");
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.scene = preview;
                camera.orthographic = true; camera.orthographicSize = 4f; camera.aspect = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.transform.SetPositionAndRotation(new Vector3(0f, 50f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
                camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
                target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                target.Create(); camera.targetTexture = target;
                readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);

                // This later transparent draw mirrors nugget depth behavior: if
                // lattice strokes do not write depth it incorrectly paints over them.
                actorMaterial = new Material(Shader.Find("MASSIVE/Singularity/Player Surface"));
                actorMesh = new Mesh { name = "render test actor" };
                actorMesh.vertices = new[] { new Vector3(-3f, 0f, -3f), new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, 3f), new Vector3(3f, 0f, -3f) };
                actorMesh.colors = new[] { Color.red, Color.red, Color.red, Color.red };
                actorMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                actorMesh.RecalculateBounds();
                var actor = new GameObject("later depth-tested actor");
                SceneManager.MoveGameObjectToScene(actor, preview);
                actor.AddComponent<MeshFilter>().sharedMesh = actorMesh;
                actor.AddComponent<MeshRenderer>().sharedMaterial = actorMaterial;
                actor.transform.position = new Vector3(0f, -surface.Depth - .1f, 0f);

                camera.Render(); RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); readback.Apply();
                Color behind = readback.GetPixel(128, 128);
                check(behind.g > .75f && behind.b > .75f, "front grid stroke covers late-drawn rear actor");
                Color emptyCell = readback.GetPixel(139, 139);
                check(emptyCell.r > .8f && emptyCell.g < .1f, "rear actor remains visible through empty lattice cells");

                actor.transform.position = new Vector3(0f, .15f, 0f);
                camera.Render(); RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); readback.Apply();
                Color inFront = readback.GetPixel(128, 128);
                check(inFront.r > .8f && inFront.g < .1f, "front actor covers grid beneath it");
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(preview);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                if (actorMaterial != null) UnityEngine.Object.DestroyImmediate(actorMaterial);
                if (actorMesh != null) UnityEngine.Object.DestroyImmediate(actorMesh);
            }
            Debug.Log("SINGULARITY rendering: " + checks + " checks passed, including GPU front/rear stroke occlusion.");
        }
    }
}
#endif
