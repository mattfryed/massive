using Massive.Lattice;
using UnityEngine;

namespace Massive.Player
{
    public partial class PlayerAttackController
    {
        public bool IsLatticeAttack { get; private set; }
        LatticePlayerMotor latticeAttackMotor;
        Vector3 latticeAttackStart, latticeAttackEnd;

        void PrepareLatticeAttack()
        {
            IsLatticeAttack = false;
            latticeAttackMotor = ownerController ? ownerController.LatticeMotor : null;
            if (!latticeAttackMotor || !ownerController.CanUseLatticeAttack) return;
            bool thrust = currentStage.StageType == AttackStageType.PrimaryLunge;
            if (!latticeAttackMotor.TryPlanAttack(stageAttackDirectionWS, thrust,
                out latticeAttackStart, out latticeAttackEnd, out Vector3 direction)) return;
            IsLatticeAttack = true;
            if (thrust) stageAttackDirectionWS = direction;
            stageTravelDistanceWS = Vector3.Distance(latticeAttackStart, latticeAttackEnd);
            stageStopDistanceWS = stageTravelDistanceWS;
        }

        void ApplyLatticeAttackMotion(float normalized)
        {
            // Preserve the existing animation curve, damage window and effects.
            // Use an absolute position so multiple render updates between physics
            // ticks cannot lose travel through accumulated MovePosition deltas.
            if (!ownerController || !ownerController.CanUseLatticeAttack || !latticeAttackMotor)
            { CancelAttack(); return; }
            float progress = normalized >= 1f ? 1f : Mathf.Clamp01(currentStage.DistanceCurve.Evaluate(normalized));
            Vector3 target = Vector3.Lerp(latticeAttackStart, latticeAttackEnd, progress);
            target.y = rb.position.y;
            if (!latticeAttackMotor.AttackPathClear(target))
            { StopAtSolidImpact(); return; }
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            if (normalized >= 1f)
            {
                // Commit the last fraction before stage-completion listeners or
                // the next combo stage can acquire the body at the old position.
                rb.position = target;
            }
            else rb.MovePosition(target);
            currentDistanceProgress = stageTravelDistanceWS * progress;
        }

        void FinishLatticeAttack(bool completed)
        {
            if (!IsLatticeAttack) return;
            IsLatticeAttack = false;
            latticeAttackMotor = null;
            if (ownerController) ownerController.EndLatticeAttack(completed);
        }
    }
}
