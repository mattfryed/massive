using Massive.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Scoring
{
    public sealed partial class TeamAmplifierPresenter
    {
        GameObject badgeCore, badgeDisc;
        Material badgeCoreMaterial, badgeDiscMaterial;
        Mesh discMesh;
        Quaternion sourceCoreRotation;
        float coreMeshDiameter;
        public Renderer GoalBadgeCore => badgeCore ? badgeCore.GetComponent<Renderer>() : null;
        public Renderer GoalBadgeDisc => badgeDisc ? badgeDisc.GetComponent<Renderer>() : null;

        void RefreshBadge(Vector3 center, Vector3 right, Vector3 up, float depth, float halfHeight,
            float side, float diameter, float discDiameter, TeamAmplifierGoalNumberSettings settings)
        {
            if (!EnsureBadge()) return;
            SetBadgeVisible(true);
            Vector3 position = center + right * (side * depth * settings.outwardOffset) + up * (halfHeight * settings.verticalOffset);
            var parentScale = transform.lossyScale;
            Vector3 normalizedScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                1 / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(parentScale.z)));
            var profile = SharedSettingsRuntime.Load<AmplifierSharedProfile>();
            var surface = profile ? profile.coreSurface : null;
            Vector3 axis = surface != null ? surface.energyRotationAxis : new Vector3(.35f, .9f, .15f);
            float speed = surface != null ? surface.energyRotationSpeed : -17;
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            badgeCore.transform.SetPositionAndRotation(position, sourceCoreRotation * Quaternion.AngleAxis(time * speed, axis.normalized));
            badgeCore.transform.localScale = normalizedScale * (diameter / coreMeshDiameter);
            badgeDisc.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.Cross(right, up), up));
            badgeDisc.transform.localScale = normalizedScale * discDiameter;
            badgeCoreMaterial.SetFloat("_PreviewTime", time);
            badgeDiscMaterial.SetColor("_Color", settings.whiteCircleColor);
            CopyGoalMask(badgeCoreMaterial); CopyGoalMask(badgeDiscMaterial);
        }

        bool EnsureBadge()
        {
            if (badgeCore && badgeDisc) return true;
            Renderer source = demonstrationBadgeSource;
            if (!source && legacyRenderers != null) foreach (var candidate in legacyRenderers)
                if (candidate && candidate.sharedMaterial && candidate.sharedMaterial.shader.name == "MASSIVE/Amplifier Core/Energy")
                { source = candidate; break; }
            var sourceFilter = source ? source.GetComponent<MeshFilter>() : null;
            var discShader = Resources.Load<Shader>("TeamAmplifierGoalDisc");
            if (!sourceFilter || !sourceFilter.sharedMesh || !discShader) return false;
            // Reuse the old HUD's actual sphere and energy material without cloning
            // its gameplay scripts, colliders, or neutral shell.
            badgeCoreMaterial = new Material(source.sharedMaterial) { name = "Goal badge rainbow core (generated)", hideFlags = HideFlags.HideAndDontSave, renderQueue = 2990 };
            badgeCoreMaterial.SetFloat("_GoalClipEnabled", 1);
            badgeCoreMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            badgeCoreMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            badgeCoreMaterial.SetFloat("_ZWrite", 0);
            badgeCoreMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
            badgeCoreMaterial.SetFloat("_SingularityBrightness", 1);
            sourceCoreRotation = source.transform.rotation;
            coreMeshDiameter = Mathf.Max(.001f, sourceFilter.sharedMesh.bounds.size.x);
            badgeCore = CreateBadgePart("Goal multiplier rainbow core (generated)", sourceFilter.sharedMesh, badgeCoreMaterial);
            discMesh = new Mesh { name = "Goal badge disc quad", hideFlags = HideFlags.HideAndDontSave };
            discMesh.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(-.5f,.5f), new Vector3(.5f,.5f), new Vector3(.5f,-.5f) };
            discMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            discMesh.triangles = new[] { 0,1,2,0,2,3 }; discMesh.RecalculateBounds();
            badgeDiscMaterial = new Material(discShader) { name = "Goal badge white disc (generated)", hideFlags = HideFlags.HideAndDontSave, renderQueue = 2991 };
            badgeDisc = CreateBadgePart("Goal multiplier white disc (generated)", discMesh, badgeDiscMaterial);
            return true;
        }

        GameObject CreateBadgePart(string label, Mesh mesh, Material material)
        {
            var part = new GameObject(label) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return part;
        }

        void CopyGoalMask(Material target)
        {
            target.SetVector("_GoalOrigin", numberMaterial.GetVector("_GoalOrigin"));
            target.SetVector("_GoalRight", numberMaterial.GetVector("_GoalRight"));
            target.SetVector("_GoalUp", numberMaterial.GetVector("_GoalUp"));
            target.SetVector("_GoalShape", numberMaterial.GetVector("_GoalShape"));
        }

        void SetBadgeVisible(bool visible)
        { if (badgeCore) badgeCore.SetActive(visible); if (badgeDisc) badgeDisc.SetActive(visible); }
        void ReleaseBadge()
        {
            ReleaseBadgeObject(badgeCore); ReleaseBadgeObject(badgeDisc);
            ReleaseBadgeObject(badgeCoreMaterial); ReleaseBadgeObject(badgeDiscMaterial); ReleaseBadgeObject(discMesh);
            badgeCore = badgeDisc = null; badgeCoreMaterial = badgeDiscMaterial = null; discMesh = null;
        }
        static void ReleaseBadgeObject(Object value)
        { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
