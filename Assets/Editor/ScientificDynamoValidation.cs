#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Massive.Dynamo;
using UnityEditor;
using UnityEngine;

public static class ScientificDynamoValidation
{
    [MenuItem("MASSIVE/Dynamo/Validate Scientific Data and Sampling")]
    public static string Run()
    {
        var checks = new List<string>();
        var m = new StormManifest {nx=2,ny=2,nz=2,originRe=new Vector3(4,4,4),spacingRe=Vector3.one};
        var values = new float[8 * ScientificStormEpisode.Channels];
        for (int z=0;z<2;z++) for(int y=0;y<2;y++) for(int x=0;x<2;x++)
        {
            int i=(z*4+y*2+x)*8;
            values[i]=4+x; values[i+1]=2*(4+y); values[i+2]=3*(4+z);
            values[i+3]=400; values[i+6]=5; values[i+7]=2;
        }
        Require(MagnetosphereSampling.TrySample(m,values,new Vector3(4.2f,4.4f,4.6f),out var s),"Interior sampling");
        Require(Vector3.Distance(s.magneticFieldNt,new Vector3(4.2f,8.8f,13.8f))<.0001f,"Affine field interpolation");
        Require(Mathf.Abs(s.velocityKmS.x-400)<.001f && Mathf.Abs(s.densityProtonMassCm3-5)<.001f,"Constant flow/density interpolation");
        Require(MagnetosphereSampling.TrySample(m,values,new Vector3(5,5,5),out s) && Mathf.Abs(s.magneticFieldNt.z-15)<.0001f,"Inclusive maximum boundary");
        Require(!MagnetosphereSampling.TrySample(m,values,new Vector3(3,4,4),out s),"Outside volume rejected");
        Require(!MagnetosphereSampling.TrySample(m,values,new Vector3(float.NaN,4,4),out s),"Non-finite position rejected");
        checks.Add("6 spatial sampling checks");
        var episode=AssetDatabase.LoadAssetAtPath<ScientificStormEpisode>(ScientificDynamoSetup.EpisodePath);
        Require(episode,"Imported episode"); episode.ValidateData();
        episode.FindFrames(-100,out int a,out int b,out float blend);
        Require(a==0 && b==1 && blend==0,"Start clamp");
        episode.FindFrames(episode.Duration+100,out a,out b,out blend);
        Require(b==episode.frames.Length-1 && Mathf.Abs(blend-1)<.0001f,"End clamp");
        double middle=(episode.manifest.frames[1].unixSeconds-episode.manifest.frames[0].unixSeconds)*.5;
        episode.FindFrames(middle,out a,out b,out blend);
        Require(a==0 && b==1 && Mathf.Abs(blend-.5f)<.0001f,"Scientific time interpolation");
        checks.Add("3 timeline checks");
        for(int i=0;i<episode.frames.Length;i++)
        {
            var data=episode.ReadFrame(i);
            Require(data.Length==episode.manifest.nx*episode.manifest.ny*episode.manifest.nz*8,"Frame dimensions");
            using(var sha=System.Security.Cryptography.SHA256.Create())
            {
                string hash=BitConverter.ToString(sha.ComputeHash(episode.frames[i].bytes)).Replace("-","").ToLowerInvariant();
                Require(hash==episode.manifest.frames[i].sha256,"Snapshot provenance hash");
            }
        }
        checks.Add(episode.frames.Length+" snapshots: dimensions, finite values and SHA-256 verified");
        string report=string.Join("; ",checks); Debug.Log("[Dynamo validation] PASS: "+report); return report;
    }

    public static string ValidateRuntime()
    {
        Require(Application.isPlaying,"Runtime validation requires Play Mode");
        var source=UnityEngine.Object.FindFirstObjectByType<ScientificMagnetosphere>();
        Require(source && source.Ready,"Scientific field loaded");
        Vector3 position=source.GsmToWorld.MultiplyPoint3x4(new Vector3(14,0,4));
        Require(source.TryGetPlanarDrift(position,6,out var drift),"Local flow sample");
        Require(drift.magnitude>.1f && drift.magnitude<=6.0001f && drift.y==0,"Planar drift direction and cap");
        Require(!source.TryGetPlanarDrift(source.transform.position,6,out drift),"Inner domain excluded");
        int visible=0; float maxZ=0,minZ=0;
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<ScientificFieldLineRenderer>(FindObjectsSortMode.None))
        {
            var field=typeof(ScientificFieldLineRenderer).GetField("segments",BindingFlags.Instance|BindingFlags.NonPublic);
            var buffer=(ComputeBuffer)field.GetValue(renderer); Require(buffer!=null,"GPU segment buffer");
            var points=new Vector4[buffer.count*2]; buffer.GetData(points);
            for(int i=0;i<points.Length;i+=2)
            {
                if(points[i+1].w==0) continue;
                Vector3 p=points[i],q=points[i+1];
                Require(ScientificStormEpisode.Finite(p.x) && ScientificStormEpisode.Finite(p.y) && ScientificStormEpisode.Finite(p.z),"Finite traced geometry");
                Require(Vector3.Distance(p,q)>.001f && Vector3.Distance(p,q)<1.01f,"Bounded integration step");
                visible++; minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);
            }
        }
        Require(visible>1000 && maxZ-minZ>5,"Volumetric field tracing");
        CheckRigidbodyResponse(source);
        return "PASS: "+visible+" valid GPU segments; volumetric geometry, finite bounded steps, physical-domain masking, local planar flow and Rigidbody acceleration verified.";
    }

    private static void CheckRigidbodyResponse(ScientificMagnetosphere source)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("Dynamo physics check " + Guid.NewGuid().ToString("N"),
            new UnityEngine.SceneManagement.CreateSceneParameters(UnityEngine.SceneManagement.LocalPhysicsMode.Physics3D));
        try
        {
            var go = new GameObject("Isolated drift body");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
            go.transform.position = source.GsmToWorld.MultiplyPoint3x4(new Vector3(14,0,4));
            var rb = go.AddComponent<Rigidbody>(); rb.useGravity=false;
            var push=go.AddComponent<PlayerStormPush>(); push.SetScientificField(source);
            Require(source.TryGetPlanarDrift(rb.position,6,out var target),"Physics fixture has local flow");
            var expected=Vector3.ClampMagnitude(target*6,30)*.02f;
            typeof(PlayerStormPush).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(push,null);
            scene.GetPhysicsScene().Simulate(.02f);
            Require(Vector3.Distance(expected,rb.linearVelocity)<.001f,"Rigidbody follows bounded local flow acceleration");
        }
        finally { UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene); }
    }

    private static void Require(bool condition,string label) { if(!condition)throw new InvalidDataException(label); }
}
#endif
