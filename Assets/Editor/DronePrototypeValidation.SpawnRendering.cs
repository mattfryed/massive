#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class DronePrototypeValidation
{
    private static IEnumerator SpawnRenderingChecks()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.TelegraphPath);
        var source = prefab.GetComponent<MeshFilter>().sharedMesh;
        var first = Own(Object.Instantiate(prefab)); var second = Own(Object.Instantiate(prefab));
        var joined = first.GetComponent<MeshFilter>().sharedMesh;
        Check(joined != source && joined == second.GetComponent<MeshFilter>().sharedMesh,
            "Concurrent warnings share one derived outline without modifying the source asset");
        var warning = first.GetComponent<EnemySpawnTelegraph>();
        warning.ghostsEnabled = false; warning.diffuseGlowStrength = 0f; warning.breathScale = 0f;
        warning.rollDegreesPerSecond = 0f; warning.Begin(); warning.Advance(1f);
        second.SetActive(false);
        CompareRenderedJoin(first, source, joined);
        Object.Destroy(first); yield return .05f;
        Check(joined != null, "Derived outline survives while another warning owns it");
        Object.Destroy(second); yield return .05f;
        Check(joined == null, "Last warning releases its derived outline mesh");

        var enemy = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DysonSpawnIndicatorSetup.PrefabPath)));
        enemy.GetComponent<EnemyBase>().Init(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DysonSpawnIndicatorSetup.DefinitionPath), null);
        var panels = enemy.GetComponentInChildren<DysonSpherePanels>();
        Check(VisibleFilledPanels(panels) == 0, "Fresh Dyson has no full black panels before its first spawn update");
        Check(panels.GetComponentsInChildren<MeshRenderer>().Length == panels.maxPanels,
            "Fresh Dyson creates one shell rather than an extra shell awaiting destruction");
        yield return panels.spawnSeconds + .1f;
        Check(VisibleFilledPanels(panels) > 0, "Dyson panels still fill normally as spawning completes");
        enemy.SetActive(false); enemy.SetActive(true);
        Check(VisibleFilledPanels(panels) == 0, "Re-enabled Dyson immediately hides retired panels before restarting its reveal");
        yield return .05f;
        Check(panels.GetComponentsInChildren<MeshRenderer>(true).Length == panels.maxPanels,
            "Retired Dyson shell is removed by the following frame");
        Object.Destroy(enemy); yield return .05f;
    }

    private static int VisibleFilledPanels(DysonSpherePanels panels) =>
        panels.GetComponentsInChildren<MeshRenderer>().Count(r => r.enabled && r.GetComponent<MeshFilter>() &&
            r.GetComponent<MeshFilter>().sharedMesh.vertices.Any(v => v.sqrMagnitude > .000001f));

    private static void CompareRenderedJoin(GameObject marker, Mesh source, Mesh joined)
    {
        var go = new GameObject("Join regression camera"); var camera = go.AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1f;
        camera.transform.position = new Vector3(0, 5, -2.5f); camera.transform.LookAt(Vector3.zero);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << LayerMask.NameToLayer("Enemy"); camera.allowHDR = true;
        var target = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGBHalf);
        var texture = new Texture2D(1024, 1024, TextureFormat.RGBAFloat, false, true);
        var previous = RenderTexture.active; var filter = marker.GetComponent<MeshFilter>();
        var renderer = marker.GetComponent<MeshRenderer>();
        var originalProperties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(originalProperties);
        var diagnosticProperties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(diagnosticProperties);
        // Separate the thin wire and broad glow into channels so dimming the wire
        // cannot accidentally pass as a successful glow correction.
        diagnosticProperties.SetColor("_Color", Color.red);
        diagnosticProperties.SetColor("_GlowColor", Color.green);
        diagnosticProperties.SetFloat("_Contrast", 0f);
        var vertices = source.vertices; Vector3 tip = vertices.OrderByDescending(v => v.z).First();
        Vector3 ring = vertices.First(v => Mathf.Abs(v.z) < .0001f);
        try
        {
            camera.targetTexture = target;
            renderer.SetPropertyBlock(diagnosticProperties);
            float[] peaks = new float[6];
            var joins = vertices.Distinct().ToArray(); var baselineJoins = new float[joins.Length];
            bool continuous = true;
            for (int pass = 0; pass < 2; pass++)
            {
                filter.sharedMesh = pass == 0 ? source : joined;
                camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); texture.Apply();
                peaks[pass * 3] = PeakAt(texture, camera.WorldToScreenPoint(marker.transform.TransformPoint(tip)));
                peaks[pass * 3 + 1] = PeakAt(texture, camera.WorldToScreenPoint(marker.transform.TransformPoint(tip)), 1);
                peaks[pass * 3 + 2] = PeakAt(texture, camera.WorldToScreenPoint(marker.transform.TransformPoint((tip + ring) * .5f)), 1);
                for (int j = 0; j < joins.Length; j++)
                {
                    float core = PeakAt(texture, camera.WorldToScreenPoint(marker.transform.TransformPoint(joins[j])));
                    if (pass == 0) baselineJoins[j] = core;
                    else continuous &= core >= baselineJoins[j] * .98f;
                }
            }
            Check(continuous && peaks[3] >= peaks[0] * .98f, "Thin wire remains fully continuous at every rendered vertex");
            Check(peaks[4] >= peaks[1] * .55f && peaks[4] < peaks[1] * .85f,
                "Broad tip glow is gently reduced, not removed: " + peaks[1] + " -> " + peaks[4]);
            Check(peaks[5] >= peaks[2] * .98f, "Broad glow keeps its brightness along the middle of each edge");
        }
        finally
        {
            filter.sharedMesh = joined; camera.targetTexture = null; RenderTexture.active = previous;
            renderer.SetPropertyBlock(originalProperties);
            Object.Destroy(texture); Object.Destroy(target); Object.Destroy(go);
        }
    }

    private static float PeakAt(Texture2D texture, Vector3 pixel, int channel = 0)
    {
        float peak = 0f;
        for (int y = -2; y <= 2; y++) for (int x = -2; x <= 2; x++)
            peak = Mathf.Max(peak, texture.GetPixel(Mathf.Clamp((int)pixel.x + x, 0, texture.width - 1),
                Mathf.Clamp((int)pixel.y + y, 0, texture.height - 1))[channel]);
        return peak;
    }
}
#endif
