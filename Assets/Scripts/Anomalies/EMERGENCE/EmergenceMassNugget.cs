using Massive.Scoring;
using UnityEngine;

namespace Massive.Emergence
{
    /// <summary>Shared mass/score rules with a level-owned bounded pool.</summary>
    public sealed class EmergenceMassNugget : MatterNuggetScript
    {
        [Tooltip("Literal increase to the collecting player's personal multiplier, capped by the shared economy. Matter pickups use 0; amplifier nugget/nugglet use 1 / 0.1.")]
        [Min(0)] public float personalMultiplierGain;
        ParticleSystemRenderer[] budRenderers;
        ParticleSystem[] budParticles;
        ParticleSystemScalingMode[] scalingModes;
        Material[] normalMaterials, budMaterials;

        /// <summary>Released appearance for an isolated, manually animated instructions pickup.
        /// Keeps the attached/noncollectible lifecycle; never enables physics or collection.</summary>
        public void UseDetachedPreviewAppearance()
        {
            SetAttachedAppearance(false);
            if (budParticles == null) return;
            foreach (var particle in budParticles)
            { var main = particle.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
        }

        protected override void SetAttachedAppearance(bool attached)
        {
            if (attached && budRenderers == null)
            {
                var shader = Resources.Load<Shader>("EmergenceBudParticle");
                budRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
                budParticles = new ParticleSystem[budRenderers.Length]; scalingModes = new ParticleSystemScalingMode[budRenderers.Length];
                normalMaterials = new Material[budRenderers.Length]; budMaterials = new Material[budRenderers.Length];
                for (int i = 0; i < budRenderers.Length; i++)
                {
                    var original = normalMaterials[i] = budRenderers[i].sharedMaterial;
                    budParticles[i] = budRenderers[i].GetComponent<ParticleSystem>(); scalingModes[i] = budParticles[i].main.scalingMode;
                    var material = budMaterials[i] = new Material(shader) { name = "Opaque reward bud", hideFlags = HideFlags.HideAndDontSave };
                    if (original.HasProperty("_MainTex")) material.SetTexture("_MainTex", original.GetTexture("_MainTex"));
                    if (original.HasProperty("_Color")) material.SetColor("_Color", original.GetColor("_Color"));
                    bool spectrum = original.shader.name == "MASSIVE/Amplifier Prismatic Particle";
                    material.SetFloat("_Spectrum", spectrum ? 1 : 0);
                    if (spectrum) foreach (string property in new[] { "_CycleSeconds", "_HueOffset", "_Saturation", "_Brightness" }) material.SetFloat(property, original.GetFloat(property));
                }
            }
            if (budRenderers == null) return;
            for (int i = 0; i < budRenderers.Length; i++)
            {
                budRenderers[i].sharedMaterial = attached ? budMaterials[i] : normalMaterials[i];
                var main = budParticles[i].main; main.scalingMode = attached ? ParticleSystemScalingMode.Hierarchy : scalingModes[i];
            }
        }
        void OnDestroy() => ReleasePreviewMaterials();

        // Instructions keep this behaviour disabled, so their owner explicitly releases its
        // temporary materials instead of relying on callbacks from a never-enabled component.
        public void ReleasePreviewMaterials()
        {
            if (budMaterials == null) return;
            for (int i = 0; i < budRenderers.Length; i++)
            {
                if (budRenderers[i]) budRenderers[i].sharedMaterial = normalMaterials[i];
                if (budParticles[i]) { var main = budParticles[i].main; main.scalingMode = scalingModes[i]; }
            }
            foreach (var material in budMaterials) if (material)
            { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            budRenderers = null; budParticles = null; scalingModes = null;
            normalMaterials = null; budMaterials = null;
        }

        protected override double ApplyPersonalMultiplierReward(PlayerControllerScript player)
        {
            var scores = MatchScoreService.Instance;
            if (!scores || !scores.IsScoringOpen || !scores.IsChainClockRunning || scores.IsBonusScoringOnly) return 0;
            var chain = player.GetComponent<PlayerScoreChain>();
            return chain ? chain.AddMultiplier(personalMultiplierGain) : 0;
        }
        protected override void FinishDespawn() => gameObject.SetActive(false);
    }
}
