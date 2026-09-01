using System;
using UnityEngine;

[Serializable]
public class SceneReference
{
    // Runtime-safe identifier (what we actually use at runtime)
    [SerializeField] private string sceneName;

    public string SceneName => sceneName;

#if UNITY_EDITOR
    // Editor-only: lets you drag a SceneAsset in the inspector
    [SerializeField] private UnityEditor.SceneAsset sceneAsset;

    public UnityEditor.SceneAsset SceneAsset => sceneAsset;

    public string ScenePath =>
        sceneAsset != null ? UnityEditor.AssetDatabase.GetAssetPath(sceneAsset) : null;

    public void SyncFromAsset()
    {
        if (sceneAsset == null) return;
        sceneName = sceneAsset.name;
    }
#endif
}
