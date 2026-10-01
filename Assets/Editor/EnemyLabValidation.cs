#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyLabValidation
{
    private const string Key = "EnemyLab.Validate", Folder = "Library/EnemyLabValidation";
    private static double deadline, nextReport;
    private static bool captured, coreCaptured;
    private static float recoilStart, recoilPeak, recoilTravel;
    private static Vector3 recoilOrigin, recoilDirection;
    private static int recoilHits;
    private static readonly List<string> errors = new();
    private static readonly HashSet<int> growthSeen = new();
    static EnemyLabValidation() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("MASSIVE/Demonstrations/Validate Enemy Lab")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder+"/report.txt","RUNNING\n");
        SessionState.SetBool(Key,true); EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key,false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 220; nextReport = 0; captured = coreCaptured = false; recoilStart = -1f; recoilPeak = recoilTravel = 0f; recoilHits = 0; errors.Clear(); growthSeen.Clear();
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key,false);
    }
    private static void Log(string message,string stack,LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message+"\n"+stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; Application.logMessageReceived -= Log; return; }
        var lab = Object.FindFirstObjectByType<EnemyLab>(); if (!lab) { Finish(false,"No Enemy Lab"); return; }
        foreach (var c in lab.columns)
        {
            if (c.CurrentTarget && c.CurrentTarget != c.Player) errors.Add(c.name+" targeted another column");
            if (c.Failure != null) { Finish(false,c.name+": "+c.Failure+" player="+(c.Player ? c.Player.transform.position.ToString() : "none")+" enemy="+(c.Enemy ? c.Enemy.transform.position.ToString() : "none")); return; }
            if (c.Attacks > c.attacksPerLoop) { Finish(false,c.name+" exceeded three attacks"); return; }
            if (c.Enemy)
            {
                var core = c.Enemy.GetComponentInChildren<DysonRepulsorCore>();
                var ranged = c.Enemy.GetComponent<RangedDroneController>();
                if (ranged && c.CompletedLoops > 0 && c.LastShotCount != c.attacksPerLoop * ranged.shotsPerBurst)
                { Finish(false,c.name+" did not complete three full bursts: "+c.LastShotCount+" shots"); return; }
                if (core && core.GrowthMultiplier >= 1.249f)
                {
                    growthSeen.Add(c.GetInstanceID());
                    if (!coreCaptured) { coreCaptured = true; Capture("dyson-core-burst.png", core.transform.position); }
                }
                var dyson = c.Enemy.GetComponent<DysonSphereRepulsorController>();
                if (dyson && c.Player && c.CompletedLoops == 0)
                {
                    if (dyson.TotalHits > recoilHits)
                    {
                        recoilHits = dyson.TotalHits; recoilStart = Time.time; recoilOrigin = c.Player.transform.position;
                        recoilDirection = (recoilOrigin-c.Enemy.transform.position).normalized;
                    }
                    if (recoilStart >= 0f && Time.time-recoilStart < .8f)
                    {
                        recoilPeak = Mathf.Max(recoilPeak,Vector3.Dot(c.Player.GetComponent<Rigidbody>().linearVelocity,recoilDirection));
                        recoilTravel = Mathf.Max(recoilTravel,Vector3.Dot(c.Player.transform.position-recoilOrigin,recoilDirection));
                    }
                }
                foreach (var p in lab.columns.Where(x => x != c && x.Player))
                    if (c.Enemy.SharesSimulationWith(p.Player)) errors.Add("Cross-column damage scope accepted");
            }
        }
        string status = string.Join("\n",lab.columns.Select(c => c.name+": "+c.Phase+" attacks="+c.Attacks+" loops="+c.CompletedLoops+" kills="+c.Counterkills+
            " target="+(c.CurrentTarget ? c.CurrentTarget.name : "none")+" player="+(c.Player ? c.Player.transform.position.ToString("F2") : "none")+
            " enemy="+(c.Enemy ? c.Enemy.transform.position.ToString("F2") : "none")+" lastShots="+c.LastShotCount));
        if (EditorApplication.timeSinceStartup >= nextReport)
        { nextReport = EditorApplication.timeSinceStartup + 2; File.WriteAllText(Folder+"/report.txt","RUNNING\n"+status+"\n"+string.Join("\n",errors.Distinct())); }
        if (!captured && lab.columns.All(c => c.Enemy) && Time.timeSinceLevelLoad > 9f)
        { captured = true; Capture("gallery.png"); }
        if (lab.columns.All(c => c.CompletedLoops >= 2))
        {
            Capture("looped.png");
            Finish(errors.Count == 0 && growthSeen.Count == 1,"Six lanes completed two real counterkill/telegraph loops. Core 25% growth observed="+growthSeen.Count+". Recoil peak="+recoilPeak.ToString("F2")+"; eased travel="+recoilTravel.ToString("F2")+"\n"+status); return;
        }
        if (EditorApplication.timeSinceStartup > deadline) Finish(false,"Timeout\n"+status);
    }
    private static void Capture(string name, Vector3? focus = null)
    {
        var camera = Camera.main; if (!camera) return;
        Camera close = null;
        if (focus.HasValue)
        {
            close = new GameObject("Validation closeup").AddComponent<Camera>(); close.CopyFrom(camera); close.enabled = false;
            close.transform.position = focus.Value + Vector3.up * 12f; close.transform.rotation = Quaternion.Euler(90,0,0);
            close.orthographic = true; close.orthographicSize = 1.45f; camera = close;
        }
        var rt = new RenderTexture(1920,1080,24); var prior = camera.targetTexture; var active = RenderTexture.active;
        var texture = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply(); File.WriteAllBytes(Folder+"/"+name,texture.EncodeToPNG()); }
        finally { camera.targetTexture = prior; RenderTexture.active = active; Object.DestroyImmediate(texture); Object.DestroyImmediate(rt); if (close) Object.DestroyImmediate(close.gameObject); }
    }
    private static void Finish(bool success,string message)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        File.WriteAllText(Folder+"/report.txt",(success ? "PASSED" : "FAILED")+"\n"+message+"\n"+string.Join("\n",errors.Distinct()));
        Debug.Log("[Enemy Lab validation] "+(success ? "PASSED" : "FAILED")+" "+message);
        EditorApplication.isPlaying = false;
    }
}
#endif
