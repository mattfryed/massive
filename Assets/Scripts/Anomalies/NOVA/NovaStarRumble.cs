using System;
using System.Collections;
using UnityEngine;

/// <summary>Visual-only growth. Authored particle values and scale are stage four.</summary>
public class NovaStarRumble : MonoBehaviour
{
    [Serializable]
    public struct StageLook
    {
        public float size, emission, speed, turbulence;
        public StageLook(float size, float emission, float speed, float turbulence)
        { this.size=size; this.emission=emission; this.speed=speed; this.turbulence=turbulence; }
    }
    public NovaStarController star;
    public ParticleSystem coronaPS;
    public ParticleSystem fuzzPS;
    public Transform visualRoot;
    public StageLook[] stages = {
        new StageLook(.42f,.22f,.3f,.25f), new StageLook(.60f,.42f,.5f,.45f),
        new StageLook(.80f,.68f,.75f,.7f), new StageLook(1f,1f,1f,1f) };
    [Min(0f)] public float shakeDistance = .035f;
    [Min(0f)] public float pulseSize = .15f;
    [Header("Pulse telegraph audio")]
    public AudioSource telegraphSource;
    public AudioClip telegraphClip;
    private ParticleSystem[] systems;
    private float[] rates, speeds, noiseStrengths, noiseScrolls;
    private Vector3 baseScale, basePosition;
    private Vector3[] particleLocalScales;
    private StageLook current;
    private bool cached;

    private void Awake() => Cache();
    private void Cache()
    {
        if (cached || !visualRoot) return;
        cached = true;
        baseScale = visualRoot.localScale; basePosition = visualRoot.localPosition;
        systems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
        particleLocalScales = new Vector3[systems.Length];
        rates = new float[systems.Length]; speeds = new float[systems.Length];
        noiseStrengths = new float[systems.Length]; noiseScrolls = new float[systems.Length];
        for (int i=0;i<systems.Length;i++)
        {
            particleLocalScales[i]=systems[i].transform.localScale;
            rates[i]=systems[i].emission.rateOverTimeMultiplier;
            speeds[i]=systems[i].main.startSpeedMultiplier;
            noiseStrengths[i]=systems[i].noise.strengthMultiplier;
            noiseScrolls[i]=systems[i].noise.scrollSpeedMultiplier;
        }
    }
    public void SetStage(int stage)
    {
        Cache();
        if (!cached) return;
        current = Look(stage);
        Apply(current);
    }
    private StageLook Look(int stage) => stages != null && stages.Length >= 4
        ? stages[Mathf.Clamp(stage-1,0,3)] : new StageLook(1,1,1,1);

    public IEnumerator WindUp(int stage, float seconds)
    {
        if (telegraphSource && telegraphClip)
        {
            telegraphSource.clip = telegraphClip;
            telegraphSource.pitch = 1f - (stage - 2) * .12f;
            telegraphSource.volume = .2f + stage * .04f;
            telegraphSource.Play();
        }
        float t=0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float u=Mathf.Clamp01(t / Mathf.Max(.001f,seconds));
            Apply(current);
            if (visualRoot)
            {
                float strength=stage/4f;
                visualRoot.localPosition=basePosition + new Vector3(Mathf.Sin(u*73f),0,Mathf.Sin(u*91f)) * (shakeDistance*strength*u);
                visualRoot.localScale *= 1f + pulseSize*strength*Mathf.Sin(u*Mathf.PI);
            }
            yield return null;
        }
    }
    public IEnumerator GrowTo(int stage, float seconds)
    {
        var from=current; var to=Look(stage);
        float t=0f;
        while(t<seconds)
        {
            t+=Time.unscaledDeltaTime;
            float u=Mathf.SmoothStep(0f,1f,t/Mathf.Max(.001f,seconds));
            Apply(new StageLook(Mathf.Lerp(from.size,to.size,u),Mathf.Lerp(from.emission,to.emission,u),
                Mathf.Lerp(from.speed,to.speed,u),Mathf.Lerp(from.turbulence,to.turbulence,u)));
            yield return null;
        }
        SetStage(stage);
    }
    private void Apply(StageLook look)
    {
        if (!cached) return;
        visualRoot.localPosition=basePosition;
        visualRoot.localScale=baseScale * look.size;
        for(int i=0;i<systems.Length;i++)
        {
            if (!systems[i]) continue;
            // Local scaling mode ignores the visual parent, so scale that system explicitly.
            if (systems[i].main.scalingMode == ParticleSystemScalingMode.Local)
                systems[i].transform.localScale = particleLocalScales[i] * look.size;
            var emission=systems[i].emission; emission.rateOverTimeMultiplier=rates[i]*look.emission;
            var main=systems[i].main; main.startSpeedMultiplier=speeds[i]*look.speed;
            var noise=systems[i].noise;
            noise.strengthMultiplier=noiseStrengths[i]*look.turbulence;
            noise.scrollSpeedMultiplier=noiseScrolls[i]*look.turbulence;
        }
    }
    private void OnDisable()
    {
        if(telegraphSource) telegraphSource.Stop();
        if(cached) Apply(new StageLook(1,1,1,1));
    }
}
