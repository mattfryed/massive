using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NovaStarRumble : MonoBehaviour
{
    [Header("Refs")]
    public NovaStarController star;
    public ParticleSystem coronaPS;
    public ParticleSystem fuzzPS;
    public Transform haloTransform;

    [Header("Rumble Timing")]
    public float rampInSeconds = 0.35f;
    public float rampOutSeconds = 0.25f;

    [Header("Halo Size Pulse")]
    public float haloBaseScaleMul = 1f;
    public float haloPulseAmount = 0.12f;
    public float haloPulseHz = 2.5f;
    public float haloJitterAmount = 0.06f;

    [Header("Emission Ramp (only during rumble)")]
    public float coronaRateMultiplier = 2.0f;
    public float fuzzRateMultiplier = 1.6f;

    [Header("Fuzz Start Speed Pulses")]
    public float fuzzSpeedPulseAmount = 0.35f;
    public float fuzzImpulseRate = 4.5f;
    public float fuzzImpulseAmount = 0.9f;
    public float fuzzImpulseDecay = 0.18f;

    [Header("Corona Start Speed Pulses")]
    public float coronaSpeedPulseAmount = 0.22f;
    public float coronaImpulseRate = 2.0f;
    public float coronaImpulseAmount = 0.55f;
    public float coronaImpulseDecay = 0.22f;

    [Header("Corona Noise Modulation")]
    public float coronaNoiseBaseFrequency = 0.52f;
    public float coronaNoiseBaseScrollSpeed = 0.2f;
    public float coronaNoiseFreqPulseAmount = 0.45f;
    public float coronaNoiseScrollPulseAmount = 0.60f;
    public float coronaNoiseModHz = 1.2f;

    [Header("Randomness")]
    public float perlinHz = 3.0f;
    public float perlinExponent = 2.2f;

    // Cached baseline values (normal gameplay)
    float _coronaBaseRate;
    float _fuzzBaseRate;

    ParticleSystem.MinMaxCurve _coronaBaseStartSpeed;
    ParticleSystem.MinMaxCurve _fuzzBaseStartSpeed;

    float _coronaNoiseBaseFreqCached;
    ParticleSystem.MinMaxCurve _coronaNoiseBaseScrollCached;


    float _coronaNoiseFreqSmoothed;
    float _coronaNoiseScrollSmoothed;

    Vector3 _haloBaseLocalScale;

    // Rumble runtime
    bool _rumbleActive = false;
    float _intensity01 = 0f;

    float _fuzzImpulse = 0f;
    float _coronaImpulse = 0f;

    float _seedA, _seedB, _seedC;

    void Awake()
    {
        if (star == null)
            star = GetComponentInParent<NovaStarController>();

        if (coronaPS != null)
        {
            _coronaBaseRate = coronaPS.emission.rateOverTimeMultiplier;

            var main = coronaPS.main;
            _coronaBaseStartSpeed = main.startSpeed;

            var noise = coronaPS.noise;
            _coronaNoiseBaseFreqCached = noise.frequency;
            _coronaNoiseBaseScrollCached = noise.scrollSpeed;

            // Use current system values as defaults unless you want to override in inspector
            coronaNoiseBaseFrequency = _coronaNoiseBaseFreqCached;
            coronaNoiseBaseScrollSpeed = GetCurveRepresentative(_coronaNoiseBaseScrollCached);

            _coronaNoiseFreqSmoothed = coronaNoiseBaseFrequency;
            _coronaNoiseScrollSmoothed = coronaNoiseBaseScrollSpeed;
        }

        if (fuzzPS != null)
        {
            _fuzzBaseRate = fuzzPS.emission.rateOverTimeMultiplier;
            _fuzzBaseStartSpeed = fuzzPS.main.startSpeed;
        }

        if (haloTransform != null)
            _haloBaseLocalScale = haloTransform.localScale;

        _seedA = Random.value * 1000f;
        _seedB = Random.value * 1000f + 17f;
        _seedC = Random.value * 1000f + 41f;

        _rumbleActive = false;
        _intensity01 = 0f;
        ApplyBaseline(); // do NOT stop/clear PS, just restore baseline parameters
    }

    void OnEnable()
    {
        if (star != null)
        {
            star.OnEntryWindowOpened += HandleEntryOpen;
            star.OnEntryWindowClosed += HandleEntryClosed;
            star.OnBounceTriggered += HandleBounce;
        }
    }

    void OnDisable()
    {
        if (star != null)
        {
            star.OnEntryWindowOpened -= HandleEntryOpen;
            star.OnEntryWindowClosed -= HandleEntryClosed;
            star.OnBounceTriggered -= HandleBounce;
        }
    }

    // Start rumble at entry window open (your countdown begins)
    void HandleEntryOpen()
    {
        _rumbleActive = true;
        StopAllCoroutines();
        StartCoroutine(RampTo(1f, rampInSeconds));
    }

    // End rumble when entry window closes (minigame begins if entrants > 0)
    void HandleEntryClosed(List<PlayerControllerScript> entrants)
    {
        StopAllCoroutines();
        StartCoroutine(RampTo(0f, rampOutSeconds));
    }

    void HandleBounce()
    {
        if (!_rumbleActive && _intensity01 <= 0.01f)
            return;

        StopAllCoroutines();
        StartCoroutine(BouncePop());
    }

    IEnumerator RampTo(float target, float seconds)
    {
        float start = _intensity01;
        float t = 0f;
        seconds = Mathf.Max(0.001f, seconds);

        while (t < seconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / seconds);
            _intensity01 = Mathf.SmoothStep(start, target, u);
            yield return null;
        }

        _intensity01 = target;

        if (_intensity01 <= 0.001f)
        {
            _rumbleActive = false;
            ApplyBaseline();
        }
    }

    IEnumerator BouncePop()
    {
        yield return RampTo(1f, 0.05f);
        yield return RampTo(0f, 0.25f);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (!_rumbleActive && _intensity01 <= 0.001f)
            return;

        UpdateImpulses(dt);

        // Halo size pulse (scale)
        if (haloTransform != null)
        {
            float sin01 = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * haloPulseHz);

            float pn = Mathf.PerlinNoise(_seedA, Time.time * perlinHz);
            pn = Mathf.Pow(pn, perlinExponent);

            float mul =
                haloBaseScaleMul *
                (1f + _intensity01 * haloPulseAmount * sin01) *
                (1f + _intensity01 * haloJitterAmount * pn);

            haloTransform.localScale = _haloBaseLocalScale * mul;
        }

        // Emission ramp (clamped to avoid maxParticles thrash)
        if (coronaPS != null)
        {
            var em = coronaPS.emission;
            float desired = Mathf.Lerp(_coronaBaseRate, _coronaBaseRate * coronaRateMultiplier, _intensity01);
            em.rateOverTimeMultiplier = SafeRateForMaxParticles(coronaPS, desired);
        }

        if (fuzzPS != null)
        {
            var em = fuzzPS.emission;
            float desired = Mathf.Lerp(_fuzzBaseRate, _fuzzBaseRate * fuzzRateMultiplier, _intensity01);
            em.rateOverTimeMultiplier = SafeRateForMaxParticles(fuzzPS, desired);
        }

        // Fuzz start speed pulses (erratic)
        if (fuzzPS != null)
        {
            var main = fuzzPS.main;

            float pn = Mathf.PerlinNoise(_seedB, Time.time * perlinHz);
            pn = Mathf.Pow(pn, perlinExponent);

            float wobble = 1f + _intensity01 * fuzzSpeedPulseAmount * pn;
            float spike  = 1f + _intensity01 * fuzzImpulseAmount * _fuzzImpulse;
            float mult = Mathf.Clamp(wobble * spike, 0.2f, 6f);

            main.startSpeed = MultiplyCurve(_fuzzBaseStartSpeed, mult);
        }

        // Corona start speed + noise modulation
        if (coronaPS != null)
        {
            var main = coronaPS.main;

            float pn = Mathf.PerlinNoise(_seedC, Time.time * (perlinHz * 0.75f));
            pn = Mathf.Pow(pn, perlinExponent);

            float wobble = 1f + _intensity01 * coronaSpeedPulseAmount * pn;
            float spike  = 1f + _intensity01 * coronaImpulseAmount * _coronaImpulse;
            float mult = Mathf.Clamp(wobble * spike, 0.2f, 6f);

            main.startSpeed = MultiplyCurve(_coronaBaseStartSpeed, mult);

            var noise = coronaPS.noise;
            if (noise.enabled)
            {
                float wob = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * coronaNoiseModHz);

                float pn2 = Mathf.PerlinNoise(_seedB + 100f, Time.time * (perlinHz * 0.55f));
                pn2 = Mathf.Pow(pn2, 1.6f);

                float targetFreq =
                    coronaNoiseBaseFrequency *
                    (1f + _intensity01 * coronaNoiseFreqPulseAmount * (wob * 2f - 1f));

                float targetScroll =
                    coronaNoiseBaseScrollSpeed *
                    (1f + _intensity01 * coronaNoiseScrollPulseAmount * (pn2 * 2f - 1f));

                float smooth = 1f - Mathf.Exp(-dt * 10f);
                _coronaNoiseFreqSmoothed   = Mathf.Lerp(_coronaNoiseFreqSmoothed, targetFreq, smooth);
                _coronaNoiseScrollSmoothed = Mathf.Lerp(_coronaNoiseScrollSmoothed, targetScroll, smooth);

                noise.frequency   = Mathf.Max(0.001f, _coronaNoiseFreqSmoothed);
                noise.scrollSpeed = new ParticleSystem.MinMaxCurve(Mathf.Max(0.001f, _coronaNoiseScrollSmoothed));

            }
        }
    }

    // --- Helpers ---

    void UpdateImpulses(float dt)
    {
        _fuzzImpulse = Mathf.Max(0f, _fuzzImpulse - dt / Mathf.Max(0.001f, fuzzImpulseDecay));
        _coronaImpulse = Mathf.Max(0f, _coronaImpulse - dt / Mathf.Max(0.001f, coronaImpulseDecay));

        if (_intensity01 <= 0.01f) return;

        if (Random.value < (fuzzImpulseRate * _intensity01 * dt))
            _fuzzImpulse = 1f;

        if (Random.value < (coronaImpulseRate * _intensity01 * dt))
            _coronaImpulse = 1f;
    }

    float SafeRateForMaxParticles(ParticleSystem ps, float desiredRate)
    {
        if (ps == null) return desiredRate;

        var main = ps.main;
        int maxP = main.maxParticles;

        // MainModule.startLifetime is a MinMaxCurve in Unity 6
        float life = GetCurveRepresentative(main.startLifetime);
        if (life <= 0.001f) life = 1f;

        float capSafeRate = (maxP / life) * 0.95f;
        return Mathf.Min(desiredRate, capSafeRate);
    }

    static float GetCurveRepresentative(ParticleSystem.MinMaxCurve c)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return c.constant;

            case ParticleSystemCurveMode.TwoConstants:
                return c.constantMax;

            case ParticleSystemCurveMode.Curve:
                return Mathf.Max(0.001f, c.curveMultiplier) * c.curve.Evaluate(0.5f);

            case ParticleSystemCurveMode.TwoCurves:
                float a = Mathf.Max(0.001f, c.curveMultiplier) * c.curveMin.Evaluate(0.5f);
                float b = Mathf.Max(0.001f, c.curveMultiplier) * c.curveMax.Evaluate(0.5f);
                return Mathf.Max(a, b);

            default:
                return 1f;
        }
    }

    static ParticleSystem.MinMaxCurve MultiplyCurve(ParticleSystem.MinMaxCurve c, float mult)
    {
        // Preserve the curve mode; multiply constants or the curve multiplier.
        mult = Mathf.Max(0f, mult);

        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant:
                c.constant *= mult;
                return c;

            case ParticleSystemCurveMode.TwoConstants:
                c.constantMin *= mult;
                c.constantMax *= mult;
                return c;

            case ParticleSystemCurveMode.Curve:
            case ParticleSystemCurveMode.TwoCurves:
                c.curveMultiplier *= mult;
                return c;

            default:
                c.constant *= mult;
                return c;
        }
    }

    void ApplyBaseline()
    {
        // Restore baseline settings without stopping the systems
        if (coronaPS != null)
        {
            var em = coronaPS.emission;
            em.rateOverTimeMultiplier = _coronaBaseRate;

            var main = coronaPS.main;
            main.startSpeed = _coronaBaseStartSpeed;

            var noise = coronaPS.noise;
            if (noise.enabled)
            {
                noise.frequency = _coronaNoiseBaseFreqCached;
                noise.scrollSpeed = _coronaNoiseBaseScrollCached;
            }
        }

        if (fuzzPS != null)
        {
            var em = fuzzPS.emission;
            em.rateOverTimeMultiplier = _fuzzBaseRate;

            var main = fuzzPS.main;
            main.startSpeed = _fuzzBaseStartSpeed;
        }

        if (haloTransform != null)
            haloTransform.localScale = _haloBaseLocalScale;

        _fuzzImpulse = 0f;
        _coronaImpulse = 0f;

        _coronaNoiseFreqSmoothed = coronaNoiseBaseFrequency;
        _coronaNoiseScrollSmoothed = coronaNoiseBaseScrollSpeed;
    }
}
