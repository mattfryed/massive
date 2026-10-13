using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Uses the shared Amplifier Nugget visual, on the enemy's paused animation clock.</summary>
    public sealed class AmplifiedDysonCore : MonoBehaviour
    {
        public DysonSphereRepulsorController controller;
        public Transform visual;
        [Min(.01f)] public float coreScale = .6f;
        [Range(0f, 1f)] public float explosionGrowth = .25f;
        private EnemyBase enemy;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float deathAge;
        private static readonly int PreviewTime = Shader.PropertyToID("_PreviewTime");

        private void Awake()
        {
            enemy = GetComponentInParent<EnemyBase>();
            renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            block = new MaterialPropertyBlock();
        }
        private void OnEnable() => deathAge = 0f;
        private void LateUpdate()
        {
            if (!controller || !visual || enemy && enemy.IsPaused) return;
            float envelope = 0f;
            if (controller.Phase == DysonSphereRepulsorController.AttackPhase.Expand)
                envelope = Mathf.Clamp01(controller.PhaseAge / Mathf.Max(.02f, controller.expansionSeconds));
            else if (controller.Phase == DysonSphereRepulsorController.AttackPhase.Hold) envelope = 1f;
            else if (controller.Phase == DysonSphereRepulsorController.AttackPhase.Return)
                envelope = 1f - Mathf.SmoothStep(0, 1, controller.PhaseAge / Mathf.Max(.05f, controller.returnSeconds));
            if (enemy && enemy.IsDead) deathAge += Time.deltaTime;
            float fade = enemy && enemy.IsDead ? 1f - Mathf.Clamp01(deathAge / Mathf.Max(.01f, enemy.despawnDelaySeconds)) : 1f;
            visual.localScale = Vector3.one * (coreScale * (1f + explosionGrowth * envelope) * fade);
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block); block.SetFloat(PreviewTime, controller.AnimationClock);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
