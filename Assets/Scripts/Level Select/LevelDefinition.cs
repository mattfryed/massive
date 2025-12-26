using UnityEngine;

[CreateAssetMenu(menuName = "MASSIVE/Level Definition", fileName = "LevelDefinition")]
public class LevelDefinition : ScriptableObject
{
    [Header("Identity")]
    public int levelNumber = 1;
    public string levelTitle = "SUPERNOVA";

    [Header("Scene Flow")]
    [Tooltip("The gameplay scene name to load after instructions.")]
    public string sceneName = "S-1_SUPERNOVA";

    [Header("Level Select Visual")]
    public GameObject iconPrefab;
}