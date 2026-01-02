using UnityEngine;

[CreateAssetMenu(menuName = "MASSIVE/Level Definition", fileName = "LevelDefinition")]
public class LevelDefinition : ScriptableObject
{
    [Header("Identity")]
    public int levelNumber = 1;
    public string levelTitle = "SUPERNOVA";

    [Header("Gameplay Scene")]
    public SceneReference gameplayScene = new SceneReference();

    [Header("Level Select Visual")]
    public GameObject iconPrefab;

    [Header("Instructions (Optional)")]
    public GameObject instructionsPanelPrefab;


    [Header("Audio")]
    public LevelAudioProfile audioProfile;   // ✅ new

    public string SceneName => gameplayScene != null ? gameplayScene.SceneName : null;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gameplayScene != null)
            gameplayScene.SyncFromAsset();
    }
#endif
}