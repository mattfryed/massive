using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Massive.Multiplier
{
    public enum AmplifierPromptState { Hidden, Entering, Aiming, Contested, Winner }

    /// <summary>One world-space ring and prompt renderer, shared by gameplay and the isolated editor preview.</summary>
    public sealed class AmplifierCoreControlUI : IDisposable
    {
        readonly GameObject root, ring;
        readonly Mesh mesh;
        readonly Material material;
        readonly TextMeshPro caption;
        public bool RingVisible => ring && ring.activeSelf && root.activeSelf;
        public bool TextVisible => caption && caption.gameObject.activeSelf && root.activeSelf;
        public TextMeshPro Caption => caption;
        public float RingRadius { get; private set; }
        public float RingWidth { get; private set; }
        public float LightFraction { get; private set; }

        public AmplifierCoreControlUI(Transform parent)
        {
            root = Child("Core control UI", parent);
            ring = Child("Contest ring", root.transform);
            mesh = new Mesh { name = "Core contest ring quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateBounds();
            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            // Resources retains the shader in player builds as well as in the Editor.
            material = new Material(Resources.Load<Shader>("AmplifierContestRing")) { hideFlags = HideFlags.HideAndDontSave };
            var renderer = ring.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            caption = Child("Control prompt", root.transform).AddComponent<TextMeshPro>();
            caption.alignment = TextAlignmentOptions.Top;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Overflow;
            caption.richText = true;
            caption.rectTransform.pivot = new Vector2(.5f, 1f);
            caption.rectTransform.sizeDelta = new Vector2(20f, 5f);
            caption.renderer.shadowCastingMode = ShadowCastingMode.Off;
            Hide();
        }

        static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer, hideFlags = parent.gameObject.hideFlags };
            // Reparent before adding renderers/TMP so preview objects stay in their owning scene.
            go.transform.SetParent(parent, false); return go;
        }

        public static Vector2 SplitDirection(float lightFraction)
        {
            float angle = Mathf.Clamp01(lightFraction) * Mathf.PI * 2f;
            return new Vector2(-Mathf.Sin(angle), -Mathf.Cos(angle));
        }

        public static string Prompt(AmplifierPossessionSettings s, AmplifierPromptState state, int team, float seconds)
        {
            string template = state switch
            {
                AmplifierPromptState.Entering => s.enteringText,
                AmplifierPromptState.Aiming => s.aimingText,
                AmplifierPromptState.Contested => s.contestedText,
                AmplifierPromptState.Winner => s.winnerText,
                _ => ""
            };
            return (template ?? "").Replace("{team}", team == 1 ? s.lightTeamText ?? "" : s.darkTeamText ?? "")
                .Replace("{time}", s.showControlTimer ? Mathf.Max(0, seconds).ToString("0.0", CultureInfo.InvariantCulture) + "s" : "").Trim();
        }

        public void Update(AmplifierPossessionSettings s, Camera camera, Vector3 center, float radius,
            AmplifierPromptState state, int team, float seconds, float lightFraction)
        {
            if (!camera || state == AmplifierPromptState.Hidden) { Hide(); return; }
            root.SetActive(true);
            // UI remains camera-facing and independent of the Core's rolling rotation.
            root.transform.SetPositionAndRotation(center, camera.transform.rotation);
            var parentScale = root.transform.parent.lossyScale;
            root.transform.localScale = new Vector3(1f / Mathf.Max(.0001f, parentScale.x),
                1f / Mathf.Max(.0001f, parentScale.y), 1f / Mathf.Max(.0001f, parentScale.z));
            radius = Mathf.Max(.001f, radius);
            RingWidth = Mathf.Max(.02f, s.contestRingWidth) * radius;
            float outline = Mathf.Max(.001f, s.contestRingOutline) * radius;
            RingRadius = radius * (1f + Mathf.Max(0f, s.contestRingGap)) + RingWidth * .5f;
            LightFraction = Mathf.Clamp01(lightFraction);
            ring.SetActive(state == AmplifierPromptState.Contested && s.showContestRing);
            float extent = RingRadius + RingWidth * 1.5f + outline * 2f;
            ring.transform.localScale = Vector3.one * extent;
            material.SetFloat("_Radius", RingRadius); material.SetFloat("_Width", RingWidth);
            material.SetFloat("_Outline", outline); material.SetFloat("_LightFraction", LightFraction);
            material.SetFloat("_Extent", extent); material.SetFloat("_HalfwayMark", s.showRingHalfwayMark ? 1 : 0);
            caption.gameObject.SetActive(s.showControlText);
            if (!s.showControlText) return;
            caption.font = s.controlTextFont ? s.controlTextFont : TMP_Settings.defaultFontAsset;
            caption.fontSize = Mathf.Max(.1f, s.controlTextSize) * radius;
            caption.color = s.controlTextColor;
            caption.lineSpacing = s.controlTextLineSpacing;
            float bottom = RingVisible ? RingRadius + RingWidth * .5f + outline : radius;
            caption.transform.localPosition = new Vector3(s.controlTextOffset.x * radius, -bottom - s.controlTextOffset.y * radius, -.05f);
            caption.text = Prompt(s, state, team, seconds);
            if (caption.havePropertiesChanged) caption.ForceMeshUpdate();
        }

        public void Hide() { if (root) root.SetActive(false); }
        public void Dispose() { Destroy(root); Destroy(mesh); Destroy(material); }
        static void Destroy(Object item) { if (!item) return; if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item); }
    }
}
