using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Turret-style opaque metaballs, animated on the shell's pausable clock.</summary>
    [DisallowMultipleComponent]
    public sealed class DysonRepulsorCore : MonoBehaviour
    {
        public DysonSphereRepulsorController controller;
        public Renderer whitePlasma, blackPlasma;
        [Min(.1f)] public float coreSize = 1f;
        [Range(0f, 1f)] public float explosionGrowth = .25f;
        [Min(0f)] public float explosionVibration = .018f;
        [Min(0f)] public float vibrationFrequency = 38f;
        [Range(0f, .2f)] public float idleBreathing = .035f;
        public float GrowthMultiplier { get; private set; } = 1f;
        private EnemyBase enemy;
        private MaterialPropertyBlock white, black;
        private readonly Vector4[] whiteBalls = new Vector4[48], blackBalls = new Vector4[48];
        private float deathAge;
        private void Awake()
        {
            enemy = GetComponentInParent<EnemyBase>();
            white = new MaterialPropertyBlock(); black = new MaterialPropertyBlock();
        }
        private void LateUpdate()
        {
            if (!controller || !whitePlasma || !blackPlasma) return;
            if (enemy && enemy.IsPaused) return;
            float clock = controller.AnimationClock, envelope = 0f;
            switch (controller.Phase)
            {
                case DysonSphereRepulsorController.AttackPhase.Expand:
                    envelope = 1f - Mathf.Pow(1f - Mathf.Clamp01(controller.PhaseAge / Mathf.Max(.02f, controller.expansionSeconds)), 3f); break;
                case DysonSphereRepulsorController.AttackPhase.Hold: envelope = 1f; break;
                case DysonSphereRepulsorController.AttackPhase.Return:
                    envelope = 1f - Mathf.SmoothStep(0f, 1f, controller.PhaseAge / Mathf.Max(.05f, controller.returnSeconds)); break;
            }
            if (enemy && enemy.IsDead) deathAge += Time.deltaTime;
            float reveal = Mathf.SmoothStep(0f, 1f, controller.panels.Spawn01);
            float death = enemy && enemy.IsDead ? 1f - Mathf.Clamp01(deathAge / Mathf.Max(.01f, enemy.despawnDelaySeconds)) : 1f;
            GrowthMultiplier = 1f + explosionGrowth * envelope;
            float proxyScale = Mathf.Max(1f, coreSize * (1f + explosionGrowth));
            whitePlasma.transform.localScale = blackPlasma.transform.localScale = Vector3.one * proxyScale;
            float scale = coreSize / proxyScale * GrowthMultiplier * reveal * death * (1f + idleBreathing * Mathf.Sin(clock * 1.8f) * (1f-envelope));
            for (int layer = 0; layer < 2; layer++)
            {
                var balls = layer == 0 ? whiteBalls : blackBalls;
                int count = layer == 0 ? 7 : 5;
                for (int i = 0; i < count; i++)
                {
                    float phase = clock * (2.1f + i * .19f) + i * 2.39996f + layer * 1.7f;
                    Vector3 p = new Vector3(Mathf.Cos(phase), Mathf.Sin(phase * 1.27f), Mathf.Sin(phase)) * (layer == 0 ? .16f : .19f);
                    if (i == 0 && layer == 0) p = Vector3.zero;
                    float t = clock * vibrationFrequency * Mathf.PI * 2f + i * 7.3f;
                    p += new Vector3(Mathf.Sin(t), Mathf.Sin(t * 1.17f), Mathf.Sin(t * .87f)) * (explosionVibration * envelope);
                    float radius = layer == 0 ? (i == 0 ? .16f : .11f) : .092f;
                    balls[i] = new Vector4(p.x * scale, p.y * scale, p.z * scale, radius * scale);
                }
                var block = layer == 0 ? white : black;
                block.SetInt("_BallCount", count); block.SetVectorArray("_Balls", balls);
                block.SetFloat("_SmoothK", .016f * scale);
                var renderer = layer == 0 ? whitePlasma : blackPlasma;
                renderer.SetPropertyBlock(block); renderer.enabled = scale > .001f;
            }
        }
    }
}
