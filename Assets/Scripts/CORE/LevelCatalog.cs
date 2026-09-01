using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MASSIVE/Level Catalog", fileName = "LevelCatalog")]
public class LevelCatalog : ScriptableObject
{
    [SerializeField] private List<LevelDefinition> levels = new();

    public IReadOnlyList<LevelDefinition> Levels => levels;

    public int Count => levels != null ? levels.Count : 0;

    public LevelDefinition Get(int index)
    {
        if (levels == null || levels.Count == 0) return null;
        index = (index % levels.Count + levels.Count) % levels.Count;
        return levels[index];
    }
}
