using Massive.Scoring;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Multiplier
{
    /// <summary>
    /// Team goal attraction and capture volume for the Amplifier Core.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed partial class AmplifierGoalCapture : MonoBehaviour
    {
        [SerializeField, Range(1, 2)] private int teamID = 1;
        [SerializeField] private Transform capturePoint;
        [SerializeField, Min(0.1f)] private float attractionRadius = 4.5f;
        [SerializeField, Min(0.05f)] private float captureRadius = 0.85f;
        [SerializeField, Min(0f)] private float pullAcceleration = 24f;
        [SerializeField] private AnimationCurve pullByProximity =
            AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);

        [Header("Goal at maximum multiplier")]
        [SerializeField] private bool repelWhenMaxed = true;
        [SerializeField, Min(0f)] private float repulsionAcceleration = 32f;
        [Tooltip("Minimum outward speed when the Core touches a full goal. Prevents it sitting inside the capture aperture.")]
        [SerializeField, Min(0f)] private float repulsionMinimumSpeed = 3f;
        [SerializeField] private ArenaBoundsFromVectorGrid arenaBounds;

        [Header("Feedback")]
        [SerializeField] private ParticleSystem captureParticles;
        [SerializeField] private AmplifierAuroraBlast auroraBlast;
        [SerializeField] private GridInteractor captureWave;
        [SerializeField] private string captureWaveTag = "Wave";
        [SerializeField] private AmplifierGoalTreatments treatments;

        public void SetTreatments(AmplifierGoalTreatments value) { treatments = value; }

        public int TeamID => teamID;
        public float AttractionRadius => attractionRadius;
        public Transform CapturePoint => capturePoint != null ? capturePoint : transform;
        private readonly List<AmplifierCoreGameplay> _rejectedCores = new List<AmplifierCoreGameplay>(4);

        private void OnDisable() { _rejectedCores.Clear(); }

        private void OnEnable()
        {
            if (arenaBounds == null)
                foreach (var candidate in FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { arenaBounds = candidate; break; }
        }

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
                trigger.isTrigger = true;
        }

        private void OnValidate()
        {
            teamID = Mathf.Clamp(teamID, 1, 2);
            attractionRadius = Mathf.Max(0.1f, attractionRadius);
            captureRadius = Mathf.Clamp(captureRadius, 0.05f, attractionRadius);
            pullAcceleration = Mathf.Max(0f, pullAcceleration);
            repulsionAcceleration = Mathf.Max(0f, repulsionAcceleration);
            repulsionMinimumSpeed = Mathf.Max(0f, repulsionMinimumSpeed);
        }

        private void FixedUpdate()
        {
            MatchScoreService service = MatchScoreService.Instance;
            if (service == null || !service.IsScoringOpen)
            {
                _rejectedCores.Clear();
                return;
            }

            bool capped = service.IsTeamAmplifierMaxed(teamID);
            // Re-arm only after leaving the field of rejection, with a little margin to
            // avoid repeat notices from jitter at the edge or multiple child colliders.
            float rearmDistance = attractionRadius + .5f;
            for (int i = _rejectedCores.Count - 1; i >= 0; i--)
            {
                var previous = _rejectedCores[i];
                if (!capped || previous == null || !previous.isActiveAndEnabled || previous.IsCaptured ||
                    previous.Body == null || previous.gameObject.scene != gameObject.scene)
                { _rejectedCores.RemoveAt(i); continue; }
                Vector3 offset = previous.Body.worldCenterOfMass - CapturePoint.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > rearmDistance * rearmDistance) _rejectedCores.RemoveAt(i);
            }

            var cores = AmplifierCoreGameplay.ActiveCores;
            for (int i = cores.Count - 1; i >= 0; i--)
            {
                AmplifierCoreGameplay core = cores[i];
                if (core == null || core.gameObject.scene != gameObject.scene || core.IsCaptured || core.IsInExternalTransit || core.Body == null)
                    continue;

                Vector3 delta = CapturePoint.position - core.Body.worldCenterOfMass;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance > attractionRadius)
                    continue;

                if (capped)
                {
                    if (Effective_repelWhenMaxed || distance <= captureRadius) NotifyRejection(core, service);
                    if (Effective_repelWhenMaxed) RepelCore(core, distance);
                    continue;
                }

                if (distance <= captureRadius)
                {
                    core.TryCapture(this);
                    continue;
                }

                float proximity = Mathf.InverseLerp(attractionRadius, captureRadius, distance);
                float shaped = Effective_pullByProximity != null
                    ? Mathf.Max(0f, Effective_pullByProximity.Evaluate(proximity))
                    : proximity;
                core.Body.AddForce(
                    delta.normalized * (Effective_pullAcceleration * shaped),
                    ForceMode.Acceleration);
                core.SetAttractionExcitement(proximity);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            AmplifierCoreGameplay core = other.GetComponentInParent<AmplifierCoreGameplay>();
            if (core != null)
            {
                var service = MatchScoreService.Instance;
                if (service != null && service.IsScoringOpen && service.IsTeamAmplifierMaxed(teamID))
                {
                    NotifyRejection(core, service);
                    if (Effective_repelWhenMaxed) RepelCore(core, 0f);
                }
                else core.TryCapture(this);
            }
        }

        private void NotifyRejection(AmplifierCoreGameplay core, MatchScoreService service)
        {
            if (core.IsCaptured || core.IsInExternalTransit || core.Body == null || !core.isActiveAndEnabled ||
                core.gameObject.scene != gameObject.scene || _rejectedCores.Contains(core)) return;
            _rejectedCores.Add(core);
            service.NotifyAmplifierCaptureRejected(teamID);
        }

        private void RepelCore(AmplifierCoreGameplay core, float distance)
        {
            if (core.IsCaptured || core.Body == null || core.Body.isKinematic) return;
            Vector3 inward = arenaBounds != null && arenaBounds.IsValid
                ? arenaBounds.Current.centerWS - CapturePoint.position
                : teamID == 1 ? Vector3.right : Vector3.left;
            inward.y = 0f;
            if (inward.sqrMagnitude < .0001f) inward = teamID == 1 ? Vector3.right : Vector3.left;
            inward.Normalize();
            Vector3 direction = RepulsionDirection(CapturePoint.position, core.Body.worldCenterOfMass, inward);
            float proximity = Mathf.InverseLerp(attractionRadius, captureRadius, distance);
            float shaped = Effective_pullByProximity != null ? Mathf.Max(0f, Effective_pullByProximity.Evaluate(proximity)) : proximity;
            core.Body.AddForce(direction * (Effective_repulsionAcceleration * shaped), ForceMode.Acceleration);
            if (distance <= captureRadius)
            {
                Vector3 velocity = core.Body.linearVelocity;
                float awaySpeed = Vector3.Dot(velocity, direction);
                if (awaySpeed < Effective_repulsionMinimumSpeed)
                    core.Body.linearVelocity = velocity + direction * (Effective_repulsionMinimumSpeed - awaySpeed);
            }
            core.Body.WakeUp();
            core.SetAttractionExcitement(proximity);
        }

        public static Vector3 RepulsionDirection(Vector3 goal, Vector3 core, Vector3 inward)
        {
            inward.y = 0f; inward.Normalize();
            if (inward.sqrMagnitude < .0001f) inward = Vector3.right;
            Vector3 away = core - goal; away.y = 0f;
            if (away.sqrMagnitude < .0001f) return inward;
            away.Normalize();
            // Cores within or behind the aperture must still return to the playable field.
            float forward = Vector3.Dot(away, inward);
            if (forward < .25f) away = (away - inward * forward + inward * .5f).normalized;
            return away;
        }

        public bool TryAdvanceTeamAmplifier()
        {
            MatchScoreService service = MatchScoreService.Instance;
            return service != null && service.AdvanceTeamAmplifier(teamID);
        }

        public void PlayCaptureFeedback()
        {
            if (treatments != null && treatments.isActiveAndEnabled)
            {
                treatments.Capture(teamID);
                if (!treatments.LegacyCaptureFeedback) return;
            }
            if (captureParticles != null)
                captureParticles.Play(true);
            if (auroraBlast != null)
                auroraBlast.Play();
            if (captureWave != null)
                captureWave.Trigger(captureWaveTag);
        }
    }
}
