using UnityEngine;

namespace Massive.Lattice
{
    /// <summary>Optional locomotion adapter, ticked only by the canonical player.
    /// It never polls input or updates the body alongside another movement owner.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerControllerScript))]
    public sealed class LatticePlayerMotor : MonoBehaviour
    {
        public LatticeDisruptionField field;
        public bool IsQuantized { get; private set; }
        public int CompletedSteps { get; private set; }
        public Vector2Int CurrentNode { get; private set; }
        public Vector3 LastStepFrom { get; private set; }
        public Vector3 LastStepTo { get; private set; }
        public float LastStepTime { get; private set; }
        public float PendingDistance => distanceCredit;
        public float TravelledDistance { get; private set; }
        PlayerControllerScript player;
        Rigidbody body;
        SphereCollider sphere;
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[64];
        RigidbodyInterpolation previousInterpolation;
        float distanceCredit;
        Vector3 continuousVelocity;
        int lifeSequence;

        void OnEnable()
        {
            player = GetComponent<PlayerControllerScript>(); body = GetComponent<Rigidbody>(); sphere = GetComponent<SphereCollider>();
            player.LatticeMotor = this;
        }
        void OnDisable() { Release(); if (player && player.LatticeMotor == this) player.LatticeMotor = null; }

        public void Release()
        {
            if (IsQuantized && body) body.interpolation = previousInterpolation;
            IsQuantized = false; distanceCredit = 0; continuousVelocity = Vector3.zero;
        }

        public static Vector2Int Direction8(Vector2 input)
        {
            if (input.sqrMagnitude < .000001f) return Vector2Int.zero;
            int sector = Mathf.RoundToInt(Mathf.Atan2(input.y, input.x) / (Mathf.PI * .25f));
            return new Vector2Int(Mathf.RoundToInt(Mathf.Cos(sector * Mathf.PI * .25f)), Mathf.RoundToInt(Mathf.Sin(sector * Mathf.PI * .25f)));
        }

        // The attack controller owns motion after this handoff. A stage captures
        // its two nodes once, so drifting noise cannot change its travel midway.
        internal bool TryPlanAttack(Vector3 aim, bool advance, out Vector3 start, out Vector3 end, out Vector3 direction)
        {
            start = end = default; direction = aim;
            if (!isActiveAndEnabled || !field || !field.isActiveAndEnabled || !field.IsReady
                || !field.quantizeMovement || !body || body.isKinematic) return false;
            if (field.DisruptionAt(body.position) < (IsQuantized ? field.exitDisruption : field.enterDisruption)) return false;
            float radius = Massive.Player.PlayerScaleAdjuster.BodyRadiusOf(player);
            Vector2Int node = field.NearestValidNode(body.position, radius);
            start = field.NodeWorld(node, body.position.y);
            if (!field.NodeFits(node, radius) || !ClearPath(body.position, start, radius)) return false;
            Vector3 local = field.transform.InverseTransformVector(aim);
            Vector2Int step = Direction8(new Vector2(local.x, local.y));
            if (step == Vector2Int.zero) step = Vector2Int.right;
            Vector3 next = field.NodeWorld(node + step, body.position.y);
            direction = (next - start).normalized;
            end = advance && field.NodeFits(node + step, radius) && ClearPath(start, next, radius) ? next : start;
            Release(); // Restore normal interpolation for the smooth attack travel.
            body.position = start;
            body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
            return true;
        }

        internal bool AttackPathClear(Vector3 destination)
        {
            if (!isActiveAndEnabled || !field || !field.isActiveAndEnabled || !body || body.isKinematic) return false;
            return ClearPath(body.position, destination, Massive.Player.PlayerScaleAdjuster.BodyRadiusOf(player));
        }

        public bool Tick(Vector3 input, float movementMultiplier, float deadzone, float dt)
        {
            if (!isActiveAndEnabled || !field || !field.isActiveAndEnabled || !field.IsReady || !field.quantizeMovement || !body || body.isKinematic)
            { Release(); return false; }
            if (lifeSequence != player.LifeSequence) { Release(); lifeSequence = player.LifeSequence; }
            float disruption = field.DisruptionAt(body.position);
            if (disruption < (IsQuantized ? field.exitDisruption : field.enterDisruption)) { ResumeContinuous(); return false; }
            float radius = Massive.Player.PlayerScaleAdjuster.BodyRadiusOf(player);
            if (!IsQuantized)
            {
                var nearest = field.NearestValidNode(body.position, radius);
                Vector3 target = field.NodeWorld(nearest, body.position.y);
                if (!field.NodeFits(nearest, radius) || !ClearPath(body.position, target, radius)) return false;
                previousInterpolation = body.interpolation; body.interpolation = RigidbodyInterpolation.None;
                continuousVelocity = new Vector3(body.linearVelocity.x, 0, body.linearVelocity.z);
                CurrentNode = nearest; body.position = target; IsQuantized = true; distanceCredit = 0;
            }
            // Contacts may displace a dynamic body. Reacquire a safe node instead of
            // pulling it back across a blocker to the previous logical coordinate.
            Vector3 anchor = field.NodeWorld(CurrentNode, body.position.y);
            if (Vector3.Distance(body.position, anchor) > .025f)
            {
                var nearest = field.NearestValidNode(body.position, radius);
                anchor = field.NodeWorld(nearest, body.position.y);
                if (!field.NodeFits(nearest, radius) || !ClearPath(body.position, anchor, radius)) { Release(); return false; }
                CurrentNode = nearest; body.position = anchor;
            }
            continuousVelocity = player.IntegrateLatticeVelocity(continuousVelocity, movementMultiplier, dt);
            body.linearVelocity = new Vector3(0, body.linearVelocity.y, 0);
            float strength = Mathf.InverseLerp(deadzone, 1f, Mathf.Clamp01(input.magnitude));
            if (strength <= 0) { distanceCredit = 0; return true; }
            Vector3 local = field.transform.InverseTransformVector(input);
            Vector2Int direction = Direction8(new Vector2(local.x, local.y));
            distanceCredit += continuousVelocity.magnitude * field.stepSpeedMultiplier * dt;
            // Carry sub-step distance forward. At high speeds more than one adjacent
            // hop can be due in one tick; each still gets its own collision query.
            for (int step = 0; step < 64; step++)
            {
                Vector2Int next = CurrentNode + direction;
                Vector3 destination = field.NodeWorld(next, body.position.y);
                float distance = Vector3.Distance(body.position, destination);
                if (distanceCredit + .00001f < distance) break;
                if (!field.NodeFits(next, radius) || !ClearPath(body.position, destination, radius))
                { distanceCredit = 0; continuousVelocity = Vector3.zero; break; }
                LastStepFrom = body.position; LastStepTo = destination; LastStepTime = Time.time;
                body.position = destination; CurrentNode = next; distanceCredit = Mathf.Max(0, distanceCredit - distance);
                CompletedSteps++; TravelledDistance += distance;
                if (field.DisruptionAt(destination) < field.exitDisruption) { ResumeContinuous(); break; }
            }
            return true;
        }

        void ResumeContinuous()
        {
            if (IsQuantized && body && !body.isKinematic)
                body.linearVelocity = new Vector3(continuousVelocity.x, body.linearVelocity.y, continuousVelocity.z);
            Release();
        }

        bool Blocks(Collider candidate)
        {
            if (!candidate || candidate.isTrigger || candidate.attachedRigidbody == body || candidate.transform.IsChildOf(transform)) return false;
            if (Physics.GetIgnoreLayerCollision(gameObject.layer, candidate.gameObject.layer)) return false;
            if (sphere && Physics.GetIgnoreCollision(sphere, candidate)) return false;
            var other = candidate.GetComponentInParent<PlayerControllerScript>();
            return !other || player.SharesSimulationWith(other);
        }
        bool ClearPath(Vector3 from, Vector3 to, float radius)
        {
            Vector3 offset = sphere ? sphere.transform.TransformPoint(sphere.center) - body.position : Vector3.zero;
            Vector3 delta = to - from;
            float distance = delta.magnitude, queryRadius = Mathf.Max(.01f, radius - .015f);
            if (distance > .001f)
            {
                int count = Physics.SphereCastNonAlloc(from + offset, queryRadius, delta / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return false;
                for (int i = 0; i < count; i++) if (Blocks(hits[i].collider)) return false;
            }
            int overlapsCount = Physics.OverlapSphereNonAlloc(to + offset, queryRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (overlapsCount == overlaps.Length) return false;
            for (int i = 0; i < overlapsCount; i++) if (Blocks(overlaps[i])) return false;
            return true;
        }
    }
}
