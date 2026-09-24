using System;
using UnityEngine;

namespace Massive.Player
{
    /// <summary>One project-owned set of player-slot tuning, shared by every gameplay scene.</summary>
    public sealed class PlayerGlobalModifiers : ScriptableObject
    {
        public const string ResourceName = "MassivePlayerModifiers";
        [SerializeField] private bool globalSizeEnabled = true;
        [SerializeField] private bool globalReversalEnabled = true;
        [SerializeField] private Slot[] players = { new Slot(), new Slot(), new Slot(), new Slot() };

        [Serializable]
        public sealed class Slot
        {
            [Range(.1f, 3f)] public float size = 1f;
            public bool scaleActionReach = true;
            public bool scaleMovement;
            public bool scaleProjectileRange;
            public bool reversalEnabled = true;
            [Range(0f, 180f)] public float backwardConeDegrees = 100f;
            [Range(0f, 1f)] public float retainedMomentum;
        }

        private static PlayerGlobalModifiers current;
        private static bool loaded;
        public static PlayerGlobalModifiers Current
        {
            get
            {
                if (!loaded) { current = Resources.Load<PlayerGlobalModifiers>(ResourceName); loaded = true; }
                return current;
            }
        }
        public bool GlobalSizeEnabled => globalSizeEnabled;
        public bool GlobalReversalEnabled => globalReversalEnabled;
        public Slot ForPlayer(int playerID) => players != null && playerID >= 0 && playerID < players.Length ? players[playerID] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() { current = null; loaded = false; }

        public static PlayerGlobalModifiers For(PlayerControllerScript player)
        {
            if (!player || player.IsPseudoPlayer || !player.gameObject.scene.IsValid()) return null;
#if UNITY_EDITOR
            // Asset/prefab previews and isolated validation fixtures keep their authored values.
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(player.gameObject.scene)) return null;
#endif
            var profile = Current;
            return profile && profile.ForPlayer(player.playerID) != null ? profile : null;
        }

        /// <summary>Older scenes and newly instantiated players need no scene-specific installation.</summary>
        public static void EnsureRuntimeComponents(PlayerControllerScript player)
        {
            if (!Application.isPlaying || !For(player)) return;
            if (!player.TryGetComponent<PlayerScaleAdjuster>(out _)) player.gameObject.AddComponent<PlayerScaleAdjuster>();
            if (!player.TryGetComponent<PlayerMovementReversal>(out _))
            {
                var reversal = player.gameObject.AddComponent<PlayerMovementReversal>();
                // If the global modifier is later disabled, restore this legacy player's
                // original lack of reversal assistance rather than inventing a local setting.
                reversal.assistEnabled = false;
            }
        }

        private void OnValidate()
        {
            if (players == null || players.Length != 4) Array.Resize(ref players, 4);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) players[i] = new Slot();
                players[i].size = Mathf.Clamp(players[i].size, .1f, 3f);
                players[i].backwardConeDegrees = Mathf.Clamp(players[i].backwardConeDegrees, 0f, 180f);
                players[i].retainedMomentum = Mathf.Clamp01(players[i].retainedMomentum);
            }
        }
    }
}
