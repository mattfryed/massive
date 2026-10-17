using Massive.Demonstrations;
using UnityEngine;

/// <summary>A self-contained instruction prefab, including its optional live demonstration.</summary>
[DisallowMultipleComponent]
public sealed class HowToPlayPane : MonoBehaviour
{
    [Tooltip("Seconds before advancing. Zero uses the carousel's default duration.")]
    [Min(0f)] [SerializeField] private float durationOverride;

    public float DurationOverride => durationOverride;
    private PlayerDemoDirector[] demonstrations;
    private HowToPlayTurretDemo[] turretDemonstrations;
    private HowToPlayEnemyDisplay[] enemyDisplays;

    public bool DemosReady
    {
        get
        {
            if (demonstrations == null) return true;
            foreach (var demo in demonstrations)
                if (demo && demo.isActiveAndEnabled && !demo.IsPresentationReady && string.IsNullOrEmpty(demo.LastFailure)) return false;
            return true;
        }
    }

    public void SetDemosHeld(bool held)
    {
        if (demonstrations == null) demonstrations = GetComponentsInChildren<PlayerDemoDirector>(true);
        foreach (var demo in demonstrations)
            if (demo) demo.SetPresentationHeld(held);
        if (turretDemonstrations == null) turretDemonstrations = GetComponentsInChildren<HowToPlayTurretDemo>(true);
        foreach (var demo in turretDemonstrations)
            if (demo) demo.SetPresentationHeld(held);
        if (enemyDisplays == null) enemyDisplays = GetComponentsInChildren<HowToPlayEnemyDisplay>(true);
        foreach (var display in enemyDisplays)
            if (display) display.SetPresentationHeld(held);
    }

    public void Bind(Camera screenCamera)
    {
        demonstrations = GetComponentsInChildren<PlayerDemoDirector>(true);
        turretDemonstrations = GetComponentsInChildren<HowToPlayTurretDemo>(true);
        enemyDisplays = GetComponentsInChildren<HowToPlayEnemyDisplay>(true);
        // Scene cameras cannot be stored in a prefab. Bind before its demo starts.
        foreach (var view in GetComponentsInChildren<PlayerDemoView>(true))
        {
            view.screenCamera = screenCamera;
            view.RefreshRect();
        }
    }
}
