using UnityEngine;

namespace Massive.Player
{
    public sealed partial class PlayerMeleePlasma
    {
        [Header("Original Particles - stage prefabs")]
        public OriginalAttackStageVfx originalThrustPrefab;
        public OriginalAttackStageVfx originalSweepPrefab;
        public OriginalAttackStageVfx originalRepulsorPrefab;
        readonly OriginalAttackStageVfx[] originalInstances = new OriginalAttackStageVfx[3];
        readonly OriginalAttackStageVfx[] originalSources = new OriginalAttackStageVfx[3];
        GameObject originalContainer;
        int originalRevision = -1, originalActiveIndex = -1;
        AttackStage originalActiveStage;
        PlayerAttackController originalEventSource;
        float originalLastTime;
        public OriginalAttackStageVfx OriginalPrefab(int index)
        {
            var shared = SharedProfile;
            return index == 0 ? (shared ? shared.originalThrustPrefab : originalThrustPrefab) :
                index == 1 ? (shared ? shared.originalSweepPrefab : originalSweepPrefab) :
                (shared ? shared.originalRepulsorPrefab : originalRepulsorPrefab);
        }
        bool OriginalPrefabsReplaceTrail => Effective_visualStyle == MeleeVisualStyle.OriginalParticles &&
            ((OriginalPrefab(0) && OriginalPrefab(1)) ||
             (attackController && attackController.IsAttacking && attackController.CurrentStage != null &&
              OriginalPrefab((int)attackController.CurrentStage.StageType)));

        void BindOriginalStageEvents()
        {
            UnbindOriginalStageEvents(); originalEventSource = attackController;
            if (originalEventSource) originalEventSource.StageCancelled += OriginalStageCancelled;
        }
        void UnbindOriginalStageEvents()
        {
            if (originalEventSource) originalEventSource.StageCancelled -= OriginalStageCancelled;
            originalEventSource = null;
        }
        void OriginalStageCancelled(AttackStage _) { StopOriginalStageEffects(); }
        void TickOriginalStageEffects()
        {
            if (originalRevision != OriginalAttackStageVfx.ContentRevision)
            { ReleaseOriginalStageEffects(); originalRevision = OriginalAttackStageVfx.ContentRevision; }
            var stage = attackController.IsAttacking ? attackController.CurrentStage : null;
            int index = stage != null ? (int)stage.StageType : -1;
            float t = attackController.StageNormalizedTime;
            if (stage != originalActiveStage || (stage != null && t < originalLastTime))
            {
                if (originalActiveIndex >= 0 && originalInstances[originalActiveIndex])
                    originalInstances[originalActiveIndex].EndEmission();
                originalActiveStage = stage; originalActiveIndex = index;
                if (index >= 0) StartOriginalEffect(index);
            }
            // Assignment changes from the persistent window also take effect during the loop.
            if (index >= 0 && OriginalPrefab(index) != originalSources[index]) StartOriginalEffect(index);
            originalLastTime = t;
            Vector3 aim = attackController.CurrentAttackVisualDirectionWS;
            for (int i = 0; i < originalInstances.Length; i++)
            {
                var effect = originalInstances[i];
                if (!effect || !effect.IsRunning) continue;
                if (i == index || effect.followDuringTail)
                    PlaceOriginalEffect(effect, i == index ? aim : effect.LastDirection);
                if (i == index)
                    effect.Sample(stage, t, i == 2 ? RepulsorActivationStart(stage) : stage.ActivationStartNormalized, aim, Time.deltaTime);
                else
                {
                    effect.TickTail(Time.deltaTime);
                    if (!effect.IsRunning) effect.gameObject.SetActive(false);
                }
            }
        }
        void StartOriginalEffect(int index)
        {
            var source = OriginalPrefab(index);
            if (originalSources[index] != source)
            {
                DestroyOriginalInstance(index); originalSources[index] = source;
            }
            if (!source) return;
            if (!originalContainer)
            {
                originalContainer = new GameObject("Original Particles - stage effects") { hideFlags = HideFlags.DontSave };
                originalContainer.transform.SetParent(transform, false);
            }
            if (!originalInstances[index])
            {
                var staging = new GameObject("Stage creation") { hideFlags = HideFlags.DontSave };
                staging.transform.SetParent(originalContainer.transform, false); staging.SetActive(false);
                originalInstances[index] = Instantiate(source, staging.transform);
                originalInstances[index].name = source.name;
                originalInstances[index].gameObject.SetActive(false);
                originalInstances[index].transform.SetParent(originalContainer.transform, false);
                Destroy(staging);
            }
            var effect = originalInstances[index];
            PlaceOriginalEffect(effect, attackController.CurrentAttackVisualDirectionWS);
            effect.gameObject.SetActive(true); effect.Begin();
        }
        void PlaceOriginalEffect(OriginalAttackStageVfx effect, Vector3 aim)
        {
            aim.y = 0; if (aim.sqrMagnitude < .0001f) aim = transform.right;
            Quaternion facing = Quaternion.LookRotation(aim, Vector3.up);
            Vector3 origin = playerVisuals && playerVisuals.visuals ? playerVisuals.visuals.position : transform.position;
            effect.transform.SetPositionAndRotation(origin + facing * (effect.positionOffset * PlayerVisualSize), facing * Quaternion.Euler(effect.rotationOffset));
            Vector3 inherited = effect.transform.parent.lossyScale;
            Vector3 desired = effect.scale * PlayerVisualSize;
            effect.transform.localScale = new Vector3(desired.x / Mathf.Max(.0001f, Mathf.Abs(inherited.x)),
                desired.y / Mathf.Max(.0001f, Mathf.Abs(inherited.y)), desired.z / Mathf.Max(.0001f, Mathf.Abs(inherited.z)));
        }
        void StopOriginalStageEffects()
        {
            foreach (var effect in originalInstances)
                if (effect) { effect.StopImmediately(); effect.gameObject.SetActive(false); }
            originalActiveStage = null; originalActiveIndex = -1; originalLastTime = 0;
        }
        void DestroyOriginalInstance(int index)
        {
            var effect = originalInstances[index]; if (!effect) return;
            effect.StopImmediately(); effect.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(effect.gameObject); else DestroyImmediate(effect.gameObject);
            originalInstances[index] = null;
        }
        void ReleaseOriginalStageEffects()
        {
            StopOriginalStageEffects();
            for (int i = 0; i < 3; i++) { DestroyOriginalInstance(i); originalSources[i] = null; }
            if (originalContainer)
            { if (Application.isPlaying) Destroy(originalContainer); else DestroyImmediate(originalContainer); }
            originalContainer = null;
        }
    }
}
