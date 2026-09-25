using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Full-footprint fitting checks using isolated preview objects.</summary>
    public static class SingularityProjectedBoundsValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Projected Grid Bounds")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            var passed = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("SINGULARITY projected bounds validation") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var surface = root.AddComponent<SingularitySurface>();
                surface.Configure(28f, 12f, .875f, 3f, 2.1f);
                Check(Near(surface.FrontHeight, 12f) && Near(surface.ProjectedHeight, 14.498407f),
                    "legacy Configure retains flat-face height semantics", passed);
                float legacyLoopLength = surface.LoopLength;
                var legacyPoint = surface.Evaluate(7f, surface.RearStart + 1f);
                int legacyRevision = surface.Revision;
                float observedHeights = 0f;
                for (int i = 0; i < 20; i++) observedHeights += surface.ProjectedHeight;
                Check(observedHeights > 0f && surface.Revision == legacyRevision && surface.LoopLength == legacyLoopLength
                    && surface.Evaluate(7f, surface.RearStart + 1f) == legacyPoint,
                    "projected extent reads do not mutate geometry or rebuild cache", passed);

                ValidateFit(surface, "standard field", 28f, 12f, .875f, 3f, 2.1f, passed);
                Check(Near(surface.FrontHeight, 9.375424f), "standard 28x12 footprint reserves room for both bends", passed);
                ValidateFit(surface, "square deep field", 16f, 16f, .6f, 9f, 3f, passed);
                ValidateFit(surface, "wide shallow field", 40f, 10f, .9f, .5f, .5f, passed);
                ValidateFit(surface, "equal faces", 28f, 12f, 1f, 3f, 2.1f, passed);
                Check(Near(surface.FrontHeight, 8.85f), "equal-face limit has the expected symmetric bend maximum", passed);
                ValidateFit(surface, "short strong curls", 12f, 9f, .5f, 1f, 3f, passed);

                surface.Configure(28f, 4f, .875f, 3f, 2.1f);
                float minimum = surface.ProjectedHeight;
                surface.ConfigureProjectedBounds(28f, minimum, .875f, 3f, 2.1f);
                Check(Near(surface.FrontHeight, 4f), "minimum supported projected height is accepted", passed);
                surface.Configure(28f, 200f, .875f, 3f, 2.1f);
                float maximum = surface.ProjectedHeight;
                surface.ConfigureProjectedBounds(28f, maximum, .875f, 3f, 2.1f);
                Check(Near(surface.FrontHeight, 200f), "maximum supported projected height is accepted", passed);
                surface.ConfigureProjectedBounds(28f, 12f, .875f, 3f, 2.1f);
                int revision = surface.Revision;
                string original = JsonUtility.ToJson(surface);
                Reject(surface, minimum - .01f, "impossible short footprint", original, revision, passed);
                Reject(surface, maximum + .01f, "impossible tall footprint", original, revision, passed);
                Reject(surface, float.NaN, "NaN footprint", original, revision, passed);
                Reject(surface, float.PositiveInfinity, "infinite footprint", original, revision, passed);

                root.SetActive(false);
                surface.ConfigureProjectedBounds(30f, 14f, .875f, 3f, 2.1f);
                Check(Near(surface.ProjectedHeight, 14f) && Near(surface.Width, 30f),
                    "fitting is safe while the component is disabled", passed);
                revision = surface.Revision;
                root.SetActive(true);
                Check(surface.Revision == revision && Near(surface.ProjectedHeight, 14f),
                    "enable preserves a valid fitted geometry cache", passed);
                root.transform.SetPositionAndRotation(new Vector3(2f, 3f, -4f), Quaternion.Euler(9f, 22f, 0f));
                root.transform.localScale = new Vector3(1.2f, 1f, 1.5f);
                Check(Near(surface.ProjectedHeight, 14f), "reported footprint remains explicitly surface-local", passed);
                return passed.Count + " SINGULARITY projected bounds checks passed:\n" + string.Join("\n", passed);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void ValidateFit(SingularitySurface surface, string name, float width, float height,
            float backScale, float depth, float reach, List<string> passed)
        {
            surface.ConfigureProjectedBounds(width, height, backScale, depth, reach);
            Check(Near(surface.Width, width) && Near(surface.ProjectedHeight, height), name + ": exact requested dimensions", passed);
            Check(Near(surface.RearScale, backScale) && Near(surface.Depth, depth) && Near(surface.CurlReach, reach),
                name + ": rear ratio, depth and curl reach are preserved", passed);
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity, maxX = 0f;
            for (int i = 0; i <= 8192; i++)
            {
                Vector3 point = surface.Evaluate(width * .5f, surface.LoopLength * i / 8192f);
                minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z); maxX = Mathf.Max(maxX, point.x);
            }
            Check(Near(maxX * 2f, width) && Near(maxZ, height * .5f) && Near(minZ, -height * .5f),
                name + ": independently sampled mesh domain fits centered full bounds", passed);
            int revision = surface.Revision;
            float loop = surface.LoopLength;
            surface.ConfigureProjectedBounds(width, height, backScale, depth, reach);
            Check(surface.Revision == revision && surface.LoopLength == loop, name + ": repeated fitting does not rebuild", passed);
            Vector3 seam = surface.Evaluate(3f, 0f);
            Check(Vector3.Distance(seam, surface.Evaluate(3f, surface.LoopLength)) < .0001f
                && Vector3.Dot(surface.Normal(0f, surface.FrontHeight * .5f), Vector3.up) > .999f,
                name + ": periodic seam and front normal survive fitting", passed);
        }

        private static void Reject(SingularitySurface surface, float height, string label, string original,
            int revision, List<string> passed)
        {
            bool rejected = false;
            try { surface.ConfigureProjectedBounds(18f, height, .875f, 3f, 2.1f); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && JsonUtility.ToJson(surface) == original && surface.Revision == revision,
                label + " is explicitly rejected without partial mutation", passed);
        }

        private static bool Near(float a, float b) { return Mathf.Abs(a - b) < .0001f; }
        private static void Check(bool condition, string label, List<string> passed)
        {
            if (!condition) throw new InvalidOperationException("SINGULARITY projected bounds check failed: " + label);
            passed.Add(label);
        }
    }
}
