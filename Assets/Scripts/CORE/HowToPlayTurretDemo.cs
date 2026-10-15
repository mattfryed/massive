using Massive.Enemies;
using Massive.PowerUps;
using UnityEngine;

/// <summary>A visual-only turret loop aimed at the pane's lower wall. Never targets or damages actors.</summary>
[DisallowMultipleComponent]
public sealed class HowToPlayTurretDemo : MonoBehaviour
{
    [SerializeField] private ParticleBeamTurretVisuals visuals;
    [SerializeField] private ParticleAcceleratorBeamVisual beam;
    [SerializeField] private ParticleBeamPlasmaContact contactPlasma, corePlasma;
    [SerializeField] private Transform targetWall;
    [Min(0f), SerializeField] private float sweepHalfWidth = .5f;
    [Min(.05f), SerializeField] private float chargeSeconds = 2f;
    [Min(.05f), SerializeField] private float fireSeconds = 2f;
    [Min(.05f), SerializeField] private float cooldownSeconds = 3f;
    [Min(.001f), SerializeField] private float beamRadius = .1f;
    [Min(.01f), SerializeField] private float beamGrowSeconds = .12f;
    [Min(.01f), SerializeField] private float beamFadeSeconds = .2f;
    [Min(1f), SerializeField] private float openingRadiusMultiplier = 1.7f;
    [Min(.01f), SerializeField] private float openingSettleSeconds = .32f;
    [Min(0f), SerializeField] private float corePlasmaForwardOffset = .1f;

    private float age;
    private Quaternion restingRotation;
    private bool captured;
    private bool presentationHeld;
    private EnemySpawnPresentation spawnPresentation;

    public void SetPresentationHeld(bool held) { presentationHeld = held; }

    private void OnEnable()
    {
        age = 0f;
        spawnPresentation = GetComponentInParent<EnemySpawnPresentation>();
        if (visuals && visuals.firingPivot)
        {
            restingRotation = visuals.firingPivot.localRotation;
            captured = true;
        }
        HideBeam(true);
    }

    private void Update()
    {
        if (presentationHeld || !visuals || !visuals.firingPivot || !beam || !targetWall) return;
        if (spawnPresentation && spawnPresentation.isActiveAndEnabled && !spawnPresentation.IsComplete) return;
        float dt = Time.deltaTime;
        age += dt;
        float charge = Mathf.Max(.05f, chargeSeconds), fire = Mathf.Max(.05f, fireSeconds);
        float loop = Mathf.Repeat(Mathf.Max(0f, age - .6f), charge + fire + Mathf.Max(.05f, cooldownSeconds));
        bool charging = age >= .6f && loop < charge;
        bool firing = loop >= charge && loop < charge + fire;
        float fireAge = loop - charge;
        float sweep = firing ? Mathf.SmoothStep(0f, 1f, fireAge / fire) : (charging ? 0f : 1f);
        var pivot = visuals.firingPivot;
        Vector3 endpoint = targetWall.position + targetWall.right * Mathf.Lerp(-sweepHalfWidth, sweepHalfWidth, sweep);
        endpoint.y = pivot.position.y;
        Vector3 direction = endpoint - pivot.position;
        float length = direction.magnitude;
        direction = length > .001f ? direction / length : transform.forward;
        Quaternion aim = Quaternion.LookRotation(direction, Vector3.up);
        pivot.rotation = Quaternion.Slerp(pivot.rotation, aim, 1f - Mathf.Exp(-dt * 8f));
        // Intersect the aimed axis with the lower wall so the beam always meets its surface.
        Vector3 normal = targetWall.forward;
        float denominator = Vector3.Dot(pivot.forward, normal);
        if (Mathf.Abs(denominator) > .001f)
            length = Mathf.Max(0f, Vector3.Dot(targetWall.position - pivot.position, normal) / denominator);
        endpoint = pivot.position + pivot.forward * length;
        visuals.SetPresentationState(charging, firing, charging ? loop / charge : 0f, endpoint);

        if (firing)
        {
            float rise = Mathf.SmoothStep(0f, 1f, fireAge / Mathf.Max(.01f, beamGrowSeconds));
            float fade = Mathf.SmoothStep(0f, 1f, (fire - fireAge) / Mathf.Max(.01f, beamFadeSeconds));
            float envelope = rise * fade;
            float opening = Mathf.Lerp(openingRadiusMultiplier, 1f, Mathf.SmoothStep(0f, 1f, fireAge / Mathf.Max(.01f, openingSettleSeconds)));
            beam.gameObject.SetActive(true);
            beam.SetClippedPath(pivot.position, pivot.forward, length, length * rise * fade, beamRadius * envelope * opening, fireAge);
            if (contactPlasma)
            {
                if (rise >= .999f && fade >= .999f) contactPlasma.SetContact(endpoint, normal, envelope);
                else contactPlasma.StopEmission();
            }
            if (corePlasma) corePlasma.SetContact(pivot.position + pivot.forward * corePlasmaForwardOffset, pivot.forward, envelope);
        }
        else HideBeam(false);
        if (contactPlasma) contactPlasma.Advance(dt);
        if (corePlasma) corePlasma.Advance(dt);
    }

    private void HideBeam(bool clear)
    {
        if (beam) beam.gameObject.SetActive(false);
        if (contactPlasma) { if (clear) contactPlasma.Clear(); else contactPlasma.StopEmission(); }
        if (corePlasma) { if (clear) corePlasma.Clear(); else corePlasma.StopEmission(); }
    }

    private void OnDisable()
    {
        HideBeam(true);
        if (visuals)
        {
            visuals.ClearPresentationState();
            if (captured && visuals.firingPivot) visuals.firingPivot.localRotation = restingRotation;
        }
        captured = false;
    }
}
