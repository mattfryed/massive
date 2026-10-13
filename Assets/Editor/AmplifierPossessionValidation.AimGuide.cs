#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Massive.Multiplier;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AmplifierPossessionValidation
{
    [MenuItem("MASSIVE/Amplifier/Validate Aim Guide Motion and Masking")]
    public static void RunAimGuide()
    {
        Run();
        SessionState.SetBool(Key + ".AimGuideOnly", true);
    }

    static float GuideError(AmplifierPossessionPresentation guide)
    {
        if (!guide.TryGetAimDot(guide.AimDotCount - 1, out var tip, out _, out _)) return 180f;
        return Vector3.Angle(tip - core.transform.position, core.AimDirection);
    }

    static float DotBrightness(Camera camera, Texture2D pixels, Vector3 point)
    {
        Vector3 viewport = camera.WorldToViewportPoint(point);
        int x = Mathf.RoundToInt(viewport.x * pixels.width), y = Mathf.RoundToInt(viewport.y * pixels.height);
        float value = 0f;
        for (int j = -3; j <= 3; j++) for (int i = -3; i <= 3; i++)
            value = Mathf.Max(value, pixels.GetPixel(Mathf.Clamp(x + i, 0, pixels.width - 1),
                Mathf.Clamp(y + j, 0, pixels.height - 1)).r);
        return value;
    }

    static void SaveAimFrame(Texture2D pixels, string name)
    {
        const string directory = "Library/AmplifierAimGuideValidation";
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllBytes(directory + "/" + name + ".png", pixels.EncodeToPNG());
    }

    static IEnumerator AimGuideChecks(Camera cam)
    {
        string original = JsonUtility.ToJson(core.Possession);
        var failures = new List<string>();
        GameObject mask = null;
        Material material = null;
        void Observe(bool pass, string label)
        {
            checks.Add((pass ? "PASS " : "FAIL ") + label);
            if (!pass) failures.Add(label);
        }
        try
        {
            core.Possession.possessionSeconds = 60f;
            core.Possession.showControlText = false;
            core.Possession.fullChargeSeconds = 10f;
            core.Possession.dotRadius = .08f;
            core.Possession.dotEndScale = 1f;
            yield return ResetCore(); yield return Enter(light); yield return Wait(.1f);
            var guide = core.GetComponent<AmplifierPossessionPresentation>();
            Check(guide && guide.AimDotCount > 3, "Real occupied Core creates its dotted guide");

            // Several distinct directions within one frame isolate any presentation timer
            // from controller sampling, physics ticks and Editor frame rate.
            float maxError = 0f;
            for (int i = 0; i < 16; i++)
            {
                Set(core, "aimDirection", Quaternion.AngleAxis(i * 1.3f, Vector3.up) * Vector3.right);
                Invoke(guide, "LateUpdate");
                maxError = Mathf.Max(maxError, GuideError(guide));
            }
            Observe(maxError < .05f, "Every small aim change refreshes immediately; max error " + maxError.ToString("F3") + " degrees");

            // Observe after both LateUpdates, at the actual render boundary, while
            // feeding the normal player input path a continuously rotating direction.
            int samples = 0;
            float renderedError = 0f;
            void BeforeRender(Camera camera)
            {
                if (camera != cam) return;
                renderedError = Mathf.Max(renderedError, GuideError(guide)); samples++;
            }
            Camera.onPreRender += BeforeRender;
            try
            {
                for (int i = 0; i < 40; i++)
                {
                    Input(light, aim: Quaternion.AngleAxis(i * 2.7f, Vector3.up) * Vector3.right);
                    yield return null;
                }
            }
            finally { Camera.onPreRender -= BeforeRender; }
            Observe(samples >= 30 && renderedError < .05f,
                "Rendered guide matches current player aim across " + samples + " frames; max error " + renderedError.ToString("F3") + " degrees");

            Input(light); yield return Wait(.12f);
            cam.transform.position = core.transform.position + new Vector3(6, 20, 0);
            cam.orthographicSize = 7f; cam.aspect = 1f;
            Vector3 inside = default, outside = default;
            for (int i = 0; i < guide.AimDotCount; i++)
            {
                guide.TryGetAimDot(i, out var point, out _, out _);
                float x = point.x - core.transform.position.x;
                if (x > 2f && x < 3f) inside = point;
                if (x > 7f && x < 8f) outside = point;
            }
            Check(inside != default && outside != default, "Guide crosses both sides of the test arena boundary");
            var pixels = Pixels(cam);
            try
            {
                Check(DotBrightness(cam, pixels, inside) > .8f && DotBrightness(cam, pixels, outside) > .8f,
                    "Both interior and exterior dots render before adding the real arena mask shader");
                SaveAimFrame(pixels, "unmasked");
            }
            finally { Object.DestroyImmediate(pixels); }

            mask = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(mask.GetComponent<Collider>());
            mask.name = "Aim guide arena mask fixture";
            mask.transform.SetPositionAndRotation(core.transform.position + Vector3.up * 2f, Quaternion.Euler(90, 0, 0));
            mask.transform.localScale = Vector3.one * 40f;
            material = new Material(Shader.Find("MASSIVE/Cosmos/ArenaMask"));
            mask.GetComponent<Renderer>().sharedMaterial = material;
            material.SetMatrix("_CosmosWorldToGrid", Matrix4x4.TRS(core.transform.position, Quaternion.Euler(90, 0, 0), Vector3.one).inverse);
            for (int shape = 0; shape < 3; shape++)
            {
                material.SetVector("_ArenaRect", new Vector4(4, 3, shape == 1 ? 2 : 0, 1));
                material.SetVector("_ArenaOval", shape == 2 ? new Vector4(4, 3, 6, 1) : Vector4.zero);
                yield return null;
                pixels = Pixels(cam);
                try
                {
                    float interior = DotBrightness(cam, pixels, inside), exterior = DotBrightness(cam, pixels, outside);
                    Observe(interior > .8f && exterior < .02f,
                        "Mask shape " + shape + " preserves interior dots and hides exterior dots (" + interior.ToString("F2") + ", " + exterior.ToString("F2") + ")");
                    SaveAimFrame(pixels, "mask-" + shape);
                }
                finally { Object.DestroyImmediate(pixels); }
            }
            core.Possession.showTrajectory = false; yield return null; yield return null;
            Check(!guide.AimGuideVisible && guide.AimDotCount == 0, "Trajectory toggle hides every dot");
            core.Possession.showTrajectory = true; yield return Wait(.1f);
            Check(guide.AimDotCount > 3, "Trajectory toggle restores the guide");
            Input(light, attack: true); yield return Wait(.2f);
            Check(ChargedCoreDots(guide) > 0, "Dot charging still progresses after continuous aiming");
            yield return Fire(light, .1f, Vector3.forward);
            Check(!guide.AimGuideVisible, "Launching hides the guide");
            Check(failures.Count == 0, string.Join("; ", failures));
        }
        finally
        {
            if (mask) Object.DestroyImmediate(mask);
            if (material) Object.DestroyImmediate(material);
            JsonUtility.FromJsonOverwrite(original, core.Possession);
        }
    }
}
#endif
