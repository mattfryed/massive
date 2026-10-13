using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Massive.Emergence
{
    /// <summary>GPU-owned ecology. Runtime readback is one 1,088-byte mailbox, never the particle population.</summary>
    public sealed class EmergenceGpuSimulation : IEmergenceSimulation
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Influence
        {
            public Vector4 wake, drift, pulse, swordCenter, swordX, swordY, swordZ, swordShape, attack;
            public uint active, swordSequence, pulseSequence, shape;
        }
        [StructLayout(LayoutKind.Explicit)]
        struct Bits { [FieldOffset(0)] public uint u; [FieldOffset(0)] public float f; }
        enum Pass { BeginTick, Interactions, ClearCells, CountCells, ScanCells, ScanBlocks, Scatter,
            InitGroups, Connect, Compress, LinkMembers, DescribeGroups, Metabolize, Aggregate, SelectEmission, Forces, Integrate }
        const int MailWords = 16 + 32 * 8;
        readonly EmergenceProfile profile;
        readonly ComputeShader compute;
        readonly Dictionary<string, ComputeBuffer> buffers = new Dictionary<string, ComputeBuffer>();
        readonly int[] kernels = new int[17];
        readonly uint[] mail = new uint[MailWords];
        readonly Queue<EmergenceSimulation.Secretion> pending = new Queue<EmergenceSimulation.Secretion>(32);
        readonly Vector4[] species = new Vector4[EmergenceProfile.SpeciesCount], attraction = new Vector4[EmergenceProfile.SpeciesCount * 2], colors = new Vector4[EmergenceProfile.SpeciesCount];
        readonly Material material;
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly int bodyCount, cellCount, blockCount;
        readonly Action<AsyncGPUReadbackRequest> readbackCompleted;
        AsyncGPUReadbackRequest readback;
        EmergenceSimulation.Particle[] snapshot;
        bool reading, disposed;
        uint tick, receivedSequence, requestedTick = uint.MaxValue;
        float recognitionIn;
        Vector3 origin, axisX = Vector3.right, axisY = Vector3.forward;
        static readonly HashSet<EmergenceGpuSimulation> instances = new HashSet<EmergenceGpuSimulation>();
        public static void DisposeAll() { foreach (var instance in new List<EmergenceGpuSimulation>(instances)) instance.Dispose(); }
        public bool IsDisposed => disposed;
        public string Error { get; private set; }
        public int Count { get; }
        public Vector2 HalfSize { get; }
        public float Age { get; private set; }
        public int BodyCount => (int)mail[7];
        public int ProductiveBodies => (int)mail[8];
        public int Consumed => (int)mail[0];
        public int Replenished => (int)mail[1];
        public int Secreted => (int)mail[2];
        public int WakeContacts => (int)mail[3];
        public int AttackContacts => (int)mail[4];
        public int RepulsorContacts => (int)mail[5];
        public int Disruptions => (int)mail[6];
        public float ExportedMaterial => Float(mail[12]);
        public float StoredMaterial => Consumed - ExportedMaterial;
        public int PendingCount => pending.Count;
        public int EmissionSlots { get; set; } = 32;
        public ComputeBuffer ParticleBuffer => buffers["Particles"];
        public EmergenceSimulation.Particle[] Particles
        {
            get
            {
                if (snapshot == null) snapshot = new EmergenceSimulation.Particle[Count];
                ParticleBuffer.GetData(snapshot); SynchronizeDiagnostics(); return snapshot;
            }
        }
        static float Float(uint value) => new Bits { u = value }.f;
        static class Id
        {
            public static readonly int Count=Shader.PropertyToID("_Count"), Body=Shader.PropertyToID("_BodyCount"),
                Columns=Shader.PropertyToID("_Columns"), Rows=Shader.PropertyToID("_Rows"), Cells=Shader.PropertyToID("_CellCount"),
                Blocks=Shader.PropertyToID("_BlockCount"), CellSize=Shader.PropertyToID("_CellSize"), Half=Shader.PropertyToID("_HalfSize"),
                Seed=Shader.PropertyToID("_Seed"), Tick=Shader.PropertyToID("_Tick"), Dt=Shader.PropertyToID("_Dt"),
                Influences=Shader.PropertyToID("_InfluenceCount"), EmissionLimit=Shader.PropertyToID("_EmissionLimit"),
                Force=Shader.PropertyToID("_ForceSettings"), Core=Shader.PropertyToID("_Core"), Motion=Shader.PropertyToID("_Motion"),
                Ecology=Shader.PropertyToID("_Ecology"), Recognition=Shader.PropertyToID("_Recognition"), Lifecycle=Shader.PropertyToID("_Lifecycle"),
                Metabolism=Shader.PropertyToID("_Metabolism"), Waste=Shader.PropertyToID("_Waste"),
                Motor=Shader.PropertyToID("_Motor"), Species=Shader.PropertyToID("_Species"), Attraction=Shader.PropertyToID("_Attraction"),
                Origin=Shader.PropertyToID("_Origin"), AxisX=Shader.PropertyToID("_AxisX"), AxisY=Shader.PropertyToID("_AxisY"),
                Particles=Shader.PropertyToID("_Particles"), Colors=Shader.PropertyToID("_Colors"),
                WasteColor=Shader.PropertyToID("_WasteColor"), DisruptionColor=Shader.PropertyToID("_DisruptionColor"),
                WasteAt=Shader.PropertyToID("_WasteColorAt"), ShowWaste=Shader.PropertyToID("_ShowWaste"), ShowDisruption=Shader.PropertyToID("_ShowDisruption");
        }
        public EmergenceGpuSimulation(EmergenceProfile settings, Vector2 halfSize)
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback)
                throw new NotSupportedException("GPU ecology requires compute shaders and asynchronous GPU readback. Select CPU Reference for up to 4,000 particles on this device.");
            string invalid = settings ? settings.ConfigurationError() : "Missing ecology profile.";
            if (invalid != null) throw new ArgumentException(invalid);
            profile = settings; Count = settings.ParticleCount; bodyCount = settings.BodyParticleCount;
            HalfSize = new Vector2(Mathf.Max(2, halfSize.x), Mathf.Max(2, halfSize.y));
            // A bin can hold every particle: prefix offsets replace fixed per-bin capacities.
            float cellSize = Mathf.Max((2 * settings.MaximumRadius + settings.particleGap) * settings.coreRangeMultiplier,
                Mathf.Max(settings.interactionRadius, Mathf.Max(settings.connectionRadius, settings.feedingRadius)));
            int columns = Mathf.CeilToInt(HalfSize.x * 2 / cellSize) + 1, rows = Mathf.CeilToInt(HalfSize.y * 2 / cellSize) + 1;
            cellCount = checked(columns * rows); blockCount = (cellCount + 255) / 256;
            if (cellCount > 65536) throw new ArgumentException("Arena/range settings require too many spatial bins. Increase the interaction range or reduce the preview area.");
            var source = Resources.Load<ComputeShader>("EmergenceGpu");
            var shader = Resources.Load<Shader>("EmergenceGpuParticles");
            if (!source || !shader || !shader.isSupported) throw new InvalidOperationException("EMERGENCE GPU compute/render assets are missing or unsupported.");
            compute = Object.Instantiate(source); compute.hideFlags = HideFlags.HideAndDontSave;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            readbackCompleted = Receive;
            try
            {
                Allocate("Particles", Count, 72); Allocate("Sorted", Count, 72); Allocate("Order", Count, 4); Allocate("Indices", Count, 4);
                Allocate("Counts", cellCount, 4); Allocate("Offsets", cellCount, 4); Allocate("Cursors", cellCount, 4); Allocate("BlockSums", blockCount, 4);
                Allocate("Forces", Count, 8); Allocate("Claims", Count, 4); Allocate("Intake", Count, 4);
                Allocate("Parents", bodyCount, 4); Allocate("Heads", bodyCount, 4); Allocate("Next", bodyCount, 4); Allocate("Groups", bodyCount, 48);
                Allocate("Neighbours", bodyCount, 4);
                Allocate("Mail", MailWords, 4); Allocate("Influences", 4, 160); Allocate("Hits", Count * 4, 8);
                ParticleBuffer.SetData(EmergenceParticleSeed.Create(profile, HalfSize));
                buffers["Mail"].SetData(mail); buffers["Hits"].SetData(new uint[Count * 8]);
                Bind(Pass.BeginTick,"Mail"); Bind(Pass.Interactions,"Particles Mail Influences Hits");
                Bind(Pass.ClearCells,"Counts Cursors"); Bind(Pass.CountCells,"ReadParticles Counts Claims Intake");
                Bind(Pass.ScanCells,"ReadCounts Offsets BlockSums"); Bind(Pass.ScanBlocks,"BlockSums");
                Bind(Pass.Scatter,"ReadParticles Cursors ReadOffsets ReadBlockSums Sorted Order Indices");
                Bind(Pass.InitGroups,"ReadParticles ReadSorted ReadOrder ReadCounts ReadOffsets ReadBlockSums Parents Heads Neighbours");
                Bind(Pass.Connect,"ReadParticles ReadSorted ReadOrder ReadCounts ReadOffsets ReadBlockSums ReadNeighbours Parents");
                Bind(Pass.Compress,"Parents"); Bind(Pass.LinkMembers,"ReadParents Heads Next");
                Bind(Pass.DescribeGroups,"ReadHeads ReadNext ReadParticles Groups");
                Bind(Pass.Metabolize,"Particles ReadGroups ReadParents");
                Bind(Pass.Aggregate,"Groups ReadHeads ReadNext ReadParticles Mail");
                Bind(Pass.SelectEmission,"ReadGroups Mail");
                Bind(Pass.Forces,"ReadParticles ReadSorted ReadOrder ReadCounts ReadOffsets ReadBlockSums ReadGroups ReadParents Claims Intake Forces");
                Bind(Pass.Integrate,"Particles ReadClaims ReadIntake ReadForces ReadParents ReadSorted ReadIndices ReadCounts ReadOffsets ReadBlockSums Mail");
                compute.SetInt(Id.Count,Count); compute.SetInt(Id.Body,bodyCount); compute.SetInt(Id.Columns,columns); compute.SetInt(Id.Rows,rows);
                compute.SetInt(Id.Cells,cellCount); compute.SetInt(Id.Blocks,blockCount); compute.SetFloat(Id.CellSize,cellSize);
                compute.SetVector(Id.Half,HalfSize); compute.SetInt(Id.Seed,profile.seed);
                properties.SetBuffer(Id.Particles,ParticleBuffer); SetPlane(Vector3.zero,Vector3.right,Vector3.forward);
                instances.Add(this);
            }
            catch { Dispose(); throw; }
        }
        void Allocate(string name,int count,int stride) => buffers.Add(name,new ComputeBuffer(Mathf.Max(1,count),stride));
        void Bind(Pass pass,string names)
        {
            int kernel=compute.FindKernel(pass.ToString()); kernels[(int)pass]=kernel;
            foreach(string name in names.Split(' ')) compute.SetBuffer(kernel,Shader.PropertyToID("_"+name),buffers[name.StartsWith("Read")?name.Substring(4):name]);
        }
        void Dispatch(Pass pass,int count,int width=128) { if(count>0) compute.Dispatch(kernels[(int)pass],(count+width-1)/width,1,1); }
        void Parameters(float dt)
        {
            compute.SetFloat(Id.Dt,dt);compute.SetInt(Id.Tick,(int)++tick);
            compute.SetVector(Id.Force,new Vector4(profile.interactionRadius,profile.separation,profile.force,profile.repulsion));
            compute.SetVector(Id.Core,new Vector4(profile.particleGap,profile.coreRangeMultiplier,profile.coreRepulsion,profile.coreDamping));
            compute.SetVector(Id.Motion,new Vector4(profile.maximumForce,profile.wallBounce,profile.MaximumRadius,profile.speedLimit));
            compute.SetVector(Id.Ecology,new Vector4(profile.connectionRadius,profile.minimumBody,profile.minimumStructureFraction,profile.minimumMotorFraction));
            compute.SetVector(Id.Recognition,new Vector4(profile.connectionSizeMultiplier,profile.minimumLocalNeighbours,0,0));
            compute.SetVector(Id.Lifecycle,new Vector4(profile.feedingApproachSeconds,profile.particleShrinkSeconds,profile.particleGrowSeconds,0));
            compute.SetVector(Id.Metabolism,new Vector4(profile.maturationSeconds,profile.processingPerSecond,profile.reservePerCatalyst,profile.feedingRadius));
            compute.SetVector(Id.Waste,new Vector4(profile.materialPerNugget,profile.secretionInterval,profile.fieldSecretionInterval,profile.nutrientReturnSeconds));
            compute.SetVector(Id.Motor,new Vector4(profile.unfedMotorResponse,profile.motorFuelPerCatalyst,0,0));
            for(int i=0;i<EmergenceProfile.SpeciesCount;i++)
            {
                var s=profile.species[i];species[i]=new Vector4(s.forceMultiplier,Mathf.Exp(-profile.drag*s.dragMultiplier*dt),profile.speedLimit*s.speedMultiplier,profile.agitation*s.agitationMultiplier);
                // Two float4 slots per row avoid scalar-array packing differences between graphics APIs.
                int row=i*EmergenceProfile.SpeciesCount;
                attraction[i*2]=new Vector4(profile.attraction[row],profile.attraction[row+1],profile.attraction[row+2],profile.attraction[row+3]);
                attraction[i*2+1]=new Vector4(profile.attraction[row+4],0,0,0);
            }
            compute.SetVectorArray(Id.Species,species);compute.SetVectorArray(Id.Attraction,attraction);
        }
        public void SetPlane(Vector3 center,Vector3 x,Vector3 y)
        {
            origin=center;axisX=x;axisY=y;
            compute.SetVector(Id.Origin,center);compute.SetVector(Id.AxisX,x);compute.SetVector(Id.AxisY,y);
        }
        public void SetInfluences(Influence[] values,int count)
        { count=Mathf.Clamp(count,0,4);if(count>0)buffers["Influences"].SetData(values,0,0,count);compute.SetInt(Id.Influences,count); }
        public void Step(float dt,bool allowSecretion=true)
        {
            if(disposed)throw new ObjectDisposedException(nameof(EmergenceGpuSimulation));
            if(Error!=null)throw new InvalidOperationException(Error);
            if(dt<=0||dt>.05f)throw new ArgumentOutOfRangeException(nameof(dt));
            Parameters(dt); Age+=dt;
            int capacity=Mathf.Clamp(EmissionSlots-pending.Count,0,32-pending.Count);
            compute.SetInt(Id.EmissionLimit,allowSecretion?(int)receivedSequence+capacity:0);
            Dispatch(Pass.BeginTick,1,1);Dispatch(Pass.Interactions,Count);
            Dispatch(Pass.ClearCells,cellCount,256);Dispatch(Pass.CountCells,Count);
            Dispatch(Pass.ScanCells,cellCount,256);Dispatch(Pass.ScanBlocks,1,1);Dispatch(Pass.Scatter,Count);
            recognitionIn-=dt;
            if(recognitionIn<=0)
            {
                Dispatch(Pass.InitGroups,bodyCount);Dispatch(Pass.Connect,bodyCount);Dispatch(Pass.Compress,bodyCount);
                Dispatch(Pass.LinkMembers,bodyCount);Dispatch(Pass.DescribeGroups,bodyCount);recognitionIn=profile.recognitionInterval;
            }
            Dispatch(Pass.Metabolize,bodyCount);Dispatch(Pass.Aggregate,bodyCount);Dispatch(Pass.SelectEmission,1,1);
            Dispatch(Pass.Forces,Count);Dispatch(Pass.Integrate,Count);
        }
        public void RequestStatus()
        {
            if(disposed||reading||requestedTick==tick)return;
            requestedTick=tick;reading=true;readback=AsyncGPUReadback.Request(buffers["Mail"],readbackCompleted);
        }
        void Receive(AsyncGPUReadbackRequest request)
        {
            reading=false;if(disposed)return;
            if(request.hasError){Error="GPU ecology status readback failed.";return;}
            var data=request.GetData<uint>();if(data[15]<mail[15])return;
            data.CopyTo(mail);AcceptEvents();
        }
        void AcceptEvents()
        {
            uint total=mail[2];
            if(total-receivedSequence>32){Error="GPU ecology event mailbox overflowed.";return;}
            while(receivedSequence<total)
            {
                int offset=16+(int)(receivedSequence%32)*8;
                if(mail[offset]!=receivedSequence+1){Error="GPU ecology event sequence mismatch.";return;}
                pending.Enqueue(new EmergenceSimulation.Secretion { position=new Vector2(Float(mail[offset+1]),Float(mail[offset+2])),velocity=new Vector2(Float(mail[offset+3]),Float(mail[offset+4])) });
                receivedSequence++;
            }
        }
        public bool TryDequeue(out EmergenceSimulation.Secretion value)
        { if(pending.Count==0){value=default;return false;}value=pending.Dequeue();return true; }
        public void SynchronizeDiagnostics()
        { buffers["Mail"].GetData(mail);AcceptEvents(); }
#if UNITY_EDITOR
        public void SetDiagnosticParticles(EmergenceSimulation.Particle[] values)
        { if(values.Length!=Count)throw new ArgumentException("Snapshot population differs.");ParticleBuffer.SetData(values);recognitionIn=0; }
#endif
        public void Draw(int layer,Camera camera=null)
        {
            if(disposed)return;
            for(int i=0;i<EmergenceProfile.SpeciesCount;i++)colors[i]=profile.species[i].color;
            properties.SetVectorArray(Id.Colors,colors);properties.SetVector(Id.Origin,origin);properties.SetVector(Id.AxisX,axisX);properties.SetVector(Id.AxisY,axisY);
            properties.SetColor(Id.WasteColor,profile.wasteColor);properties.SetColor(Id.DisruptionColor,profile.disruptionColor);
            properties.SetFloat(Id.WasteAt,profile.wasteColorAt);properties.SetInt(Id.ShowWaste,profile.showWasteColor?1:0);properties.SetInt(Id.ShowDisruption,profile.showDisruptionColor?1:0);
            Vector3 extent=new Vector3(Mathf.Abs(axisX.x)*HalfSize.x+Mathf.Abs(axisY.x)*HalfSize.y,
                Mathf.Abs(axisX.y)*HalfSize.x+Mathf.Abs(axisY.y)*HalfSize.y,Mathf.Abs(axisX.z)*HalfSize.x+Mathf.Abs(axisY.z)*HalfSize.y)+Vector3.one;
            Graphics.DrawProcedural(material,new Bounds(origin,extent*2),MeshTopology.Triangles,6,Count,camera,properties,ShadowCastingMode.Off,false,layer);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;instances.Remove(this);
            // Waiting is confined to teardown; no normal simulation/render update synchronizes with the GPU.
            if(reading)readback.WaitForCompletion();
            foreach(var buffer in buffers.Values)buffer.Release();buffers.Clear();
            if(Application.isPlaying){Object.Destroy(compute);Object.Destroy(material);}else{Object.DestroyImmediate(compute);Object.DestroyImmediate(material);}
        }
    }
}
