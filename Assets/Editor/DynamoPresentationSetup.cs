#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Dynamo;
using MirzaBeig.ParticleSystems;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DynamoPresentationSetup
{
    const string IconPath = "Assets/Scripts/Level Select/LS_DYNAMO icon.prefab";
    const string PanelPath = "Assets/Scripts/Instructions/InstructionsPanel-DYNAMO.prefab";
    const string MeshPath = "Assets/Scripts/Level Select/Dynamo Icon Field.asset";
    const string MaterialPath = "Assets/Scripts/Level Select/Dynamo Icon Field.mat";

    [MenuItem("MASSIVE/Dynamo/Refresh icon and instructions")]
    public static void Refresh()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material)
        {
            material = new Material(Shader.Find("MASSIVE/Dynamo Icon Field"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        RefreshIconField();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        var icon = PrefabUtility.LoadPrefabContents(IconPath);
        try
        {
            Remove(icon.transform.Find("DYNAMO Icon Rings"));
            Remove(icon.transform.Find("Bounded magnetic field"));
            StripPhysics(icon);
            icon.transform.localScale = Vector3.one * .14f;
            foreach (var r in icon.GetComponentsInChildren<Rotator>(true)) { r.localRotationSpeed = r.worldRotationSpeed = Vector3.zero; r.executeInEditMode = false; }
            var field = new GameObject("Bounded magnetic field", typeof(MeshFilter), typeof(MeshRenderer), typeof(Rotator));
            field.transform.SetParent(icon.transform, false);
            field.transform.localRotation = Quaternion.Euler(18, 0, 12);
            field.GetComponent<MeshFilter>().sharedMesh = mesh;
            field.GetComponent<MeshRenderer>().sharedMaterial = material;
            field.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            field.GetComponent<MeshRenderer>().receiveShadows = false;
            var turn = field.GetComponent<Rotator>(); turn.localRotationSpeed = new Vector3(0, 0, 3); turn.unscaledTime = true;
            PrefabUtility.SaveAsPrefabAsset(icon, IconPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(icon); }

        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/S-6_DYNAMO.unity");
        var panel = PrefabUtility.LoadPrefabContents(PanelPath);
        try
        {
            MagnetosphereFieldLinesGPU2D sourceField = null;
            DynamoFlowBlanketRenderer sourceFlow = null;
            DynamoSelectiveBloom sourceBloom = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!sourceField) sourceField = root.GetComponentInChildren<MagnetosphereFieldLinesGPU2D>(true);
                if (!sourceFlow) sourceFlow = root.GetComponentInChildren<DynamoFlowBlanketRenderer>(true);
                if (!sourceBloom) sourceBloom = root.GetComponentInChildren<DynamoSelectiveBloom>(true);
            }
            if (!sourceField || !sourceFlow || !sourceBloom) throw new InvalidOperationException("Dynamo gameplay visual sources are missing.");
            var inheritedIcon = panel.transform.Find("LS_DYNAMO icon");
            foreach (var component in inheritedIcon.GetComponents<MonoBehaviour>())
                if (component && component.GetType().Name == "RotateAroundAxis") UnityEngine.Object.DestroyImmediate(component);
            inheritedIcon.localRotation = Quaternion.identity;
            inheritedIcon.localScale = Vector3.one * .68f;
            // Replace the old unframed particle demonstration with an isolated HDR view.
            Remove(panel.transform.Find("Mid panel/Rect Transform"));
            Remove(panel.transform.Find("Mid panel (1)"));
            Remove(panel.transform.Find("Dynamo storm demonstration"));
            Remove(panel.transform.Find("Mid panel/Storm demonstration display"));

            var stage = new GameObject("Dynamo storm demonstration");
            stage.SetActive(false); stage.transform.SetParent(panel.transform, false);
            stage.transform.localPosition = new Vector3(1000, 0, 1000);
            var demo = stage.AddComponent<DynamoInstructionsDemo>();
            var f = stage.AddComponent<MagnetosphereFieldLinesGPU2D>(); EditorUtility.CopySerialized(sourceField, f);
            f.scientificRenderer = null; f.showDebugEnvelope = false;
            f.dipolePosition = stage.transform.position;
            f.stormInterval = f.stormIntensity = f.stormWarpScale = 0;
            f.randomizeWindOnStormStart = f.driftWindWhenCalm = false;
            f.seedCount = 144; f.magnetosphereRadius = 8; f.seedOuterFill = .68f;
            f.step = .06f; f.spinStrength = .2f; f.pathWarbleStrength = .11f;
            f.loopDepthScale = .35f; f.loopSpreadDegrees = 55;
            var flow = stage.AddComponent<DynamoFlowBlanketRenderer>(); EditorUtility.CopySerialized(sourceFlow, flow);
            flow.controller = null; flow.field = f; flow.pathsPerWorldUnit = 7; flow.maxPaths = 256;
            flow.streakLength = .45f; flow.streakWidth = .055f; flow.streakSpacing = .8f;
            flow.sharpCoreIntensity = .8f;
            var cameraObject = new GameObject("Demonstration camera", typeof(Camera));
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 40, 0);
            cameraObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 10; camera.nearClipPlane = .1f; camera.farClipPlane = 70;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = true;
            var bloom = stage.AddComponent<DynamoSelectiveBloom>(); EditorUtility.CopySerialized(sourceBloom, bloom);
            bloom.field = f; bloom.flow = flow; bloom.targetCamera = camera;
            bloom.fieldBloom.intensity = 1.2f; bloom.fieldBloom.diffusion = 4;
            bloom.flowBloom.intensity = 1.2f; bloom.flowBloom.diffusion = 4;

            var displayObject = new GameObject("Storm demonstration display", typeof(RectTransform), typeof(Canvas));
            displayObject.transform.SetParent(panel.transform.Find("Mid panel"), false);
            displayObject.transform.localPosition = new Vector3(0, 0, -.035f);
            displayObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = displayObject.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(7.35f, 3.88f);
            var imageObject = new GameObject("Storm blanket view", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(rect, false);
            var imageRect = imageObject.GetComponent<RectTransform>(); imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one; imageRect.sizeDelta = Vector2.zero;
            var image = imageObject.GetComponent<RawImage>(); image.raycastTarget = false;
            demo.field = f; demo.flow = flow; demo.bloom = bloom; demo.view = camera; demo.display = image;
            var coreSource = AssetDatabase.LoadAssetAtPath<GameObject>(IconPath).transform.Find("Neutron Star");
            var core = UnityEngine.Object.Instantiate(coreSource.gameObject, stage.transform);
            core.name = "Dynamo core"; core.transform.localPosition = Vector3.zero; core.transform.localScale = Vector3.one * .85f;
            stage.SetActive(true);

            foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Instructions title") text.text = "Ride out the solar wind";
                if (text.name.Trim() == "Anomaly Type") { text.text = "SOLAR-WIND STORMS"; text.fontSize = 43; }
                if (text.name == "Instructions body")
                {
                    text.text = "A blanket of SOLAR WIND sweeps across the arena and wraps around the MAGNETOSPHERE.\n\nStay inside the magenta field. Its protective boundary compresses and shifts with each storm.\n\nOutside, the cyan flow pushes you and drains mass. Follow the changing shelter as storms grow stronger.";
                    text.rectTransform.sizeDelta = new Vector2(800, 410);
                    text.rectTransform.pivot = new Vector2(.5f, 1);
                    text.alignment = TextAlignmentOptions.TopLeft;
                    text.fontSize = 25; text.enableAutoSizing = false;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(panel, PanelPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(panel); EditorSceneManager.ClosePreviewScene(scene); }
        AssetDatabase.SaveAssets();
    }

    static void StripPhysics(GameObject root)
    {
        foreach (var c in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (var r in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);
        foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (c && c.GetType().Name == "GridForceSource") UnityEngine.Object.DestroyImmediate(c);
    }
    static void Remove(Transform target) { if (target) UnityEngine.Object.DestroyImmediate(target.gameObject); }

    [MenuItem("MASSIVE/Dynamo/Refresh icon field only")]
    public static void RefreshIconField()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        var mesh = BuildFieldMesh(existing);
        if (!existing) AssetDatabase.CreateAsset(mesh, MeshPath);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
    }

    static Mesh BuildFieldMesh(Mesh mesh = null)
    {
        const int meridians = 14, layers = 8, samples = 96, sides = 4;
        var vertices = new List<Vector3>(); var normals = new List<Vector3>();
        var colors = new List<Color>(); var uv = new List<Vector2>(); var indices = new List<int>();
        var strands = new List<Vector4>(); var tubes = new List<Vector3>();
        for (int m = 0; m < meridians; m++)
        for (int layer = 0; layer < layers; layer++)
        {
            float shell = layer / (layers - 1f);
            float phase = m * 2.399963f + layer * 1.618034f;
            float reach = Mathf.Lerp(1.6f, 6.25f, shell) * (.965f + .035f * Mathf.Sin(phase));
            float phi = m * Mathf.PI * 2 / meridians + .13f * Mathf.Sin(m * 2.4f + layer * .67f) + .06f * Mathf.Sin(phase);
            float bundle = ((m / 2) * 2 + .5f) * Mathf.PI * 2 / meridians;
            float start = Mathf.Asin(Mathf.Sqrt(.6f / reach));
            Vector3 radial = new Vector3(Mathf.Cos(phi), Mathf.Sin(phi), 0);
            Vector3 normal = new Vector3(-Mathf.Sin(phi), Mathf.Cos(phi), 0);
            int first = vertices.Count;
            for (int s = 0; s < samples; s++)
            {
                float t = s / (samples - 1f), theta = Mathf.Lerp(start, Mathf.PI - start, t);
                float sin = Mathf.Sin(theta), cos = Mathf.Cos(theta), r = reach * sin * sin;
                Vector3 p = radial * (r * sin) + Vector3.forward * (r * cos * 1.55f);
                // Analytic dipole arcs stay within a fixed, compact volume.
                Vector3 tangent = (radial * (3 * sin * sin * cos) + Vector3.forward * ((2 * sin * cos * cos - sin * sin * sin) * 1.55f)).normalized;
                Vector3 second = Vector3.Cross(tangent, normal).normalized;
                float pole = Mathf.Pow(1 - Mathf.Sin(Mathf.PI * t), 3);
                Color color = Color.Lerp(new Color(.9f, .005f, .55f), new Color(1, .5f, .43f), pole);
                color *= Mathf.Lerp(1, .72f, shell) * (.9f + .1f * Mathf.Sin(phase * 1.7f));
                float thickness = Mathf.Lerp(.012f, .008f, shell);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    Vector3 n = normal * Mathf.Cos(angle) + second * Mathf.Sin(angle);
                    vertices.Add(p + n * thickness); normals.Add(n); colors.Add(color); uv.Add(new Vector2(t, shell));
                    // All four vertices of a tube section share its strand data.
                    strands.Add(new Vector4(reach, phi, phase, bundle));
                    tubes.Add(new Vector3(thickness, Mathf.Cos(angle), Mathf.Sin(angle)));
                }
                if (s == 0) continue;
                for (int side = 0; side < sides; side++)
                {
                    int a = first + (s - 1) * sides + side, b = first + (s - 1) * sides + (side + 1) % sides;
                    int c = first + s * sides + side, d = first + s * sides + (side + 1) % sides;
                    indices.Add(a); indices.Add(c); indices.Add(b); indices.Add(b); indices.Add(c); indices.Add(d);
                }
            }
        }
        // Rebuild the native layout in place. CopySerialized can leave the GPU
        // using the old layout when an existing mesh gains UV channels.
        if (!mesh) mesh = new Mesh();
        else mesh.Clear(false);
        mesh.name = "Bounded 3D Dynamo field";
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
        mesh.SetUVs(1, strands); mesh.SetUVs(2, tubes);
        // Shader motion rotates azimuth, scales inward only, and adds <= .16 Z.
        // A sphere of radius 6.6 encloses every pose, including tube thickness.
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 13.2f);
        mesh.UploadMeshData(false);
        return mesh;
    }
}
#endif
