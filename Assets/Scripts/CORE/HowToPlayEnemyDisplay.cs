using Massive.Enemies;
using UnityEngine;

/// <summary>Keeps the instruction pane's enemy models isolated from gameplay.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-10500)]
public sealed class HowToPlayEnemyDisplay : MonoBehaviour
{
    private void Awake()
    {
        // These prefab instances retain their native geometry and idle visuals.
        // Dyson's controller also drives its shell/core animation, so keep it alive
        // while suppressing its movement, targeting, damage and global registration.
        foreach (var enemy in GetComponentsInChildren<EnemyBase>(true))
        {
            enemy.ConfigureDemonstration(transform, null);
            enemy.HoldPosition = true;
            enemy.AttacksEnabled = false;
            enemy.enabled = false;
        }
        foreach (var body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.useGravity = false;
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }
}
