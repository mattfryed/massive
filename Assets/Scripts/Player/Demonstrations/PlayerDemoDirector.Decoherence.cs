using System.Collections;
using Massive.Enemies;
using Massive.Player;
using Massive.PowerUps;
using UnityEngine;

namespace Massive.Demonstrations
{
    public sealed partial class PlayerDemoDirector
    {
        public int ConfirmedLingeringFieldKills { get; private set; }
        public int ConfirmedDecoherenceSecondWaves { get; private set; }

        private IEnumerator DecoherencePaintDemo(DecoherencePowerUpDefinition definition)
        {
            // Reclaim an expiring pickup through the normal melee interaction.
            // Never extend its lifetime or refill its meter just for presentation.
            if (primary.powerUps.RemainingSeconds < 10f)
            {
                Phase = "Waiting for a fresh pickup";
                yield return Until(() => !primary.powerUps.HasActive, "Decoherence did not expire.", 11f);
                if (failed) yield break;
                yield return MoveActorTo(primary, primaryHome, Vector3.right);
                yield return ClaimPowerUp();
            }
            if (failed) yield break;
            yield return Until(() => primary.powerUps.CanActivateDecoherence && primary.powerUps.DecoherenceCharge01 >= .95f,
                "Decoherence did not refill.", definition.emptyToFullRefillSeconds + 1f);
            if (failed) yield break;

            float size = PlayerScaleAdjuster.SizeOf(primary);
            Vector3 start = primaryHome + new Vector3(-1f, 0f, -1.1f) * size;
            Vector3 bend = primaryHome + new Vector3(1f, 0f, -1.1f) * size;
            Vector3 finish = primaryHome + new Vector3(3.3f, 0f, .2f) * size;
            yield return MoveActorTo(primary, start, Vector3.right);
            if (failed) yield break;
            int before = ConfirmedKills;
            SpawnCombatDrone(new Vector3(.3f, 0f, .1f) * size);
            SpawnCombatDrone(new Vector3(3.3f, 0f, 1.6f) * size);
            Phase = "Enemies forming";
            yield return new WaitForSeconds(.9f);

            Phase = "Hold Shield + move: paint a trail";
            yield return Shield(primary, Vector3.right);
            if (failed) yield break;
            yield return PaintTowards(bend, .75f);
            if (failed) yield break;
            yield return PaintTowards(finish, .75f);
            if (failed) yield break;
            SetFrame(primary, Vector2.zero, Vector3.right, holdShield: true);
            yield return new WaitForSeconds(.1f);

            int atRelease = ConfirmedKills;
            float chargeAtRelease = primary.powerUps.DecoherenceCharge01;
            SetFrame(primary, Vector2.zero, Vector3.right);
            Phase = "Release: refill while the trail keeps damaging";
            yield return MoveActorTo(primary, start, Vector3.right);
            if (failed) yield break;
            yield return Until(() => ConfirmedKills - before == 2, "The painted trail did not defeat both sample enemies.", definition.paintLifetimeSeconds + 1f);
            if (failed) yield break;
            ConfirmedLingeringFieldKills += ConfirmedKills - atRelease;
            if (primary.powerUps.DecoherenceFieldActive || primary.powerUps.DecoherenceCharge01 <= chargeAtRelease)
            { Fail("Releasing Decoherence did not stop painting and refill its meter."); yield break; }
            LastOutcome = "Painted trail defeated both enemies; charge refilled";
            if (scenario.decoherenceSecondEnemy)
            {
                yield return DecoherenceSecondWave(definition, start);
                yield break;
            }
            var field = primary.GetComponentInChildren<DecoherenceFieldVisual>();
            Phase = "The lingering trail recedes";
            yield return Until(() => !field || field.Paint.Deposits.Count == 0,
                "The painted trail did not expire.", definition.paintLifetimeSeconds + 1f);
        }

        private IEnumerator DecoherenceSecondWave(DecoherencePowerUpDefinition definition, Vector3 retreat)
        {
            float size = PlayerScaleAdjuster.SizeOf(primary);
            Vector3 center = primaryHome + Vector3.right * (1.5f * size);
            var target = SpawnCombatEnemy(scenario.decoherenceSecondEnemy, center - primaryHome);
            Phase = "A tougher enemy is arriving";
            // Preserve the Dyson's normal formation, health and panel erosion.
            var panels = target.GetComponentInChildren<DysonSpherePanels>();
            if (panels) yield return Until(() => !panels.IsSpawning, "The second enemy did not finish spawning.");
            else yield return new WaitForSeconds(.9f);
            if (failed) yield break;
            var firstField = primary.GetComponentInChildren<DecoherenceFieldVisual>();
            yield return Until(() => !firstField || firstField.Paint.Deposits.Count == 0,
                "The first trail did not clear before repainting.", definition.paintLifetimeSeconds + 1f);
            if (failed) yield break;

            // A tough enemy can survive several fields. Repaint after each field recedes,
            // retaining its real health and claiming fresh pickups only when needed.
            while (target && !target.IsDead && !failed)
            {
                yield return PaintSecondEnemy(definition, target, center, retreat, size);
            }
            if (failed) yield break;
            ConfirmedDecoherenceSecondWaves++;
            LastOutcome = "Drones and Dyson Sphere defeated by separately painted fields";
        }

        private IEnumerator PaintSecondEnemy(DecoherencePowerUpDefinition definition, EnemyBase target,
            Vector3 center, Vector3 retreat, float size)
        {
            float paintSeconds = Mathf.Min(3.5f, definition.fullMeterPaintSeconds * .65f);
            if (!primary.powerUps.HasActive || primary.powerUps.RemainingSeconds < paintSeconds + 2f)
            {
                Phase = "Claim a fresh Decoherence pickup";
                if (primary.powerUps.HasActive)
                    yield return Until(() => !primary.powerUps.HasActive, "Decoherence did not expire before repainting.", primary.powerUps.RemainingSeconds + 1f);
                if (failed) yield break;
                yield return MoveActorTo(primary, primaryHome, Vector3.left);
                if (failed) yield break;
                // Approach from the open side: the Dyson occupies the usual pickup lane.
                yield return ClaimPowerUp(primaryHome, Vector3.left);
            }
            if (failed) yield break;
            yield return Until(() => primary.powerUps.CanActivateDecoherence && primary.powerUps.DecoherenceCharge01 >= .95f,
                "Decoherence did not recharge for the second wave.", definition.emptyToFullRefillSeconds + 1f);
            if (failed) yield break;

            float radius = 2f * size;
            yield return MoveActorTo(primary, center + Vector3.left * radius, Vector3.right);
            if (failed) yield break;
            float healthBefore = target.HealthRemaining;
            Phase = "Hold Shield: paint around the tougher enemy";
            yield return Shield(primary, Vector3.right);
            if (failed) yield break;
            // Let the expanding brush cover the exposed core before circling the shell.
            yield return new WaitForSeconds(.4f);
            float began = Time.time;
            var body = primary.GetComponent<Rigidbody>();
            while (Time.time - began < paintSeconds && !failed)
            {
                if (!primary.powerUps.DecoherenceFieldActive)
                { Fail("Decoherence ended during the second painted pass."); yield break; }
                float angle = Mathf.PI + (Time.time - began) / paintSeconds * Mathf.PI * 2f;
                Vector3 marker = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                Vector3 delta = marker - body.position; delta.y = 0;
                SetFrame(primary, new Vector2(delta.x, delta.z).normalized * Mathf.Clamp01(delta.magnitude * 2f),
                    center - body.position, holdShield: true);
                yield return null;
            }
            SetFrame(primary, Vector2.zero, Vector3.right);
            Phase = "Release: the second field keeps damaging";
            // Finish on the entry side and walk outside the solid shell on the way home.
            yield return MoveActorTo(primary, center + new Vector3(-radius, 0, radius), Vector3.left);
            if (failed) yield break;
            yield return MoveActorTo(primary, center + Vector3.left * (radius + size), Vector3.left);
            if (failed) yield break;
            yield return MoveActorTo(primary, retreat, Vector3.right);
            if (failed) yield break;
            var field = primary.GetComponentInChildren<DecoherenceFieldVisual>();
            yield return Until(() => !field || field.Paint.Deposits.Count == 0, "The second field did not clear.", definition.paintLifetimeSeconds + 1f);
            if (target && !target.IsDead && target.HealthRemaining >= healthBefore)
                Fail("The second painted field did not damage its enemy.");
        }

        private IEnumerator PaintTowards(Vector3 target, float seconds)
        {
            float end = Time.time + seconds;
            var body = primary.GetComponent<Rigidbody>();
            while (Time.time < end)
            {
                if (!primary.powerUps.DecoherenceFieldActive)
                { Fail("Decoherence stopped while the demonstration held Shield."); yield break; }
                Vector3 delta = target - body.position; delta.y = 0;
                Vector2 stick = new Vector2(delta.x, delta.z).normalized * Mathf.Clamp01(delta.magnitude * 1.5f);
                SetFrame(primary, stick, delta.sqrMagnitude > .01f ? delta : Vector3.right, holdShield: true);
                yield return null;
            }
        }
    }
}
