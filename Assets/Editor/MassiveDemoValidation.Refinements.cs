using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Massive.Demonstrations;
using UnityEditor;
using UnityEngine;

public static partial class MassiveDemoValidation
{
    private sealed class ActorSample
    {
        public Vector3 position;
        public float time;
        public int samples;
        public float largestStep;
    }
    private static readonly Dictionary<int, ActorSample> samples = new Dictionary<int, ActorSample>();
    private static readonly Dictionary<PlayerDemoDirector, int> identities = new Dictionary<PlayerDemoDirector, int>();
    private static readonly Dictionary<PlayerDemoDirector, Vector3> cameraPositions = new Dictionary<PlayerDemoDirector, Vector3>();
    private static int identityChanges, discontinuities, cameraChanges;
    private static bool observing;

    public static string ObserveContinuity()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Use Play Mode.");
        samples.Clear(); identities.Clear(); cameraPositions.Clear();
        identityChanges = discontinuities = cameraChanges = 0;
        observing = true;
        EditorApplication.update -= SampleContinuity;
        EditorApplication.update += SampleContinuity;
        return "Observing persistent actor identities, frame displacement and fixed cameras.";
    }
    private static void SampleContinuity()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= SampleContinuity;
            observing = false;
            return;
        }
        foreach (var d in UnityEngine.Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None))
        {
            if (!d.Primary || !d.PresentationCamera.enabled) continue;
            int id = d.Primary.GetInstanceID();
            if (identities.TryGetValue(d, out int previous) && previous != id) identityChanges++;
            identities[d] = id;
            Vector3 camera = d.PresentationCamera.transform.position;
            if (cameraPositions.TryGetValue(d,out Vector3 previousCamera) && Vector3.Distance(camera,previousCamera)>.001f) cameraChanges++;
            cameraPositions[d] = camera;
            foreach (var p in new[] { d.Primary,d.Partner })
            {
                if (!p) continue;
                int key = p.GetInstanceID();
                if (!samples.TryGetValue(key,out var sample)) samples[key] = sample = new ActorSample { position=p.transform.position,time=Time.time };
                float dt = Time.time - sample.time;
                if (dt<=0f) continue;
                float distance = Vector3.Distance(p.transform.position,sample.position);
                sample.largestStep = Mathf.Max(sample.largestStep,distance);
                if (!p.temporarilyEliminated && distance>Mathf.Max(1f,30f*dt)) discontinuities++;
                sample.position=p.transform.position; sample.time=Time.time; sample.samples++;
            }
        }
    }
    public static string RefinementStatus()
    {
        var sb = new StringBuilder(Status());
        sb.AppendLine("Continuity observing="+observing+" actor replacements="+identityChanges+" abrupt steps="+discontinuities+" camera moves="+cameraChanges);
        sb.AppendLine("samples="+samples.Values.Sum(s=>s.samples)+" largest frame step="+(samples.Count>0?samples.Values.Max(s=>s.largestStep):0));
        foreach(var d in UnityEngine.Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None))
        {
            sb.AppendLine(d.name+" kills="+d.ConfirmedKills+" respawns="+d.ConfirmedRespawns+" grid sources="+(d.Grid?d.Grid.ResponsiveAttractorCount:0));
            if(d.Partner) sb.AppendLine("  target mass="+d.Partner.massScore+" eliminated="+d.Partner.temporarilyEliminated+" position="+d.Partner.transform.position);
            if(d.Primary && d.Primary.GetComponentsInChildren<GridInteractor>(true).Any(g=>g.grid!=d.Grid)) throw new Exception("Wrong grid binding: "+d.name);
        }
        int captions = UnityEngine.Object.FindObjectsByType<Massive.PowerUps.PowerUpPickupToast>(FindObjectsSortMode.None).Count(t=>t.name.EndsWith("permanent title"));
        sb.AppendLine("Permanent captions="+captions);
        if(identityChanges>0||discontinuities>0||cameraChanges>0) throw new Exception(sb.ToString());
        return sb.ToString();
    }
    public static string GridChecks() => GridResponsiveAttractionValidation.Run() + "\n" + GridResponsiveAttractionValidation.RunGpu() + "\n" + GridCurveSamplingValidation.Run();
}
