#if UNITY_EDITOR
using System.Collections;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AmplifierPossessionValidation
{
    static IEnumerator GoalAndFeedbackChecks(MatchScoreService service)
    {
        string saved = JsonUtility.ToJson(core.Possession);
        GameObject goalGO = null;
        var visual = core.GetComponent<AmplifierCoreVisual>();
        try
        {
            core.Possession.settledSpeed = 3f; core.Possession.settlingSeconds = 0f;
            yield return ResetCore();
            Check(core.Body.isKinematic, "Goal test starts with a fully settled Core");
            goalGO = new GameObject("Low-speed goal pull fixture"); goalGO.SetActive(false);
            goalGO.transform.SetParent(scope.transform); goalGO.transform.position = core.Body.position + Vector3.right * 2f;
            var shape = goalGO.AddComponent<SphereCollider>(); shape.isTrigger = true; shape.radius = .3f;
            var goal = goalGO.AddComponent<AmplifierGoalCapture>(); goal.UseSharedSettings = false;
            Set(goal, "captureRadius", .3f); Set(goal, "pullAcceleration", 1f);
            Set(goal, "pullByProximity", AnimationCurve.Linear(0, 1, 1, 1));
            goalGO.SetActive(true);
            yield return Wait(.5f);
            Check(core.IsLaunched && !core.Body.isKinematic && core.ShotVelocity.magnitude > .1f && core.ShotVelocity.magnitude < 3f,
                "Active goal wakes a settled Core and keeps pulling below Stop Speed with zero settling time");
            Check(core.ShotOwner == null, "Goal pull alone does not create an NPC-damaging player shot");
            yield return Until(() => core.HasBeenCaptured, 5f, "Low-speed attraction completes normal goal capture");
            goalGO.SetActive(false); service.ResetForMatch(true); yield return ResetCore();
            Check(!AmplifierGoalCapture.IsDrivingCore(core) && core.Body.isKinematic, "Disabled goal leaves normal resting behavior intact");

            yield return Enter(light);
            goalGO.transform.position = core.Body.position + Vector3.right;
            goalGO.SetActive(true); yield return Wait(.1f);
            Check(core.IsOccupied && core.Body.isKinematic && !core.HasBeenCaptured, "Goal attraction cannot take an occupied Core");
            goalGO.SetActive(false);
            core.Possession.fullChargeSeconds = .8f;
            core.Possession.chargeVibration = true; core.Possession.ejectionRipple = true;
            Vector3 origin = core.Body.position;
            Input(light, attack: true); yield return Wait(.15f);
            float early = visual.ChargeVibration01;
            yield return Wait(.4f);
            Check(visual.ChargeVibration01 > early && visual.ChargeVibration01 > .2f, "Vibration ramps up as charge accumulates");
            Check(Vector3.Distance(core.Body.position, origin) < .0001f, "Charge vibration never moves the physics body");
            core.Possession.chargeVibration = false; yield return null; yield return null;
            Check(visual.ChargeVibration01 == 0f, "Charge vibration toggle removes the effect immediately");
            core.Possession.chargeVibration = true;
            yield return Until(() => core.IsLaunched, .8f, "A held attack automatically releases at the configured maximum charge");
            Input(light); yield return null; yield return null;
            Check(visual.EjectionRippleActive && visual.ChargeVibration01 == 0f, "Shot ejection starts a ripple and ends charge vibration");
            yield return Wait(core.Possession.ejectionRippleSeconds + .1f);
            Check(!visual.EjectionRippleActive, "Ejection ripple decays completely after its duration");
            yield return ResetCore(); yield return Enter(light);
            Set(core, "controlDeadline", Time.time + .05f);
            yield return Until(() => !core.IsOccupied, .3f, "Possession timeout ejects without firing");
            Check(visual.EjectionRippleActive && !core.IsLaunched, "Timeout ejection also ripples the Core");
            yield return ResetCore();
            Check(!visual.EjectionRippleActive && visual.ChargeVibration01 == 0f, "Respawn clears all interaction feedback");
        }
        finally
        {
            if (goalGO) Object.DestroyImmediate(goalGO);
            service.ResetForMatch(true);
            JsonUtility.FromJsonOverwrite(saved, core.Possession);
        }
    }
}
#endif
