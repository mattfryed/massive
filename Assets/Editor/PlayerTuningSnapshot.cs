using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    // Subassets preserve material, prefab and profile references across editor sessions.
    public sealed class PlayerTuningSnapshot : ScriptableObject
    {
        public PlayerTuningProfile tuning;
        public PlayerAttackProfile attacks;
        public PlayerGlobalModifiers players;
        public MeleeVisualProfile visuals;

        public static PlayerTuningSnapshot Capture()
        {
            var result = CreateInstance<PlayerTuningSnapshot>();
            result.tuning = Instantiate(PlayerTuningEditing.GetOrCreate());
            if (result.tuning.attackProfile) result.attacks = Instantiate(result.tuning.attackProfile);
            result.players = Instantiate(PlayerGlobalModifiersEditing.GetOrCreate());
            result.visuals = Instantiate(SharedSettingsRuntime.Load<MeleeVisualProfile>());
            result.name = "Player tuning snapshot";
            return result;
        }

        public void Apply()
        {
            var live = PlayerTuningEditing.GetOrCreate();
            var liveAttack = live.attackProfile;
            var livePlayers = PlayerGlobalModifiersEditing.GetOrCreate();
            var liveVisuals = SharedSettingsRuntime.Load<MeleeVisualProfile>();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply player tuning snapshot");
            Copy(tuning, live); Copy(attacks, liveAttack); Copy(players, livePlayers); Copy(visuals, liveVisuals);
            live.attackProfile = liveAttack;
            PlayerTuningEditing.Save(live);
            Undo.CollapseUndoOperations(group);
        }

        private static void Copy(Object source, Object destination)
        {
            if (!source || !destination) return;
            string originalName = destination.name;
            Undo.RegisterCompleteObjectUndo(destination, "Apply player tuning snapshot");
            EditorUtility.CopySerialized(source, destination);
            destination.name = originalName;
            PlayerTuningEditing.Save(destination);
        }

        public void SaveAsset(string path)
        {
            AssetDatabase.CreateAsset(this, path);
            foreach (var part in new Object[] { tuning, attacks, players, visuals })
                if (part) { part.name = part.GetType().Name; AssetDatabase.AddObjectToAsset(part, this); }
            tuning.attackProfile = attacks;
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        public void ReleaseTemporary()
        {
            if (EditorUtility.IsPersistent(this)) return;
            foreach (var part in new Object[] { tuning, attacks, players, visuals }) if (part) DestroyImmediate(part);
            DestroyImmediate(this);
        }
    }
}
