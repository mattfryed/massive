using System;
using UnityEngine;

public sealed class GameFlowContext : MonoBehaviour
{
    public static GameFlowContext Instance { get; private set; }

    public event Action<GameMode> OnModeChanged;
    public event Action<LevelDefinition> OnLevelSelected;

    [Header("Runtime State")]
    [SerializeField] private GameMode mode = GameMode.OneVOne;
    [SerializeField] private LevelDefinition selectedLevel;

    // Fallback / convenience if you ever need to set by string (eg debug)
    [SerializeField] private string selectedGameplaySceneName;

    public GameMode Mode => mode;
    public bool IsTwoVTwo => mode == GameMode.TwoVTwo;

    public LevelDefinition SelectedLevel => selectedLevel;

    public string SelectedGameplaySceneName
    {
        get
        {
            // LevelDefinition is now authoritative
            if (selectedLevel != null && !string.IsNullOrEmpty(selectedLevel.SceneName))
                return selectedLevel.SceneName;

            return selectedGameplaySceneName;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureExists();

    public static void EnsureExists()
    {
        if (Instance != null) return;

        Instance = FindFirstObjectByType<GameFlowContext>();
        if (Instance != null)
        {
            DontDestroyOnLoad(Instance.gameObject);
            return;
        }

        var go = new GameObject("GameFlowContext");
        Instance = go.AddComponent<GameFlowContext>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetMode(GameMode newMode)
    {
        if (mode == newMode) return;
        mode = newMode;
        OnModeChanged?.Invoke(mode);
    }

    public void SelectLevel(LevelDefinition def)
    {
        selectedLevel = def;
        selectedGameplaySceneName = def != null ? def.SceneName : null;
        OnLevelSelected?.Invoke(def);
    }

    public void ClearSelection()
    {
        selectedLevel = null;
        selectedGameplaySceneName = null;
    }
}
