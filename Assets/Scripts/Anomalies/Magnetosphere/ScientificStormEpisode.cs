using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Massive.Dynamo
{
    /// <summary>Prepared MHD samples. All stored vectors use GSM axes and physical units.</summary>
    [CreateAssetMenu(menuName = "MASSIVE/Dynamo/Scientific storm episode")]
    public sealed class ScientificStormEpisode : ScriptableObject
    {
        public const int Channels = 8; // B[nT], velocity[km/s], density[proton masses/cm3], pressure[nPa]
        public StormManifest manifest;
        public TextAsset[] frames;

        public double Duration => manifest.frames[manifest.frames.Length - 1].unixSeconds - manifest.frames[0].unixSeconds;

        public void ValidateData()
        {
            var m = manifest;
            if (m == null || m.schemaVersion != 1 || m.coordinateSystem != "GSM")
                throw new InvalidDataException("Expected Dynamo episode schema 1 in GSM coordinates.");
            if (m.nx < 2 || m.ny < 2 || m.nz < 2 || m.nx > 256 || m.ny > 256 || m.nz > 256 ||
                !Finite(m.spacingRe.x) || !Finite(m.spacingRe.y) || !Finite(m.spacingRe.z) ||
                m.spacingRe.x <= 0 || m.spacingRe.y <= 0 || m.spacingRe.z <= 0 || m.innerBoundaryRe < 1)
                throw new InvalidDataException("Invalid MHD sampling domain.");
            if (m.frames == null || m.frames.Length < 2 || frames == null || frames.Length != m.frames.Length)
                throw new InvalidDataException("An episode needs at least two matching field snapshots.");
            for (int i = 0; i < frames.Length; i++)
            {
                var f = m.frames[i];
                if (!frames[i] || double.IsNaN(f.unixSeconds) || double.IsInfinity(f.unixSeconds) ||
                    (i > 0 && f.unixSeconds <= m.frames[i - 1].unixSeconds))
                    throw new InvalidDataException("Missing snapshot or non-increasing event timestamp.");
                float q = f.gsmToGeo.x * f.gsmToGeo.x + f.gsmToGeo.y * f.gsmToGeo.y +
                          f.gsmToGeo.z * f.gsmToGeo.z + f.gsmToGeo.w * f.gsmToGeo.w;
                if (!Finite(q) || Mathf.Abs(q - 1) > .001f)
                    throw new InvalidDataException("Invalid GSM to GEO rotation.");
            }
        }

        public void FindFrames(double elapsed, out int first, out int second, out float blend)
        {
            double t = manifest.frames[0].unixSeconds + Math.Max(0, Math.Min(Duration, elapsed));
            int lo = 0, hi = manifest.frames.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (manifest.frames[mid].unixSeconds <= t) lo = mid; else hi = mid;
            }
            first = lo; second = hi;
            blend = (float)((t - manifest.frames[lo].unixSeconds) /
                            (manifest.frames[hi].unixSeconds - manifest.frames[lo].unixSeconds));
        }

        public float[] ReadFrame(int index)
        {
            int count = checked(manifest.nx * manifest.ny * manifest.nz * Channels);
            var bytes = new byte[checked(count * sizeof(float))];
            using (var input = new MemoryStream(frames[index].bytes, false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                int n = 0;
                while (n < bytes.Length)
                {
                    int read = gzip.Read(bytes, n, bytes.Length - n);
                    if (read == 0) throw new InvalidDataException("Truncated MHD snapshot.");
                    n += read;
                }
                if (gzip.ReadByte() != -1) throw new InvalidDataException("Unexpected MHD snapshot length.");
            }
            if (!BitConverter.IsLittleEndian)
                for (int i = 0; i < bytes.Length; i += 4) Array.Reverse(bytes, i, 4);
            var values = new float[count];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            foreach (float v in values)
                if (!Finite(v)) throw new InvalidDataException("Non-finite MHD sample.");
            return values;
        }

        public static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }

    [Serializable]
    public sealed class StormManifest
    {
        public int schemaVersion = 1;
        public string title, model, runId, sourceUrl, attribution, limitations;
        public string coordinateSystem = "GSM";
        public int nx, ny, nz;
        public Vector3 originRe, spacingRe;
        public float innerBoundaryRe = 2.5f;
        public StormFrameMetadata[] frames;
    }

    [Serializable]
    public sealed class StormFrameMetadata
    {
        public string utc, file, sha256, sourceFile;
        public double unixSeconds;
        public Quaternion gsmToGeo;
        public Vector3 dipoleAxisGsm, upstreamVelocityKmS, upstreamBNt;
        public float upstreamDensity, upstreamTemperatureK, upstreamDynamicPressureNpa;
    }

    public struct MagnetosphereSample
    {
        public Vector3 magneticFieldNt, velocityKmS;
        public float densityProtonMassCm3, pressureNpa;
    }

    public static class MagnetosphereSampling
    {
        // x is the fastest-varying dimension, matching Texture3D.SetPixels and the exporter.
        public static bool TrySample(StormManifest m, float[] data, Vector3 p, out MagnetosphereSample sample)
        {
            sample = default;
            Vector3 g = new Vector3((p.x - m.originRe.x) / m.spacingRe.x,
                (p.y - m.originRe.y) / m.spacingRe.y, (p.z - m.originRe.z) / m.spacingRe.z);
            if (!ScientificStormEpisode.Finite(g.x) || !ScientificStormEpisode.Finite(g.y) ||
                !ScientificStormEpisode.Finite(g.z) || p.sqrMagnitude < m.innerBoundaryRe * m.innerBoundaryRe ||
                g.x < 0 || g.y < 0 || g.z < 0 || g.x > m.nx - 1 || g.y > m.ny - 1 || g.z > m.nz - 1)
                return false;
            int x = Mathf.Min(Mathf.FloorToInt(g.x), m.nx - 2);
            int y = Mathf.Min(Mathf.FloorToInt(g.y), m.ny - 2);
            int z = Mathf.Min(Mathf.FloorToInt(g.z), m.nz - 2);
            Vector3 f = g - new Vector3(x, y, z);
            for (int dz = 0; dz < 2; dz++) for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
            {
                float w = (dx == 0 ? 1 - f.x : f.x) * (dy == 0 ? 1 - f.y : f.y) * (dz == 0 ? 1 - f.z : f.z);
                int i = ((z + dz) * m.ny * m.nx + (y + dy) * m.nx + x + dx) * ScientificStormEpisode.Channels;
                sample.magneticFieldNt += new Vector3(data[i], data[i + 1], data[i + 2]) * w;
                sample.velocityKmS += new Vector3(data[i + 3], data[i + 4], data[i + 5]) * w;
                sample.densityProtonMassCm3 += data[i + 6] * w;
                sample.pressureNpa += data[i + 7] * w;
            }
            return true;
        }
    }
}
