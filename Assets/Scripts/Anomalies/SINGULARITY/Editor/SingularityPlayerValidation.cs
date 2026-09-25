using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Isolated logical-motion and owned-resource checks. Never changes
    /// Play Mode, input configuration, loaded gameplay scenes, or saved assets.</summary>
    public static class SingularityPlayerValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Prototype Player")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Run SINGULARITY player validation in Edit Mode.");
            var passed = new List<string>();
            var measurements = new List<string>();
            Scene originalActiveScene = SceneManager.GetActiveScene();
            int originalSceneCount = SceneManager.sceneCount;
            UnityEngine.Object originalSelection = Selection.activeObject;
            Scene fixtureScene = EditorSceneManager.NewPreviewScene();
            GameObject surfaceObject = null, playerObject = null;
            try
            {
                surfaceObject = new GameObject("SINGULARITY validation surface");
                playerObject = new GameObject("SINGULARITY validation player");
                surfaceObject.SetActive(false);
                playerObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(surfaceObject, fixtureScene);
                SceneManager.MoveGameObjectToScene(playerObject, fixtureScene);
                surfaceObject.hideFlags = playerObject.hideFlags = HideFlags.HideAndDontSave;
                var surface = surfaceObject.AddComponent<SingularitySurface>();
                surface.Configure(28f, 12f, .875f, 3f, 2.1f);
                var player = playerObject.AddComponent<SingularityPlayerMotor>();
                player.Surface = surface;
                Check(player.Surface == surface && player.AttractionRadius > 0 && player.AttractionPull > 0,
                    "explicit isolated surface and independent attraction tuning are available", passed);

                ValidateTimeAndReset(surface, player, passed);
                ValidateLoop(surface, player, 1f, passed);
                ValidateLoop(surface, player, -1f, passed);
                ValidatePhysicalSpeed(surface, player, passed, measurements);
                ValidateAttractionWrap(surface, player, 1f, passed);
                ValidateAttractionWrap(surface, player, -1f, passed);
                ValidateSideEdges(surface, player, passed);
                ValidatePreviewAndResources(surface, player, surfaceObject, playerObject, passed);
            }
            finally
            {
                if (playerObject) UnityEngine.Object.DestroyImmediate(playerObject);
                if (surfaceObject) UnityEngine.Object.DestroyImmediate(surfaceObject);
                EditorSceneManager.ClosePreviewScene(fixtureScene);
            }
            Check(SceneManager.GetActiveScene() == originalActiveScene && SceneManager.sceneCount == originalSceneCount
                && Selection.activeObject == originalSelection,
                "fixture leaves loaded gameplay scenes, active scene, and selection unchanged", passed);
            return passed.Count + " SINGULARITY player checks passed.\n" + string.Join("\n", passed)
                + "\n\nMeasured path speeds (world units/second; consecutive world-position chords):\n"
                + string.Join("\n", measurements);
        }

        private static void ValidateTimeAndReset(SingularitySurface surface, SingularityPlayerMotor player, List<string> passed)
        {
            StartAt(player, 0f, .15f);
            Vector2 start = player.SurfacePosition;
            player.Simulate(.3f, Vector2.up);
            Check(player.SurfaceVelocity.y > 0 && surface.LoopDelta(start.y, player.SurfacePosition.y) > 0,
                "positive surface input advances immediately with acceleration", passed);
            Vector2 p = player.SurfacePosition, v = player.SurfaceVelocity, a = player.SmoothedAttractionPosition;
            foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                player.Simulate(dt, Vector2.one);
            Check(player.SurfacePosition == p && player.SurfaceVelocity == v && player.SmoothedAttractionPosition == a,
                "zero/pause, negative, and nonfinite time do not move player or attraction state", passed);

            float previousSpeed = player.SurfaceVelocity.magnitude;
            bool monotonic = true;
            for (int i = 0; i < 90; i++)
            {
                player.Simulate(1f / 120f, Vector2.zero);
                float speed = player.SurfaceVelocity.magnitude;
                monotonic &= speed <= previousSpeed + .000001f;
                previousSpeed = speed;
            }
            Check(monotonic && player.SurfaceVelocity == Vector2.zero,
                "released input brakes monotonically to an exact stop", passed);
            p = player.SurfacePosition;
            for (int i = 0; i < 120; i++) player.Simulate(1f / 120f, Vector2.zero);
            Check(player.SurfacePosition == p && Mathf.Abs(surface.LoopDelta(player.SmoothedAttractionPosition.y, p.y)) < .00002f,
                "stationary player does not creep and attraction settles without oscillation", passed);

            player.ResetToStart();
            Check(player.SurfacePosition == start && player.SmoothedAttractionPosition == start && player.SurfaceVelocity == Vector2.zero,
                "reset restores start, zero velocity, and coincident attraction center", passed);
            player.Simulate(.2f, new Vector2(float.NaN, 1));
            player.Simulate(.2f, new Vector2(1, float.PositiveInfinity));
            Check(player.SurfacePosition == start && player.SurfaceVelocity == Vector2.zero,
                "nonfinite input is rejected as neutral rather than poisoning coordinates", passed);
            player.Simulate(.2f, new Vector2(.01f, .01f));
            Check(player.SurfacePosition == start, "small input inside the deadzone does not drift", passed);
            player.SetInputOverride(new Vector2(float.NaN, 1));
            player.ClearInputOverride();
            Check(player.SurfacePosition == start, "override assignment and clear do not mutate logical position", passed);

            player.Surface = null;
            player.Simulate(1, Vector2.one);
            Check(player.SurfacePosition == Vector2.zero && player.SurfaceVelocity == Vector2.zero,
                "missing surface is a finite stationary state", passed);
            player.Surface = surface;
        }

        private static void ValidateLoop(SingularitySurface surface, SingularityPlayerMotor player, float sign, List<string> passed)
        {
            StartAt(player, 0f, sign > 0 ? .97f : .03f);
            const float dt = 1f / 120f;
            float cumulative = 0, maxWorldStep = 0;
            int wrapCount = 0;
            bool finite = true, monotonic = true, frontDirection = false, rearDirection = false;
            int count = Mathf.CeilToInt((surface.LoopLength * 1.2f / player.MoveSpeed + .5f) / dt);
            for (int i = 0; i < count; i++)
            {
                Vector2 before = player.SurfacePosition;
                Vector3 worldBefore = surface.WorldPosition(before.x, before.y);
                player.Simulate(dt, Vector2.up * sign);
                Vector2 after = player.SurfacePosition;
                Vector3 worldAfter = surface.WorldPosition(after.x, after.y);
                float delta = surface.LoopDelta(before.y, after.y);
                cumulative += delta * sign;
                monotonic &= delta * sign >= -.000001f;
                finite &= Finite(after) && Finite(player.SurfaceVelocity);
                maxWorldStep = Mathf.Max(maxWorldStep, Vector3.Distance(worldBefore, worldAfter));
                if (Mathf.Abs(after.y - before.y) > surface.LoopLength * .5f) wrapCount++;
                if (before.y > .3f && before.y < surface.TopStart - .3f && Mathf.Abs(delta) > .001f)
                    frontDirection |= (worldAfter.z - worldBefore.z) * sign > 0;
                if (before.y > surface.RearStart + .3f && before.y < surface.BottomStart - .3f && Mathf.Abs(delta) > .001f)
                    rearDirection |= (worldAfter.z - worldBefore.z) * sign < 0;
            }
            string label = sign > 0 ? "positive" : "negative";
            Check(finite && monotonic && cumulative > surface.LoopLength && wrapCount >= 1,
                label + " held input completes a full periodic loop without coordinate reversal", passed);
            Check(maxWorldStep <= player.MoveSpeed * dt * 1.025f,
                label + " traversal is physically continuous at joins and the wrap seam (max step " + F(maxWorldStep) + ")", passed);
            Check(frontDirection && rearDirection,
                label + " input naturally reverses its projected vertical direction on the rear face", passed);
        }

        private static void ValidatePhysicalSpeed(SingularitySurface surface, SingularityPlayerMotor player,
            List<string> passed, List<string> measurements)
        {
            Vector2[] endpoints = new Vector2[3];
            float[] pathLengths = new float[3];
            int[] rates = { 30, 60, 120 };
            for (int index = 0; index < rates.Length; index++)
            {
                int fps = rates[index];
                StartAt(player, 6f, .97f);
                float minSpeed = float.PositiveInfinity, maxSpeed = 0f, total = 0f;
                int frames = fps * 9;
                for (int i = 0; i < frames; i++)
                {
                    Vector3 before = World(surface, player.SurfacePosition);
                    player.Simulate(1f / fps, Vector2.up);
                    float distance = Vector3.Distance(before, World(surface, player.SurfacePosition));
                    total += distance;
                    if (i >= fps)
                    {
                        minSpeed = Mathf.Min(minSpeed, distance * fps);
                        maxSpeed = Mathf.Max(maxSpeed, distance * fps);
                    }
                }
                endpoints[index] = player.SurfacePosition;
                pathLengths[index] = total;
                Check(minSpeed > player.MoveSpeed * .98f && maxSpeed < player.MoveSpeed * 1.02f,
                    fps + " fps physical path speed remains within 2% through narrowing curls and both faces", passed);
                measurements.Add(fps + " fps loop: " + F(minSpeed) + "–" + F(maxSpeed) + "; total " + F(total));
            }
            Check(Vector2.Distance(endpoints[0], endpoints[1]) < .02f && Vector2.Distance(endpoints[1], endpoints[2]) < .02f
                && Mathf.Abs(pathLengths[0] - pathLengths[2]) < .08f,
                "30/60/120 fps converge to the same loop endpoint and physical path length", passed);

            foreach (Vector2 direction in new[] { Vector2.up, new Vector2(1f, 1f).normalized, new Vector2(-1f, 1f).normalized })
            {
                StartAt(player, 3f, (surface.TopStart - .2f) / surface.LoopLength);
                float worstError = MeasureSteadySpeed(surface, player, direction, 1.6f, 120);
                Check(worstError < player.MoveSpeed * .025f,
                    "physical speed is isotropic through the upper curl for input " + direction + " (max error " + F(worstError) + ")", passed);
            }
            surface.transform.SetPositionAndRotation(new Vector3(3, 2, -4), Quaternion.Euler(17, 31, -8));
            surface.transform.localScale = new Vector3(1.3f, .7f, .9f);
            StartAt(player, 3f, (surface.TopStart - .2f) / surface.LoopLength);
            float transformedError = MeasureSteadySpeed(surface, player, new Vector2(1, 1).normalized, 1.6f, 120);
            Check(transformedError < player.MoveSpeed * .03f,
                "world metric preserves diagonal speed under a rotated, nonuniformly scaled surface", passed);
            surface.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            surface.transform.localScale = Vector3.one;
        }

        private static float MeasureSteadySpeed(SingularitySurface surface, SingularityPlayerMotor player,
            Vector2 direction, float seconds, int fps)
        {
            float error = 0f;
            for (int i = 0; i < Mathf.RoundToInt(seconds * fps); i++)
            {
                Vector3 before = World(surface, player.SurfacePosition);
                player.Simulate(1f / fps, direction);
                float speed = Vector3.Distance(before, World(surface, player.SurfacePosition)) * fps;
                if (i >= fps / 2) error = Mathf.Max(error, Mathf.Abs(speed - player.MoveSpeed));
            }
            return error;
        }

        private static void ValidateAttractionWrap(SingularitySurface surface, SingularityPlayerMotor player, float sign, List<string> passed)
        {
            StartAt(player, 0, sign > 0 ? .999f : .001f);
            bool crossed = false, continuous = true, phaseClose = true;
            Vector2 previousAttraction = player.SmoothedAttractionPosition;
            for (int i = 0; i < 150; i++)
            {
                player.Simulate(1f / 120f, Vector2.up * sign);
                Vector2 attraction = player.SmoothedAttractionPosition;
                if (Mathf.Abs(attraction.y - previousAttraction.y) > surface.LoopLength * .5f) crossed = true;
                continuous &= Vector3.Distance(World(surface, previousAttraction), World(surface, attraction)) < .06f;
                phaseClose &= Mathf.Abs(surface.LoopDelta(attraction.y, player.SurfacePosition.y)) < .25f
                    && Vector3.Distance(World(surface, attraction), World(surface, player.SurfacePosition)) < .3f;
                previousAttraction = attraction;
            }
            Check(crossed && continuous && phaseClose,
                (sign > 0 ? "forward" : "reverse") + " attraction smoothing crosses the phase seam locally, without an opposite-face jump", passed);

            StartAt(player, 0, (surface.RearStart + surface.FrontHeight * surface.RearScale * .5f) / surface.LoopLength);
            Vector2 center = player.SmoothedAttractionPosition;
            float oppositeFront = surface.FrontHeight * .5f;
            Check(Mathf.Abs(surface.LoopDelta(center.y, oppositeFront)) > player.AttractionRadius * 2f
                && Mathf.Abs(World(surface, center).z - World(surface, new Vector2(0, oppositeFront)).z) < .001f,
                "overlapping front/rear screen positions retain separate logical attraction phases", passed);
        }

        private static void ValidateSideEdges(SingularitySurface surface, SingularityPlayerMotor player, List<string> passed)
        {
            StartAt(player, surface.Width, .15f);
            player.Simulate(2f, Vector2.right);
            Check(Mathf.Abs(player.SurfacePosition.x) < surface.Width * .5f && Mathf.Abs(player.SurfaceVelocity.x) < .0001f,
                "right side edge retains player radius/padding and stops outward movement", passed);
            StartAt(player, -surface.Width, (surface.RearStart + 1f) / surface.LoopLength);
            player.Simulate(2f, Vector2.left);
            Check(Mathf.Abs(player.SurfacePosition.x) < surface.Width * .5f && Mathf.Abs(player.SurfaceVelocity.x) < .0001f,
                "narrower rear side edge also retains radius/padding and stops outward movement", passed);
        }

        private static void ValidatePreviewAndResources(SingularitySurface surface, SingularityPlayerMotor player,
            GameObject surfaceObject, GameObject playerObject, List<string> passed)
        {
            Check(Shader.Find("MASSIVE/Singularity/Player Surface") != null,
                "prototype surface player shader is imported", passed);
            StartAt(player, -5f, .14f);
            surfaceObject.SetActive(true);
            playerObject.SetActive(true);
            player.RefreshVisual();
            ValidateRimGeometry(surface, player, passed);
            StartAt(player, -5f, .14f);
            player.RefreshVisual();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                MeshFilter[] filters = playerObject.GetComponentsInChildren<MeshFilter>(true);
                MeshRenderer[] renderers = playerObject.GetComponentsInChildren<MeshRenderer>(true);
                Check(filters.Length == 1 && renderers.Length == 1 && filters[0].sharedMesh.vertexCount > 100,
                    "enable cycle " + cycle + " owns exactly one populated conformed visual", passed);
                Mesh mesh = filters[0].sharedMesh;
                Material material = renderers[0].sharedMaterial;
                Check((mesh.hideFlags & HideFlags.DontSaveInEditor) != 0 && (material.hideFlags & HideFlags.DontSaveInEditor) != 0
                    && (filters[0].gameObject.hideFlags & HideFlags.DontSaveInBuild) != 0,
                    "enable cycle " + cycle + " keeps generated visual resources nonpersistent", passed);
                player.enabled = false;
                Check(mesh == null && material == null && playerObject.transform.childCount == 0,
                    "disable cycle " + cycle + " releases mesh, material, and generated child immediately in Edit Mode", passed);
                player.enabled = true;
                player.RefreshVisual();
            }
            var serialized = new SerializedObject(player);
            serialized.FindProperty("usePreviewPosition").boolValue = true;
            serialized.FindProperty("previewLoopFraction").floatValue = .73f;
            serialized.FindProperty("previewAcross").floatValue = 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            typeof(SingularityPlayerMotor).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            Check(Mathf.Abs(player.SurfacePosition.x - 2f) < .0001f && Mathf.Abs(player.SurfacePosition.y / surface.LoopLength - .73f) < .0001f,
                "edit-mode inspector preview scrubs the actual player to the authored surface pose", passed);
            string authoredBefore = JsonUtility.ToJson(player);
            player.Simulate(.4f, Vector2.up);
            player.RefreshVisual();
            player.ResetToStart();
            Check(JsonUtility.ToJson(player) == authoredBefore && Mathf.Abs(player.SurfacePosition.y / surface.LoopLength - .14f) < .0001f,
                "simulation/reset and transient mesh generation do not serialize runtime state or replace the authored start", passed);
        }

        private static void ValidateRimGeometry(SingularitySurface surface, SingularityPlayerMotor player, List<string> passed)
        {
            float originalHeight = player.RimHeight;
            Check(originalHeight > 0f, "default player rim has actual nonzero dimensional height", passed);
            StartAt(player, 0f, surface.FrontHeight * .5f / surface.LoopLength);
            player.RefreshVisual();
            Bounds frontBounds = WorldMeshBounds(player);
            Check(Mathf.Abs(frontBounds.size.y - originalHeight) < .0001f,
                "front-face geometry has the configured world-space extrusion height", passed);
            var serialized = new SerializedObject(player);
            float lift = serialized.FindProperty("surfaceLift").floatValue;
            Check(Mathf.Abs(frontBounds.min.y - lift) < .0001f && Mathf.Abs(frontBounds.max.y - lift - originalHeight) < .0001f,
                "rim base stays at Surface Lift and its face stays at Surface Lift plus Rim Height", passed);

            float edgeDistance = surface.TopStart, mostEdgeOn = float.PositiveInfinity;
            for (int i = 0; i <= 512; i++)
            {
                float distance = Mathf.Lerp(surface.TopStart, surface.RearStart, i / 512f);
                float verticalNormal = Mathf.Abs(surface.Normal(0, distance).y);
                if (verticalNormal < mostEdgeOn) { mostEdgeOn = verticalNormal; edgeDistance = distance; }
            }
            StartAt(player, 0, edgeDistance / surface.LoopLength);
            player.RefreshVisual();
            float thickDepth = WorldMeshBounds(player).size.z;
            serialized.Update();
            serialized.FindProperty("rimHeight").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            player.RefreshVisual();
            float flatDepth = WorldMeshBounds(player).size.z;
            Check(mostEdgeOn < .02f && thickDepth > flatDepth + originalHeight * .6f,
                "true side geometry adds projected silhouette at an edge-on curl without billboarding", passed);
            StartAt(player, 0f, surface.FrontHeight * .5f / surface.LoopLength);
            player.RefreshVisual();
            Check(WorldMeshBounds(player).size.y < .0001f,
                "zero rim height restores the flat surface-conforming disc", passed);
            serialized.Update();
            serialized.FindProperty("rimHeight").floatValue = originalHeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Bounds WorldMeshBounds(SingularityPlayerMotor player)
        {
            MeshFilter filter = player.GetComponentInChildren<MeshFilter>();
            if (!filter || !filter.sharedMesh) throw new InvalidOperationException("Player visual mesh is missing.");
            Vector3[] vertices = filter.sharedMesh.vertices;
            Bounds bounds = new Bounds(filter.transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (Vector3 vertex in vertices) bounds.Encapsulate(filter.transform.TransformPoint(vertex));
            return bounds;
        }

        private static void StartAt(SingularityPlayerMotor player, float across, float fraction)
        {
            var serialized = new SerializedObject(player);
            serialized.FindProperty("initialAcross").floatValue = across;
            serialized.FindProperty("initialLoopFraction").floatValue = fraction;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            player.ResetToStart();
        }
        private static Vector3 World(SingularitySurface surface, Vector2 point) => surface.WorldPosition(point.x, point.y);
        private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        private static string F(float value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
        private static void Check(bool condition, string description, List<string> passed)
        {
            if (!condition) throw new InvalidOperationException("SINGULARITY player validation failed: " + description);
            passed.Add("PASS: " + description);
        }
    }
}
