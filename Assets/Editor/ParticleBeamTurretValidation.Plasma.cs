#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.PowerUps;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class ParticleBeamTurretValidation
{
    private static IEnumerator PlasmaRenderChecks()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(ParticleBeamTurretSetup.PrefabPath).GetComponent<ParticleBeamTurretController>();
        var beam = Object.Instantiate(source.beamVisual.gameObject).GetComponent<ParticleAcceleratorBeamVisual>();
        var contact = Object.Instantiate(source.contactPlasma.gameObject).GetComponent<ParticleBeamPlasmaContact>();
        beam.gameObject.SetActive(true); beam.gameObject.layer = contact.gameObject.layer = 31;
        Vector3 origin = new Vector3(1000f, 0f, 1000f), end = origin + Vector3.right * 8f, middle = (origin + end) * .5f;
        beam.SetSegment(origin, end, .13f, 1f); beam.SetAnimationTime(.5f);
        var go = new GameObject("Opaque plasma inspection camera"); var camera = go.AddComponent<Camera>();
        camera.transform.position = middle + Vector3.up * 12f; camera.transform.LookAt(middle, Vector3.forward);
        camera.orthographic = true; camera.orthographicSize = 2.3f; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f,.2f,.2f);
        var target = new RenderTexture(1200,600,24); var texture = new Texture2D(1200,600,TextureFormat.RGB24,false);
        var oldActive = RenderTexture.active; camera.targetTexture = target;
        yield return null; yield return null;
        Color32[] Read(string file)
        {
            camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,1200,600),0,0); texture.Apply();
            File.WriteAllBytes(Output + "/" + file, texture.EncodeToPNG()); return texture.GetPixels32();
        }
        try
        {
            var authoredPixels = Read("plasma-authored-prefab.png"); Color32 authoredBackground = authoredPixels[0];
            Check(authoredPixels.Count(p => p.r == 255) > 1000 && authoredPixels.All(p => p.Equals(authoredBackground) || (p.r == p.g && p.g == p.b && (p.r == 0 || p.r == 255))),
                "The user's applied prefab beam tuning renders opaque pure black and white");
            // Stable renderer fixture, independent of ongoing user prefab tuning.
            beam.whiteFraction = .75f; beam.strandCount = 6; beam.swirlSpeed = 7f;
            beam.waveLength = 1.6f; beam.thicknessWaveAmplitude = .12f; beam.curveAmplitude = .4f;
            beam.strandSeparation = 1f; beam.strandWander = 1.15f; beam.strandTaper = 1f;
            beam.overallThickness = beam.startThickness = beam.bodyThickness = beam.endThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(1f, 1f);
            yield return null; yield return null;
            Check(beam.plasmaLayers && Mathf.Approximately(beam.whiteFraction,.75f), "Reference plasma fixture uses a 75% white / 25% black opaque plasma split");
            Color32[] pixels = Read("plasma-sustained.png"); Color32 background = pixels[0];
            int white = 0, black = 0, intermediate = 0;
            foreach (Color32 p in pixels)
            {
                if (p.Equals(background)) continue;
                if (p.r == 255 && p.g == 255 && p.b == 255) white++;
                else if (p.r == 0 && p.g == 0 && p.b == 0) black++;
                else intermediate++;
            }
            float whiteRatio = white / (float)Mathf.Max(1, white + black);
            Check(white > 1000 && black > 100 && intermediate == 0,
                $"Rendered plasma contains only opaque pure white/black pixels (white {white}, black {black}, other {intermediate})");
            Check(whiteRatio > .65f && whiteRatio < .85f, $"Visible strand coverage is approximately 75/25 ({whiteRatio:P1} white in this view)");
            float minCenter = float.MaxValue, maxCenter = float.MinValue; int minWidth = 1000, maxWidth = 0;
            bool continuous = true; int splitColumns = 0, maxStrands = 0;
            for (int x = 120; x < 1060; x += 4)
            {
                int first = 600, last = -1;
                for (int y = 220; y < 380; y++) if (!pixels[y * 1200 + x].Equals(background)) { first = Mathf.Min(first, y); last = y; }
                if (last < first) { continuous = false; continue; }
                float center = (first + last) * .5f; int width = last - first;
                minCenter = Mathf.Min(minCenter, center); maxCenter = Mathf.Max(maxCenter, center);
                minWidth = Mathf.Min(minWidth, width); maxWidth = Mathf.Max(maxWidth, width);
                int separateVolumes = 0, runLength = 0;
                for (int y = first + 1; y <= last; y++)
                {
                    bool isVolume = y < last && !pixels[y * 1200 + x].Equals(background);
                    if (isVolume) runLength++;
                    else { if (runLength >= 2) separateVolumes++; runLength = 0; }
                }
                if (separateVolumes >= 2) splitColumns++;
                maxStrands = Mathf.Max(maxStrands, separateVolumes);
            }
            Check(continuous && maxCenter - minCenter > 3f && maxWidth - minWidth > 3,
                $"Continuous rendered beam has a curved centerline and varying thickness (bend {maxCenter-minCenter:F1}px, width {minWidth}–{maxWidth}px)");
            Check(splitColumns >= 25 && maxStrands >= 2,
                $"True empty space separates the plasma tubes ({splitColumns} sampled columns, up to {maxStrands} separate volumes)");
            var renderer = beam.GetComponent<Renderer>(); var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties); var tubes = properties.GetVectorArray("_PlasmaTubeInfo");
            var originalTubes = (Vector4[])tubes.Clone();
            for (int i = 0; i < tubes.Length; i++) if (tubes[i].x > .5f) tubes[i].z = 0f;
            properties.SetVectorArray("_PlasmaTubeInfo", tubes); renderer.SetPropertyBlock(properties);
            var onlyWhite = Read("plasma-white-volumes.png");
            for (int i = 0; i < tubes.Length; i++) { tubes[i] = originalTubes[i]; if (tubes[i].x < .5f) tubes[i].z = 0f; }
            properties.SetVectorArray("_PlasmaTubeInfo", tubes); renderer.SetPropertyBlock(properties);
            var onlyBlack = Read("plasma-black-volumes.png");
            properties.SetVectorArray("_PlasmaTubeInfo", originalTubes); renderer.SetPropertyBlock(properties);
            int whiteInFront = 0, blackInFront = 0;
            for (int i = 0; i < pixels.Length; i++)
                if (onlyWhite[i].r == 255 && onlyBlack[i].r == 0)
                { if (pixels[i].r == 0) blackInFront++; else if (pixels[i].r == 255) whiteInFront++; }
            Check(whiteInFront > 30 && blackInFront > 30,
                $"Both colors occlude the other at crossings using real strand depth (white in front {whiteInFront}px, black {blackInFront}px)");
            beam.SetAnimationTime(.8f); yield return null; yield return null;
            var moving = Read("plasma-swirl-later.png");
            Check(Enumerable.Range(0,pixels.Length).Count(i => !pixels[i].Equals(moving[i])) > 2000,
                "Advancing gameplay time moves independent strands and beam waves");
            beam.thicknessWaveAmplitude = 1f; yield return null; yield return null;
            var fullWave = Read("plasma-amplitude-one.png");
            Check(fullWave.Count(p => p.r == 255) > 1000 && fullWave.All(p => p.Equals(background) || (p.r == p.g && p.g == p.b && (p.r == 0 || p.r == 255))),
                "Thickness wave amplitude 1 renders separate strands with pure binary colors");
            renderer.GetPropertyBlock(properties); var nodes = properties.GetVectorArray("_PlasmaNodes");
            var sampling = properties.GetVector("_PlasmaTubeSampling"); Vector3 extent = properties.GetVector("_VolumeHalfExtents");
            bool contained = true;
            for (int s = 0; s < (int)sampling.x; s++) for (int n = 0; n < (int)sampling.y; n++)
            { Vector4 p = nodes[s * 65 + n]; contained &= Mathf.Abs(p.x) + p.w < extent.x && Mathf.Abs(p.y) + p.w < extent.y && Mathf.Abs(p.z) + p.w < extent.z; }
            Check(contained, "Fitted render bounds contain every strand even at maximum thickness-wave amplitude");
            beam.thicknessWaveAmplitude = source.beamVisual.thicknessWaveAmplitude; yield return null; yield return null;
            renderer.GetPropertyBlock(properties); nodes = properties.GetVectorArray("_PlasmaNodes");
            int lastNode = (int)properties.GetVector("_PlasmaTubeSampling").y - 1;
            int[] sampledNodes = { 0, lastNode / 2, lastNode };
            float[] originalRadii = sampledNodes.Select(n => nodes[n].w * beam.VolumeSizeWorld).ToArray();
            Check(originalRadii.All(r => r > .0001f), "Thickness profile fixture has visible start, body and end radii");
            beam.overallThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(2f, 2f);
            yield return null; yield return null;
            renderer.GetPropertyBlock(properties); nodes = properties.GetVectorArray("_PlasmaNodes");
            Check(Enumerable.Range(0, 3).All(i => Mathf.Abs(nodes[sampledNodes[i]].w * beam.VolumeSizeWorld / originalRadii[i] - 2f) < .001f),
                "Overall thickness range scales the entire beam by its fixed multiplier");
            Read("plasma-thickness-overall.png");
            beam.overallThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(1f, 1f);
            beam.startThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(2f, 2f);
            beam.bodyThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(.5f, .5f);
            beam.endThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(1.5f, 1.5f);
            yield return null; yield return null;
            renderer.GetPropertyBlock(properties); nodes = properties.GetVectorArray("_PlasmaNodes");
            float[] expected = { 2f, .5f, 1.5f };
            Check(Enumerable.Range(0, 3).All(i => Mathf.Abs(nodes[sampledNodes[i]].w * beam.VolumeSizeWorld / originalRadii[i] - expected[i]) < .001f),
                "Start, body and end thickness ranges independently shape their beam sections");
            Read("plasma-thickness-zones.png");
            beam.curveAmplitude = 2f; beam.thicknessWaveAmplitude = 1f; yield return null; yield return null;
            var maxCurve = Read("plasma-curve-two-profile.png");
            renderer.GetPropertyBlock(properties); nodes = properties.GetVectorArray("_PlasmaNodes");
            sampling = properties.GetVector("_PlasmaTubeSampling"); extent = properties.GetVector("_VolumeHalfExtents");
            contained = true;
            for (int s = 0; s < (int)sampling.x; s++) for (int n = 0; n < (int)sampling.y; n++)
            { Vector4 p = nodes[s * 65 + n]; contained &= Mathf.Abs(p.x) + p.w < extent.x && Mathf.Abs(p.y) + p.w < extent.y && Mathf.Abs(p.z) + p.w < extent.z; }
            Check(contained && maxCurve.Any(p => p.r == 255) && maxCurve.All(p => p.Equals(background) || (p.r == p.g && p.g == p.b && (p.r == 0 || p.r == 255))),
                "Curve amplitude 2 and thickness profiles stay inside fitted bounds and preserve pure black/white rendering");
            beam.startThickness = beam.bodyThickness = beam.endThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(1f, 1f);
            beam.curveAmplitude = source.beamVisual.curveAmplitude; beam.thicknessWaveAmplitude = source.beamVisual.thicknessWaveAmplitude;
            beam.overallThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(0f, 0f); yield return null; yield return null;
            Check(Read("plasma-zero-thickness.png").All(p => p.Equals(background)), "Zero overall thickness leaves no stale strands rendered");
            beam.overallThickness = new ParticleAcceleratorBeamVisual.ThicknessRange(1f, 1f);
            beam.SetSegment(origin, origin, 0f, 1f); yield return null; yield return null;
            var vanished = Read("plasma-fully-retracted.png");
            Check(vanished.All(p => p.Equals(background)), "A fully retracted volumetric beam leaves no stale strands rendered");
            beam.SetSegment(origin, end, .13f, 1f); yield return null; yield return null;
            contact.SetContact(end, Vector3.left, 1f);
            for (int i=0;i<30;i++) contact.Advance(.02f);
            Check(contact.LiveDropCount > 5 && contact.LiveDropCount <= 20 && contact.GetComponent<MetaballSDFInstance>().Count <= 45,
                "Sustained goop uses a bounded pool with connected droplet necks");
            Read("plasma-contact.png");
            camera.transform.position = end + Vector3.up * 8f; camera.transform.LookAt(end, Vector3.forward); camera.orthographicSize = .9f;
            Read("plasma-contact-close.png");
            contact.SetContact(end + Vector3.left * 5f, Vector3.left, 1f); contact.Advance(.02f);
            Check(contact.GetComponent<Renderer>().bounds.size.x > 4f && contact.GetComponent<Renderer>().bounds.size.y < 1f,
                "Moving contact keeps narrow fitted bounds while previous droplets drain away");
            contact.StopEmission(); for(int i=0;i<50;i++) contact.Advance(.02f);
            Check(contact.LiveDropCount == 0 && contact.GetComponent<MetaballSDFInstance>().Count == 0 && !contact.GetComponent<Renderer>().enabled,
                "All detached plasma contracts to zero without alpha fading or leftover renderers");
        }
        finally
        {
            RenderTexture.active = oldActive; camera.targetTexture = null; target.Release();
            Object.Destroy(target); Object.Destroy(texture); Object.Destroy(go); Object.Destroy(beam.gameObject); Object.Destroy(contact.gameObject);
        }
    }
}
#endif
