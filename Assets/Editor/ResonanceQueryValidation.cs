using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Massive.Resonance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Differential oracle below is the pre-optimization calculation from commit 29ff79b.
// It intentionally remains independent of the optimized bounds/cache implementation.
public static class ResonanceQueryValidation
{
    [MenuItem("MASSIVE/Resonance/Validate Optimized Queries")]
    public static void RunMenu() { UnityEngine.Debug.Log(Run()); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static float Random(System.Random random, float low, float high) => low + (float)random.NextDouble() * (high-low);
    public static string Run()
    {
        var random = new System.Random(71883);
        int contacts = 0, sweeps = 0, limited = 0;
        var root = new GameObject("Query differential fixture");
        var segment = root.AddComponent<ResonanceSegment>();
        try
        {
            for (int fixture = 0; fixture < 64; fixture++)
            {
                int count = new[] { 2, 7, 32, 96, 180, 720 }[fixture % 6];
                var points = new Vector3[count + 1]; var path = new Vector4[count + 1];
                var arc = new ResonanceArc { thickness = Random(random,.05f,2), colliderThickness = Random(random,.01f,1.5f),
                    sweepDegrees = fixture % 3 == 0 ? 360 : 145, endTaperFraction = Random(random,0,.5f),
                    independentCollisionProfile = fixture % 2 == 0, collisionStart = Random(random,0,.35f), collisionEnd = Random(random,.65f,1),
                    collisionTaperFraction = Random(random,0,.5f), falloffPower = Random(random,.1f,3), collisionFalloffPower = Random(random,.1f,3),
                    falloffShape = (ResonanceTaperShape)(fixture % 3), collisionFalloffShape = (ResonanceTaperShape)((fixture+1) % 3) };
                float length = 0;
                for (int i = 0; i <= count; i++)
                {
                    float angle = i / (float)count * arc.sweepDegrees * Mathf.Deg2Rad;
                    points[i] = new Vector3(Mathf.Cos(angle) * (3 + Mathf.Sin(angle*4)),0,Mathf.Sin(angle)*2);
                    if (fixture % 7 == 0 && i > 0 && i % 4 == 0) points[i] = points[i-1]; // degenerate spans
                    if (i > 0) length += Vector3.Distance(points[i],points[i-1]);
                    path[i] = new Vector4(points[i].x,points[i].z,arc.Envelope(i/(float)count),length);
                }
                segment.Initialize(arc,points,~0,false);
                var effects = new ResonanceContactVisuals(path,path.Length,arc.sweepDegrees>=359.999f);
                for (int q = 0; q < 256; q++)
                {
                    // Change transforms and mutable contact profile after queries have warmed their caches.
                    if (q % 64 == 0)
                    {
                        root.transform.position = new Vector3(Random(random,-3,3),1,Random(random,-3,3));
                        root.transform.rotation = Quaternion.Euler(0,Random(random,0,360),0);
                        root.transform.localScale = new Vector3((fixture%2==0?-1:1)*Random(random,.2f,3),1,Random(random,.2f,3));
                        arc.collisionStart = Random(random,0,.35f); arc.collisionEnd = Random(random,.65f,1);
                        arc.thickness = Random(random,.05f,2); arc.colliderThickness = Random(random,.01f,1.5f);
                        arc.falloffPower = Random(random,.1f,3);
                    }
                    Vector3 point = root.transform.TransformPoint(new Vector3(Random(random,-7,7),Random(random,-.2f,.2f),Random(random,-5,5)));
                    bool a = ReferenceContact(arc,points,root.transform,1,point,out var an,out var ag,out var ae);
                    bool b = segment.NearestContact(point,out var bn,out var bg,out var be);
                    Check(a==b && (!a || (Mathf.Abs(ag-bg)<.00001f && (an-bn).sqrMagnitude<.000001f && Mathf.Abs(ae-be)<.00001f)),
                        "Contact differs at fixture "+fixture+" query "+q+" gap "+ag+"/"+bg);
                    contacts++;
                    float limit = q%4==0 && a ? ag : Random(random,0,3);
                    bool c = segment.NearestContact(point,out var cn,out var cg,out var ce,limit);
                    Check(c==(a && ag<=limit) && (!c || Mathf.Abs(cg-ag)<.00001f && (cn-an).sqrMagnitude<.000001f && Mathf.Abs(ce-ae)<.00001f),
                        "Limited contact differs at fixture "+fixture+" query "+q);
                    limited++;
                    Vector2 from = new Vector2(Random(random,-6,6),Random(random,-5,5));
                    Vector2 to = q%3==0 ? from : new Vector2(Random(random,-6,6),Random(random,-5,5));
                    var av = ReferenceVisual(path,path.Length,from,to); var bv = effects.Nearest(from,to);
                    Check(Mathf.Abs(av.distance-bv.distance)<.00001f && Mathf.Abs(av.along-bv.along)<.00001f
                        && Mathf.Abs(av.envelope-bv.envelope)<.00001f && (av.tangent-bv.tangent).sqrMagnitude<.000001f,
                        "Swept contact differs at fixture "+fixture+" query "+q);
                    float reach = Random(random,0,2);
                    Check(effects.MayIntersect(from,to,reach) || av.distance>reach, "Bounds rejected a real swept contact");
                    sweeps++;
                }
            }
            // Same inputs for old/new timings; no profiler instrumentation or gameplay changes.
            var p = new Vector3[181];
            for(int i=0;i<p.Length;i++) { float t=i*Mathf.PI/(p.Length-1);p[i]=new Vector3(Mathf.Cos(t)*5,0,Mathf.Sin(t)*3); }
            var settings = new ResonanceArc { thickness=.3f, colliderThickness=.3f, sweepDegrees=180, independentCollisionProfile=true };
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
            segment.Initialize(settings,p,~0,false);
            var queries = new Vector3[4096]; for(int i=0;i<queries.Length;i++) queries[i]=new Vector3(Random(random,-12,12),0,Random(random,-6,6));
            double before=0,after=0,capped=0; var watch=new Stopwatch();
            for(int repeat=0;repeat<4;repeat++)
            {
                watch.Restart();foreach(var q in queries)ReferenceContact(settings,p,root.transform,1,q,out _,out _,out _);
                watch.Stop();if(repeat>0)before+=watch.Elapsed.TotalMilliseconds;
                watch.Restart();foreach(var q in queries)segment.NearestContact(q,out _,out _,out _);
                watch.Stop();if(repeat>0)after+=watch.Elapsed.TotalMilliseconds;
                watch.Restart();foreach(var q in queries)segment.NearestContact(q,out _,out _,out _,1.2f);
                watch.Stop();if(repeat>0)capped+=watch.Elapsed.TotalMilliseconds;
            }
            return contacts+" exact contact comparisons, "+limited+" limited contact comparisons, "+sweeps+
                " swept visual comparisons passed. 4096-query mean ms: original="+(before/3).ToString("F3")+
                ", optimized="+(after/3).ToString("F3")+", influence-limited="+(capped/3).ToString("F3");
        }
        finally { Object.DestroyImmediate(root); }
    }
        private static bool ReferenceContact(ResonanceArc settings, Vector3[] points, Transform transform, float widthScale, Vector3 worldPoint, out Vector3 normal, out float gap, out float envelope)
        {
            normal = Vector3.right; gap = float.PositiveInfinity; envelope = 0f;
            if (settings == null || settings.behavior == ResonanceBehavior.VisualOnly || settings.colliderThickness <= 0f) return false;
            Vector3 p = transform.InverseTransformPoint(worldPoint); p.y = 0f;
            float from = settings.independentCollisionProfile ? settings.collisionStart : 0f;
            float to = settings.independentCollisionProfile ? settings.collisionEnd : 1f;
            float scale = Mathf.Min(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            for (int i = 0; i < points.Length - 1; i++)
            {
                float ta = i / (points.Length-1f), tb = (i+1f)/(points.Length-1f);
                if (tb <= from || ta >= to) continue;
                float low = Mathf.Clamp01((from-ta)/(tb-ta)), high = Mathf.Clamp01((to-ta)/(tb-ta));
                Vector3 a = Vector3.Lerp(points[i], points[i+1], low), b = Vector3.Lerp(points[i], points[i+1], high);
                Vector3 d = b-a;
                float u = Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.000001f,d.sqrMagnitude));
                float t = Mathf.Lerp(ta,tb,Mathf.Lerp(low,high,u));
                float e = settings.CollisionEnvelope(t);
                float width = Mathf.Min(settings.colliderThickness*e, settings.thickness*widthScale*settings.Envelope(t))*.5f;
                if (width <= .00001f) continue;
                Vector3 delta = p - (a+d*u);
                float distance = (delta.magnitude-width)*scale;
                if (distance >= gap) continue;
                gap = distance; envelope = e;
                normal = transform.TransformDirection(delta.sqrMagnitude > .000001f ? delta.normalized : Vector3.Cross(Vector3.up,d).normalized);
            }
            return !float.IsPositiveInfinity(gap);
        }

        private static ResonanceContactVisuals.Contact ReferenceVisual(Vector4[] path, int count, Vector2 from, Vector2 to)
        {
            ResonanceContactVisuals.Contact result = new ResonanceContactVisuals.Contact { distance = float.PositiveInfinity };
            Vector2 travel = to - from;
            for (int i = 0; i < count - 1; i++)
            {
                Vector2 a = new Vector2(path[i].x, path[i].y), b = new Vector2(path[i+1].x, path[i+1].y);
                Vector2 d = b-a;
                float aa = travel.sqrMagnitude, ee = d.sqrMagnitude;
                if (ee < .00000001f) continue;
                Vector2 r = from-a;
                float f = Vector2.Dot(d,r), s = 0f, t;
                if (aa < .00000001f) t = Mathf.Clamp01(f/ee);
                else
                {
                    float c = Vector2.Dot(travel,r), cross = Vector2.Dot(travel,d), denom = aa*ee-cross*cross;
                    s = denom > .00000001f ? Mathf.Clamp01((cross*f-c*ee)/denom) : 0f;
                    t = (cross*s+f)/ee;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c/aa); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((cross-c)/aa); }
                }
                float distance = Vector2.Distance(from+travel*s,a+d*t);
                if (distance >= result.distance) continue;
                result = new ResonanceContactVisuals.Contact { distance = distance, along = Mathf.Lerp(path[i].w,path[i+1].w,t),
                    envelope = Mathf.Lerp(path[i].z,path[i+1].z,t), tangent = d.normalized };
            }
            return result;
        }

}
