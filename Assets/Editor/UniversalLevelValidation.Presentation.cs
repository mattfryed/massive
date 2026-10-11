#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class UniversalLevelValidation
{
    const string PresentationKey = "MASSIVE.UniversalLevelPresentation";
    static double presentationDeadline, presentationCaptureAt;
    static bool presentationCaptured, presentationBackground;
    static int presentationErrors;
    static void InitializePresentationCapture() => EditorApplication.playModeStateChanged += PresentationState;

    [MenuItem("MASSIVE/Levels/Capture Production Presentation (DYNAMO)")]
    public static void CapturePresentation()
    {
        var scene = SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty) throw new InvalidOperationException("Capture from a saved scene in Edit Mode.");
        SessionState.SetString(PresentationKey + ".restore",scene.path);
        SessionState.SetBool(PresentationKey,true);
        EditorSceneManager.OpenScene("Assets/Scenes/S-6_DYNAMO.unity");
        EnemyEncounterPreviewMode.Request(false);
        EditorApplication.isPlaying = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void PresentationStartLog()
    {
        if(!SessionState.GetBool(PresentationKey,false)) return;
        presentationErrors=0;
        Application.logMessageReceived += PresentationLog;
    }
    static void PresentationLog(string message,string stack,LogType type)
    {
        if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        presentationErrors++;
        File.AppendAllText(Folder + "/presentation.txt", "ERROR " + message + "\n" + stack + "\n");
    }
    static void PresentationState(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(PresentationKey,false)) return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            presentationBackground=Application.runInBackground; Application.runInBackground=true;
            presentationCaptured=false; presentationCaptureAt=-1;
            presentationDeadline=EditorApplication.timeSinceStartup+45;
            EditorApplication.update+=PresentationTick;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(PresentationKey,false);
            EditorApplication.update-=PresentationTick;
            EditorSceneManager.OpenScene(SessionState.GetString(PresentationKey+".restore",""));
            File.AppendAllText(Folder+"/presentation.txt", "Restored saved Edit Mode scene: " + SceneManager.GetActiveScene().path + "\n");
        }
    }
    static void PresentationTick()
    {
        double now=EditorApplication.timeSinceStartup;
        var match=All<GameManagerScript>(SceneManager.GetActiveScene()).FirstOrDefault();
        if(presentationCaptureAt<0 && match && match.Phase==MatchRuntimePhase.Regulation) presentationCaptureAt=now+4;
        if(!presentationCaptured && presentationCaptureAt>0 && now>=presentationCaptureAt)
        {
            ScreenCapture.CaptureScreenshot(Folder+"/DYNAMO-presentation-final.png");
            presentationCaptured=true;
        }
        if(now<presentationDeadline && (!presentationCaptured || now<presentationCaptureAt+1)) return;
        EditorApplication.update-=PresentationTick;
        Application.logMessageReceived-=PresentationLog;
        Application.runInBackground=presentationBackground;
        File.AppendAllText(Folder+"/presentation.txt", (presentationCaptured?"Captured live DYNAMO regulation":"FAILED capture timeout") + "; runtime errors=" + presentationErrors + "\n");
        EditorApplication.isPlaying=false;
    }
}
#endif
