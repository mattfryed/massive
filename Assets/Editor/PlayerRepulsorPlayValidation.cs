using System;
using UnityEditor;
using UnityEngine;

/// <summary>Starts bounded Play-mode checks; the temporary runner lives in the runtime assembly.</summary>
public static class PlayerRepulsorPlayValidation
{
    public static string Status => PlayerRepulsorPlayValidationRunner.Status;
    public static string Result => PlayerRepulsorPlayValidationRunner.Result;

    [MenuItem("MASSIVE/Player/Validate Repulsor in Play Mode")]
    public static void Start()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        if (UnityEngine.Object.FindFirstObjectByType<PlayerRepulsorPlayValidationRunner>() ||
            UnityEngine.Object.FindFirstObjectByType<PlayerRepulsorRecorder>())
            throw new InvalidOperationException("Finish the current validation or recording first.");
        var go = new GameObject("Repulsor validation (temporary)") { hideFlags = HideFlags.DontSave };
        go.AddComponent<PlayerRepulsorPlayValidationRunner>().Run();
    }
}
