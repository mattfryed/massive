#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Isolated geometry, ownership and prefab checks. No production
    /// scene, prefab, material or carousel registration is changed.</summary>
    public static class SingularityLevelIconValidation
    {
        private const string PrefabPath = "Assets/Scripts/Anomalies/SINGULARITY/LS_SINGULARITY Icon.prefab";
        private const float Epsilon = .0001f;

        [MenuItem("MASSIVE/SINGULARITY/Validate Level Select Icon")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run icon validation in Edit Mode.");
            int checks = 0;
            Action<bool, string> check = (ok, label) => {
                if (!ok) throw new InvalidOperationException("SINGULARITY level icon: " + label);
                checks++;
            };
            Scene preview = EditorSceneManager.NewPreviewScene();
            Mesh previousMesh = null;
            try
            {
                var root = new GameObject("Icon validation only");
                SceneManager.MoveGameObjectToScene(root, preview);
                var icon = root.AddComponent<SingularityLevelIcon>();
                CheckFaces(icon, check);

                var filter = root.GetComponent<MeshFilter>();
                previousMesh = new Mesh { name = "Icon validation previous mesh", hideFlags = HideFlags.HideAndDontSave };
                filter.sharedMesh = previousMesh;
                icon.Rebuild();
                Mesh first = filter.sharedMesh;
                check(first != null && first != previousMesh, "rebuild installs an owned lattice mesh");
                check(first.hideFlags == HideFlags.HideAndDontSave, "derived lattice is excluded from serialization");
                check(first.subMeshCount == 1 && first.GetIndexCount(0) > 0,
                    "derived geometry contains only the wire lattice, with no generated accretion annulus");
                CheckMesh(first, check);
                int initialCount = first.vertexCount;
                icon.Rebuild();
                check(first == null && filter.sharedMesh != null && icon.VertexCount == initialCount,
                    "rebuild releases the old mesh and deterministically replaces it");

                Mesh second = filter.sharedMesh;
                icon.enabled = false;
                check(second == null && icon.VertexCount == 0 && filter.sharedMesh == previousMesh,
                    "disable releases derived geometry and restores the prior mesh");
                icon.Rebuild();
                check(filter.sharedMesh == previousMesh && icon.VertexCount == 0,
                    "rebuild while disabled does not create hidden geometry");
                icon.enabled = true;
                icon.Rebuild();
                check(icon.VertexCount == initialCount && filter.sharedMesh != previousMesh,
                    "re-enable and rebuild recover the complete lattice");

                icon.widthCells = int.MaxValue; icon.depthCells = int.MaxValue;
                icon.Rebuild();
                check(icon.VertexCount <= 52000 && icon.VertexCount > initialCount,
                    "unbounded requested cell counts clamp below 52000 vertices");
                CheckMesh(filter.sharedMesh, check);
                icon.widthCells = int.MinValue; icon.depthCells = int.MinValue;
                icon.Rebuild();
                check(icon.VertexCount > 0 && icon.VertexCount < initialCount,
                    "negative cell counts clamp to a valid minimum mesh");

                Mesh last = filter.sharedMesh;
                UnityEngine.Object.DestroyImmediate(icon);
                check(last == null && filter.sharedMesh == previousMesh && previousMesh != null,
                    "destroy releases only owned geometry and preserves pre-existing mesh");

                var shader = Shader.Find("MASSIVE/Singularity/Level Icon");
                check(shader != null && shader.isSupported, "icon shader is available and supported on the active graphics device");
                check(!ShaderUtil.ShaderHasError(shader), "icon shader has no compiler errors");

                CheckPrefab(preview, check);
                return "SINGULARITY level icon: PASS " + checks +
                    " checks (symmetric opposing wells, fixed edges, bounded mesh, lifecycle ownership, shader and isolated presentation prefab).";
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (previousMesh != null) UnityEngine.Object.DestroyImmediate(previousMesh);
            }
        }

        private static void CheckFaces(SingularityLevelIcon icon, Action<bool, string> check)
        {
            bool opposing = true, mirrored = true, finite = true;
            for (int x = -8; x <= 8; x++)
            for (int z = -8; z <= 8; z++)
            {
                float px = x * icon.flatWidth / 16f, pz = z * icon.depth / 16f;
                Vector3 a = icon.EvaluateFace(px, pz, true);
                Vector3 b = icon.EvaluateFace(px, pz, false);
                Vector3 left = icon.EvaluateFace(-px, pz, true);
                Vector3 back = icon.EvaluateFace(px, -pz, true);
                opposing &= Near(a.x, b.x) && Near(a.z, b.z) && Near(a.y, -b.y);
                mirrored &= Near(a.x, -left.x) && Near(a.y, left.y) && Near(a.z, left.z)
                    && Near(a.x, back.x) && Near(a.y, back.y) && Near(a.z, -back.z);
                finite &= Finite(a) && Finite(b);
            }
            check(opposing, "upper and lower gravity wells are exact opposing reflections");
            check(mirrored, "gravity deformation is symmetric along both horizontal axes");
            check(finite, "all sampled face positions remain finite");
            Vector3 center = icon.EvaluateFace(0f, 0f, true);
            check(Near(center.x, 0) && Near(center.z, 0) && center.y > 0f && center.y < icon.layerSeparation * .1f,
                "both wells approach the central event horizon without swapping faces");
            check(Near(center.y, icon.layerSeparation * .5f * (1f - icon.wellDepth)),
                "well depth produces the expected center separation");
            Vector3 shoulder = icon.EvaluateFace(.5f, .3f, true);
            check(shoulder.x < .5f && shoulder.z < .3f && shoulder.y < icon.layerSeparation * .5f,
                "the surrounding lattice pulls inward as well as down");

            bool edges = true;
            for (int i = 0; i <= 24; i++)
            {
                float x = Mathf.Lerp(-icon.flatWidth * .5f, icon.flatWidth * .5f, i / 24f);
                float z = Mathf.Lerp(-icon.depth * .5f, icon.depth * .5f, i / 24f);
                foreach (bool upper in new[] { true, false })
                foreach (int sign in new[] { -1, 1 })
                {
                    float y = icon.layerSeparation * .5f * (upper ? 1f : -1f);
                    edges &= Vector3.Distance(icon.EvaluateFace(sign * icon.flatWidth * .5f, z, upper),
                        new Vector3(sign * icon.flatWidth * .5f, y, z)) < Epsilon;
                    edges &= Vector3.Distance(icon.EvaluateFace(x, sign * icon.depth * .5f, upper),
                        new Vector3(x, y, sign * icon.depth * .5f)) < Epsilon;
                }
            }
            check(edges, "all four face boundaries remain fixed for seamless fold attachment");
            icon.wellDepth = 0f; icon.radialPull = 0f;
            check(Vector3.Distance(icon.EvaluateFace(.4f, -.2f, true), new Vector3(.4f, icon.layerSeparation * .5f, -.2f)) < Epsilon,
                "zero well depth and radial pull recover a flat undistorted face");
            icon.wellDepth = .92f; icon.radialPull = .18f;
        }

        private static void CheckMesh(Mesh mesh, Action<bool, string> check)
        {
            Vector3[] vertices = mesh.vertices;
            bool finite = true, enclosed = true, validIndices = true;
            Bounds bounds = mesh.bounds;
            bounds.Expand(.001f);
            foreach (Vector3 vertex in vertices) { finite &= Finite(vertex); enclosed &= bounds.Contains(vertex); }
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                foreach (int index in mesh.GetIndices(submesh)) validIndices &= index >= 0 && index < vertices.Length;
            check(finite && enclosed, "all mesh vertices are finite and enclosed by the renderer bounds");
            check(validIndices && mesh.colors.Length == vertices.Length && mesh.uv.Length == vertices.Length,
                "mesh triangles and per-vertex color/UV streams are complete");
        }

        private static void CheckPrefab(Scene preview, Action<bool, string> check)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            check(prefab != null, "standalone icon prefab exists at the authored asset path");
            check(prefab.transform.localPosition == Vector3.zero && prefab.transform.localRotation == Quaternion.identity,
                "prefab has a neutral root for future carousel placement");
            check(prefab.GetComponent<LevelIcon>() != null, "prefab exposes the existing carousel selection hook");
            check(prefab.GetComponentsInChildren<Collider>(true).Length == 0 && prefab.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                "icon contains no colliders or physics bodies");
            bool presentationOnly = true, missingScripts = false;
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                missingScripts |= GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0;
            foreach (MonoBehaviour behavior in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                presentationOnly &= behavior is SingularityLevelIcon || behavior is LevelIcon
                    || behavior is MirzaBeig.ParticleSystems.Rotator;
            check(!missingScripts && presentationOnly, "all scripts are present and presentation-only, with no portal or grid simulation");
            check(prefab.GetComponentsInChildren<ParticleSystem>(true).Length > 0,
                "authored particle accretion remains present in the icon");
            check(prefab.GetComponentInChildren<MirzaBeig.ParticleSystems.Rotator>(true) != null,
                "authored particle rotation is preserved");

            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var icon = copy.GetComponentInChildren<SingularityLevelIcon>(true);
            check(icon != null && icon.isActiveAndEnabled, "isolated prefab instance has an active lattice component");
            icon.Rebuild();
            check(icon.VertexCount > 0 && icon.VertexCount <= 52000, "prefab builds its bounded transient geometry in Edit Mode");
            var renderer = icon.GetComponent<MeshRenderer>();
            var materials = renderer.sharedMaterials;
            bool supported = materials.Length == 1;
            foreach (Material material in materials) supported &= material != null && material.shader != null && material.shader.isSupported;
            check(supported, "lattice has exactly one supported material and no legacy annulus material slot");
            bool particlesSupported = true, particlesVisible = false;
            foreach (var particles in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
                particlesSupported &= particleRenderer != null;
                if (particleRenderer != null)
                    foreach (var material in particleRenderer.sharedMaterials)
                        particlesSupported &= material != null && material.shader != null && material.shader.isSupported;
                particles.Simulate(2f, false, true, true);
                particlesVisible |= particles.particleCount > 0;
            }
            check(particlesSupported, "authored particle materials resolve to supported shaders");
            check(particlesVisible, "authored accretion emits particles in an isolated preview");
            Mesh owned = icon.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(copy);
            check(owned == null, "destroying the prefab instance releases its generated mesh");
        }

        private static bool Near(float a, float b) { return Mathf.Abs(a - b) < Epsilon; }
        private static bool Finite(Vector3 p) { return Finite(p.x) && Finite(p.y) && Finite(p.z); }
        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    }
}
#endif
