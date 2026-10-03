using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    [CreateAssetMenu(menuName = "MASSIVE/Enemies/Formation")]
    public sealed class EnemyFormation : ScriptableObject
    {
        public enum Integrity { Strict, Flexible }
        public enum AdjustmentPattern { Local, Rows, Ring, Groups }
        [Min(.1f)] public float warningSeconds = 2f;
        [Header("Spawn conflict handling")]
        public Integrity integrity;
        [Min(0f), Tooltip("Maximum world-space displacement before a warning appears. Wall sockets never move.")]
        public float maxPositionAdjustment;
        [Min(0f), Tooltip("Flexible slots may wait this long beyond their scheduled arrival; each still needs a full warning.")]
        public float blockedSlotGrace = 3f;
        [Tooltip("Prefer row-aligned offsets, rotation around the ring center, or translation of placement groups before local corrections.")]
        public AdjustmentPattern adjustmentPattern;
        public List<Slot> slots = new();

        [Serializable]
        public sealed class Slot
        {
            public EnemyDefinition enemy;
            public EnemySpawnTelegraph telegraph;
            [Tooltip("Named region on the arena layout. Coordinates within it run from 0 to 1.")]
            public string region = "Arena";
            public Vector2 position = new(.5f, .5f);
            public Vector2 facing = Vector2.down;
            [Tooltip("Optional wall socket ID. Replaces region, position and facing.")]
            public string socket;
            [Min(0f)] public float releaseDelay;
            [Min(0), Tooltip("Optional rigid placement group, e.g. one phalanx. 0 uses the formation's default grouping.")]
            public int placementGroup;
            [Min(0), Tooltip("Flexible slots with the same positive ID announce/release/expire together. Use equal release delays. 0 is independent.")]
            public int releaseGroup;
            [Tooltip("Optional straight, collision-aware entrance before normal AI. Applies to Drone variants only.")]
            [Min(0f)] public float entrySeconds;
            [Min(0f)] public float entrySpeed = 1.5f;
        }
    }
}
