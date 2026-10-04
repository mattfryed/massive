using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Lattice.Editor
{
    [InitializeOnLoad]
    public static class LatticeLevelIconPlayValidation
    {
        const string Key = "MASSIVE.LatticeIconPlayValidation";
        static IEnumerator routine;
        static readonly List<string> results = new(), errors = new();
        static Scene scene;
        static int frame;
        static double deadline;
        static LatticeLevelIconPlayValidation() { EditorApplication.playModeStateChanged += State; }
        [MenuItem("MASSIVE/LATTICE/Validate Icon in Play Mode %#&k")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            LatticeLevelIconAssets.CreateMissing();
            Directory.CreateDirectory(LatticeLevelIconValidation.Output);
            File.WriteAllText(LatticeLevelIconValidation.Output + "/play-report.txt", "RUNNING");
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                results.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 40;
                SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
                routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            { Application.runInBackground = SessionState.GetBool(Key + "Background", true); SessionState.SetBool(Key, false); }
        }
        static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.frameCount == frame) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Icon runtime validation timed out");
                frame = Time.frameCount;
                if (!routine.MoveNext()) Finish(null);
            }
            catch (Exception e) { Finish(e); }
        }
        static void Finish(Exception e)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            bool ok = e == null && errors.Count == 0;
            File.WriteAllText(LatticeLevelIconValidation.Output + "/play-report.txt", (ok ? "PASSED\n" : "FAILED\n") + string.Join("\n", results) + "\n" + e + "\n" + string.Join("\n", errors));
            Time.timeScale = SessionState.GetFloat(Key + "TimeScale", 1);
            if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
            routine = null; EditorApplication.isPlaying = false;
            Debug.Log("LATTICE icon runtime validation " + (ok ? "PASSED" : "FAILED"));
        }
        static void Check(bool ok, string label) { if (!ok) throw new Exception(label); results.Add("PASS " + label); }
        static IEnumerator Checks()
        {
            SessionState.SetFloat(Key + "TimeScale", Time.timeScale);
            scene = SceneManager.CreateScene("Temporary LATTICE icon validation");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LatticeLevelIconAssets.PrefabPath);
            var root = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(1000, 0, 0);
            var icon = root.GetComponentInChildren<LatticeLevelIcon>();
            var camera = LatticeLevelIconValidation.MakeCamera(scene); camera.transform.position += root.transform.position;
            yield return null; yield return null;
            int n = icon.cellsPerAxis + 1;
            Check(icon.IsReady && icon.NodeCount == n * n * n, "Prefab initializes automatically in Play Mode");
            float start = icon.FieldTime; float until = Time.realtimeSinceStartup + .3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Check(icon.FieldTime > start + .1f, "The icon animates from its normal runtime Update");
            Time.timeScale = 0; start = icon.FieldTime; until = Time.realtimeSinceStartup + .3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Check(icon.FieldTime > start + .1f, "Menu animation continues on unscaled time when gameplay time is paused");
            var visual = icon.transform; Vector3 scale = visual.localScale;
            root.GetComponent<LevelIcon>().ApplySelected(true, true);
            Check(Vector3.Distance(visual.localScale, scale * 1.15f) < .0001f && icon.IsReady, "Existing LevelIcon selection enlarges the live cube");
            LatticeLevelIconValidation.Capture(camera, "cube-play-selected.png");
            root.GetComponent<LevelIcon>().ApplySelected(false, true);
            Check(Vector3.Distance(visual.localScale, scale) < .0001f, "Deselection restores its authored size");
            root.SetActive(false); yield return null;
            Check(!icon.IsReady && icon.GetComponent<MeshFilter>().sharedMesh == null, "Runtime disable releases rendering resources");
            root.SetActive(true); yield return null; yield return null;
            Check(icon.IsReady && icon.EdgeCount == 3 * icon.cellsPerAxis * n * n, "Runtime re-enable recreates every connection");
            var second = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(second, scene);
            second.transform.position = new Vector3(1002, 0, 0); yield return null; yield return null;
            var other = second.GetComponentInChildren<LatticeLevelIcon>();
            Check(other.IsReady && icon.GetComponent<MeshFilter>().sharedMesh != other.GetComponent<MeshFilter>().sharedMesh,
                "Multiple icons own independent meshes and field buffers");
            UnityEngine.Object.Destroy(root); yield return null; yield return null;
            Check(other.IsReady, "Destroying one instance leaves the other instance intact");
            Check(errors.Count == 0, "No runtime errors or exceptions during the icon lifecycle");
        }
    }
}
