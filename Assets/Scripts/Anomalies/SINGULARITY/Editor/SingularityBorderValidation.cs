#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Isolated mesh, property and GPU checks for centered rows and folded hard edges.</summary>
    public static class SingularityBorderValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Centered Lattice And Borders")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            int checks = 0;
            Action<bool, string> check = (passed, message) => {
                if (!passed) throw new InvalidOperationException("SINGULARITY borders: " + message);
                checks++;
            };
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D image = null;
            Mesh actorMesh = null;
            Material actorMaterial = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var root = new GameObject("Centered folded lattice validation");
                SceneManager.MoveGameObjectToScene(root, scene);
                var surface = root.AddComponent<SingularitySurface>();
                surface.ConfigureProjectedBounds(28f, 12f, .875f, 3f, 2.1f);
                var gridObject = new GameObject("lattice");
                SceneManager.MoveGameObjectToScene(gridObject, scene);
                gridObject.transform.SetParent(root.transform, false);
                var grid = gridObject.AddComponent<SingularityGridRenderer>();
                grid.surface = surface;
                grid.showPlayerAttraction = false;
                check(grid.sideBorderColor == Color.white && grid.sideBorderWidthPixels > grid.lineWidthPixels,
                    "hard edges default to independent thicker pure white");
                check(!grid.applySideBorderStyleToRear, "rear recoloring starts disabled");
                grid.Rebuild();
                ValidateLattice(grid, surface, check);
                grid.cellSpacing = .73f; grid.RefreshPresentation();
                ValidateLattice(grid, surface, check);
                surface.Configure(37f, 13.4f, .63f, 4.2f, 1.6f);
                grid.RefreshPresentation();
                ValidateLattice(grid, surface, check);
                grid.cellSpacing = .1f; surface.Configure(200f, 200f, .5f, 50f, 30f);
                grid.RefreshPresentation();
                ValidateLattice(grid, surface, check);
                check(grid.VertexCount <= 200000 && grid.SamplingLimited, "extreme lattice remains within existing vertex budget");

                surface.ConfigureProjectedBounds(28f, 12f, .875f, 3f, 2.1f);
                grid.cellSpacing = .73f; grid.lineWidthPixels = 1.3f;
                grid.frontColor = new Color(.04f, .1f, .7f, 1f);
                grid.rearColor = new Color(.03f, .07f, .15f, 1f);
                grid.sideBorderColor = Color.white; grid.sideBorderWidthPixels = 5f;
                grid.rearDashLength = .4f; grid.equalRearDashesAndGaps = true;
                grid.RefreshPresentation();
                Mesh originalMesh = grid.GetComponent<MeshFilter>().sharedMesh;
                grid.applySideBorderStyleToRear = true; grid.RefreshPresentation();
                var block = new MaterialPropertyBlock(); grid.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                check(block.GetVector("_SideBorderStyle").y == 1f, "rear toggle uploads without rebuilding geometry");
                check(grid.GetComponent<MeshFilter>().sharedMesh == originalMesh, "style tuning preserves generated mesh");
                grid.applySideBorderStyleToRear = false; grid.RefreshPresentation();

                var cameraObject = new GameObject("border render test camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 8f; camera.aspect = 2f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.transform.SetPositionAndRotation(new Vector3(0f, 50f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
                camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
                target = new RenderTexture(1024, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                target.Create(); camera.targetTexture = target;
                image = new Texture2D(1024, 512, TextureFormat.RGBA32, false);
                Render(camera, target, image);
                Vector3 frontEdge = surface.WorldPosition(surface.Width * .5f, surface.FrontHeight * .5f + .41f);
                check(IsWhite(Brightest(camera, image, frontEdge)), "front side border renders opaque white");
                float topS = Mathf.Lerp(surface.TopStart, surface.TopCrestDistance, .5f);
                float bottomS = surface.Wrap(surface.FrontHeight - topS);
                check(IsWhite(Brightest(camera, image, surface.WorldPosition(surface.Width * .5f, topS))), "front-facing upper curved edge retains solid white style");
                check(IsWhite(Brightest(camera, image, surface.WorldPosition(-surface.Width * .5f, bottomS))), "front-facing lower curved edge retains solid white style");
                int acrossCells = Mathf.Clamp(Mathf.RoundToInt(surface.Width / (grid.cellSpacing * 2f)) * 2, 2, 128);
                float innerColumn = surface.Width * .5f - surface.Width / acrossCells;
                float ordinaryEnergy = CrossEnergy(camera, image, surface.WorldPosition(innerColumn, surface.FrontHeight * .5f + .41f));
                check(CrossEnergy(camera, image, frontEdge) > ordinaryEnergy * 1.8f, "hard edge is visibly thicker than ordinary lattice");
                float dashS = FindRearSample(grid, surface, .2f);
                float gapS = FindRearSample(grid, surface, .6f);
                Vector3 rearDash = surface.WorldPosition(surface.Width * .5f, dashS);
                Vector3 rearGap = surface.WorldPosition(surface.Width * .5f, gapS);
                check(!IsWhite(Brightest(camera, image, rearDash)), "rear border retains its original subdued treatment with toggle off");
                grid.applySideBorderStyleToRear = true; grid.RefreshPresentation();
                Render(camera, target, image);
                check(IsWhite(Brightest(camera, image, rearDash)), "rear border receives white hard-edge style with toggle on");
                check(Brightest(camera, image, rearGap).maxColorComponent < .1f, "rear style toggle preserves empty dash gaps");
                ValidateFoldAndCrestRendering(grid, surface, camera, target, image, check);

                // A late transparent actor must not overpaint the opaque front
                // boundary. Rear gaps must not write a hidden depth occluder.
                actorMaterial = new Material(Shader.Find("MASSIVE/Singularity/Player Surface"));
                actorMesh = new Mesh { name = "border test rear actor" };
                actorMesh.vertices = new[] { new Vector3(10, 0, -4), new Vector3(10, 0, 4), new Vector3(15, 0, 4), new Vector3(15, 0, -4) };
                actorMesh.colors = new[] { Color.red, Color.red, Color.red, Color.red };
                actorMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; actorMesh.RecalculateBounds();
                var actor = new GameObject("late rear actor"); SceneManager.MoveGameObjectToScene(actor, scene);
                actor.AddComponent<MeshFilter>().sharedMesh = actorMesh;
                actor.AddComponent<MeshRenderer>().sharedMaterial = actorMaterial;
                actor.transform.position = new Vector3(0f, -surface.Depth - .1f, 0f);
                Render(camera, target, image);
                check(IsWhite(Brightest(camera, image, frontEdge)), "front boundary depth protects it from a later rear actor");
                Color gapWithActor = Brightest(camera, image, rearGap);
                check(gapWithActor.r > .8f && gapWithActor.g < .1f, "rear gap remains transparent to actors behind it");
                actor.transform.position = new Vector3(0f, .15f, 0f);
                Render(camera, target, image);
                Color actorAbove = Brightest(camera, image, frontEdge);
                check(actorAbove.r > .8f && actorAbove.g < .1f, "actor above the front face still draws over its boundary");
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (actorMaterial != null) UnityEngine.Object.DestroyImmediate(actorMaterial);
                if (actorMesh != null) UnityEngine.Object.DestroyImmediate(actorMesh);
            }
            return "SINGULARITY borders: " + checks + " centered-lattice, side-style and GPU depth checks passed.";
        }

        private static void ValidateLattice(SingularityGridRenderer grid, SingularitySurface surface, Action<bool, string> check)
        {
            Mesh mesh = grid.GetComponent<MeshFilter>().sharedMesh;
            var points = new List<Vector2>(); var strokes = new List<Vector4>();
            mesh.GetUVs(0, points); mesh.GetUVs(2, strokes);
            var rowSet = new HashSet<float>(); bool centerColumn = false, edgeTagsValid = true, bothSides = false;
            bool left = false, right = false;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i]; Vector4 stroke = strokes[i];
                if (stroke.z > .5f) rowSet.Add(p.y);
                else if (Mathf.Abs(p.x) < .0001f) centerColumn = true;
                bool expected = stroke.z < .5f && Mathf.Abs(Mathf.Abs(p.x) - surface.Width * .5f) < .0001f;
                edgeTagsValid &= (stroke.w > .5f) == expected;
                if (stroke.w > .5f) { left |= p.x < 0f; right |= p.x > 0f; }
            }
            bothSides = left && right;
            var rows = new List<float>(rowSet); rows.Sort();
            float frontCenter = surface.FrontHeight * .5f;
            float rearCenter = surface.RearStart + surface.FrontHeight * surface.RearScale * .5f;
            bool front = false, rear = false, spacing = true, symmetry = true;
            float[] boundaries = { 0f, surface.TopStart, surface.TopCrestDistance, surface.RearStart,
                surface.BottomStart, surface.BottomCrestDistance, surface.LoopLength };
            var regionalRows = new int[6];
            foreach (float row in rows)
                regionalRows[RegionIndex(row, boundaries)]++;
            for (int i = 0; i < rows.Count; i++)
            {
                front |= Mathf.Abs(surface.LoopDelta(rows[i], frontCenter)) < .0001f;
                rear |= Mathf.Abs(surface.LoopDelta(rows[i], rearCenter)) < .0001f;
                float next = rows[(i + 1) % rows.Count];
                int region = RegionIndex(rows[i], boundaries);
                float expectedGap = (boundaries[region + 1] - boundaries[region]) / regionalRows[region];
                spacing &= Mathf.Abs(surface.Wrap(next - rows[i]) - expectedGap) < .0002f;
                float mirrored = surface.Wrap(surface.FrontHeight - rows[i]); bool found = false;
                foreach (float candidate in rows) found |= Mathf.Abs(surface.LoopDelta(candidate, mirrored)) < .0002f;
                symmetry &= found;
            }
            check(centerColumn, "center longitudinal line lies exactly at X=0");
            check(front && rear && rows.Count % 2 == 0, "both face centers have exact Z=0 horizontal lines");
            int frontRows = regionalRows[0], rearRows = regionalRows[3];
            check(spacing && frontRows == rearRows && regionalRows[1] == regionalRows[5]
                    && regionalRows[2] == regionalRows[4],
                "faces and mirrored crest-split turns use balanced spacing without duplicated seams");
            check(frontRows % 2 == 0 && Mathf.Abs((surface.FrontHeight * surface.RearScale / rearRows)
                    / (surface.FrontHeight / frontRows) - surface.RearScale) < .0001f,
                "rear row spacing shrinks by rear scale instead of superimposing all front rows");
            check(symmetry, "horizontal rows are mirror-symmetric above and below center");
            check(edgeTagsValid && bothSides, "only the two longitudinal boundaries carry hard-edge tags");
            foreach (float crest in new[] { surface.TopCrestDistance, surface.BottomCrestDistance })
            {
                int matchingRows = 0; foreach (float row in rows) if (Mathf.Abs(surface.LoopDelta(row, crest)) < .0001f) matchingRows++;
                bool tagged = false;
                for (int i = 0; i < points.Count; i++)
                    tagged |= strokes[i].z > 1.5f && Mathf.Abs(surface.LoopDelta(points[i].y, crest)) < .0001f;
                check(matchingRows == 1 && tagged, "each actual crest has one exact solid-front-style horizontal row");
                check(Mathf.Abs(Mathf.Abs(surface.Evaluate(0f, crest).z) - surface.ProjectedHeight * .5f) < .0002f,
                    "crest row coincides with the true projected outermost Z extent");
            }
        }

        private static int RegionIndex(float s, float[] boundaries)
        {
            for (int i = 1; i < boundaries.Length - 1; i++) if (s < boundaries[i] - .00001f) return i - 1;
            return boundaries.Length - 2;
        }

        private static void ValidateFoldAndCrestRendering(SingularityGridRenderer grid, SingularitySurface surface,
            Camera camera, RenderTexture target, Texture2D image, Action<bool, string> check)
        {
            MeshFilter filter = grid.GetComponent<MeshFilter>(); Mesh source = filter.sharedMesh;
            Mesh isolated = UnityEngine.Object.Instantiate(source);
            isolated.name = "Isolated production fold strips for GPU regression";
            var points = new List<Vector2>(); var strokes = new List<Vector4>();
            source.GetUVs(0, points); source.GetUVs(2, strokes); int[] triangles = source.triangles;
            Vector3 cameraPosition = camera.transform.position;
            float cameraSize = camera.orthographicSize, originalWidth = grid.lineWidthPixels;
            Color frontColor = grid.frontColor, rearColor = grid.rearColor;
            bool rearStyle = grid.applySideBorderStyleToRear;
            try
            {
                filter.sharedMesh = isolated;
                // Sample the real generated strips in isolation. Other front rows
                // must not fill a rear dash gap and mask the rendering regression.
                float[] starts = { surface.TopStart, surface.TopCrestDistance,
                    surface.BottomStart, surface.BottomCrestDistance };
                float[] ends = { surface.TopCrestDistance, surface.RearStart,
                    surface.BottomCrestDistance, surface.LoopLength };
                for (int side = -1; side <= 1; side += 2)
                for (int region = 0; region < starts.Length; region++)
                {
                    float start = starts[region], end = ends[region];
                    bool frontFacing = region == 0 || region == 3;
                    var selected = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        float middle = (points[a].y + points[b].y + points[c].y) / 3f;
                        if (strokes[a].w > .5f && strokes[a].z < .5f && points[a].x * side > 0f
                            && middle >= start && middle < end)
                        { selected.Add(a); selected.Add(b); selected.Add(c); }
                    }
                    isolated.SetTriangles(selected, 0, false);
                    Vector3 center = surface.WorldPosition(side * surface.Width * .5f, (start + end) * .5f);
                    camera.orthographicSize = 2f;
                    camera.transform.position = new Vector3(center.x, 50f, center.z);
                    float dashS = FindFoldPhase(surface, camera, side, start, end, grid.rearDashLength, .5f);
                    float gapS = FindFoldPhase(surface, camera, side, start, end, grid.rearDashLength, 1.5f);
                    for (int style = 0; style <= 1; style++)
                    {
                        grid.applySideBorderStyleToRear = style == 1; grid.RefreshPresentation();
                        Render(camera, target, image);
                        Color dash = Brightest(camera, image, surface.WorldPosition(side * surface.Width * .5f, dashS));
                        Color gap = Brightest(camera, image, surface.WorldPosition(side * surface.Width * .5f, gapS));
                        string label = (side < 0 ? "left " : "right ") + (region < 2 ? "top " : "bottom ")
                            + (frontFacing ? "front-facing " : "rear-facing ") + "fold, rear style " + style;
                        check(frontFacing || style == 1 ? IsWhite(dash) : !IsWhite(dash) && dash.maxColorComponent > .05f,
                            label + " has the correct visible dash/stroke color");
                        check(frontFacing ? IsWhite(gap) : gap.maxColorComponent < .08f,
                            label + (frontFacing ? " stays continuous through would-be rear dash gaps" : " keeps actual empty dash gaps"));
                    }
                }

                camera.transform.position = cameraPosition; camera.orthographicSize = cameraSize;
                grid.frontColor = Color.green; grid.rearColor = Color.red; grid.lineWidthPixels = 4f;
                float crestOffset = (surface.TopCrestDistance - surface.TopStart) / grid.cellSpacing;
                check(Mathf.Abs(crestOffset - Mathf.Round(crestOffset)) > .01f,
                    "GPU fixture places the crest off the ordinary cell-spacing lattice");
                foreach (float crest in new[] { surface.TopCrestDistance, surface.BottomCrestDistance })
                {
                    var selected = new List<int>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i];
                        if (strokes[a].z > 1.5f && Mathf.Abs(surface.LoopDelta(points[a].y, crest)) < .0001f)
                        { selected.Add(a); selected.Add(triangles[i + 1]); selected.Add(triangles[i + 2]); }
                    }
                    isolated.SetTriangles(selected, 0, false);
                    for (int style = 0; style <= 1; style++)
                    {
                        grid.applySideBorderStyleToRear = style == 1; grid.RefreshPresentation();
                        Render(camera, target, image);
                        bool solidFrontColor = true;
                        // More than a dozen dash periods are covered: neither a
                        // phase-coincident sample nor a nearby vertical line can
                        // disguise a missing/dashed crest row.
                        for (float x = -surface.Width * .4f; x <= surface.Width * .4f; x += .173f)
                        {
                            Color color = Brightest(camera, image, surface.WorldPosition(x, crest));
                            solidFrontColor &= color.g > .7f && color.r < .1f && color.b < .1f;
                        }
                        check(solidFrontColor, "exact " + (crest == surface.TopCrestDistance ? "top" : "bottom")
                            + " crest row is entirely solid Front Color with rear style " + style);
                    }
                }
            }
            finally
            {
                filter.sharedMesh = source;
                grid.frontColor = frontColor; grid.rearColor = rearColor; grid.lineWidthPixels = originalWidth;
                grid.applySideBorderStyleToRear = rearStyle; grid.RefreshPresentation();
                camera.transform.position = cameraPosition; camera.orthographicSize = cameraSize;
                UnityEngine.Object.DestroyImmediate(isolated);
            }
        }

        private static float FindFoldPhase(SingularitySurface surface, Camera camera, int side,
            float start, float end, float dashLength, float phaseMultiplier)
        {
            float period = dashLength * 2f, desired = dashLength * phaseMultiplier;
            for (float s = start + .2f; s < end - .2f; s += .003f)
            {
                if (Mathf.Abs(Mathf.Repeat(s, period) - desired) > .008f) continue;
                Vector3 before = camera.WorldToScreenPoint(surface.WorldPosition(side * surface.Width * .5f, s - .06f));
                Vector3 after = camera.WorldToScreenPoint(surface.WorldPosition(side * surface.Width * .5f, s + .06f));
                // Stay away from almost edge-on compression where a whole dash
                // gap fits inside the pixel sampling/antialiasing footprint.
                if (new Vector2(after.x - before.x, after.y - before.y).magnitude > 2.5f) return s;
            }
            throw new InvalidOperationException("No well-resolved fold dash/gap sample found for " + start + ".." + end);
        }

        private static float FindRearSample(SingularityGridRenderer grid, SingularitySurface surface, float desiredPhase)
        {
            var points = new List<Vector2>(); var strokes = new List<Vector4>();
            grid.GetComponent<MeshFilter>().sharedMesh.GetUVs(0, points);
            grid.GetComponent<MeshFilter>().sharedMesh.GetUVs(2, strokes);
            var rows = new HashSet<float>();
            for (int i = 0; i < points.Count; i++) if (strokes[i].z > .5f) rows.Add(surface.Evaluate(0f, points[i].y).z);
            float period = grid.rearDashLength + grid.EffectiveRearGap;
            for (float s = surface.RearStart + 1f; s < surface.BottomStart - 1f; s += .007f)
            {
                if (Mathf.Abs(Mathf.Repeat(s, period) - desiredPhase) > .015f) continue;
                float z = surface.Evaluate(0f, s).z;
                if (Mathf.Abs(z) > 3.5f) continue;
                bool clear = true; foreach (float row in rows) clear &= Mathf.Abs(z - row) > .12f;
                if (clear) return s;
            }
            throw new InvalidOperationException("No isolated rear dash sample found.");
        }

        private static void Render(Camera camera, RenderTexture target, Texture2D image)
        {
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        }
        private static bool IsWhite(Color color) { return color.r > .7f && color.g > .7f && color.b > .7f; }
        private static Color Brightest(Camera camera, Texture2D image, Vector3 world)
        {
            Vector3 pixel = camera.WorldToScreenPoint(world); Color brightest = Color.black;
            int x = Mathf.RoundToInt(pixel.x - .5f), y = Mathf.RoundToInt(pixel.y - .5f);
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                Color sample = image.GetPixel(Mathf.Clamp(x + dx, 0, image.width - 1), Mathf.Clamp(y + dy, 0, image.height - 1));
                if (sample.r + sample.g + sample.b > brightest.r + brightest.g + brightest.b) brightest = sample;
            }
            return brightest;
        }
        private static float CrossEnergy(Camera camera, Texture2D image, Vector3 world)
        {
            Vector3 pixel = camera.WorldToScreenPoint(world); float sum = 0f;
            int x = Mathf.RoundToInt(pixel.x - .5f), y = Mathf.RoundToInt(pixel.y - .5f);
            for (int dx = -6; dx <= 6; dx++) sum += image.GetPixel(x + dx, y).maxColorComponent;
            return sum;
        }
    }
}
#endif
