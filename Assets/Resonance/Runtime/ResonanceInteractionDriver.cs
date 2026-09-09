using System.Collections.Generic;
using Massive.Multiplier;
using Massive.Enemies;
using UnityEngine;

namespace Massive.Resonance
{
    /// <summary>Runtime-only actor policy over the same tapered contacts. Does not edit the layer matrix.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ResonanceInteractionDriver : MonoBehaviour
    {
        private sealed class Actor
        {
            public Rigidbody body;
            public PlayerControllerScript player;
            public AmplifierCoreGameplay core;
            public EnemyBase enemy;
            public Collider[] colliders;
            public bool[] colliderEnabled;
            public bool filtersReady, seen, magnetic;
            public ResonancePlayerResponse playerMode;
            public ResonanceCoreResponse coreMode;
            public bool enemySludge;
            public Vector3 incoming, normal;
            public Vector3 previousPosition;
            public Vector3 surfaceNormal;
            public ResonanceSegment surface;
            public float age, brake, radius, cooldown;
        }
        private struct IgnoredPair { public Collider actor, wall; public bool previous; }
        private readonly List<Actor> actors = new List<Actor>(8);
        private readonly List<IgnoredPair> ignored = new List<IgnoredPair>();
        private ResonancePatternController pattern;
        public int MagneticTurnarounds { get; private set; }

        public void Initialize(ResonancePatternController owner) { pattern = owner; }

        private Actor GetActor(Rigidbody body, PlayerControllerScript player, AmplifierCoreGameplay core, EnemyBase enemy = null)
        {
            foreach (var a in actors) if (a.body == body) { a.seen = true; return a; }
            var colliders = body.GetComponentsInChildren<Collider>(true);
            var actor = new Actor { body = body, player = player, core = core, enemy = enemy, colliders = colliders,
                colliderEnabled = new bool[colliders.Length], seen = true, previousPosition=body.worldCenterOfMass };
            actors.Add(actor); return actor;
        }

        private void FixedUpdate()
        {
            if (pattern == null || !pattern.isActiveAndEnabled) return;
            foreach (var actor in actors) actor.seen = false;
            foreach (var core in AmplifierCoreGameplay.ActiveCores)
                if (core != null && core.Body != null && core.gameObject.scene == gameObject.scene)
                    Tick(GetActor(core.Body, null, core));
            foreach (var player in PlayerControllerScript.ActivePlayers)
                if (player != null && !player.IsPseudoPlayer && player.gameObject.scene == gameObject.scene)
                {
                    var body = player.GetComponent<Rigidbody>();
                    if (body != null) Tick(GetActor(body, player, null));
                }
            foreach (var enemy in EnemyBase.ActiveEnemies)
                if (enemy != null && !enemy.IsDead && enemy.gameObject.scene == gameObject.scene)
                {
                    var body = enemy.GetComponent<Rigidbody>();
                    if (body != null) Tick(GetActor(body, null, null, enemy));
                }
            for (int i = actors.Count - 1; i >= 0; i--)
                if (!actors[i].seen || actors[i].body == null)
                { Release(actors[i]); actors.RemoveAt(i); }
        }

        private void ConfigureFilters(Actor actor)
        {
            bool dirty = !actor.filtersReady || actor.playerMode != pattern.playerResponse || actor.coreMode != pattern.coreResponse
                || actor.enemySludge != pattern.enemiesUseSludge;
            actor.radius = 0f;
            for (int i = 0; i < actor.colliders.Length; i++)
            {
                var c = actor.colliders[i];
                // Drones have a trigger-only body. It still needs a contact radius for sludge.
                bool enabled = c != null && c.enabled && c.gameObject.activeInHierarchy && (!c.isTrigger || actor.enemy != null) && c.attachedRigidbody == actor.body;
                if (actor.colliderEnabled[i] != enabled) dirty = true;
                actor.colliderEnabled[i] = enabled;
                if (enabled) actor.radius = Mathf.Max(actor.radius, Mathf.Max(c.bounds.extents.x, c.bounds.extents.z));
            }
            if (!dirty) return;
            RestorePairs(actor);
            actor.filtersReady = true; actor.playerMode = pattern.playerResponse; actor.coreMode = pattern.coreResponse;
            actor.enemySludge = pattern.enemiesUseSludge;
            foreach (var segment in pattern.InteractiveSegments)
            {
                bool ignore = actor.enemy != null ? pattern.enemiesUseSludge : actor.core != null
                    ? pattern.coreResponse != ResonanceCoreResponse.PatternDefault || segment.Settings.behavior != ResonanceBehavior.HardWall
                    : pattern.playerResponse == ResonancePlayerResponse.PassThrough || pattern.playerResponse == ResonancePlayerResponse.Sludge
                        || (pattern.playerResponse == ResonancePlayerResponse.PatternDefault && segment.Settings.behavior != ResonanceBehavior.HardWall);
                if (!ignore) continue;
                for (int i = 0; i < actor.colliders.Length; i++)
                {
                    if (!actor.colliderEnabled[i] || actor.colliders[i].isTrigger) continue;
                    foreach (var wall in segment.HardColliders)
                    {
                        if (wall == null || !wall.enabled) continue;
                        var pair = new IgnoredPair { actor = actor.colliders[i], wall = wall, previous = Physics.GetIgnoreCollision(actor.colliders[i], wall) };
                        ignored.Add(pair); Physics.IgnoreCollision(pair.actor, pair.wall, true);
                    }
                }
            }
        }

        private void Tick(Actor actor)
        {
            ConfigureFilters(actor);
            Vector3 previous=actor.previousPosition;
            actor.previousPosition=actor.body.worldCenterOfMass;
            if (actor.player != null) actor.player.RemoveMovementInfluence(this);
            if (actor.enemy != null) actor.enemy.RemoveMovementInfluence(this);
            if (actor.body.isKinematic || actor.radius <= 0f || (actor.enemy != null && (actor.enemy.IsPaused || actor.enemy.IsDead))
                || (actor.core != null && (actor.core.IsCaptured || actor.core.IsPresentationOnly)))
            { actor.magnetic = false; return; }
            if (Mathf.Abs(pattern.transform.InverseTransformPoint(actor.body.worldCenterOfMass).y) > pattern.colliderHeight * .5f + actor.radius)
            { actor.magnetic = false; return; }
            if ((pattern.fieldAffectedLayers.value & (1 << actor.body.gameObject.layer)) == 0
                || Physics.GetIgnoreLayerCollision(pattern.obstacleLayer, actor.body.gameObject.layer)) return;
            bool sludge = actor.enemy != null ? pattern.enemiesUseSludge
                : actor.player != null && pattern.playerResponse == ResonancePlayerResponse.Sludge;
            bool magnetic = actor.core != null && pattern.coreResponse == ResonanceCoreResponse.MagneticRepulsion;
            if (actor.player!=null)
            {
                // Sweep real motion for fast crossings, but never draw a trail across a spawn/teleport.
                float maxTravel=actor.body.linearVelocity.magnitude*Time.fixedDeltaTime*2f+actor.radius*2f;
                if ((actor.previousPosition-previous).sqrMagnitude>maxTravel*maxTravel) previous=actor.previousPosition;
                pattern.NotifyPlayerPassage(actor.body.GetInstanceID(),previous,actor.previousPosition,
                    actor.body.linearVelocity,actor.radius,Time.fixedDeltaTime);
            }
            if (!sludge && !magnetic) { actor.magnetic = false; return; }
            float dt = Time.fixedDeltaTime;
            if (actor.magnetic)
            {
                float oldAge = actor.age; actor.age += dt;
                Vector3 velocity = EvaluatePath(actor,dt);
                velocity.y = actor.body.linearVelocity.y;
                actor.body.linearVelocity = velocity;
                if (oldAge < actor.brake && actor.age >= actor.brake)
                { MagneticTurnarounds++; CoreVisual(actor,true); }
                else if (actor.age<actor.brake) CoreVisual(actor,false);
                if (actor.age >= actor.brake + pattern.magneticReleaseSeconds)
                { actor.magnetic = false; actor.cooldown = .08f; }
                return;
            }
            actor.cooldown = Mathf.Max(0f, actor.cooldown - dt);
            float strongest = 0f, bestGap = float.PositiveInfinity, bestEnvelope = 0f;
            Vector3 bestNormal = Vector3.right;
            ResonanceSegment bestSurface=null;
            foreach (var segment in pattern.InteractiveSegments)
            {
                Vector3 normal; float gap, envelope;
                if (!segment.NearestContact(actor.body.worldCenterOfMass, out normal, out gap, out envelope)) continue;
                gap -= actor.radius;
                float weight = Mathf.Clamp01(1f - gap / Mathf.Max(.01f, pattern.interactionReach)) * envelope;
                strongest = Mathf.Max(strongest, weight);
                if (gap < bestGap) { bestGap = gap; bestNormal = normal; bestEnvelope = envelope; bestSurface=segment; }
            }
            if (sludge && strongest > 0f)
            {
                float multiplier = Mathf.Lerp(1f, pattern.sludgeMovementMultiplier, strongest);
                if (actor.enemy != null) actor.enemy.SetMovementInfluence(this, multiplier);
                else actor.player.SetMovementInfluence(this, multiplier);
                Vector3 v = actor.body.linearVelocity;
                float decay = Mathf.Exp(-pattern.sludgeDrag * strongest * dt);
                actor.body.linearVelocity = new Vector3(v.x*decay, v.y, v.z*decay);
            }
            if (!magnetic || actor.cooldown > 0f || bestEnvelope <= .001f) return;
            Vector3 incoming = actor.body.linearVelocity; incoming.y = 0f;
            float approach = -Vector3.Dot(incoming, bestNormal);
            // One-step lookahead prevents a fast Core skipping a thin interaction field.
            if (approach <= .05f || bestGap > pattern.magneticReach * bestEnvelope + approach * dt) return;
            actor.incoming = incoming; actor.normal = bestNormal;
            actor.surfaceNormal=bestNormal; actor.surface=bestSurface;
            actor.age = 0f; actor.magnetic = true;
            // Shorten braking at high speeds to keep the stopping distance on the entry side.
            actor.brake = Mathf.Max(dt, Mathf.Min(pattern.magneticBrakeSeconds, Mathf.Max(0f, bestGap) * 1.8f / approach));
            actor.age = dt;
            actor.body.linearVelocity = EvaluatePath(actor,dt) + Vector3.up * actor.body.linearVelocity.y;
            if (actor.age >= actor.brake) { MagneticTurnarounds++; CoreVisual(actor,true); }
            else CoreVisual(actor,false);
        }

        private void CoreVisual(Actor actor, bool turnaround)
        {
            pattern.NotifyCoreContact(actor.body,actor.surface,actor.body.worldCenterOfMass,actor.incoming,
                turnaround,actor.age/Mathf.Max(.001f,actor.brake));
        }

        public static Vector3 MagneticVelocity(Vector3 incoming, Vector3 normal, float age, float brake, float release, float retention)
        {
            normal.y = 0f; normal.Normalize();
            Vector3 planar = new Vector3(incoming.x, 0f, incoming.z);
            if (age <= brake)
                planar *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / Mathf.Max(.0001f, brake)));
            else
                planar = Vector3.Reflect(planar, normal) * Mathf.Max(0f, retention)
                    * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - brake) / Mathf.Max(.0001f, release)));
            return new Vector3(planar.x, incoming.y, planar.z);
        }

        private Vector3 EvaluatePath(Actor actor, float dt)
        {
            if (pattern.magneticPath == ResonanceMagneticPath.StopAndReflect)
                return MagneticVelocity(actor.incoming,actor.normal,actor.age,actor.brake,pattern.magneticReleaseSeconds,pattern.magneticSpeedRetention);
            Vector3 normal=actor.surfaceNormal; float gap=float.PositiveInfinity, envelope;
            bool contact=actor.surface!=null && actor.surface.NearestContact(actor.body.worldCenterOfMass,out normal,out gap,out envelope);
            if (contact && Vector3.Dot(normal,actor.normal)>0f)
                actor.surfaceNormal=Vector3.RotateTowards(actor.surfaceNormal,normal,pattern.magneticSurfaceFollow*240f*Mathf.Deg2Rad*dt,0f).normalized;
            Vector3 velocity=MagneticGlideVelocity(actor.incoming,actor.normal,actor.age,actor.brake,
                pattern.magneticReleaseSeconds,pattern.magneticSpeedRetention,pattern.magneticTangentialCarry);
            velocity=Quaternion.FromToRotation(actor.normal,actor.surfaceNormal)*velocity;
            if (contact)
            {
                // Do not let retained tangential momentum carry the body into a
                // curved contact surface while braking. No position teleport.
                float inward=Vector3.Dot(velocity,normal);
                float limit=Mathf.Max(0f,gap-actor.radius-.005f)/Mathf.Max(.0001f,dt);
                if (inward < -limit) velocity += normal*(-limit-inward);
            }
            return velocity;
        }

        public static Vector3 MagneticGlideVelocity(Vector3 incoming, Vector3 normal, float age, float brake, float release, float retention, float tangentialCarry)
        {
            normal.y=0f; normal.Normalize();
            Vector3 planar=new Vector3(incoming.x,0f,incoming.z);
            float inward=Vector3.Dot(planar,normal);
            Vector3 tangent=planar-normal*inward;
            float t=Mathf.Clamp01(age/Mathf.Max(.0001f,brake+release));
            // Linear normal acceleration plus continuing tangent motion describes
            // a parabolic bend, rather than two straight legs joined at a stop.
            float n=age<=brake ? inward*(1f-Mathf.Clamp01(age/Mathf.Max(.0001f,brake)))
                : -inward*Mathf.Max(0f,retention)*Mathf.Clamp01((age-brake)/Mathf.Max(.0001f,release));
            tangent *= Mathf.Lerp(1f,Mathf.Max(0f,tangentialCarry*retention),Mathf.SmoothStep(0f,1f,t));
            return normal*n+tangent+Vector3.up*incoming.y;
        }

        private void RestorePairs(Actor actor)
        {
            for (int i = ignored.Count - 1; i >= 0; i--)
            {
                var pair = ignored[i];
                if (pair.actor != null && pair.actor.attachedRigidbody != actor.body) continue;
                if (pair.actor != null && pair.wall != null) Physics.IgnoreCollision(pair.actor, pair.wall, pair.previous);
                ignored.RemoveAt(i);
            }
        }
        private void Release(Actor actor)
        {
            if (actor.player != null) actor.player.RemoveMovementInfluence(this);
            if (actor.enemy != null) actor.enemy.RemoveMovementInfluence(this);
            RestorePairs(actor);
        }
        private void OnDisable()
        {
            foreach (var actor in actors) Release(actor);
            actors.Clear(); ignored.Clear();
        }
    }
}
