using UnityEngine;

namespace Massive.Resonance
{
    [System.Serializable]
    public sealed class ResonanceCoreVisualSettings
    {
        public bool enabled = true;
        [Range(0f, 3f)] public float strength = 1f;
        [Tooltip("Local pattern units along the arc, not a circular world-space blast.")]
        [Min(.02f)] public float reach = .65f;
        [Range(0f, 1f)] public float compression = .6f;
        [Range(0f, 4f)] public float flare = 1.8f;
        [Range(0f, 3f)] public float pulseStrength = 1f;
        [Min(0f)] public float pulseSpeed = 3f;
        [Min(.02f)] public float pulseWidth = .24f;
        [Min(.05f)] public float recoverySeconds = .9f;
        [Tooltip("World speed for full-strength feedback. Slow/glancing contacts are gentler.")]
        [Min(.1f)] public float referenceSpeed = 8f;
    }

    [System.Serializable]
    public sealed class ResonancePlayerVisualSettings
    {
        public bool enabled = true;
        [Range(0f, 3f)] public float strength = 1f;
        [Min(.02f)] public float reach = .7f;
        [Range(0f, 1f)] public float filamentDiffusion = .65f;
        [Tooltip("Haze displacement in multiples of the local arc half-width. Visual only.")]
        [Range(0f, 3f)] public float wakeStretch = 1.1f;
        [Range(0f, 2f)] public float wakeNoise = .55f;
        [Min(.02f)] public float attackSeconds = .08f;
        [Min(.05f)] public float recoverySeconds = .7f;
        [Min(.1f)] public float referenceSpeed = 6f;
    }

    /// <summary>
    /// Bounded, allocation-free contact presentation on the SAME sampled curve as the shader.
    /// Never writes transforms, velocities, colliders, materials, or grid forces.
    /// Four simultaneous Cores and four player wakes per arc; oldest slot yields at capacity.
    /// </summary>
    public sealed class ResonanceContactVisuals
    {
        public const int Capacity = 4;
        public struct Contact
        {
            public float along, distance, envelope;
            public Vector2 tangent;
        }
        private struct CoreState
        {
            public int key;
            public bool occupied, approach;
            public double touched, impact;
            public float along, strength, bias;
        }
        private struct PlayerState
        {
            public int key;
            public bool occupied;
            public double touched;
            public float along, strength;
            public Vector2 direction;
        }
        private readonly CoreState[] cores = new CoreState[Capacity];
        private readonly PlayerState[] players = new PlayerState[Capacity];
        private readonly Vector4[] coreData = new Vector4[Capacity], playerData = new Vector4[Capacity];
        private readonly Vector4[] path;
        private readonly int count;
        private readonly bool closed;
        public float Length => path[count - 1].w;
        public int ImpactCount { get; private set; }
        public int ActiveCoreCount { get; private set; }
        public int ActivePlayerCount { get; private set; }

        public ResonanceContactVisuals(Vector4[] curve, int curveCount, bool isClosed)
        { path = curve; count = curveCount; closed = isClosed; }

        /// <summary>Clear transient contacts when a pattern leaves play; retained geometry and allocation stay intact.</summary>
        public void Reset()
        {
            System.Array.Clear(cores, 0, Capacity); System.Array.Clear(players, 0, Capacity);
            System.Array.Clear(coreData, 0, Capacity); System.Array.Clear(playerData, 0, Capacity);
            ActiveCoreCount = ActivePlayerCount = ImpactCount = 0;
        }

        // A swept query catches a fast passage without inflating its detection radius.
        // Both arguments are local XZ. Uses finite segments, so a teleport can be excluded by the caller.
        public Contact Nearest(Vector2 from, Vector2 to)
        {
            Contact result = new Contact { distance = float.PositiveInfinity };
            Vector2 travel = to - from;
            for (int i = 0; i < count - 1; i++)
            {
                Vector2 a = new Vector2(path[i].x, path[i].y), b = new Vector2(path[i+1].x, path[i+1].y);
                Vector2 d = b-a;
                float aa = travel.sqrMagnitude, ee = d.sqrMagnitude;
                if (ee < .00000001f) continue;
                Vector2 r = from-a;
                float f = Vector2.Dot(d,r), s = 0f, t;
                if (aa < .00000001f) t = Mathf.Clamp01(f/ee);
                else
                {
                    float c = Vector2.Dot(travel,r), cross = Vector2.Dot(travel,d), denom = aa*ee-cross*cross;
                    s = denom > .00000001f ? Mathf.Clamp01((cross*f-c*ee)/denom) : 0f;
                    t = (cross*s+f)/ee;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c/aa); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((cross-c)/aa); }
                }
                float distance = Vector2.Distance(from+travel*s,a+d*t);
                if (distance >= result.distance) continue;
                result = new Contact { distance = distance, along = Mathf.Lerp(path[i].w,path[i+1].w,t),
                    envelope = Mathf.Lerp(path[i].z,path[i+1].z,t), tangent = d.normalized };
            }
            return result;
        }

        public static float ImpactStrength(Vector2 velocity, Vector2 tangent, float referenceSpeed)
        {
            float speed = velocity.magnitude;
            float bias = speed > .001f ? Vector2.Dot(velocity/speed,tangent) : 0f;
            return Mathf.Lerp(.2f,1f,Mathf.Clamp01(speed/Mathf.Max(.1f,referenceSpeed)))
                * Mathf.Lerp(.45f,1f,Mathf.Sqrt(Mathf.Max(0f,1f-bias*bias)));
        }

        private int CoreSlot(int key)
        {
            int oldest = 0;
            for (int i=0; i<Capacity; i++) if (cores[i].occupied && cores[i].key==key) return i;
            for (int i=0; i<Capacity; i++)
            { if (!cores[i].occupied) return i; if (cores[i].touched<cores[oldest].touched) oldest=i; }
            return oldest;
        }
        public void Approach(int key, Contact contact, Vector2 velocity, float strength, float progress, double now)
        {
            int slot=CoreSlot(key);
            var old=cores[slot];
            cores[slot]=new CoreState { occupied=true, key=key, approach=true, touched=now,
                impact=old.occupied && old.key==key ? old.impact : double.NegativeInfinity,
                along=contact.along, strength=strength*Mathf.Lerp(.15f,1f,Mathf.Clamp01(progress)),
                bias=Vector2.Dot(velocity.normalized,contact.tangent) };
        }
        public bool Impact(int key, Contact contact, Vector2 velocity, float strength, double now, float cooldown)
        {
            int slot=CoreSlot(key);
            if (cores[slot].occupied && cores[slot].key==key && now-cores[slot].impact<Mathf.Max(.03f,cooldown)) return false;
            cores[slot]=new CoreState { occupied=true, key=key, touched=now, impact=now,
                along=contact.along, strength=strength, bias=Vector2.Dot(velocity.normalized,contact.tangent) };
            ImpactCount++;
            return true;
        }
        public void Passage(int key, Contact contact, Vector2 velocity, float strength, double now, float dt,
            ResonancePlayerVisualSettings settings)
        {
            int slot=-1, oldest=0;
            for (int i=0;i<Capacity;i++)
            {
                if (players[i].occupied && players[i].key==key) { slot=i; break; }
                if (players[i].touched<players[oldest].touched) oldest=i;
            }
            if (slot<0)
            {
                slot=oldest;
                for (int i=0;i<Capacity;i++) if (!players[i].occupied) { slot=i; break; }
                players[slot]=new PlayerState { occupied=true, key=key, touched=now };
            }
            var p=players[slot];
            // Recover from the last touch, rather than repeatedly starting a new flash in sludge.
            float previous=p.strength*Recovery((float)(now-p.touched),settings.recoverySeconds);
            p.strength=Mathf.Lerp(previous,strength,1f-Mathf.Exp(-Mathf.Max(.001f,dt)/Mathf.Max(.02f,settings.attackSeconds)));
            p.along=contact.along; p.touched=now;
            Vector2 normal=new Vector2(-contact.tangent.y,contact.tangent.x);
            if (velocity.sqrMagnitude>.0025f)
                p.direction=new Vector2(Vector2.Dot(velocity.normalized,contact.tangent),Vector2.Dot(velocity.normalized,normal));
            players[slot]=p;
        }
        public static float Recovery(float age, float duration)
        { float t=Mathf.Clamp01(age/Mathf.Max(.05f,duration)); return 1f-t*t*t*(t*(t*6f-15f)+10f); }

        public void Apply(MaterialPropertyBlock block, double now, ResonanceCoreVisualSettings core, ResonancePlayerVisualSettings player)
        {
            ActiveCoreCount=ActivePlayerCount=0;
            for (int i=0;i<Capacity;i++)
            {
                var c=cores[i]; float age=(float)(now-c.touched);
                coreData[i]=Vector4.zero;
                if (core.enabled && c.occupied && age<(c.approach ? .16f : core.recoverySeconds))
                {
                    coreData[ActiveCoreCount++]=new Vector4(c.along,c.approach ? -1f : Mathf.Max(0f,age),
                        c.strength*core.strength*(c.approach ? Recovery(age,.16f) : 1f),c.bias);
                }
            }
            // Clear unused packed entries separately: an active entry can move into an earlier slot.
            for (int i=ActiveCoreCount;i<Capacity;i++) coreData[i]=Vector4.zero;
            for (int i=0;i<Capacity;i++)
            {
                var p=players[i]; float age=(float)(now-p.touched);
                if (!player.enabled || !p.occupied || age>=player.recoverySeconds) continue;
                playerData[ActivePlayerCount++]=new Vector4(p.along,p.strength*player.strength*Recovery(age,player.recoverySeconds),p.direction.x,p.direction.y);
            }
            for (int i=ActivePlayerCount;i<Capacity;i++) playerData[i]=Vector4.zero;
            block.SetInt("_ContactCoreCount",ActiveCoreCount); block.SetInt("_ContactPlayerCount",ActivePlayerCount);
            block.SetVectorArray("_ContactCore",coreData); block.SetVectorArray("_ContactPlayer",playerData);
            block.SetVector("_ContactPath",new Vector4(Length,closed ? 1f : 0f,0f,0f));
            block.SetVector("_CoreContactShape",new Vector4(core.reach,core.compression,core.flare,core.pulseStrength));
            block.SetVector("_CoreContactTiming",new Vector4(core.pulseSpeed,core.pulseWidth,core.recoverySeconds,0f));
            block.SetVector("_PlayerContactShape",new Vector4(player.reach,player.filamentDiffusion,player.wakeStretch,player.wakeNoise));
        }
    }
}
