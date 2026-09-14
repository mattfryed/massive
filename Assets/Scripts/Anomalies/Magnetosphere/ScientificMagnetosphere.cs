using System;
using UnityEngine;

namespace Massive.Dynamo
{
    public enum MagnetosphereViewFrame { EarthFixed, SunOriented }

    /// <summary>Owns one scientific clock and the two MHD snapshots used by every consumer.</summary>
    [DisallowMultipleComponent]
    public sealed class ScientificMagnetosphere : MonoBehaviour
    {
        [SerializeField] private ScientificStormEpisode episode;
        [SerializeField] private MagnetosphereViewFrame viewFrame = MagnetosphereViewFrame.EarthFixed;
        [SerializeField, Min(.01f)] private float worldUnitsPerEarthRadius = .7f;
        [SerializeField, Min(0)] private float scientificSecondsPerSecond = 20;
        [SerializeField] private bool playing = true;
        [SerializeField, Range(0, 1)] private float previewPosition;
        [Tooltip("Optional observed episodes. Selection changes the entire event, including its timestamp.")]
        [SerializeField] private ScientificStormEpisode[] catalogue;

        private float[] firstValues, secondValues;
        private ScientificStormEpisode loadedEpisode;
        private int firstIndex = -1, secondIndex = -1;
        private Texture3D firstB, secondB, firstU, secondU;
        private double elapsed;
        private float lastPreview = -1;
        private Quaternion gsmToGeo;
        private Matrix4x4 gsmToWorld, worldToGsm;

        public ScientificStormEpisode Episode => episode;
        public bool Ready => firstValues != null && secondValues != null && enabled && gameObject.activeInHierarchy;
        public float FrameBlend { get; private set; }
        public Texture3D FirstB => firstB;
        public Texture3D SecondB => secondB;
        public Texture3D FirstU => firstU;
        public Texture3D SecondU => secondU;
        public Matrix4x4 GsmToWorld => gsmToWorld;
        public double Elapsed => elapsed;
        public double UtcSeconds => episode.manifest.frames[0].unixSeconds + elapsed;
        public bool Playing { get => playing; set => playing = value; }
        public float PlaybackSpeed { get => scientificSecondsPerSecond; set => scientificSecondsPerSecond = Mathf.Max(0, value); }
        public Vector3 FrameOmegaGsm => viewFrame == MagnetosphereViewFrame.EarthFixed
            ? Quaternion.Inverse(gsmToGeo) * new Vector3(0, 0, 7.292115e-5f) : Vector3.zero;
        public Vector3 DipoleAxisGsm => Vector3.Slerp(episode.manifest.frames[firstIndex].dipoleAxisGsm,
            episode.manifest.frames[secondIndex].dipoleAxisGsm, FrameBlend).normalized;
        public float UpstreamPressure => Mathf.Lerp(episode.manifest.frames[firstIndex].upstreamDynamicPressureNpa,
            episode.manifest.frames[secondIndex].upstreamDynamicPressureNpa, FrameBlend);

        public void Configure(ScientificStormEpisode value) { episode = value; if (isActiveAndEnabled) LoadEpisode(); }

        private void OnEnable() { if (episode) LoadEpisode(); }
        private void OnDisable() { Release(); }

        private void LoadEpisode()
        {
            Release();
            if (!episode) return;
            try
            {
                episode.ValidateData();
                loadedEpisode = episode;
                elapsed = Mathf.Clamp01(previewPosition) * episode.Duration;
                RefreshSamples();
            }
            catch (Exception ex)
            {
                Release(); enabled = false;
                Debug.LogError("[Dynamo scientific data] " + ex.Message, this);
            }
        }

        private void Update()
        {
            if (episode != loadedEpisode) { previewPosition = 0; LoadEpisode(); }
            if (!Ready) return;
            if (!Mathf.Approximately(previewPosition, lastPreview)) elapsed = previewPosition * episode.Duration;
            else if (playing) elapsed = Math.Min(episode.Duration, elapsed + Time.deltaTime * scientificSecondsPerSecond);
            if (elapsed >= episode.Duration) playing = false; // No unphysical jump from recovery to another event.
            RefreshSamples();
        }

        public void SeekNormalized(float position)
        {
            if (!episode) return;
            elapsed = Mathf.Clamp01(position) * episode.Duration;
            if (firstValues != null) RefreshSamples();
        }

        public void SelectObservedEpisode(int index)
        {
            if (catalogue == null || index < 0 || index >= catalogue.Length || !catalogue[index]) return;
            previewPosition = 0; episode = catalogue[index]; LoadEpisode(); playing = true;
        }

        private void RefreshSamples()
        {
            episode.FindFrames(elapsed, out int a, out int b, out float blend);
            if (a != firstIndex || b != secondIndex)
            {
                // Reuse the previous upper snapshot when advancing. Work occurs at snapshot boundaries only.
                if (a == secondIndex)
                {
                    DestroyTextures(ref firstB, ref firstU);
                    firstValues = secondValues; firstB = secondB; firstU = secondU;
                    secondB = null; secondU = null;
                }
                else
                {
                    DestroyTextures(ref firstB, ref firstU);
                    firstValues = episode.ReadFrame(a);
                    CreateTextures(firstValues, out firstB, out firstU);
                }
                DestroyTextures(ref secondB, ref secondU);
                secondValues = episode.ReadFrame(b);
                CreateTextures(secondValues, out secondB, out secondU);
                firstIndex = a; secondIndex = b;
            }
            FrameBlend = blend;
            gsmToGeo = Quaternion.Slerp(episode.manifest.frames[a].gsmToGeo, episode.manifest.frames[b].gsmToGeo, blend);
            var axes = Matrix4x4.identity;
            // Scientific axes are right handed; Unity's gameplay plane is XZ with Y up.
            axes.SetColumn(1, new Vector4(0, 0, 1, 0)); axes.SetColumn(2, new Vector4(0, 1, 0, 0));
            gsmToWorld = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * worldUnitsPerEarthRadius) *
                         axes * Matrix4x4.Rotate(viewFrame == MagnetosphereViewFrame.EarthFixed ? gsmToGeo : Quaternion.identity);
            worldToGsm = gsmToWorld.inverse;
            previewPosition = lastPreview = (float)(elapsed / episode.Duration);
        }

        private void CreateTextures(float[] data, out Texture3D magnetic, out Texture3D velocity)
        {
            var m = episode.manifest;
            var b = new Color[m.nx * m.ny * m.nz];
            var u = new Color[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                int j = i * ScientificStormEpisode.Channels;
                b[i] = new Color(data[j], data[j + 1], data[j + 2], 1);
                u[i] = new Color(data[j + 3], data[j + 4], data[j + 5], 1);
            }
            magnetic = MakeTexture(b); velocity = MakeTexture(u);
        }

        private Texture3D MakeTexture(Color[] pixels)
        {
            var m = episode.manifest;
            var texture = new Texture3D(m.nx, m.ny, m.nz, TextureFormat.RGBAFloat, false)
                { name = "Dynamo MHD snapshot", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                  hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels(pixels); texture.Apply(false, true);
            return texture;
        }

        public bool TrySampleWorld(Vector3 world, out MagnetosphereSample sample)
        {
            sample = default;
            if (!Ready) return false;
            Vector3 p = worldToGsm.MultiplyPoint3x4(world);
            if (!MagnetosphereSampling.TrySample(episode.manifest, firstValues, p, out var a) ||
                !MagnetosphereSampling.TrySample(episode.manifest, secondValues, p, out var b)) return false;
            sample.magneticFieldNt = Vector3.Lerp(a.magneticFieldNt, b.magneticFieldNt, FrameBlend);
            sample.velocityKmS = Vector3.Lerp(a.velocityKmS, b.velocityKmS, FrameBlend);
            sample.densityProtonMassCm3 = Mathf.Lerp(a.densityProtonMassCm3, b.densityProtonMassCm3, FrameBlend);
            sample.pressureNpa = Mathf.Lerp(a.pressureNpa, b.pressureNpa, FrameBlend);
            return true;
        }

        public bool TryGetPlanarDrift(Vector3 world, float maximumSpeed, out Vector3 drift)
        {
            drift = Vector3.zero;
            if (!TrySampleWorld(world, out var sample)) return false;
            Vector3 p = worldToGsm.MultiplyPoint3x4(world);
            // Relative velocity in Earth-fixed coordinates includes the rotating-frame correction.
            Vector3 relative = sample.velocityKmS - Vector3.Cross(FrameOmegaGsm, p * 6371.2f);
            drift = gsmToWorld.MultiplyVector(relative) / worldUnitsPerEarthRadius;
            drift.y = 0;
            // Explicit gameplay conversion. Physical data is never rescaled or overwritten.
            drift = Vector3.ClampMagnitude(drift / 700f * maximumSpeed, maximumSpeed);
            return true;
        }

        private static void DestroyTextures(ref Texture3D b, ref Texture3D u)
        {
            if (b) { if (Application.isPlaying) Destroy(b); else DestroyImmediate(b); }
            if (u) { if (Application.isPlaying) Destroy(u); else DestroyImmediate(u); }
            b = null; u = null;
        }

        private void Release()
        {
            DestroyTextures(ref firstB, ref firstU); DestroyTextures(ref secondB, ref secondU);
            firstValues = null; secondValues = null; firstIndex = secondIndex = -1;
            loadedEpisode = null;
        }
    }
}
