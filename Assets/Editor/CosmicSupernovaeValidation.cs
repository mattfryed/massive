#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Cosmos;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercises both real scene layers; all clock/seed changes are temporary Play Mode state.</summary>
[InitializeOnLoad]
public static class CosmicSupernovaeValidation
{
    const string Key = "COSMOS.SupernovaValidation";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static double deadline;
    static float originalTimeScale;
    static int frame;
    static CosmicSupernovaeValidation() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("MASSIVE/COSMOS/Supernovae/Validate both layers in Play Mode %#&F5")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CosmosLayoutSetup.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open saved COSMOS in Edit Mode.");
        Directory.CreateDirectory(CosmicSupernovaeSetup.Output);
        CaptureComparison();
        SessionState.SetString(Key + ".SceneHash", Hash128.Compute(File.ReadAllText(scene.path)).ToString());
        SessionState.SetBool(Key, true);
        File.WriteAllText(CosmicSupernovaeSetup.Output + "/validation.txt", "STARTING\n");
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 120;
            originalTimeScale = Time.timeScale; routine = Checks();
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Supernova validation timed out.");
            frame = Time.frameCount;
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception failure)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Time.timeScale = originalTimeScale;
        bool passed = failure == null && errors.Count == 0;
        File.WriteAllText(CosmicSupernovaeSetup.Output + "/validation.txt", (passed ? "PASSED" : "FAILED") + "\n" +
            string.Join("\n", checks) + "\n" + failure + "\n" + string.Join("\n", errors));
        EditorApplication.isPlaying = false;
        Debug.Log("COSMOS supernova validation " + (passed ? "PASSED" : "FAILED: " + failure));
    }
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks.Add("PASS " + label); }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private | BindingFlags.Public).GetValue(owner);
    static GameObject Pool(CosmicSupernovae layer) => Field<GameObject>(layer, "poolRoot");
    static MatterNuggetScript[] Pickups(CosmicSupernovae layer) => Pool(layer).GetComponentsInChildren<MatterNuggetScript>(true);
    static void SetRemaining(GameManagerScript match, float value) =>
        typeof(GameManagerScript).GetField("_regulationRemainingSeconds", Private).SetValue(match, value);
    static IEnumerator Checks()
    {
        yield return null;
        var layers = CosmosLayoutSetup.All<CosmicSupernovae>().ToArray();
        Check(layers.Length == 2, "Both supernova layers are installed");
        var normal = layers.Single(s => s.kind == CosmicSupernovae.SupernovaKind.Normal);
        var super = layers.Single(s => s.kind == CosmicSupernovae.SupernovaKind.Superluminous);
        var match = normal.web.match;
        Check(normal.web == super.web && normal.enabled && super.enabled, "Both layers share the current web and regulation clock");
        Check(!ShaderUtil.ShaderHasError(normal.flashShader) && normal.flashShader.isSupported, "Flash shader compiles on the active device");
        Check(Enumerable.Range(0, 201).All(i => Mathf.Abs(super.FrequencyAtAge(i / 200f) - normal.FrequencyAtAge(i / 200f) * .5f) < .00001f),
            "Superluminous frequency is half normal across all 201 sampled ages");
        Check(normal.FrequencyAtAge(0) == 0 && super.FrequencyAtAge(1) == 0, "Both bell curves reach zero at timeline endpoints");
        Check(Mathf.Approximately(normal.nuggletPrefab.rewardMultiplier, .1f) && Mathf.Approximately(super.nuggletPrefab.rewardMultiplier, 1),
            "Normal uses fractional nugglets; superluminous uses full-mass nuggets");
        Check(super.flashRadius > normal.flashRadius * 2 && super.flashBrightness >= normal.flashBrightness * 2,
            "Superluminous explosions are larger and brighter");
        Check(normal.ExplosionsReleased == 0 && super.ExplosionsReleased == 0, "Countdown releases no pickups");
        while (match.Phase != MatchRuntimePhase.Regulation) yield return null;
        var maskCheck = CheckBorderMask(normal, super);
        while (maskCheck.MoveNext()) yield return maskCheck.Current;
        SetRemaining(match, match.RegulationDurationSeconds * .6f);
        foreach (var layer in layers)
        {
            layer.enabled = false; layer.fixedRandomSeed = true;
            layer.randomSeed = layer == normal ? 65077 : 65078; layer.enabled = true;
        }
        Time.timeScale = 3;
        var eventColors = new Dictionary<(int, float), Color>();
        var palette = new HashSet<Color>();
        bool stable = true, captured = false, warningsBeforePickup = false, lingeringCloud = false, dissolvingCloud = false;
        float stop = match.RegulationRemainingSeconds - 18;
        while (match.RegulationRemainingSeconds > stop)
        {
            var flashes = Field<Array>(normal, "flashes");
            for (int i = 0; i < flashes.Length; i++)
            {
                object flash = flashes.GetValue(i);
                if (!Field<bool>(flash, "active")) continue;
                var key = (i, Field<float>(flash, "flickerSeed"));
                Color color = Field<Color>(flash, "color"); palette.Add(color);
                if (eventColors.TryGetValue(key, out Color original)) stable &= color == original;
                else eventColors.Add(key, color);
                if (!Field<bool>(flash, "exploded") && Field<float>(flash, "elapsed") > .1f)
                    warningsBeforePickup = true;
            }
            foreach (object flash in Field<Array>(super, "flashes"))
                if (Field<bool>(flash, "active") && Field<bool>(flash, "exploded"))
                {
                    float age = Field<float>(flash, "elapsed") - Field<float>(flash, "buildUp");
                    lingeringCloud |= age > Mathf.Max(Field<float>(flash,"duration"), Field<float>(flash,"dieOff")) + .1f;
                    dissolvingCloud |= age > Field<float>(flash,"cloudExpansion") + .2f;
                    if (!captured && age > super.cloudExpansionSeconds * .7f)
                    { CaptureCamera(Camera.main, CosmicSupernovaeSetup.Output + "/gameplay.png"); captured = true; }
                }
            yield return null;
        }
        Check(normal.ExplosionsReleased > 5 && super.ExplosionsReleased > 2,
            $"Both schedules release pickups in regulation ({normal.ExplosionsReleased} normal, {super.ExplosionsReleased} superluminous)");
        Check(warningsBeforePickup, "Events retain a buildup before their burst");
        Check(lingeringCloud && dissolvingCloud, "Superluminous cloud survives the center flash and continues through its independent dissolve");
        Check(stable && palette.Count == 4, "All four normal colors occur and remain stable for each event");
        Check(Pickups(normal).Any(p => p.gameObject.activeSelf) && Pickups(super).Any(p => p.gameObject.activeSelf),
            "Both pickup types coexist in the arena");
        Check(Pickups(super).All(p => Mathf.Approximately(p.rewardMultiplier, 1)) &&
            Pickups(normal).All(p => Mathf.Approximately(p.rewardMultiplier, .1f)), "Spawned pickups retain their prefab reward values");
        Check(layers.All(s => Pickups(s).Length <= s.nuggletCapacity && Pickups(s).All(p => Mathf.Abs(p.transform.position.y) < .01f)),
            "Pickup pools stay bounded and use the XZ gameplay plane");
        Check(captured, "Captured a live superluminous burst over the current web");
        Check(match.BeginBonusRound(true), "Paused-clock bonus phase entered");
        int normalCount = normal.ExplosionsReleased, superCount = super.ExplosionsReleased;
        double until = EditorApplication.timeSinceStartup + 1;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Check(normal.ExplosionsReleased == normalCount && super.ExplosionsReleased == superCount, "Paused regulation creates no new explosions or releases");
        var oldIds = new HashSet<int>(Pickups(super).Select(p => p.GetInstanceID()));
        SetRemaining(match, match.RegulationDurationSeconds * .65f);
        yield return null; yield return null;
        Check(layers.All(l => l.ExplosionsReleased == 0 && Pickups(l).All(p => !p.gameObject.activeSelf)),
            "Rewinding clears both layers and releases reservations");
        match.EndBonusRound();
        stop = match.RegulationRemainingSeconds - 8;
        while (match.RegulationRemainingSeconds > stop) yield return null;
        Check(super.ExplosionsReleased > 0 && Pickups(super).Any(p => p.gameObject.activeSelf && oldIds.Contains(p.GetInstanceID())),
            "Full-mass nuggets are reused from the pool after reset");
        normal.enabled = false;
        Check(Pickups(normal).All(p => !p.gameObject.activeSelf) && super.enabled && Pickups(super).Any(p => p.gameObject.activeSelf),
            "Disabling normal events clears only their owned pickups");
        super.enabled = false;
        Check(Pickups(super).All(p => !p.gameObject.activeSelf), "Disabling superluminous events clears their owned pickups");
        normal.enabled = true; super.enabled = true;
        yield return null;
        Check(normal.ExplosionsReleased == 0 && super.ExplosionsReleased == 0, "Re-enabling resets both schedules safely");
        Check(Hash128.Compute(File.ReadAllText(CosmosLayoutSetup.ScenePath)).ToString() == SessionState.GetString(Key + ".SceneHash", ""),
            "Play Mode validation leaves the authored scene unchanged");
        Check(errors.Count == 0, "No runtime errors or exceptions");
    }

    static IEnumerator CheckBorderMask(CosmicSupernovae normal, CosmicSupernovae super)
    {
        var layout = super.GetComponent<CosmosArenaLayout>();
        var arena = super.GetComponent<ArenaBoundsFromVectorGrid>();
        var camera = Camera.main;
        Check(layout.maskOutsideOval && layout.outsideMaskShader && !ShaderUtil.ShaderHasError(layout.outsideMaskShader),
            "Continuous outside-oval black mask is installed and its shader compiles");
        float oldTimeScale = Time.timeScale; bool oldMask = layout.maskOutsideOval;
        var objects = new List<GameObject>(); var materials = new List<Material>();
        try
        {
            Time.timeScale = 0;
            // Compare the complete HUD after its startup reveal, before adding test emitters.
            layout.maskOutsideOval = false; yield return null; yield return null;
            var hudBefore = CaptureCamera(camera,CosmicSupernovaeSetup.Output + "/hud-unmasked.png");
            layout.maskOutsideOval = true; yield return null; yield return null;
            var hudAfter = CaptureCamera(camera,CosmicSupernovaeSetup.Output + "/hud-masked.png");
            Vector4 oval = arena.OutlineShaderParameters;
            for (int i = 0; i < 12; i++)
            {
                var layer = i % 2 == 0 ? normal : super;
                float angle = (i + .5f) * Mathf.PI * 2 / 12;
                var go = new GameObject("Border VFX validation probe", typeof(MeshFilter), typeof(MeshRenderer)); objects.Add(go);
                go.transform.SetPositionAndRotation(arena.transform.TransformPoint(new Vector3(
                    Mathf.Cos(angle) * oval.z * .985f, Mathf.Sin(angle) * oval.y * .985f, -super.web.backgroundDepth)), arena.transform.rotation);
                go.transform.localScale = Vector3.one * layer.flashRadius;
                go.GetComponent<MeshFilter>().sharedMesh = Field<Mesh>(layer,"flashMesh");
                var material = new Material(layer.flashShader); materials.Add(material);
                material.SetColor("_Color",layer.flashColor); material.SetColor("_ShellColor",super.shellColor);
                material.SetFloat("_Brightness",layer.flashBrightness); material.SetFloat("_Superluminous",layer == super ? 1 : 0);
                material.SetFloat("_Phase",layer == super ? 1 : .3f); material.SetFloat("_Glow",.15f);
                material.SetFloat("_BurstSeed",73 + i * 19); SetCloudProperties(material,super,1.5f);
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            layout.maskOutsideOval = false; yield return null; yield return null;
            var before = CaptureCamera(camera,CosmicSupernovaeSetup.Output + "/border-unmasked.png");
            layout.maskOutsideOval = true; yield return null; yield return null;
            var after = CaptureCamera(camera,CosmicSupernovaeSetup.Output + "/border-masked.png");
            int leaked = 0, covered = 0, hud = 0, retained = 0;
            var plane = new Plane(arena.transform.forward,arena.transform.position);
            for (int y = 0; y < 900; y += 2)
                for (int x = 0; x < 1600; x += 2)
                {
                    int index = y * 1600 + x; Color32 a = before[index], b = after[index];
                    Color32 ha = hudBefore[index], hb = hudAfter[index];
                    if ((y < 125 || y > 780) && Mathf.Max(ha.r,Mathf.Max(ha.g,ha.b)) > 12)
                    {
                        hud++;
                        // Unit labels pulse on an unscaled clock even while gameplay is frozen.
                        // Require their visibility; compare all other HUD pixels directly.
                        bool unitLabel = y > 17 && y < 40;
                        if (unitLabel ? Mathf.Max(hb.r,Mathf.Max(hb.g,hb.b)) >= Mathf.Max(ha.r,Mathf.Max(ha.g,ha.b)) * .75f :
                            Mathf.Abs(ha.r-hb.r) <= 3 && Mathf.Abs(ha.g-hb.g) <= 3 && Mathf.Abs(ha.b-hb.b) <= 3) retained++;
                    }
                    var ray = camera.ViewportPointToRay(new Vector3((x + .5f)/1600,(y + .5f)/900,0));
                    if (!plane.Raycast(ray,out float distance)) continue;
                    Vector3 p = arena.transform.InverseTransformPoint(ray.GetPoint(distance));
                    float ellipse = p.x*p.x/(oval.z*oval.z) + p.y*p.y/(oval.y*oval.y);
                    // Curved corners inside the old rectangular opening; exclude the border and HUD.
                    if (ellipse > 1.01f && Mathf.Abs(p.y) < oval.y * .92f && Mathf.Abs(p.x) < oval.x &&
                        Mathf.Max(a.r,Mathf.Max(a.g,a.b)) > 12)
                    { leaked++; if (b.r < 3 && b.g < 3 && b.b < 3) covered++; }
                }
            Check(leaked > 40 && covered == leaked, $"Black mask covers every sampled corner spill pixel ({covered}/{leaked}) for staged normal/superluminous border bursts");
            Check(hud > 1000 && retained >= hud * .999f, $"Full HUD/title pixels remain unoccluded after startup ({retained}/{hud}; unit-label pulse allowed)");
        }
        finally
        {
            Time.timeScale = oldTimeScale; layout.maskOutsideOval = oldMask;
            foreach (var go in objects) Object.DestroyImmediate(go);
            foreach (var material in materials) Object.DestroyImmediate(material);
        }
    }

    // Render the actual shader: normal palette, four precursor seeds, then four cloud ages.
    public static void CaptureComparison()
    {
        var layers = CosmosLayoutSetup.All<CosmicSupernovae>().ToArray();
        var normal = layers.Single(s => s.kind == CosmicSupernovae.SupernovaKind.Normal);
        var super = layers.Single(s => s.kind == CosmicSupernovae.SupernovaKind.Superluminous);
        var scene = EditorSceneManager.NewPreviewScene();
        var materials = new List<Material>();
        var meshes = new List<Mesh>();
        try
        {
            var cameraObject = new GameObject("Supernova comparison camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.transform.position = new Vector3(0,0,-10); camera.orthographic = true; camera.orthographicSize = 6.5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.allowHDR = false;
            Color[] colors = { normal.flashColor, normal.cyanWhiteColor, normal.cyanBlueColor, normal.lightPurpleColor };
            for (int i = 0; i < 12; i++)
            {
                bool luminous = i >= 4; var layer = luminous ? super : normal;
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.position = new Vector3((i % 4 - 1.5f) * 5, 4.2f - (i / 4) * 4.2f, 0);
                go.transform.localScale = Vector3.one * (layer.flashRadius * 2);
                var material = new Material(layer.flashShader); materials.Add(material);
                material.SetColor("_Color", luminous ? super.flashColor : colors[i]);
                material.SetColor("_ShellColor", super.shellColor); material.SetFloat("_ShellDetail", super.shellDetail);
                material.SetFloat("_Superluminous", luminous ? 1 : 0); material.SetFloat("_Brightness", layer.flashBrightness);
                bool precursor = i >= 4 && i < 8;
                float age = luminous && !precursor ? new[] { .4f, 1.2f, 2.2f, 3.4f }[i % 4] : 0;
                material.SetFloat("_Phase", luminous ? precursor ? -1 : Mathf.Clamp01(age / super.flashSeconds) : .23f);
                material.SetFloat("_Glow", luminous ? precursor ? super.buildUpBrightness :
                    1 - Mathf.SmoothStep(0,1,Mathf.Clamp01(age / super.dieOffSeconds)) : .6f);
                material.SetFloat("_BurstSeed", precursor ? 73 + (i % 4) * 37 : 73);
                SetCloudProperties(material, super, age, precursor);
                // The production quad uses signed UVs; remap the primitive's mesh for the same contract.
                var mesh = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
                meshes.Add(mesh);
                mesh.uv = mesh.uv.Select(uv => uv * 2 - Vector2.one).ToArray();
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            CaptureCamera(camera, CosmicSupernovaeSetup.Output + "/comparison.png");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            foreach (var material in materials) Object.DestroyImmediate(material);
        }
    }
    static void SetCloudProperties(Material material, CosmicSupernovae super, float age, bool precursor = false)
    {
        material.SetFloat("_CloudExpansion", Mathf.Clamp01(age / super.cloudExpansionSeconds));
        material.SetFloat("_CloudOpacity", precursor ? 0 : 1 - Mathf.SmoothStep(0,1,
            Mathf.Clamp01((age - super.cloudExpansionSeconds) / super.cloudFadeSeconds)));
        material.SetFloat("_CloudDistortion", super.cloudDistortion);
        material.SetFloat("_TelegraphDistortion", super.telegraphDistortion);
        material.SetFloat("_FlowTime", (super.buildUpSeconds + age) * super.cloudMixingSpeed);
    }
    static Color32[] CaptureCamera(Camera camera, string path)
    {
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(1600,900,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG());
            return image.GetPixels32();
        }
        finally
        { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
    }
}
#endif
