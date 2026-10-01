using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    [CreateAssetMenu(menuName = "MASSIVE/Enemies/Formation")]
    public sealed class EnemyFormation : ScriptableObject
    {
        [Min(.1f)] public float warningSeconds = 2f;
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
            [Tooltip("Optional straight, collision-aware entrance before normal AI. Applies to Drone variants only.")]
            [Min(0f)] public float entrySeconds;
            [Min(0f)] public float entrySpeed = 1.5f;
        }
    }
}
