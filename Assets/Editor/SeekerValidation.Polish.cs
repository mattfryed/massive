#if UNITY_EDITOR
using System.Collections;
using Massive.Enemies;
using UnityEngine;

public static partial class SeekerValidation
{
    private static IEnumerator PolishChecks()
    {
        // A complete unforced cycle with a real target: miss, coast, cooldown, stalk, player hit.
        seeker.stalkSeconds = 0f; seeker.avoidanceChance = 0f;
        Place(seeker, Vector3.back, Quaternion.identity); Place(player, new Vector3(0,0,2.8f));
        while (seeker.Phase != SeekerController.AttackPhase.Charging) yield return null;
        seeker.stalkSeconds = 3f;
        while (seeker.Charge01 < .5f) yield return null;
        float multiplier = visual.NoseSpinMultiplier, angle = visual.NoseAngle, clock = visual.AnimationTime;
        var delay = Delay(.16f); while (delay.MoveNext()) yield return null;
        float degrees = Mathf.Abs(Mathf.DeltaAngle(angle, visual.NoseAngle));
        Check(visual.NoseSpinMultiplier > multiplier && degrees > Mathf.Abs(visual.noseSpinSpeed) * (visual.AnimationTime-clock) * 2f,
            "Nose rotation accelerates progressively during charge without changing the rear-ring spin rates");
        while (seeker.Charge01 < .9f) yield return null;
        Place(player, new Vector3(3f,0,2.8f)); int lunges = seeker.TotalLunges, ghostsBefore = visual.TotalGhosts;
        while (seeker.TotalLunges == lunges) yield return null;
        float maxStretch = 1f; int maxGhosts = 0; bool captured = false, paused = false;
        while (seeker.Phase == SeekerController.AttackPhase.Firing)
        {
            maxStretch = Mathf.Max(maxStretch, visual.NoseStretch); maxGhosts = Mathf.Max(maxGhosts, visual.LiveGhostCount);
            if (visual.LiveGhostCount >= 3 && !paused)
            {
                paused = true; enemy.Pause(true); angle = visual.NoseAngle; float stretch = visual.NoseStretch;
                int ghostSlot = visual.NewestGhostSlot;
                Vector3 ghost = visual.GhostCornerWorld(ghostSlot,0); int total = visual.TotalGhosts;
                delay = Delay(.12f); while (delay.MoveNext()) yield return null;
                Check(visual.NoseAngle == angle && visual.NoseStretch == stretch && visual.TotalGhosts == total && visual.GhostCornerWorld(ghostSlot,0) == ghost,
                    "Pause freezes nose spin, elasticity, ghost emission and ghost aging");
                Capture("seeker-thrust-ghosts.png", true, true); captured = true; enemy.Pause(false);
                float fixedTime = Time.fixedTime; while (Time.fixedTime <= fixedTime + .02f) yield return null;
                Check(visual.GhostCornerWorld(ghostSlot,0) == ghost, "Existing outline ghosts stay fixed in world space as the Seeker advances");
            }
            yield return null;
        }
        Check(maxStretch > 1f + visual.thrustNoseStretch * .65f, "Nose stretches subtly along its own axis during thrust");
        Check(maxGhosts >= 3 && captured && visual.TotalGhosts - ghostsBefore <= 16,
            "Thrust leaves a bounded series of complete shell outline snapshots (live " + maxGhosts
            + ", emitted " + (visual.TotalGhosts-ghostsBefore) + ", captured " + captured
            + ", phase " + seeker.Phase + ", distance " + seeker.DistanceTravelled + ")");
        Check(seeker.Phase == SeekerController.AttackPhase.Gliding, "Missed thrust carries forward momentum into a glide");
        Vector3 glideStart = seeker.transform.position; float spinAtLaunch = visual.NoseSpinMultiplier;
        float minimumStretch = visual.NoseStretch;
        while (seeker.Phase == SeekerController.AttackPhase.Gliding)
        { minimumStretch = Mathf.Min(minimumStretch, visual.NoseStretch); yield return null; }
        Check(seeker.Phase == SeekerController.AttackPhase.Cooldown && seeker.GlideDistanceTravelled > .2f
            && seeker.GlideDistanceTravelled < 1.5f && Vector3.Dot(seeker.transform.position-glideStart,seeker.LungeDirection) > .1f,
            "Miss glide advances a short extra distance and eases to rest before cooldown");
        float until = Time.time + visual.noseBounceSeconds;
        while (Time.time < until) { minimumStretch = Mathf.Min(minimumStretch, visual.NoseStretch); yield return null; }
        Check(minimumStretch < .995f && Mathf.Abs(visual.NoseStretch-1f) < .001f,
            "Nose recoil compresses below rest length then settles with a damped rubber-band bounce");
        Check(visual.NoseSpinMultiplier < spinAtLaunch && visual.NoseSpinMultiplier < 1.1f,
            "Nose spin eases back to its normal rate after launch");
        Check(visual.LiveGhostCount == 0, "Thrust ghosts contract their white outlines and fully retire after thrust");
        Check(!seeker.LastAttackHitPlayer && seeker.CurrentCooldownSeconds == seeker.cooldownSeconds,
            "A miss selects the longer cooldown");
        Place(player, Vector3.zero); Place(seeker, Vector3.back * 3.5f, Quaternion.identity);
        while (seeker.Phase != SeekerController.AttackPhase.Stalking) yield return null;
        float stalkStartAge = seeker.PhaseAge, activeSeconds = 0f;
        Vector3 start = seeker.transform.position; int before = seeker.TotalLunges; float minDistance = 100f, maxDistance = 0f;
        float previousTime = Time.time; bool earlyAttack = false, stalkPaused = false;
        while (seeker.Phase == SeekerController.AttackPhase.Stalking)
        {
            activeSeconds += Time.time - previousTime; previousTime = Time.time;
            float range = Vector3.Distance(seeker.transform.position, player.transform.position);
            minDistance = Mathf.Min(minDistance, range); maxDistance = Mathf.Max(maxDistance, range);
            if (seeker.TotalLunges != before) earlyAttack = true;
            if (seeker.PhaseAge > 1f && !stalkPaused)
            {
                stalkPaused = true;
                Capture("seeker-stalk.png", true);
                enemy.Pause(true); float phaseAge = seeker.PhaseAge; Vector3 position = seeker.transform.position;
                delay = Delay(.15f); while (delay.MoveNext()) yield return null;
                Check(seeker.PhaseAge == phaseAge && seeker.transform.position == position, "Pause freezes the stalking timer and orbit movement");
                enemy.Pause(false); previousTime = Time.time;
            }
            yield return null;
        }
        Check(!earlyAttack && activeSeconds + stalkStartAge >= 2.9f, "Seeker stalks for the full three seconds before attempting another attack");
        Check(minDistance > seeker.stalkDistanceRange.x - .15f && maxDistance < seeker.stalkDistanceRange.y + .15f
            && Vector3.Distance(start,seeker.transform.position) > 1.5f,
            "Stalking circles and strafes around the target while maintaining the configured distance band");
        while (seeker.Phase != SeekerController.AttackPhase.Charging) yield return null;
        Check(seeker.Target == player, "Stalking returns to a new charge against the tracked player");
        while (seeker.Phase != SeekerController.AttackPhase.Hit) yield return null;
        Check(seeker.LastAttackHitPlayer && seeker.CurrentCooldownSeconds == seeker.playerHitCooldownSeconds
            && seeker.playerHitCooldownSeconds < seeker.cooldownSeconds,
            "Accepted player damage selects the independent shorter cooldown");
        Vector3 impact = seeker.transform.position;
        while (seeker.Phase == SeekerController.AttackPhase.Hit) yield return null;
        Check(Vector3.Dot(impact - seeker.transform.position, seeker.LungeDirection) > .1f,
            "Successful player contact bounces the Seeker backward without another hit");
        float cooldownStart = Time.time - seeker.PhaseAge;
        while (seeker.Phase == SeekerController.AttackPhase.Cooldown) yield return null;
        Check(seeker.Phase == SeekerController.AttackPhase.Stalking && Mathf.Abs(Time.time-cooldownStart-seeker.playerHitCooldownSeconds) < .08f,
            "Successful hit uses the shorter cooldown and still enters stalking");
    }
}
#endif
