#if UNITY_EDITOR
using System;
using System.Reflection;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EnemyBaselineIntegration
{
    [MenuItem("MASSIVE/Enemies/Integrate Current Dyson")]
    public static void Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        DysonRepulsorSetup.Install();
        const string galleryPath = "Assets/Editor/Gameplay Prefab Gallery.prefab";
        var root = PrefabUtility.LoadPrefabContents(galleryPath);
        try
        {
            bool changed = false;
            foreach (var old in root.GetComponentsInChildren<DysonSphereController>(true))
            {
                var oldTransform = old.transform;
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(DysonRepulsorSetup.PrefabPath), oldTransform.parent);
                replacement.name = old.name;
                replacement.transform.SetLocalPositionAndRotation(oldTransform.localPosition, oldTransform.localRotation);
                replacement.transform.localScale = oldTransform.localScale;
                replacement.transform.SetSiblingIndex(oldTransform.GetSiblingIndex());
                replacement.SetActive(old.gameObject.activeSelf);
                typeof(Massive.EditorTools.Prototyping.GameplayPrefabGalleryBuilder)
                    .GetMethod("MakeDisplayOnly", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { replacement });
                Object.DestroyImmediate(old.gameObject); changed = true;
            }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, galleryPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[Enemy baseline] Canonical Dyson and display gallery use the Repulsor prefab; existing health and score preserved.");
    }
}
#endif
