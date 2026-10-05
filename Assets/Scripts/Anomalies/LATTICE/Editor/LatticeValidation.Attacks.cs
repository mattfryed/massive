#if UNITY_EDITOR
using System.Collections;
using Massive.Player;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Lattice.EditorTools
{
    public static partial class LatticeValidation
    {
        static void AttackInput(Vector2 aim, bool press = true) => actor.SetScriptedInput(new PlayerInputFrame {
            moveInput = aim, attackDown = press, hasAimDirWS = aim.sqrMagnitude > 0, aimDirWS = new Vector3(aim.x,0,aim.y) });

        static IEnumerator ReadyAttack()
        {
            actor.attackController.CancelAttack(); Input(Vector2.zero);
            yield return Seconds(Mathf.Max(.35f, actor.attackController.Effective_attackCooldown + .05f));
            ResetActor(); yield return Seconds(.06f);
        }

        static IEnumerator AttackChecks()
        {
            var attack = actor.attackController;
            field.mode = LatticeDisruptionField.FieldMode.Disconnected;
            field.quantizeMovement = true;
            yield return Seconds(1.2f);
            var directions = new[] { new Vector2(1,.15f), new Vector2(.8f,1), new Vector2(-.12f,1), new Vector2(-1,.8f),
                new Vector2(-1,-.1f), new Vector2(-.8f,-1), new Vector2(.1f,-1), new Vector2(1,-.8f) };
            for (int i = 0; i < directions.Length; i++)
            {
                yield return ReadyAttack();
                Vector2 input = directions[i].normalized;
                Vector3 local = field.transform.InverseTransformVector(new Vector3(input.x,0,input.y));
                Vector2Int step = LatticePlayerMotor.Direction8(new Vector2(local.x,local.y));
                Vector2Int startNode = field.NearestNode(body.position);
                Vector3 start = body.position, target = field.NodeWorld(startNode + step, start.y);
                AttackInput(input);
                yield return Until(() => attack.IsLatticeAttack, "Thrust " + i + " captures the disrupted grid");
                bool intermediate = false, aligned = true;
                float began = Time.time - attack.TimeInStage;
                while (attack.IsAttacking)
                {
                    Vector3 delta = body.position - start, travel = target - start;
                    intermediate |= delta.magnitude > .03f && delta.magnitude < travel.magnitude - .03f;
                    aligned &= Vector3.Cross(delta, travel.normalized).magnitude < .015f;
                    if (i == 0 && attack.StageNormalizedTime > .3f && attack.StageNormalizedTime < .7f)
                        CaptureFixture(field.transform, "attack-thrust-mid.png", Vector2.zero, 2.6f);
                    yield return null;
                }
                Input(Vector2.zero);
                Check(intermediate && aligned && Vector3.Distance(body.position,target) < .015f,
                    "Thrust " + i + " travels smoothly along one snapped direction to exactly one neighboring dot");
                Check(Mathf.Abs(Time.time - began - attack.Profile.GetStage(0).Duration) < .085f,
                    "Thrust " + i + " retains its authored duration, including longer diagonals");
                yield return Until(() => motor.IsQuantized, "Thrust " + i + " returns directly to grid locomotion", .12f);
                Check(Vector3.Distance(body.position,target) < .015f, "Thrust " + i + " has no post-attack slide or extra step");
            }

            yield return ReadyAttack();
            AttackInput(Vector2.right);
            yield return Until(() => attack.IsLatticeAttack, "Boundary-exit Thrust starts quantized");
            Vector3 exitTarget = origin + Vector3.right * field.Spacing;
            field.mode = LatticeDisruptionField.FieldMode.Connected;
            AttackInput(Vector2.up, false);
            while (attack.IsAttacking)
            {
                Check(attack.IsLatticeAttack && Vector3.Dot(attack.CurrentAttackDirectionWS,Vector3.right) > .999f,
                    "Reconnection and changed stick direction cannot retarget an in-flight Thrust");
                yield return null;
            }
            Input(Vector2.zero);
            Check(Vector3.Distance(body.position,exitTarget) < .015f, "Thrust finishes its original neighbor after leaving disruption");
            field.mode = LatticeDisruptionField.FieldMode.Disconnected; yield return Seconds(1.2f);

            yield return ReadyAttack();
            AttackInput(Vector2.right);
            yield return Until(() => attack.IsLatticeAttack, "Combo begins with a grid Thrust");
            yield return Until(() => attack.TimeInStage >= attack.ComboWindowSeconds(attack.CurrentStage).x, "Thrust combo window opens");
            attack.RegisterAttackPress();
            yield return Until(() => attack.CurrentStageIndex == 1, "Thrust chains into Swipe");
            Vector3 anchored = body.position; float firstArc = attack.CurrentWeaponYawOffsetDeg; bool arcMoved = false;
            bool queued = false, stayed = true;
            while (attack.IsAttacking && attack.CurrentStageIndex == 1)
            {
                stayed &= attack.IsLatticeAttack && Vector3.Distance(body.position,anchored) < .015f;
                arcMoved |= Mathf.Abs(attack.CurrentWeaponYawOffsetDeg-firstArc) > 5f;
                if (!queued && attack.TimeInStage >= attack.ComboWindowSeconds(attack.CurrentStage).x)
                { attack.RegisterAttackPress(); queued = true; }
                yield return null;
            }
            Check(stayed && arcMoved, "Swipe stays on its dot while its weapon arc continues animating");
            Check(attack.CurrentStageIndex == 2 && attack.IsLatticeAttack, "Swipe chains into anchored Repulsor");
            while (attack.IsAttacking) { stayed &= Vector3.Distance(body.position,anchored) < .015f; yield return null; }
            Input(Vector2.zero);
            Check(stayed && Vector3.Distance(anchored, origin + Vector3.right*field.Spacing) < .015f,
                "Full combo advances one cell total; Swipe and Repulsor add zero movement even with the stick held");

            yield return ReadyAttack();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(wall);
            wall.transform.position = origin + Vector3.right * field.Spacing * .55f;
            wall.transform.localScale = new Vector3(.025f,2,2); Physics.SyncTransforms();
            AttackInput(Vector2.right);
            yield return Until(() => attack.IsLatticeAttack, "Blocked Thrust retains its attack animation");
            while (attack.IsAttacking) yield return null;
            Input(Vector2.zero);
            Check(Vector3.Distance(body.position,origin) < .015f, "A thin blocker prevents the cell advance without leaving the starting dot");
            wall.SetActive(false);

            yield return ReadyAttack(); AttackInput(Vector2.right);
            yield return Until(() => attack.IsLatticeAttack && attack.StageNormalizedTime > .2f, "Pause check reaches moving Thrust");
            Time.timeScale = 0; yield return null; yield return null;
            var paused = body.position; var progress = attack.StageNormalizedTime;
            double resume = EditorApplication.timeSinceStartup + .15;
            while (EditorApplication.timeSinceStartup < resume) yield return null;
            Check(Vector3.Distance(body.position,paused) < .001f && attack.StageNormalizedTime == progress, "Pause freezes smooth grid attack travel and its animation clock");
            Time.timeScale = 1; actor.ProtectActionMomentum(.3f); Input(Vector2.zero);
            var interrupted = body.position; body.linearVelocity = Vector3.back * 2; actor.ExternalStun(.25f);
            yield return Seconds(.1f);
            Check(!attack.IsLatticeAttack && !motor.IsQuantized && body.position.z < interrupted.z-.03f,
                "Cancellation releases the attack path so incoming knockback can move the player freely");

            yield return ReadyAttack(); AttackInput(Vector2.right);
            yield return Until(() => attack.IsLatticeAttack, "Disabling test starts a grid attack");
            motor.enabled = false; yield return null; yield return null;
            Check(!attack.IsAttacking && !attack.IsLatticeAttack, "Disabling the grid motor cancels its active attack without stale ownership");
            motor.enabled = true;

            field.mode = LatticeDisruptionField.FieldMode.Connected; yield return Seconds(1.2f);
            yield return ReadyAttack(); AttackInput(Vector2.right);
            yield return Until(() => attack.IsAttacking, "Normal-grid attack still starts");
            Check(!attack.IsLatticeAttack, "An attack starting outside disruption retains the normal controller path");
            field.mode = LatticeDisruptionField.FieldMode.Disconnected;
            while (attack.IsAttacking) { Check(!attack.IsLatticeAttack, "Moving disruption cannot capture an already-started normal attack"); yield return null; }
            Input(Vector2.zero); attack.CancelAttack(); ResetActor();
        }
    }
}
#endif
