using Massive.Scoring;
using UnityEngine;

namespace Massive.Multiplier
{
    /// <summary>
    /// Team goal attraction and capture volume for the Amplifier Core.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AmplifierGoalCapture : MonoBehaviour
    {
        [SerializeField, Range(1, 2)] private int teamID = 1;
        [SerializeField] private Transform capturePoint;
        [SerializeField, Min(0.1f)] private float attractionRadius = 4.5f;
        [SerializeField, Min(0.05f)] private float captureRadius = 0.85f;
        [SerializeField, Min(0f)] private float pullAcceleration = 24f;
        [SerializeField] private AnimationCurve pullByProximity =
            AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);

        [Header("Feedback")]
        [SerializeField] private ParticleSystem captureParticles;
        [SerializeField] private AmplifierAuroraBlast auroraBlast;
        [SerializeField] private GridInteractor captureWave;
        [SerializeField] private string captureWaveTag = "Wave";

        public int TeamID => teamID;
        public Transform CapturePoint => capturePoint != null ? capturePoint : transform;

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
        }

        private void FixedUpdate()
        {
            MatchScoreService service = MatchScoreService.Instance;
            if (service == null || !service.IsScoringOpen)
                return;

            var cores = AmplifierCoreGameplay.ActiveCores;
            for (int i = cores.Count - 1; i >= 0; i--)
            {
                AmplifierCoreGameplay core = cores[i];
                if (core == null || core.IsCaptured || core.Body == null)
                    continue;

                Vector3 delta = CapturePoint.position - core.Body.worldCenterOfMass;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance > attractionRadius)
                    continue;

                if (distance <= captureRadius)
                {
                    core.TryCapture(this);
                    continue;
                }

                float proximity = Mathf.InverseLerp(attractionRadius, captureRadius, distance);
                float shaped = pullByProximity != null
                    ? Mathf.Max(0f, pullByProximity.Evaluate(proximity))
                    : proximity;
                core.Body.AddForce(
                    delta.normalized * (pullAcceleration * shaped),
                    ForceMode.Acceleration);
                core.SetAttractionExcitement(proximity);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            AmplifierCoreGameplay core = other.GetComponentInParent<AmplifierCoreGameplay>();
            if (core != null)
                core.TryCapture(this);
        }

        public bool TryAdvanceTeamAmplifier()
        {
            MatchScoreService service = MatchScoreService.Instance;
            return service != null && service.AdvanceTeamAmplifier(teamID);
        }

        public void PlayCaptureFeedback()
        {
            if (captureParticles != null)
                captureParticles.Play(true);
            if (auroraBlast != null)
                auroraBlast.Play();
            if (captureWave != null)
                captureWave.Trigger(captureWaveTag);
        }
    }
}
