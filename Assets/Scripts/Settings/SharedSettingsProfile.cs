using UnityEngine;

namespace Massive.Settings
{
    public abstract class SharedSettingsProfile : ScriptableObject
    {
        [Tooltip("Use these project defaults in every scene. Individual components can opt out.")]
        public bool sharedEnabled = true;
    }

    public interface ISharedSettingsConsumer
    {
        bool UseSharedSettings { get; set; }
        SharedSettingsProfile SharedSettingsAsset { get; }
        string SharedSettingsGroup { get; }
    }

    public static class SharedSettingsRuntime
    {
        private static class Cache<T> where T : SharedSettingsProfile
        {
            internal static T value;
            internal static bool loaded;
        }

        public static T Load<T>() where T : SharedSettingsProfile
        {
            if (!Cache<T>.loaded)
            {
                Cache<T>.value = Resources.Load<T>(typeof(T).Name);
                Cache<T>.loaded = true;
            }
            return Cache<T>.value;
        }

        public static T Resolve<T>(Component owner, bool useShared) where T : SharedSettingsProfile
        {
            if (!useShared) return null;
#if UNITY_EDITOR
            // Isolated editor fixtures and prefab-stage contents retain their authored tuning.
            if (owner && UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(owner.gameObject)) return null;
#endif
            T profile = Load<T>();
            return profile && profile.sharedEnabled ? profile : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            Reset<MeleeVisualProfile>(); Reset<AmplifierSharedProfile>();
            Reset<ResonanceSharedProfile>(); Reset<TextAnimationSharedProfile>(); Reset<PlayerTuningProfile>();
        }

        private static void Reset<T>() where T : SharedSettingsProfile
        { Cache<T>.loaded = false; Cache<T>.value = null; }
    }
}
