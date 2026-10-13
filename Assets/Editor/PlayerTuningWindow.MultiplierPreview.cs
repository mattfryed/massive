using Massive.Scoring;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    public sealed partial class PlayerTuningWindow
    {
        [SerializeField] private bool multiplierPreviewOpen = true, multiplierPreviewAnimated = true;
        [SerializeField] private float multiplierPreviewValue = 16f, multiplierPreviewZoom = 1f;
        [SerializeField] private int multiplierPreviewSlot;
        private PlayerMultiplierShimmerPreview multiplierPreview;
        private float multiplierPreviewSeconds;
        private double multiplierPreviewRepaint;
        private Rect multiplierPreviewRect;

        private void ReleaseMultiplierPreview() { multiplierPreview?.Dispose(); multiplierPreview = null; }
        private void TickMultiplierPreview(double now, float dt)
        {
            if (tab != 4 || !multiplierPreviewOpen || !multiplierPreviewAnimated || multiplierPreview == null ||
                EditorApplication.isCompiling || !UnityEditorInternal.InternalEditorUtility.isApplicationActive) return;
            multiplierPreviewSeconds += dt;
            if (now - multiplierPreviewRepaint < 1.0 / 30.0) return;
            multiplierPreviewRepaint = now; Repaint();
        }
        private void DrawMultiplierPreview()
        {
            multiplierPreviewOpen = EditorGUILayout.Foldout(multiplierPreviewOpen, "Live preview · world scale", true);
            if (!multiplierPreviewOpen) { ReleaseMultiplierPreview(); return; }
            multiplierPreview ??= new PlayerMultiplierShimmerPreview();
            if (!multiplierPreview.Resolve(multiplierPreviewSlot, tuning.multiplierShimmer))
            {
                EditorGUILayout.HelpBox("Preview needs the PlayerActor and PLAYING FIELD prefabs and their assigned materials.", MessageType.Warning);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                multiplierPreviewSlot = GUILayout.Toolbar(multiplierPreviewSlot, new[] { "P1", "P2", "P3", "P4" }, GUILayout.Width(150));
                GUILayout.FlexibleSpace();
                multiplierPreviewAnimated = GUILayout.Toggle(multiplierPreviewAnimated, "Animate", GUILayout.Width(75));
                if (GUILayout.Button("1×", GUILayout.Width(36))) multiplierPreviewValue = 1;
                if (GUILayout.Button("8×", GUILayout.Width(36))) multiplierPreviewValue = 8;
                if (GUILayout.Button("12×", GUILayout.Width(40))) multiplierPreviewValue = 12;
                if (GUILayout.Button("16×", GUILayout.Width(40))) multiplierPreviewValue = 16;
            }
            multiplierPreviewValue = EditorGUILayout.Slider("Preview multiplier", multiplierPreviewValue, 1,
                Mathf.Max(16, tuning.multiplierShimmer.fullIntensityMultiplier));
            using (new EditorGUILayout.HorizontalScope())
            {
                multiplierPreviewZoom = EditorGUILayout.Slider("View zoom", multiplierPreviewZoom, .25f, 3f);
                if (GUILayout.Button("Fit", GUILayout.Width(42)))
                    multiplierPreviewZoom = multiplierPreview.FitZoom(multiplierPreviewRect.width > 1 ? multiplierPreviewRect : new Rect(0,0,600,230));
                if (GUILayout.Button("100%", GUILayout.Width(48))) multiplierPreviewZoom = 1;
            }
            Rect rect = GUILayoutUtility.GetRect(200, 230, GUILayout.ExpandWidth(true));
            multiplierPreviewRect = rect;
            if (Event.current.type == EventType.Repaint)
            {
                var texture = multiplierPreview.Render(rect, tuning.multiplierShimmer, multiplierPreviewValue,
                    multiplierPreviewSeconds, multiplierPreviewSlot, multiplierPreviewZoom);
                if (texture) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            }
            DrawMultiplierPreviewLabels(rect);
            EditorGUILayout.LabelField($"Body radius {multiplierPreview.BodyRadius:0.###} u · Shimmer reach {multiplierPreview.Reach:0.###} u · {multiplierPreview.SizeSource}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("PlayerActor resting outline + PLAYING FIELD bar. Both use the same world scale; grid squares = 1 unit. Zoom changes only the view; Fit shows both samples in full. Preview controls do not change a player's score.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5);
        }
        private void DrawMultiplierPreviewLabels(Rect rect)
        {
            var p = multiplierPreview;
            var style = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { normal = { textColor = new Color(.75f,.8f,.86f) } };
            GUI.Label(new Rect(rect.x, rect.y + 8, rect.width * .48f, 18), "PLAYER OUTLINE", style);
            GUI.Label(new Rect(rect.x + rect.width * .48f, rect.y + 8, rect.width * .52f, 18), "MULTIPLIER BAR", style);
            if (p.PointsPerUnit <= 0) return;
            var textStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } };
            float left = rect.x + p.BarCenter.x - p.FullBarWidth * .5f * p.PointsPerUnit;
            Rect pane = new Rect(rect.x + rect.width * .46f, rect.y, rect.width * .54f, rect.height);
            EditorGUI.DrawRect(new Rect(pane.x, pane.y + 28, 1, pane.height - 74), new Color(.2f,.23f,.27f));
            GUI.BeginGroup(pane);
            GUI.Label(new Rect(left - pane.x, p.BarCenter.y - p.BarHeight * .5f * p.PointsPerUnit,
                Mathf.Min(p.BarWidth, .75f * p.BarScale.x) * p.PointsPerUnit, p.BarHeight * p.PointsPerUnit),
                $"{multiplierPreviewValue:0.##}", textStyle);
            GUI.EndGroup();
            float ruler = p.PointsPerUnit;
            float x = rect.x + 16, y = rect.yMax - 30;
            EditorGUI.DrawRect(new Rect(x,y,ruler,1), Color.gray);
            EditorGUI.DrawRect(new Rect(x,y-3,1,7), Color.gray);
            EditorGUI.DrawRect(new Rect(x+ruler,y-3,1,7), Color.gray);
            GUI.Label(new Rect(x,y+2,ruler,18), "1 world unit", style);
            float strength = tuning.multiplierShimmer.Strength(multiplierPreviewValue);
            GUI.Label(new Rect(rect.x+rect.width*.48f,rect.yMax-33,rect.width*.52f,20),
                strength > 0 ? $"{multiplierPreviewValue:0.##}× · strength {strength:P0}" : $"{multiplierPreviewValue:0.##}× · shimmer off", style);
        }
    }
}
