#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.PowerUps;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercises canonical prefabs and the actual renderer geometry, alongside the live melee demo checks.</summary>
public static class PowerUpIconAnimationValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static void Sample(PowerUpIconManifestAnimator animator, float t) => Call(animator, "SampleSpawn", t);
    static List<ParametricPolyhedronWire.WireSegment> Segments(ParametricPolyhedronWire wire)
    { var list = new List<ParametricPolyhedronWire.WireSegment>(); wire.GetPickupSegments(list); return list; }
    static float[] Inner(PowerUpPickup pickup) => pickup.GetComponentsInChildren<MetaballManifest>(true)
        .SelectMany(m => Enumerable.Range(0, (int)Get(m, "_lastFrameCount")).Select(i => (float)Call(m, "Evaluate", i)))
        .Concat(pickup.GetComponentsInChildren<PowerUpIconParticleSizeEase>(true).Select(p => (float)Get(p, "_size01"))).ToArray();
    static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .00000001f;
    static bool Same(float[] a, float[] b) => a.Length == b.Length && a.Zip(b, (x, y) => Mathf.Abs(x-y) < .00001f).All(x => x);
    static bool Same(List<ParametricPolyhedronWire.WireSegment> a, List<ParametricPolyhedronWire.WireSegment> b) =>
        a.Count == b.Count && a.Zip(b, (x,y) => Near(x.a,y.a) && Near(x.b,y.b)).All(x => x);

    public static bool IsImmediateClaim(PowerUpPickup pickup)
    {
        var animator = pickup.GetComponent<PowerUpIconManifestAnimator>();
        return animator.IsDespawning && (float)Get(animator, "_acquireTime") == 0f &&
            pickup.GetComponentInChildren<ParametricPolyhedronWire>().IsAcquiring &&
            pickup.GetComponentsInChildren<MetaballManifest>().All(m => Get(m, "_state").ToString() == "Acquire");
    }

    public static IEnumerator Run(IEnumerable<PowerUpDefinition> definitions, Action<bool,string> check)
    {
        int index = 0;
        foreach (var def in definitions)
        {
            var pickup = PowerUpPickup.Spawn(def, new Vector3(1000 + 6 * index++, 0, 0), Quaternion.identity);
            try
            {
                pickup.RestrictClaimsTo(null); pickup.enabled = false;
                var animator = pickup.GetComponent<PowerUpIconManifestAnimator>();
                var wire = pickup.GetComponentInChildren<ParametricPolyhedronWire>();
                Set(animator, "destroyOnComplete", false);
                yield return null; yield return null;
                animator.enabled = false;
                float shell = (float)Get(animator, "wireInDuration");
                float total = (float)Call(animator, "SpawnDuration");
                string label = def.displayName + ": ";
                check(wire.UsePickupAnimation, label + "canonical prefab uses vertex/face rendering");
                Sample(animator, total);
                var full = Segments(wire);
                var vertices = full.SelectMany(s => new[] { s.a, s.b }).Distinct().ToArray();
                check(full.Count == 30 && vertices.Length == 12, label + "complete icosahedron has 30 edges and 12 vertices");
                Sample(animator, 0f);
                check(Segments(wire).Count == 0 && Inner(pickup).All(x => x == 0f), label + "spawn begins completely invisible");
                Sample(animator, shell * .3f);
                var partial = Segments(wire);
                check(wire.foldProgress == 1 && partial.Count > 20 && partial.All(s => vertices.Any(v => Near(v,s.a) || Near(v,s.b))),
                    label + "partial edges remain anchored to original full-3D vertices");
                check(partial.Select(s => Mathf.Round(Vector3.Distance(s.a,s.b)*10000)).Distinct().Count() > 5,
                    label + "edge timing and draw speeds vary");
                Sample(animator, shell * .5f);
                check(Inner(pickup).All(x => x == 0f), label + "inner icon hidden through shell midpoint");
                Sample(animator, shell * .65f);
                check(Inner(pickup).Any(x => x > .01f), label + "inner icon starts during second half of shell draw");
                yield return null;
                Capture(pickup, def.Type + "-spawn.png");

                Sample(animator, total);
                wire.IsAcquiring = true; wire.shatterProgress = .55f;
                var faces = Segments(wire).GroupBy(s => s.face).Select(g => g.ToArray()).ToArray();
                check(faces.Length == 20 && faces.All(f => f.Length == 3 &&
                    Near(f[0].b,f[1].a) && Near(f[1].b,f[2].a) && Near(f[2].b,f[0].a)),
                    label + "20 connected triangular faces separate");
                float edgeLength = Vector3.Distance(full[0].a,full[0].b);
                check(faces.All(f => f.All(s => Mathf.Abs(Vector3.Distance(s.a,s.b)-edgeLength)<.00001f)),
                    label + "separated faces retain rigid edge lengths");
                wire.collapseProgress = .45f;
                var erased = Segments(wire);
                check(erased.Count > 0 && erased.Count < 60 && erased.Any(s => Vector3.Distance(s.a,s.b) < edgeLength*.99f),
                    label + "faces undraw along their perimeter");
                check(erased.GroupBy(s=>s.face).Select(g=>Mathf.Round(g.Sum(s=>Vector3.Distance(s.a,s.b))*1000)).Distinct().Count()>5,
                    label + "face undraw timing and speed vary");
                Capture(pickup, def.Type + "-faces.png");
                wire.collapseProgress = 1;
                check(Segments(wire).Count == 0, label + "undraw removes every fragment");

                // Observe the real reverse Update path, then compare its geometry/envelope
                // with spawn evaluated at the same timestamp.
                animator.enabled = true;
                Sample(animator, total);
                animator.BeginDespawn();
                float previous = total;
                bool reverse = true, decreasing = true;
                while (pickup.gameObject.activeSelf)
                {
                    yield return null;
                    float t = (float)Get(animator, "_sequenceTime");
                    decreasing &= t <= previous;
                    var actual = Segments(wire); var inner = Inner(pickup);
                    Sample(animator, t);
                    reverse &= Same(actual, Segments(wire)) && Same(inner, Inner(pickup));
                    previous = t;
                }
                check(reverse && decreasing && previous == 0f, label + "natural despawn exactly reverses shell and inner spawn envelopes");

                // Hit while the intro is still incomplete: neither shell nor icon may pop.
                pickup.gameObject.SetActive(true); animator.enabled = false;
                Sample(animator, shell * .65f);
                float drawBefore = wire.drawProgress;
                var innerBefore = Inner(pickup);
                animator.BeginAttackDespawn();
                check(wire.drawProgress == drawBefore && Same(innerBefore, Inner(pickup)) && IsImmediateClaim(pickup),
                    label + "early acquire starts immediately without completing/flashing the intro");
                Call(animator, "Update");
                var innerAfter = Inner(pickup);
                check(innerAfter.Zip(innerBefore, (a,b)=>a<=b+.00001f).All(x=>x) &&
                    innerAfter.Zip(innerBefore, (a,b)=>a<b-.000001f).Any(x=>x),
                    label + "every visible inner element shrinks on the first acquisition frame");
                check(pickup.GetComponentsInChildren<Collider>().All(c=>!c.enabled), label + "acquire disables pickup colliders immediately");

                // Re-enable/pool, then observe a complete real acquisition.
                pickup.gameObject.SetActive(false); animator.enabled = true; pickup.gameObject.SetActive(true);
                Sample(animator, total);
                animator.BeginAttackDespawn();
                bool captured = false;
                while (pickup.gameObject.activeSelf)
                {
                    yield return null;
                    if (!captured && (float)Get(animator, "_acquireTime") > .12f)
                    { Capture(pickup, def.Type + "-acquire.png"); captured = true; }
                }
                check(Segments(wire).Count==0 && Inner(pickup).All(x=>x==0f), label + "acquire finishes fully hidden");
            }
            finally { if (pickup) Object.Destroy(pickup.gameObject); }
            yield return null;
        }
    }

    static void Capture(PowerUpPickup pickup, string filename)
    {
        var camera = Camera.main; if (!camera) return;
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        var previous = camera.targetTexture; var active = RenderTexture.active; float size = camera.orthographicSize;
        var target = new RenderTexture(768,768,24); var pixels = new Texture2D(768,768,TextureFormat.RGB24,false);
        try
        {
            camera.transform.SetPositionAndRotation(pickup.transform.position + Vector3.up*20, Quaternion.Euler(90,0,0));
            camera.orthographicSize = 1.55f; camera.targetTexture = target; camera.Render(); RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,768,768),0,0); pixels.Apply();
            File.WriteAllBytes(PowerUpIconDemoSetup.Output + "/" + filename,pixels.EncodeToPNG());
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation); camera.orthographicSize=size;
            camera.targetTexture=previous; RenderTexture.active=active;
            Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target);
        }
    }
}
#endif
