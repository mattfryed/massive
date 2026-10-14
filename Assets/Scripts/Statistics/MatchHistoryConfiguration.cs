using System;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Statistics
{
    [CreateAssetMenu(menuName = "MASSIVE/Match History Configuration")]
    public sealed class MatchHistoryConfiguration : ScriptableObject
    {
        [Serializable]
        public struct LevelIdentity
        {
            public LevelDefinition level;
            public string id;
        }

        public LevelCatalog catalog;
        public ScoreEconomyProfile standardEconomy;
        [HideInInspector] public string standardSettingsHash;
        [HideInInspector] public LevelIdentity[] levels = Array.Empty<LevelIdentity>();

        // Baked asset GUIDs survive level renames, scene renames and catalog reordering.
        public string GetLevelId(LevelDefinition level)
        {
            if (!level || !catalog) return null;
            bool inCatalog = false;
            foreach (var entry in catalog.Levels) if (entry == level) { inCatalog = true; break; }
            if (!inCatalog) return null;
            foreach (var entry in levels) if (entry.level == level) return entry.id;
            return null;
        }
    }
}
