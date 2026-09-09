using System.Collections.Generic;
using UnityEngine;

namespace Massive.Resonance
{
    public enum ResonanceRendering { OptionAContinuous, OptionBParticles }

    [System.Serializable]
    public sealed class ResonanceParticlePopulation
    {
        [Min(0f)] public float particlesPerUnit = 180f;
        [Min(.001f)] public float dotSize = .025f;
        [Range(0f,1f)] public float sizeVariation = .45f;
        [Min(0f)] public float brightness = 1.5f;
    }

    [System.Serializable]
    public sealed class ResonanceParticleSettings
    {
        [Tooltip("Hard combined cap, including the hidden full-pattern ghost. No per-frame emit/destroy churn.")]
        [Range(256,60000)] public int particleBudget = 48000;
        public int seed = 345;
        public ResonanceParticlePopulation filament = new ResonanceParticlePopulation { particlesPerUnit=400f, dotSize=.035f, brightness=2f };
        public ResonanceParticlePopulation ribbon = new ResonanceParticlePopulation { particlesPerUnit=300f, dotSize=.033f, brightness=2.3f };
        public ResonanceParticlePopulation diffuse = new ResonanceParticlePopulation { particlesPerUnit=220f, dotSize=.03f, brightness=3.5f };
        public ResonanceParticlePopulation fullPattern = new ResonanceParticlePopulation { particlesPerUnit=80f, dotSize=.03f, brightness=1.8f };
        public ResonanceParticlePopulation center = new ResonanceParticlePopulation { particlesPerUnit=90f, dotSize=.023f };
        [Tooltip("Logical particle lifetime, animated on the GPU over a fixed Particle System pool.")]
        [Min(.05f)] public float lifetime = 1.2f;
        [Range(0f,.9f)] public float lifetimeVariation = .45f;
        [Tooltip("Fraction of each lifetime the grain exists. Lower values leave more gaps between appearances.")]
        [Range(.05f,1f)] public float presence = .85f;
        [Tooltip("Fraction of the visible lifetime spent fading at each end. Small values pop; large values breathe.")]
        [Range(.005f,.45f)] public float birthDeathFeather = .12f;
        [Tooltip("Local-unit wandering of grains, independent of the original plasma displacement.")]
        [Range(0f,.2f)] public float grainWander = .022f;
        [Min(0f)] public float grainMotionSpeed = 1.3f;
        [Range(.02f,1f)] public float dotEdgeSoftness = .35f;
        public ResonanceParticlePopulation Population(int layer)
        { return layer==0 ? filament : layer==1 ? ribbon : layer==2 ? diffuse : layer==3 ? fullPattern : center; }
    }

    /// <summary>One reusable Unity Particle System population. GPU field sampling owns visibility,
    /// grain wandering and lifetime; the pattern remains the only collision/grid authority.</summary>
    public sealed class ResonanceParticleLayer
    {
        private readonly Transform owner;
        private readonly Vector4[] path;
        private readonly int pathCount, layer;
        private readonly Color color;
        private readonly float halfWidth, surfaceHeight, centerRadius;
        private readonly int seed;
        private Bounds idleBounds;
        public ParticleSystem System { get; private set; }
        public ParticleSystemRenderer Renderer { get; private set; }
        public int Count { get; private set; }
        public int Layer => layer;
        public float Length => pathCount>1 ? path[pathCount-1].w : centerRadius*8f;

        public ResonanceParticleLayer(Transform parent, Vector4[] curve, int count, int visualLayer,
            Color arcColor, float width, float height, int randomSeed, float radius=0f)
        {
            owner=parent; path=curve; pathCount=count; layer=visualLayer; color=arcColor;
            halfWidth=width; surfaceHeight=height; seed=randomSeed; centerRadius=radius;
        }

        public int DesiredCount(ResonanceParticleSettings settings)
        { return Mathf.Clamp(Mathf.CeilToInt(Length*Mathf.Max(0f,settings.Population(layer).particlesPerUnit)),0,60000); }

        public void Build(int capacity, Material material, ResonanceParticleSettings settings)
        {
            Count=Mathf.Max(0,capacity);
            if (Count==0) return;
            string label=layer==0 ? "Filament grains" : layer==1 ? "Ribbon grains" : layer==2 ? "Diffuse grains" : layer==3 ? "Full pattern grains" : "Center grains";
            var go=new GameObject(label) { hideFlags=HideFlags.DontSave, layer=owner.gameObject.layer };
            go.transform.SetParent(owner,false);
            System=go.AddComponent<ParticleSystem>();
            System.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=System.main; main.playOnAwake=false; main.loop=false; main.maxParticles=Count;
            main.simulationSpace=ParticleSystemSimulationSpace.Local; main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.startSpeed=0f; main.startLifetime=100000f; main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=System.emission; emission.enabled=false;
            var shape=System.shape; shape.enabled=false;
            var collision=System.collision; collision.enabled=false;
            var custom=System.customData; custom.enabled=false; // Explicit data must not be overwritten by a module.
            Renderer=System.GetComponent<ParticleSystemRenderer>();
            Renderer.sharedMaterial=material; Renderer.renderMode=ParticleSystemRenderMode.Billboard;
            Renderer.alignment=ParticleSystemRenderSpace.View; Renderer.minParticleSize=0f; Renderer.maxParticleSize=.1f;
            Renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; Renderer.receiveShadows=false;
            // UV + UV2 fill TEXCOORD0; Custom1/2 are then full TEXCOORD1/2 vectors.
            Renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
                ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.Custom1XYZW,ParticleSystemVertexStream.Custom2XYZW });
            var particles=new ParticleSystem.Particle[Count];
            var data1=new List<Vector4>(Count); var data2=new List<Vector4>(Count);
            var random=new System.Random(seed);
            var population=settings.Population(layer);
            var bounds=new Bounds(Vector3.up*surfaceHeight,Vector3.one*.01f);
            for (int i=0;i<Count;i++)
            {
                float fraction=(i+(float)random.NextDouble())/Count;
                float along=fraction*Length, envelope=1f;
                Vector2 point, normal;
                if (layer==4)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2f;
                    point=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Mathf.Sqrt(fraction)*centerRadius;
                    normal=point.sqrMagnitude>.000001f ? point.normalized : Vector2.up;
                }
                else Sample(path,pathCount,along,out point,out normal,out envelope);
                float size=population.dotSize*Mathf.Lerp(1f-population.sizeVariation,1f+population.sizeVariation,(float)random.NextDouble());
                Vector3 position=new Vector3(point.x,surfaceHeight,point.y);
                particles[i]=new ParticleSystem.Particle { position=position, startColor=color,
                    startSize=Mathf.Max(.001f,size), startLifetime=100000f, remainingLifetime=100000f, randomSeed=(uint)(i+1) };
                // Initial particles sit on the centerline. Vertex displacement distributes grains across each layer.
                data1.Add(new Vector4(along,envelope,0f,(float)random.NextDouble()*1000f));
                data2.Add(new Vector4(point.x,point.y,normal.x,normal.y));
                bounds.Encapsulate(position);
            }
            // Cover the widest legal layer + contact wake + optional wandering (visual only).
            bounds.Expand(halfWidth*28f+settings.grainWander*4f+population.dotSize*2f);
            idleBounds=bounds;
            Renderer.localBounds=bounds;
            // A stopped, newly-created system ignores SetParticles until its native simulation
            // is initialized (including Edit Mode). Freeze it before assigning the static pool.
            System.Play(true);
            System.Pause(true);
            System.SetParticles(particles,Count);
            System.SetCustomParticleData(data1,ParticleSystemCustomData.Custom1);
            System.SetCustomParticleData(data2,ParticleSystemCustomData.Custom2);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // In Edit Mode, a paused pool can have real billboard geometry but zero
                // native custom vertex channels until its simulation buffers initialize.
                // Zero-time simulation initializes those buffers without aging/moving grains;
                // then restore the exact authored pool and streams. No per-frame uploads.
                System.Simulate(0f,false,false,false);
                System.SetParticles(particles,Count);
                System.SetCustomParticleData(data1,ParticleSystemCustomData.Custom1);
                System.SetCustomParticleData(data2,ParticleSystemCustomData.Custom2);
                System.Pause(false);
            }
#endif
        }

        /// <summary>GPU motion is invisible to Unity's automatic particle bounds. Keep both
        /// the full formation rectangle and the original idle envelope inside the culling bound.</summary>
        public void SetDispersalBounds(Vector4 area, float vibration, Vector4 localSettings = default)
        {
            if (Renderer==null) return;
            var bounds=idleBounds;
            if (localSettings.x>.5f)
            {
                // The local ellipse is bounded by its radius; coherent/random tremor is
                // bounded by sqrt(2) * amplitude. Idle bounds already include shader motion.
                float reach=Mathf.Max(0f,localSettings.y)+Mathf.Max(0f,vibration)*1.5f;
                bounds.Expand(new Vector3(reach*2f,0f,reach*2f));
            }
            else if (area.z>0f && area.w>0f)
            {
                float margin=Mathf.Max(0f,vibration)*1.5f+.2f;
                bounds.Encapsulate(new Vector3(area.x-area.z-margin,surfaceHeight-margin,area.y-area.w-margin));
                bounds.Encapsulate(new Vector3(area.x+area.z+margin,surfaceHeight+margin,area.y+area.w+margin));
            }
            Renderer.localBounds=bounds;
        }

        public void Apply(MaterialPropertyBlock block, ResonanceParticleSettings settings, double clock)
        {
            if (Renderer==null) return;
            var population=settings.Population(layer);
            block.SetFloat("_ParticleLayer",layer);
            block.SetFloat("_ParticleClock",(float)(clock%10000));
            block.SetVector("_ParticleLife",new Vector4(settings.lifetime,settings.lifetimeVariation,settings.presence,settings.birthDeathFeather));
            block.SetVector("_ParticleLook",new Vector4(settings.grainWander,settings.grainMotionSpeed,settings.dotEdgeSoftness,population.brightness));
            block.SetFloat("_ParticleHeight",surfaceHeight);
            block.SetMatrix("_ParticleLocalToWorld",owner.localToWorldMatrix);
        }

        public static void Sample(Vector4[] curve,int count,float along,out Vector2 point,out Vector2 normal,out float envelope)
        {
            int low=0, high=count-1;
            while (high-low>1) { int mid=(low+high)/2; if(curve[mid].w<along)low=mid;else high=mid; }
            Vector4 a=curve[low], b=curve[high];
            float t=Mathf.Clamp01((along-a.w)/Mathf.Max(.000001f,b.w-a.w));
            point=Vector2.Lerp(new Vector2(a.x,a.y),new Vector2(b.x,b.y),t);
            Vector2 tangent=new Vector2(b.x-a.x,b.y-a.y).normalized;
            normal=new Vector2(-tangent.y,tangent.x); envelope=Mathf.Lerp(a.z,b.z,t);
        }
    }
}
