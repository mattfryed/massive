using System;
using UnityEngine;

[CreateAssetMenu(menuName = "MASSIVE/Audio/Scene Audio Catalog", fileName = "SceneAudioCatalog")]
public class SceneAudioCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string sceneName;
        public SceneAudioProfile profile;
    }

    public Entry[] entries;

    public SceneAudioProfile Get(string sceneName)
    {
        if (entries == null) return null;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].sceneName == sceneName)
                return entries[i].profile;
        return null;
    }
}