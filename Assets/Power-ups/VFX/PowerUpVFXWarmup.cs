using System.Collections;
using UnityEngine;

public class PowerUpVFXWarmup : MonoBehaviour
{
    [SerializeField] private GameObject[] warmupPrefabs;
    [SerializeField] private bool runOncePerApp = true;

    private static bool _ran;

    private IEnumerator Start()
    {
        if (runOncePerApp && _ran) yield break;
        _ran = true;

        if (warmupPrefabs == null || warmupPrefabs.Length == 0) yield break;

        // Hidden camera rendering to a tiny RT triggers actual draw/compile without showing it.
        var camGO = new GameObject("[VFXWarmupCamera]");
        camGO.hideFlags = HideFlags.HideAndDontSave;

        var cam = camGO.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.orthographicSize = 1f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 10f;

        var rt = new RenderTexture(16, 16, 16, RenderTextureFormat.ARGB32);
        rt.Create();
        cam.targetTexture = rt;

        // Spawn warmups in front of warmup cam
        cam.transform.position = new Vector3(0, 10000f, 0);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var instances = new GameObject[warmupPrefabs.Length];
        for (int i = 0; i < warmupPrefabs.Length; i++)
        {
            if (!warmupPrefabs[i]) continue;

            var go = Instantiate(warmupPrefabs[i]);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.position = cam.transform.position + cam.transform.forward * 2f;

            // prevent interactions
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            // force fully visible state (so both metaball + wire draw at least once)
            var wire = go.GetComponentInChildren<ParametricPolyhedronWire>(true);
            if (wire) { wire.drawProgress = 1f; wire.foldProgress = 1f; }

            foreach (var m in go.GetComponentsInChildren<MetaballManifest>(true))
                m.SetShownInstant();

            instances[i] = go;
        }

        // Wait one frame so scripts Awake/OnEnable run, then render once
        yield return null;
        cam.Render();

        // Cleanup
        for (int i = 0; i < instances.Length; i++)
            if (instances[i]) Destroy(instances[i]);

        Destroy(camGO);
        rt.Release();
        Destroy(rt);
    }
}
