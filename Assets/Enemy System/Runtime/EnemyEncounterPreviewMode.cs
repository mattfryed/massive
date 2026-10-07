#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Massive.Enemies
{
    // A one-shot Editor request. Never serialized onto a level, never included in a player build.
    public static class EnemyEncounterPreviewMode
    {
        const string Key = "Massive.EncounterComposer.NextPlayMode";
        public static void Request(bool twoVTwo) => SessionState.SetInt(Key, twoVTwo ? 1 : 0);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            int mode = SessionState.GetInt(Key, -1); SessionState.EraseInt(Key);
            if (mode < 0) return;
            GameFlowContext.EnsureExists();
            GameFlowContext.Instance.SetMode(mode == 1 ? GameMode.TwoVTwo : GameMode.OneVOne);
        }
    }
}
#endif
