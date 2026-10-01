using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    [CreateAssetMenu(menuName = "MASSIVE/Enemies/Encounter Timeline")]
    public sealed class EnemyEncounterTimeline : ScriptableObject
    {
        [Min(1f)] public float duration = 85f;
        [Min(0f), Tooltip("0 disables the pressure limit. Counts living enemies AND announced arrivals, including Carrier children.")]
        public float maxPressure = 60f;
        public List<PressureCost> pressureCosts = new();
        public List<Cue> cues = new();
        public float Cost(EnemyDefinition enemy)
        {
            foreach (var cost in pressureCosts) if (cost.enemy == enemy) return Mathf.Max(0f, cost.cost);
            return 1f;
        }
        [Serializable] public sealed class PressureCost { public EnemyDefinition enemy; [Min(0f)] public float cost = 1f; }
        [Serializable] public sealed class Cue
        {
            public string label;
            [Min(0f), Tooltip("Time of first arrival, including its full preceding warning.")]
            public float arrivalSeconds = 3f;
            public EnemyFormation formation;
            [Tooltip("Optional deterministic alternatives. Seed chooses once per cue.")]
            public List<EnemyFormation> variants = new();
            public bool allowHorizontalMirror;
            [Tooltip("Approved alternate, tried only BEFORE any warning is shown.")]
            public EnemyFormation fallback;
            [Min(0f), Tooltip("Latest permitted delay of the first arrival. An expired cue is skipped, never queued indefinitely.")]
            public float allowedLateness = 2f;
        }
    }
}
