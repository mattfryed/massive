#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.EditorTools;
using Massive.PowerUps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PowerUpSettingsValidation
{
    const BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
    static object Get(object o,string name)=>o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static readonly List<string> editChecks = new();
    public static IReadOnlyList<string> EditChecks => editChecks;
    static void Check(bool ok,string label)
    { if(!ok) throw new Exception(label); editChecks.Add("PASS "+label); }

    public static void RunEdit()
    {
        editChecks.Clear();
        var saved=PowerUpSettingsEditing.GetOrCreate();
        Check(PowerUpSettingsEditing.Validate(saved).Count==0,"Global profile and all canonical prefab links are valid");
        Check(Resources.Load<PowerUpSettings>("PowerUpSettings")==saved,"Global profile is included through Resources");
        foreach (var def in saved.definitions)
            Check(ColliderFitMatchesShell(def.pickupPrefab), def.displayName+": authored trigger and solid collider fit the shell");
        var clone=Object.Instantiate(saved);
        clone.definitions=saved.definitions.Select(Object.Instantiate).ToList();
        Scene original=SceneManager.GetActiveScene(), fixture=default;
        try
        {
            PowerUpSettingsEditing.SetPercentage(clone,clone.definitions[0],70);
            Check(Mathf.Abs(clone.definitions.Sum(d=>d.spawnWeight)-100)<.001f && Mathf.Abs(clone.definitions[0].spawnWeight-70)<.001f,
                "Percentage mixer redistributes to exactly 100 percent");
            PowerUpSettingsEditing.SetPercentage(clone,clone.definitions[0],0);
            Check(Enumerable.Range(0,1000).All(i=>clone.Select(i/1000f)!=clone.definitions[0]),"Zero-percent power-up is never selected");
            foreach(var d in clone.definitions) d.spawnWeight=0;
            Check(clone.Select(.5f)==null,"All-zero mixer spawns nothing");
            clone.definitions[2].spawnWeight=100;
            Check(Enumerable.Range(0,1000).All(i=>clone.Select(i/1000f)==clone.definitions[2]),"100-percent mixer selects only the requested power-up");
            clone.spawning.minDelay=7; clone.spawning.maxDelay=1; clone.toasts.totalSeconds=.01f;
            clone.NotifyChanged();
            Check(Mathf.Approximately(clone.spawning.maxDelay,7) && clone.toasts.totalSeconds+.00001f>=clone.toasts.introSeconds+clone.toasts.outroSeconds,
                $"Delay bounds and toast phase durations are clamped consistently (delay={clone.spawning.maxDelay}, toast={clone.toasts.totalSeconds})");
            Undo.IncrementCurrentGroup();
            using(var data=new SerializedObject(clone))
            {
                data.FindProperty("visuals.shellScale").floatValue=1.7f; data.ApplyModifiedProperties();
            }
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Check(Mathf.Approximately(clone.visuals.shellScale,saved.visuals.shellScale),"Global settings edit supports Undo");
            Undo.PerformRedo();
            Check(Mathf.Approximately(clone.visuals.shellScale,1.7f),"Global settings edit supports Redo");

            fixture=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var gridGO=new GameObject("Validation grid"); gridGO.SetActive(false);
            SceneManager.MoveGameObjectToScene(gridGO,fixture);
            var grid=gridGO.AddComponent<VectorGridGPU>();
            var camGO=new GameObject("Validation camera"); camGO.SetActive(false);
            SceneManager.MoveGameObjectToScene(camGO,fixture); var camera=camGO.AddComponent<Camera>();
            var spawner=PowerUpSettingsEditing.EnsureScene(fixture,grid,camera);
            Check(spawner && spawner.vectorGrid==grid && PowerUpSettingsEditing.InScene<PowerUpPickupToastSystem>(fixture).Length==1,
                "Install creates a missing spawner and toast system with grid binding");
            var toast=PowerUpSettingsEditing.InScene<PowerUpPickupToastSystem>(fixture)[0];
            Check((Camera)Get(toast,"cameraOverride")==camera && (PowerUpPickupToast)Get(toast,"toastPrefab")==saved.toasts.prefab,
                "Install hooks up the scene camera and shared toast prefab");
            spawner.enabled=false;
            var again=PowerUpSettingsEditing.EnsureScene(fixture,grid,camera);
            Check(again==spawner && !again.enabled && PowerUpSettingsEditing.InScene<PowerUpSpawner>(fixture).Length==1 &&
                PowerUpSettingsEditing.InScene<PowerUpPickupToastSystem>(fixture).Length==1,
                "Repeated installation is idempotent and preserves intentional disabled states");
            spawner.vectorGrid=null;
            PowerUpSettingsEditing.EnsureScene(fixture,grid,camera);
            Check(spawner.vectorGrid==grid,"Repair restores a missing scene grid reference");
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            if(fixture.IsValid()) EditorSceneManager.CloseScene(fixture,true);
            foreach(var d in clone.definitions) Object.DestroyImmediate(d);
            Object.DestroyImmediate(clone);
        }
        Directory.CreateDirectory(PowerUpIconDemoSetup.Output);
        File.WriteAllLines(PowerUpIconDemoSetup.Output+"/settings-edit.txt",editChecks);
    }

    public static IEnumerator RunPlay(Action<bool,string> check)
    {
        var original=PowerUpSettings.Current;
        var profile=Object.Instantiate(original);
        profile.definitions=original.definitions.Select(Object.Instantiate).ToList();
        var current=typeof(PowerUpSettings).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);
        current.SetValue(null,profile);
        var scenes=new List<Scene>();
        var roots=new List<GameObject>();
        try
        {
            profile.lifecycle.worldLifetimeSeconds=3;
            profile.spawning.minDelay=profile.spawning.maxDelay=100;
            profile.spawning.maxConcurrent=1; profile.spawning.noSpawnMask=0;
            profile.spawning.minPlayerDistance=0; profile.spawning.minPickupDistance=0;
            foreach(var def in profile.definitions) def.spawnWeight=0;
            profile.definitions[0].spawnWeight=100;
            profile.visuals.shellScale=1.4f; profile.visuals.innerScale=.75f; profile.visuals.shellSeconds=1.3f;
            profile.NotifyChanged();
            var spawners=new List<PowerUpSpawner>(); var pickups=new List<PowerUpPickup>();
            for(int i=0;i<2;i++)
            {
                var scene=SceneManager.CreateScene("Power-up settings fixture "+i); scenes.Add(scene);
                var root=new GameObject("Shared settings runtime fixture"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root,scene); roots.Add(root);
                root.transform.position=new Vector3(2000+i*100,0,0);
                var gridGO=new GameObject("Playfield"); gridGO.transform.SetParent(root.transform,false);
                gridGO.transform.localRotation=Quaternion.Euler(90,0,0);
                var grid=gridGO.AddComponent<VectorGridGPU>(); grid.enabled=false; grid.size=new Vector2(20,12);
                var spawner=root.AddComponent<PowerUpSpawner>(); spawner.vectorGrid=grid;
                spawner.minSpawnDelay=999; spawner.maxActivePickups=999;
                var toast=root.AddComponent<PowerUpPickupToastSystem>(); Set(toast,"prewarmOnStart",false);
                root.SetActive(true); spawners.Add(spawner);
                var pickup=spawner.TrySpawnOne(); pickups.Add(pickup);
                check(pickup && pickup.definition==profile.definitions[0] && pickup.gameObject.scene==scene,
                    "Shared catalogue and mixer override scene-local data in scene "+i);
                check(spawner.ActivePickupCount==1 && spawner.TrySpawnOne()==null,"Global concurrency limit enforced in scene "+i);
                check(Mathf.Abs((float)Get(pickup,"_deathTime")-Time.time-3)<.05f,"Global lifetime applied in scene "+i);
                var animator=pickup.GetComponent<PowerUpIconManifestAnimator>();
                var wire=pickup.GetComponentInChildren<ParametricPolyhedronWire>();
                float radius=pickup.definition.pickupPrefab.GetComponentInChildren<ParametricPolyhedronWire>().radius;
                check(Mathf.Abs(wire.radius-radius*1.4f)<.0001f && Mathf.Approximately((float)Get(animator,"wireInDuration"),1.3f),
                    "Global shell scale and animation duration applied in scene "+i);
                var icon=pickup.GetComponentInChildren<MetaballManifest>();
                check(Mathf.Approximately(icon.transform.localScale.x,.75f),"Global inner-icon scale applied in scene "+i);
                check(PowerUpPickupToastSystem.ForScene(scene)==toast,"Toast lookup stays local to scene "+i);
            }
            profile.visuals.shellScale=.8f; profile.visuals.innerScale=1.2f; profile.NotifyChanged();
            yield return null; yield return null;
            for(int i=0;i<2;i++)
            {
                var wire=pickups[i].GetComponentInChildren<ParametricPolyhedronWire>();
                float radius=pickups[i].definition.pickupPrefab.GetComponentInChildren<ParametricPolyhedronWire>().radius;
                check(Mathf.Abs(wire.radius-radius*.8f)<.0001f &&
                    Mathf.Approximately(pickups[i].GetComponentInChildren<MetaballManifest>().transform.localScale.x,1.2f),
                    "Live shared visual edits update scene "+i+" without multiplying previous scale");
            }
            profile.toasts.introSeconds=0; profile.toasts.totalSeconds=.2f; profile.toasts.outroSeconds=.05f;
            profile.toasts.showDescription=false; profile.toasts.worldScale=.02f; profile.toasts.screenUpOffset=2;
            var toastSystem=PowerUpPickupToastSystem.ForScene(scenes[1]);
            var shown=toastSystem.Show(profile.definitions[0],roots[1].transform.position,null);
            check(shown && shown.gameObject.scene==scenes[1],"Ordinary toast stays in its system's scene when another scene is active");
            check(shown && Mathf.Approximately((float)Get(shown,"worldScale"),.02f) &&
                shown.GetComponentInChildren<TMPro.TMP_Text>().text==profile.definitions[0].displayName,
                "Shared toast scale and description toggle reach the actual spawned toast");
            profile.toasts.enabled=false;
            check(!toastSystem.Show(profile.definitions[0],roots[1].transform.position,roots[1].transform),"Global toast disable is honored");
            float wait=Time.unscaledTime+.45f;
            while(Time.unscaledTime<wait) yield return null;
            check(!shown,"Shared toast timing destroys the toast after its outro");
            profile.spawning.enabled=false;
            check(!spawners[0].TrySpawnOne(),"Global spawning switch stops manual and scheduled attempts");
            profile.spawning.enabled=true; profile.spawning.maxConcurrent=2;
            var duplicateGO=new GameObject("Duplicate spawner fixture"); duplicateGO.transform.SetParent(roots[0].transform,false);
            var duplicate=duplicateGO.AddComponent<PowerUpSpawner>(); duplicate.vectorGrid=spawners[0].vectorGrid;
            check(spawners[0].IsPrimary!=duplicate.IsPrimary,"Duplicate spawners elect one scheduler per scene");
            var primary=spawners[0].IsPrimary ? spawners[0] : duplicate;
            var secondary=spawners[0].IsPrimary ? duplicate : spawners[0];
            check(!secondary.TrySpawnOne(),"Secondary spawner cannot bypass scheduling or limits");
            profile.lifecycle.worldLifetimeSeconds=.1f;
            profile.lifecycle.cleanupDelaySeconds=.1f;
            profile.visuals.shellSeconds=.1f; profile.visuals.innerSpawnSeconds=.05f; profile.visuals.innerStagger=0; profile.NotifyChanged();
            var timed=primary.TrySpawnOne();
            check(timed,"Standard spawner resumes with updated global settings");
            check(Mathf.Approximately((float)Get(timed.GetComponent<PowerUpIconManifestAnimator>(),"destroyDelay"),.1f),
                "Shared cleanup delay overrides the prefab delay");
            wait=Time.unscaledTime+1;
            while(timed && Time.unscaledTime<wait) yield return null;
            check(!timed,"New pickup honors changed lifetime and completes reverse outro");
            profile.lifecycle.effectDurationMultiplier=1.5f;
            check(Mathf.Approximately(profile.definitions[0].EffectDurationSeconds,profile.definitions[0].effectDurationSeconds*1.5f),
                "Global effect duration multiplier reaches shared definitions");
            var colliderChecks=RunColliderChecks(profile,roots[0].transform,check);
            while(colliderChecks.MoveNext()) yield return colliderChecks.Current;
        }
        finally
        {
            current.SetValue(null,original);
            foreach(var root in roots) if(root) Object.Destroy(root);
            foreach(var scene in scenes) if(scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
            foreach(var d in profile.definitions) Object.Destroy(d);
            Object.Destroy(profile);
        }
        yield return null; yield return null;
        check(PowerUpSettings.Current==original,"Validation restores the real shared profile without saving test values");
    }

    static bool ColliderFitMatchesShell(GameObject pickup)
    {
        var wire=pickup.GetComponentInChildren<ParametricPolyhedronWire>(true);
        var colliders=pickup.GetComponentsInChildren<Collider>(true);
        if(!wire || !colliders.Any(c=>c.isTrigger) || !colliders.Any(c=>!c.isTrigger)) return false;
        // Compare to the renderer's complete geometry, not the sizing implementation.
        float draw=wire.drawProgress;
        wire.drawProgress=1;
        var segments=new List<ParametricPolyhedronWire.WireSegment>();
        wire.GetPickupSegments(segments); wire.drawProgress=draw;
        if(segments.Count!=30) return false;
        var center=wire.transform.position;
        float radius=segments.SelectMany(s=>new[]{s.a,s.b})
            .Max(v=>Vector3.Distance(center,wire.transform.TransformPoint(v)));
        foreach(var collider in colliders)
        {
            if(collider is not SphereCollider sphere) return false;
            if(Vector3.Distance(sphere.transform.TransformPoint(sphere.center),center)>.001f) return false;
            var scale=sphere.transform.lossyScale;
            float colliderRadius=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            if(Mathf.Abs(colliderRadius-radius)>.001f) return false;
            if(Application.isPlaying && sphere.enabled && sphere.gameObject.activeInHierarchy)
            {
                // Both the physics contact and trigger volumes must end at the visible shell radius.
                var surface=sphere.ClosestPoint(center+Vector3.right*radius*2);
                if(Mathf.Abs(Vector3.Distance(surface,center)-radius)>.001f) return false;
            }
        }
        return true;
    }

    static IEnumerator RunColliderChecks(PowerUpSettings profile, Transform owner, Action<bool,string> check)
    {
        profile.lifecycle.worldLifetimeSeconds=0;
        profile.visuals.shellScale=1; profile.NotifyChanged();
        var pickups=new List<PowerUpPickup>();
        try
        {
            for(int i=0;i<profile.definitions.Count;i++)
            {
                var pickup=PowerUpPickup.Spawn(profile.definitions[i],owner.position+new Vector3(i*5,0,30),Quaternion.identity,owner);
                pickup.RestrictClaimsTo(null); pickups.Add(pickup);
            }
            foreach(float scale in new[]{.25f,2f,.75f,1f})
            {
                profile.visuals.shellScale=scale; profile.NotifyChanged();
                yield return null; yield return null;
                Physics.SyncTransforms();
                foreach(var pickup in pickups)
                    check(ColliderFitMatchesShell(pickup.gameObject),pickup.definition.displayName+
                        ": live trigger and solid physics volume match shell at scale "+scale);
            }
            profile.visuals.innerScale=1.8f; profile.NotifyChanged();
            yield return null; yield return null;
            Physics.SyncTransforms();
            foreach(var pickup in pickups)
            {
                check(ColliderFitMatchesShell(pickup.gameObject),pickup.definition.displayName+": inner scale does not change shell collider fit");
                pickup.gameObject.SetActive(false);
            }
            profile.visuals.shellScale=.6f; profile.NotifyChanged();
            foreach(var pickup in pickups) pickup.gameObject.SetActive(true);
            Physics.SyncTransforms();
            foreach(var pickup in pickups)
            {
                check(ColliderFitMatchesShell(pickup.gameObject),pickup.definition.displayName+": re-enabled pickup uses current shell size");
                var animator=pickup.GetComponent<PowerUpIconManifestAnimator>();
                animator.BeginAttackDespawn();
                profile.visuals.shellScale=1.2f; profile.NotifyChanged(); animator.RefreshSharedSettings();
                check(pickup.GetComponentsInChildren<Collider>().All(c=>!c.enabled),pickup.definition.displayName+
                    ": resizing an acquired shell keeps its colliders disabled");
            }
        }
        finally { foreach(var pickup in pickups) if(pickup) Object.Destroy(pickup.gameObject); }
    }
}
#endif
