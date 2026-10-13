#if UNITY_EDITOR
using System.Collections;
using Massive.Multiplier;
using Massive.Player;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AmplifierPossessionValidation
{
    static int ChargedCoreDots(AmplifierPossessionPresentation guide)
    {
        int count = 0;
        for (int i = 0; i < guide.AimDotCount; i++)
            if (guide.TryGetAimDot(i, out _, out _, out float charge) && charge > .001f) count++;
        return count;
    }

    static Texture2D Pixels(Camera camera)
    {
        var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); pixels.Apply(); return pixels;
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; target.Release(); Object.DestroyImmediate(target); }
    }

    static void DarkPalette(Camera cam, string frameName)
    {
        var visual = dark.visualsController;
        var oldPosition = cam.transform.position; float oldSize = cam.orthographicSize;
        cam.transform.position = dark.transform.position + Vector3.up * 20;
        cam.orthographicSize = PlayerScaleAdjuster.BodyRadiusOf(dark) * 1.65f;
        var pixels = Pixels(cam);
        try
        {
            Check(visual.blobMat.GetColor("_FillColor").maxColorComponent < .01f &&
                visual.blobMat.GetColor("_OutlineColor").r > .99f,
                "Dark body keeps its authored black fill and white outline: " + frameName);
            int black = 0, white = 0;
            for (int y = 270; y < 370; y++) for (int x = 270; x < 370; x++)
            {
                var c = pixels.GetPixel(x, y);
                if (c.maxColorComponent < .1f) black++;
            }
            for (int y = 200; y < 440; y++) for (int x = 200; x < 440; x++)
            {
                var c = pixels.GetPixel(x, y);
                if (new Vector2(x - 320, y - 320).sqrMagnitude < 14400 && c.r > .65f && c.g > .65f && c.b > .65f) white++;
            }
            SaveFrame(cam, frameName);
            Check(black > 6500 && white > 0 && visual.nuggetsGPU && visual.nuggetsGPU.enabled,
                "Rendered Dark center stays black with visible internal dots: " + frameName + " (black " + black + ", white " + white + ")");
        }
        finally { Object.DestroyImmediate(pixels); cam.transform.position = oldPosition; cam.orthographicSize = oldSize; }
    }

    static void LightPalette(Camera cam, string frameName)
    {
        var visual = light.visualsController;
        var oldPosition = cam.transform.position; float oldSize = cam.orthographicSize;
        cam.transform.position = light.transform.position + Vector3.up * 20;
        cam.orthographicSize = PlayerScaleAdjuster.BodyRadiusOf(light) * 1.65f;
        var pixels = Pixels(cam);
        try
        {
            SaveFrame(cam, frameName);
            Check(visual.blobMat.GetColor("_FillColor").maxColorComponent < .01f &&
                visual.blobMat.GetColor("_OutlineColor").r > .99f,
                "Light body keeps its authored black fill and white outline: " + frameName);
            int white = 0, black = 0;
            for (int y = 120; y < 520; y++) for (int x = 120; x < 520; x++)
            {
                float r2 = new Vector2(x - 320, y - 320).sqrMagnitude;
                var c = pixels.GetPixel(x, y);
                if (r2 < 16900 && c.r > .9f) white++;
                if (r2 > 21025 && r2 < 27225 && c.maxColorComponent < .1f) black++;
            }
            Check(white > 100 && black > 4000 && visual.nuggetsGPU && visual.nuggetsGPU.enabled,
                "Rendered Light has a black body and white floating particles: " + frameName + " (white " + white + ", black " + black + ")");
        }
        finally { Object.DestroyImmediate(pixels); cam.transform.position = oldPosition; cam.orthographicSize = oldSize; }
    }

    static void ShellOpeningChecks()
    {
        var source = core.GetComponentsInChildren<MeshRenderer>();
        MeshRenderer shell = null;
        foreach (var renderer in source)
            if (renderer.sharedMaterial.shader.name == "MASSIVE/Amplifier Core/Neutral Shell") shell = renderer;
        Check(shell != null, "Fixture uses the real Neutral Shell shader");
        var go = new GameObject("Isolated shell aperture", typeof(MeshFilter), typeof(MeshRenderer));
        var cameraGO = new GameObject("Aperture test camera", typeof(Camera));
        var material = Object.Instantiate(shell.sharedMaterial);
        try
        {
            var mesh = shell.GetComponent<MeshFilter>().sharedMesh;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.transform.position = new Vector3(12000, 0, 0);
            float radius = mesh.bounds.extents.x;
            var camera = cameraGO.GetComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = radius * 1.25f;
            camera.transform.SetPositionAndRotation(go.transform.position + Vector3.up * 10, Quaternion.Euler(90, 0, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.allowHDR = false; camera.allowMSAA = false;
            // Diagnostic colors make any outer (red) or recessed (green) ribbon leakage measurable.
            material.SetColor("_WhiteColor", Color.red); material.SetColor("_BlackColor", Color.green);
            material.SetFloat("_ShellThickness", .12f); material.SetFloat("_DepthSteps", 6);
            material.SetFloat("_PossessionClearance", .16f);
            for (int layout = 0; layout < 2; layout++) for (int team = 0; team < (layout == 0 ? 2 : 1); team++) for (int phase = 0; phase < 3; phase++)
            {
                bool dual = layout == 1;
                material.SetVector("_PossessionMarker", new Vector4(.33f, .075f, dual ? 1 : team, phase));
                material.SetVector("_PossessionMarker2", dual ? new Vector4(.33f, .075f, 0, phase) : Vector4.zero);
                material.SetVector("_PossessionMarkerOffsets", dual ? new Vector4(-.4f, .4f, 0, 0) : Vector4.zero);
                material.SetFloat("_TimeoutWarning", phase);
                go.transform.rotation = Quaternion.Euler(phase * 37, phase * 63, phase * 29);
                var pixels = Pixels(camera);
                try
                {
                    int leaks = 0, shellPixels = 0, checkedPixels = 0;
                    for (int y = 0; y < 640; y += 2) for (int x = 0; x < 640; x += 2)
                    {
                        Vector2 p = new Vector2(x + .5f - 320, y + .5f - 320) / 256f;
                        float d = dual ? Mathf.Min((p + Vector2.right * .4f).magnitude, (p - Vector2.right * .4f).magnitude) : p.magnitude;
                        var c = pixels.GetPixel(x, y);
                        bool colored = Mathf.Max(c.r, c.g) - Mathf.Min(c.r, c.g) > .2f;
                        if (d < .28f || (d > .345f && d < .365f))
                        { checkedPixels++; if (colored) leaks++; }
                        if (d > .4f && d < .95f && colored) shellPixels++;
                    }
                    Check(checkedPixels > 1000 && shellPixels > 1000 && leaks == 0,
                        "No shell layer overlaps the marker or its clearance (dual " + dual + ", team " + team + ", rotation/flow " + phase + ")");
                    float fill = pixels.GetPixel(dual ? 218 : 320, 320).r;
                    Check(dual || team == 1 ? fill > .95f : fill < .05f, "Marker fill remains team-correct through animated shell geometry");
                    if (dual) Check(pixels.GetPixel(422, 320).r < .05f && pixels.GetPixel(422, 401).r > .7f,
                        "Second marker renders a black center and white outline on the right");
                }
                finally { Object.DestroyImmediate(pixels); }
            }
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(cameraGO); Object.DestroyImmediate(material); }
    }

    static IEnumerator PresentationChecks(Camera cam)
    {
        float originalSize = cam.orthographicSize;
        Vector3 originalPosition = cam.transform.position;
        float chargeDuration = core.Possession.fullChargeSeconds;
        float contestDuration = core.Possession.contestSeconds;
        try
        {
            // Screenshot readbacks and UI toggle checks can consume several slow Editor frames.
            core.Possession.contestSeconds = 8f;
            yield return ResetCore(); yield return Wait(.1f);
            DarkPalette(cam, "10-dark-player-resting");
            LightPalette(cam, "10-light-player-resting");
            ShellOpeningChecks();
            // The Core's new charge display must not change ordinary player Thrust.
            Set(light.attackController.Profile.GetStage(0), "travelDistance", 4f);
            Input(light, attack: true);
            yield return Until(() => light.attackController.IsCharging && light.attackController.ChargeElapsed >= .04f,
                1f, "Ordinary Thrust input has advanced beyond its first charge frame");
            var thrustGuide = light.GetComponent<PlayerThrustChargeGuide>();
            Check(thrustGuide && thrustGuide.IsVisible && thrustGuide.TryGetArrow(0, out _, out _, out _, out float width) && width > 0,
                "Ordinary player Thrust still reveals an open charge chevron (guide " + (bool)thrustGuide +
                ", charging " + light.attackController.IsCharging + ", elapsed " + light.attackController.ChargeElapsed +
                ", reach " + light.attackController.Profile.GetStage(0).TravelDistance + ", cooldown " + light.attackController.CooldownRemaining + ")");
            light.attackController.CancelAttack(); Input(light);
            Set(light.attackController.Profile.GetStage(0), "travelDistance", 0f);
            Check(!thrustGuide.IsVisible, "Cancelling player Thrust hides its guide");
            core.Possession.fullChargeSeconds = 2f;
            cam.orthographicSize = 4.2f;
            yield return ResetCore(); yield return Enter(light);
            cam.transform.position = core.transform.position + Vector3.up * 20 + Vector3.right * 5;
            var guide = core.GetComponent<AmplifierPossessionPresentation>();
            int restingCount = guide.AimDotCount;
            Check(!guide.ContestBarVisible && restingCount > 3 && ChargedCoreDots(guide) == 0,
                "Uncharged possession shows the full resting dot guide with no charge bar");
            guide.TryGetAimDot(0, out _, out float restingFirst, out _);
            guide.TryGetAimDot(restingCount - 1, out var restingEnd, out float restingLast, out _);
            Check(restingFirst > restingLast && Mathf.Abs(restingLast / restingFirst - core.Possession.dotEndScale) < .001f,
                "Resting aim dots retain the authored near-to-far size taper");
            SaveFrame(cam, "11-light-uncharged-dots");
            Input(light, attack: true); yield return Wait(.12f);
            guide.TryGetAimDot(0, out _, out float firstGrowing, out float nearCharge);
            guide.TryGetAimDot(restingCount - 1, out _, out float lastWaiting, out float farCharge);
            Check(nearCharge > 0 && firstGrowing > restingFirst && farCharge == 0 && Mathf.Abs(lastWaiting - restingLast) < .001f,
                "Holding Attack grows the nearest dot first while the farthest stays at rest");
            yield return Wait(.9f);
            Check(ChargedCoreDots(guide) > 1 && ChargedCoreDots(guide) < guide.AimDotCount && !guide.ContestBarVisible,
                "Partial charge progresses outward through the dots without a bar");
            float previousCharge = 1f;
            for (int i = 0; i < guide.AimDotCount; i++)
            {
                guide.TryGetAimDot(i, out _, out _, out float charge);
                Check(charge <= previousCharge + .0001f, "Charge never jumps ahead of a nearer dot");
                previousCharge = charge;
            }
            SaveFrame(cam, "11-light-partial-charge");
            guide.TryGetAimDot(guide.AimDotCount - 1, out var fullEnd, out _, out _);
            Check(guide.AimDotCount == restingCount && Vector3.Distance(restingEnd, fullEnd) < .001f,
                "Dot count and guide endpoint remain fixed throughout charge");
            Input(light, attack: true, aim: Vector3.forward); yield return Wait(.12f);
            guide.TryGetAimDot(guide.AimDotCount - 1, out var tip, out _, out _);
            Check(Mathf.Abs(tip.x - core.transform.position.x) < .01f, "Charged dots follow changes of aim");
            Input(light, attack: true); yield return Wait(.12f);
            SaveFrame(cam, "12-light-full-charge");
            core.Possession.showDotCharge = false;
            guide.TryGetAimDot(0, out _, out float plainRadius, out _);
            Check(ChargedCoreDots(guide) == 0 && guide.AimDotCount == restingCount && Mathf.Abs(plainRadius - restingFirst) < .001f,
                "Dot charge toggle restores a plain aim line without hiding it");
            core.Possession.showDotCharge = true;
            core.Possession.showTrajectory = false;
            Check(guide.AimDotCount == 0 && !guide.ChargeGuideVisible, "Aim guide toggle hides all charge dots");
            core.Possession.showTrajectory = true;
            yield return Until(() => core.IsLaunched, 1.5f, "Continuing to hold Attack automatically fires at full charge");
            Input(light); yield return Wait(.5f);
            Check(guide.AimDotCount == 0 && !guide.ChargeGuideVisible, "Automatic shot removes its charge dots");
            LightPalette(cam, "14-light-player-after-exit");
            yield return ResetCore(); yield return Enter(dark, 1);
            cam.transform.position = core.transform.position + Vector3.up * 20 + Vector3.left * 5;
            Input(dark, attack: true, aim: Vector3.left); yield return Wait(.95f);
            Check(ChargedCoreDots(guide) > 0 && !guide.ContestBarVisible, "Dark possession uses outward-growing charge dots too");
            SaveFrame(cam, "13-dark-charge");
            yield return Fire(dark, .1f, Vector3.left); yield return Wait(.5f);
            DarkPalette(cam, "14-dark-player-after-exit");
            yield return ResetCore(); yield return Enter(light); yield return Enter(dark, 1);
            cam.transform.position = core.transform.position + Vector3.up * 20;
            Check(guide.ContestRingVisible && !guide.ChargeGuideVisible && guide.AimDotCount == 0 && Mathf.Abs(guide.LightBarFraction - .5f) < .001f,
                "Opponent entry replaces charge dots with an equal white-left / black-right contest ring");
            Check(guide.ControlUI != null && guide.ControlUI.RingVisible && guide.ControlUI.TextVisible &&
                guide.ControlUI.Caption.text.Contains("ALTERNATE"), "Runtime ring and contest prompt use the shared presentation renderer");
            Check(guide.ControlUI.Caption.GetComponentInParent<AmplifierCoreGameplay>() == core &&
                Mathf.Abs(guide.ControlUI.Caption.fontSize - core.Possession.controlTextSize * core.GetComponent<SphereCollider>().bounds.extents.x) < .01f,
                "Gameplay control text uses the same Core-relative size as edit preview");
            core.Possession.showContestRing = false; yield return null; yield return null;
            Check(!guide.ContestRingVisible && !guide.ControlUI.RingVisible && guide.ControlUI.TextVisible, "Gameplay ring toggle leaves the control prompt visible");
            core.Possession.showContestRing = true; core.Possession.showControlText = false; yield return null; yield return null;
            Check(guide.ContestRingVisible && guide.ControlUI.RingVisible && !guide.ControlUI.TextVisible, "Gameplay text toggle leaves the contest ring visible");
            core.Possession.showControlText = true; yield return null; yield return null;
            CheckPossessionMarkers(true, 0);
            SaveFrame(cam, "15-contest-even");
            Input(light); Input(dark); yield return Wait(.04f);
            Input(dark, attack: true); yield return Wait(.2f);
            float afterAttack = guide.LightBarFraction;
            Input(dark); yield return null;
            Input(dark, shield: true); yield return Wait(.2f);
            Check(afterAttack < .5f && guide.LightBarFraction < afterAttack,
                "Dark alternating successes grow the right black portion and shrink white proportionately");
            SaveFrame(cam, "16-contest-dark-leading");
            Input(light, attack: true); yield return Wait(.2f);
            Check(guide.LightBarFraction > .5f - 2 * core.Possession.progressPerPress + .001f,
                "Light success grows the left white portion back against Dark progress");
            yield return ResetCore();
            Check(!guide.ContestBarVisible && !guide.ChargeGuideVisible, "Reset removes both contest and charge presentation");
            core.Body.position = Vector3.zero; core.transform.position = Vector3.zero; Physics.SyncTransforms();
        }
        finally
        {
            core.Possession.fullChargeSeconds = chargeDuration;
            core.Possession.contestSeconds = contestDuration;
            cam.orthographicSize = originalSize; cam.transform.position = originalPosition;
        }
    }

    static void CheckPossessionMarkers(bool contested, int winner)
    {
        var renderer = System.Array.Find(core.GetComponentsInChildren<MeshRenderer>(),
            r => r.sharedMaterial.shader.name == "MASSIVE/Amplifier Core/Neutral Shell");
        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
        Vector4 a = block.GetVector("_PossessionMarker"), b = block.GetVector("_PossessionMarker2");
        Vector4 offsets = block.GetVector("_PossessionMarkerOffsets");
        if (contested)
            Check(a.x > .2f && b.x > .2f && a.z == 1 && b.z == 0 && offsets.x < 0 && offsets.y > 0 &&
                offsets.y - offsets.x > a.x + b.x,
                "Contest has separate Light-left and Dark-right shell openings");
        else
            Check(winner == 1 ? a.x > .2f && b.x == 0 && Mathf.Abs(offsets.x) < .001f :
                b.x > .2f && a.x == 0 && Mathf.Abs(offsets.y) < .001f,
                "Only the winning team's marker remains and returns to the center");
    }
}
#endif
