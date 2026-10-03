using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    [CreateAssetMenu(menuName = "MASSIVE/Enemies/Encounter Timeline")]
    public sealed class EnemyEncounterTimeline : ScriptableObject
    {
        [Min(1f)] public float duration = 85f;
        [Header("Population (authoritative while this timeline runs)")]
        [Min(0), Tooltip("0 disables the total limit. Includes living enemies, reserved arrivals and Carrier-launched Drones. Replaces profile and definition caps.")]
        public int maxAliveTotal = 40;
        [Tooltip("Optional limits by enemy definition. Missing entries or 0 mean unlimited; legacy definition/category caps do not apply.")]
        public List<PopulationLimit> populationLimits = new();
        [Min(0f), Tooltip("0 disables the pressure limit. Counts living enemies AND all reserved arrivals, including blocked slots and Carrier children.")]
        public float maxPressure = 60f;
        public List<PressureCost> pressureCosts = new();
        public List<Cue> cues = new();
        public int Limit(EnemyDefinition enemy)
        {
            // Duplicate entries use the tightest nonzero limit, independent of list order.
            int result = 0;
            foreach (var limit in populationLimits)
                if (limit != null && limit.enemy == enemy && limit.maxAlive > 0)
                    result = result == 0 ? limit.maxAlive : Mathf.Min(result, limit.maxAlive);
            return result;
        }
        public float Cost(EnemyDefinition enemy)
        {
            foreach (var cost in pressureCosts) if (cost.enemy == enemy) return Mathf.Max(0f, cost.cost);
            return 1f;
        }
        [Serializable] public sealed class PressureCost { public EnemyDefinition enemy; [Min(0f)] public float cost = 1f; }
        [Serializable] public sealed class PopulationLimit { public EnemyDefinition enemy; [Min(0)] public int maxAlive; }
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
            [Min(0f), Tooltip("Window for initially reserving a batch with a full warning. Strict releases use this lateness; Flexible slots use blocked-slot grace once reserved.")]
            public float allowedLateness = 2f;
            [Tooltip("Otherwise inherit conflict settings from the chosen formation, including a fallback.")]
            public bool overrideSpawnPolicy;
            public EnemyFormation.Integrity integrity;
            [Min(0f)] public float maxPositionAdjustment = 1f;
            [Min(0f)] public float blockedSlotGrace = 3f;
            public EnemyFormation.Integrity IntegrityFor(EnemyFormation value) => overrideSpawnPolicy ? integrity : value.integrity;
            public float AdjustmentFor(EnemyFormation value) => Mathf.Max(0, overrideSpawnPolicy ? maxPositionAdjustment : value.maxPositionAdjustment);
            public float GraceFor(EnemyFormation value) => Mathf.Max(0, overrideSpawnPolicy ? blockedSlotGrace : value.blockedSlotGrace);
        }
    }
}
