using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Singularity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Copies spatial layout only from a disposable reference scene. Never
/// replaces gameplay components, cross-object references, text or shared assets.</summary>
public static class SingularityStandardLayout
{
    public const string ReferenceScene = "Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity";
    private const string UndoName = "Match SINGULARITY standard layout";

    [MenuItem("MASSIVE/SINGULARITY/Match Standard DYNAMO Layout")]
    public static void ApplyMenu()
    {
        Scene target = SceneManager.GetActiveScene();
        string result = Apply(target);
        if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Could not save SINGULARITY.");
        Debug.Log(result);
    }

    public static string Apply(Scene target) => Process(target, true);

    [MenuItem("MASSIVE/SINGULARITY/Validate Standard Scene Layout")]
    public static string Validate() => Process(SceneManager.GetActiveScene(), false);

    private static string Process(Scene target, bool apply)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || target.path != SingularitySceneSetup.ScenePath)
            throw new InvalidOperationException("Open SINGULARITY in Edit Mode with compilation complete.");
        Transform imported = target.GetRootGameObjects().Single(g => g.name == "SINGULARITY - Gameplay").transform;
        var surface = target.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SingularitySurface>(true)).Single();
        Camera view = target.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
        Scene source = EditorSceneManager.OpenPreviewScene(ReferenceScene);
        int transforms = 0, dimensions = 0;
        try
        {
            Transform field = source.GetRootGameObjects().Single(g => g.name == "PLAYING FIELD").transform;
            var pairs = new List<(Transform from, Transform to, bool root)>();
            foreach (string group in new[] { "UI", "TEAM 1 goal", "TEAM 2 goal" })
            {
                Transform from = field.Find(group), to = imported.Find(group);
                if (!from || !to) throw new InvalidOperationException("Missing standard group: " + group);
                foreach (Transform child in from.GetComponentsInChildren<Transform>(true))
                {
                    string path = AnimationUtility.CalculateTransformPath(child, from);
                    Transform match = path.Length == 0 ? to : to.Find(path);
                    if (!match) throw new InvalidOperationException("Missing standard element: " + group + "/" + path);
                    pairs.Add((child, match, path.Length == 0));
                }
            }
            if (apply) { Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName(UndoName); }
            foreach (var pair in pairs)
            {
                MatchTransform(pair.from, pair.to, pair.root, apply);
                transforms++;
                MatchDimensions(pair.from, pair.to, apply, ref dimensions);
            }

            // Match actual game spawn anchors, not inactive players' off-field
            // parking positions in the reference scene. Keep all four enabled.
            Transform sourcePlayers = source.GetRootGameObjects().Single(g => g.name == "GameplayObjects").transform.Find("Players");
            foreach (var player in imported.Find("Players").GetComponentsInChildren<PlayerControllerScript>(true))
            {
                var reference = sourcePlayers.GetComponentsInChildren<PlayerControllerScript>(true).Single(p => p.playerID == player.playerID);
                var sourceAnchor = new SerializedObject(reference).FindProperty("respawnPointOverride").objectReferenceValue as Transform;
                var anchor = new SerializedObject(player).FindProperty("respawnPointOverride").objectReferenceValue as Transform;
                if (!sourceAnchor || !anchor) throw new InvalidOperationException("Missing spawn anchor for " + player.name);
                MatchTransform(sourceAnchor, anchor, true, apply); transforms++;
                if (apply)
                {
                    Undo.RecordObject(player.transform, UndoName);
                    player.transform.SetPositionAndRotation(sourceAnchor.position, reference.transform.rotation);
                    player.transform.localScale = reference.transform.localScale;
                }
                Require((player.transform.position - sourceAnchor.position).sqrMagnitude < .00000001f &&
                    Quaternion.Angle(player.transform.rotation, reference.transform.rotation) < .001f &&
                    (player.transform.lossyScale - reference.transform.lossyScale).sqrMagnitude < .00000001f,
                    player.name + " matches standard match spawn and size");
                transforms++;
            }

            Camera referenceView = source.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
            var framing = view.GetComponent<SingularityCameraFraming>();
            if (apply)
            {
                if (framing) { Undo.RecordObject(framing, UndoName); framing.autoFrame = false; framing.minimumOrthographicSize = referenceView.orthographicSize; }
                MatchTransform(referenceView.transform, view.transform, true, true);
                Undo.RecordObject(view, UndoName);
                view.orthographic = referenceView.orthographic;
                view.orthographicSize = referenceView.orthographicSize;
                view.fieldOfView = referenceView.fieldOfView;
                view.nearClipPlane = referenceView.nearClipPlane;
                view.farClipPlane = referenceView.farClipPlane;
                view.rect = referenceView.rect;
            }
            MatchTransform(referenceView.transform, view.transform, true, false); transforms++;
            Require((!framing || !framing.autoFrame) && view.orthographic == referenceView.orthographic &&
                view.orthographicSize == referenceView.orthographicSize && view.rect == referenceView.rect,
                "Standard camera framing remains stable");

            var referenceGrid = field.GetComponentInChildren<VectorGridGPU>(true);
            Vector3 cornerA = referenceGrid.transform.TransformPoint(new Vector3(-referenceGrid.size.x * .5f, -referenceGrid.size.y * .5f, 0f));
            Vector3 cornerB = referenceGrid.transform.TransformPoint(new Vector3(referenceGrid.size.x * .5f, referenceGrid.size.y * .5f, 0f));
            float width = Mathf.Abs(cornerB.x - cornerA.x), height = Mathf.Abs(cornerB.z - cornerA.z);
            Vector3 center = (cornerA + cornerB) * .5f;
            Require(Quaternion.Angle(surface.transform.rotation, Quaternion.identity) < .001f &&
                (surface.transform.lossyScale - Vector3.one).sqrMagnitude < .00000001f, "Surface uses unscaled world XZ axes");
            if (apply)
            {
                Undo.RecordObject(surface, UndoName); Undo.RecordObject(surface.transform, UndoName);
                surface.transform.position = new Vector3(center.x, surface.transform.position.y, center.z);
                surface.ConfigureProjectedBounds(width, height, surface.RearScale, surface.Depth, surface.CurlReach);
                foreach (var grid in surface.GetComponentsInChildren<SingularityGridRenderer>(true)) grid.RefreshPresentation();
                EditorSceneManager.MarkSceneDirty(target);
                EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            }
            Require(Mathf.Abs(surface.Width - width) < .0001f && Mathf.Abs(surface.ProjectedHeight - height) < .0001f &&
                Mathf.Abs(surface.transform.position.x - center.x) < .0001f && Mathf.Abs(surface.transform.position.z - center.z) < .0001f,
                "Full folded grid matches standard XZ bounds, including both turns");
            string report = "SINGULARITY standard layout: " + transforms + " transforms and " + dimensions +
                " shape dimensions match DYNAMO; folded bounds " + width.ToString("F3") + " x " + height.ToString("F3") +
                ", flat-face height " + surface.FrontHeight.ToString("F6") + ", camera " + view.orthographicSize + ".";
            return report;
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
    }

    private static void MatchTransform(Transform from, Transform to, bool root, bool apply)
    {
        if (apply)
        {
            Undo.RecordObject(to, UndoName);
            if (from is RectTransform a && to is RectTransform b)
            {
                b.anchorMin = a.anchorMin; b.anchorMax = a.anchorMax; b.pivot = a.pivot;
                b.sizeDelta = a.sizeDelta; b.anchoredPosition3D = a.anchoredPosition3D;
            }
            if (root)
            {
                to.SetPositionAndRotation(from.position, from.rotation);
                Vector3 parentScale = to.parent ? to.parent.lossyScale : Vector3.one;
                Vector3 scale = from.lossyScale;
                to.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            }
            else { to.localPosition = from.localPosition; to.localRotation = from.localRotation; to.localScale = from.localScale; }
        }
        Require((from.position - to.position).sqrMagnitude < .00000001f && Quaternion.Angle(from.rotation, to.rotation) < .001f &&
            (from.lossyScale - to.lossyScale).sqrMagnitude < .00000001f, "Transform mismatch: " + to.name);
        if (from is RectTransform r1 && to is RectTransform r2)
            Require((r1.sizeDelta - r2.sizeDelta).sqrMagnitude < .00000001f && r1.anchorMin == r2.anchorMin &&
                r1.anchorMax == r2.anchorMax && r1.pivot == r2.pivot, "Rect layout mismatch: " + to.name);
    }

    private static void MatchDimensions(Transform from, Transform to, bool apply, ref int count)
    {
        var a = from.GetComponent<Shapes.Disc>(); var b = to.GetComponent<Shapes.Disc>();
        if (a)
        {
            Require(b != null, "Missing disc on " + to.name);
            if (apply) { Undo.RecordObject(b, UndoName); b.Radius = a.Radius; b.Thickness = a.Thickness; }
            Require(a.Radius == b.Radius && a.Thickness == b.Thickness, "Disc dimensions mismatch: " + to.name); count++;
        }
        var c = from.GetComponent<Shapes.Rectangle>(); var d = to.GetComponent<Shapes.Rectangle>();
        if (c)
        {
            Require(d != null, "Missing rectangle on " + to.name);
            if (apply) { Undo.RecordObject(d, UndoName); d.Width = c.Width; d.Height = c.Height; d.Thickness = c.Thickness; d.CornerRadii = c.CornerRadii; }
            Require(c.Width == d.Width && c.Height == d.Height && c.Thickness == d.Thickness && c.CornerRadii == d.CornerRadii,
                "Rectangle dimensions mismatch: " + to.name); count++;
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
