using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    public static class EmergenceGpuValidation
    {
        const string Folder="Library/EmergenceValidation";
        static readonly List<string> checks=new List<string>();
        static void Check(bool ok,string label) { if(!ok)throw new InvalidOperationException(label);checks.Add("PASS "+label); }
        static EmergenceProfile Profile(int structure,int motor,int catalyst,int nutrient)
        {
            var p=ScriptableObject.CreateInstance<EmergenceProfile>();p.backend=EmergenceBackend.GPU;
            int[] counts={structure,motor,catalyst,nutrient};
            for(int i=0;i<4;i++){p.species[i].count=counts[i];p.species[i].diameter=.03f;p.species[i].sizeVariation=0;}
            p.initialCoverage=1;p.initialSpeed=p.agitation=0;return p;
        }
        [MenuItem("MASSIVE/EMERGENCE/Validate GPU Model")]
        public static string RunCore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Run model checks in Edit Mode.");
            checks.Clear();Directory.CreateDirectory(Folder);
            try
            {
                var p=Profile(24,8,4,12);
                try
                {
                    p.maturationSeconds=100;
                    var cpu=new EmergenceSimulation(p,new Vector2(4,4),p.seed);
                    using(var gpu=new EmergenceGpuSimulation(p,new Vector2(4,4)))
                    {
                        gpu.SetDiagnosticParticles(cpu.Particles);
                        for(int i=0;i<12;i++){cpu.Step(1f/60);gpu.Step(1f/60);}
                        var actual=gpu.Particles;float maxError=actual.Select((v,i)=>Vector2.Distance(v.position,cpu.Particles[i].position)).Max();
                        Check(maxError<.0001f,"CPU/GPU force and integration agreement over 12 ticks (max position error "+maxError+")");
                        Check(actual.All(v=>v.Alive && !float.IsNaN(v.velocity.x)),"GPU particles remain finite and alive");
                        Check(gpu.Secreted==0,"immature bodies cannot create nuggets");
                    }
                }
                finally{Object.DestroyImmediate(p);}

                p=Profile(2,0,0,0);
                try
                {
                    p.force=p.repulsion=p.coreDamping=0;
                    foreach(int hz in new[]{30,60,120})
                        using(var gpu=new EmergenceGpuSimulation(p,new Vector2(4,4)))
                        {
                            var state=gpu.Particles;state[0].position=state[1].position=Vector2.zero;gpu.SetDiagnosticParticles(state);
                            for(int i=0;i<hz;i++)gpu.Step(1f/hz,false);
                            state=gpu.Particles;Check(Vector2.Distance(state[0].position,state[1].position)>.03f,"coincident particles separate through GPU forces at "+hz+" Hz");
                        }
                }
                finally{Object.DestroyImmediate(p);}

                p=Profile(510,1,1,0);
                try
                {
                    // Exercise union connectivity independently of the normal density/link-size gates.
                    p.minimumLocalNeighbours=0;p.connectionSizeMultiplier=100;
                    p.minimumBody=3;p.minimumMotorFraction=0;p.minimumStructureFraction=0;p.force=p.repulsion=p.coreRepulsion=0;
                    using(var gpu=new EmergenceGpuSimulation(p,new Vector2(64,4)))
                    {
                        var state=gpu.Particles;for(int i=0;i<state.Length;i++)state[i].position=new Vector2((i-256)*.2f,0);
                        gpu.SetDiagnosticParticles(state);gpu.Step(1f/60,false);gpu.SynchronizeDiagnostics();
                        Check(gpu.BodyCount==1,"GPU recognition connects a 512-particle chain into one body");
                    }
                }
                finally{Object.DestroyImmediate(p);}

                p=Profile(0,0,0,4096);
                try
                {
                    p.force=p.repulsion=p.coreRepulsion=p.coreDamping=0;
                    using(var gpu=new EmergenceGpuSimulation(p,new Vector2(4,4)))
                    {
                        var state=gpu.Particles;for(int i=0;i<state.Length;i++)state[i].position=Vector2.zero;
                        gpu.SetDiagnosticParticles(state);gpu.Step(1f/60);state=gpu.Particles;
                        Check(state.Length==4096 && state.All(v=>v.position==Vector2.zero && v.Alive),"one spatial bin retains all 4,096 particles without overflow");
                        Check(gpu.BodyCount==0 && gpu.Consumed==0 && gpu.Secreted==0,"nutrient-only population does not create organisms or rewards");
                    }
                }
                finally{Object.DestroyImmediate(p);}

                p=Profile(1,1,4,32);
                try
                {
                    p.minimumBody=3;p.minimumStructureFraction=0;p.minimumMotorFraction=0;
                    p.force=p.repulsion=p.coreRepulsion=p.coreDamping=0;p.maturationSeconds=.1f;
                    p.processingPerSecond=6;p.materialPerNugget=2;p.secretionInterval=.02f;p.fieldSecretionInterval=.02f;p.nutrientReturnSeconds=50;
                    using(var gpu=new EmergenceGpuSimulation(p,new Vector2(4,4)))
                    {
                        var state=gpu.Particles;for(int i=0;i<state.Length;i++)state[i].position=new Vector2((i%8)*.015f,(i/8)*.015f);
                        gpu.SetDiagnosticParticles(state);gpu.EmissionSlots=2;
                        for(int i=0;i<240;i++)gpu.Step(1f/60);
                        state=gpu.Particles;
                        Check(gpu.Consumed>0 && gpu.Consumed<=32,"competing catalysts consume each nutrient at most once");
                        Check(gpu.Consumed==state.Count(v=>v.species==3&&!v.Alive),"consumption counter matches depleted nutrients");
                        float stored=state.Sum(v=>v.reserve+v.waste);
                        Check(Mathf.Abs(gpu.Consumed-stored-gpu.ExportedMaterial)<.002f,"GPU feeding/storage/waste/export conserves material");
                        Check(gpu.Secreted==2 && gpu.PendingCount==2,"nugget capacity credits stop production with two uncollected events");
                        int events=0;while(gpu.TryDequeue(out var e)){events++;Check(!float.IsNaN(e.position.x),"emission position is finite");}
                        gpu.SynchronizeDiagnostics();Check(events==2 && gpu.PendingCount==0,"reading the mailbox again does not duplicate events");
                        for(int i=0;i<30;i++)gpu.Step(1f/60);gpu.SynchronizeDiagnostics();
                        Check(gpu.Secreted==4 && gpu.PendingCount==2,"acknowledged capacity permits exactly two further emissions");
                    }
                }
                finally{Object.DestroyImmediate(p);}
                string report="PASSED ("+checks.Count+" checks)\n"+string.Join("\n",checks);
                File.WriteAllText(Folder+"/gpu-model.txt",report);return report;
            }
            catch(Exception e){File.WriteAllText(Folder+"/gpu-model.txt","FAILED\n"+string.Join("\n",checks)+"\n"+e);throw;}
        }

        // Includes command submission and waiting for GPU completion. Never labels CPU dispatch time as GPU execution time.
        public static string Benchmark(int count,bool scaleDistances)
        {
            var source=AssetDatabase.LoadAssetAtPath<EmergenceProfile>(EmergenceLevelSetup.ProfilePath);
            var p=Object.Instantiate(source);p.backend=EmergenceBackend.GPU;
            try
            {
                int original=source.ParticleCount;
                EmergenceProfileEditor.ScalePopulation(p,count,false);
                float scale=Mathf.Sqrt((float)original/count);
                // Fit the larger population while keeping the source profile untouched.
                foreach(var s in p.species)s.diameter=Mathf.Max(.005f,s.diameter*Mathf.Min(1,scale));
                p.particleGap*=Mathf.Min(1,scale);
                if(scaleDistances)
                {
                    p.interactionRadius*=scale;p.separation*=scale;p.connectionRadius*=scale;p.feedingRadius*=scale;
                    p.initialSpeed*=scale;p.speedLimit*=scale;p.force*=scale;p.repulsion*=scale;p.coreRepulsion*=scale;p.agitation*=scale;p.maximumForce*=scale;
                }
                using(var gpu=new EmergenceGpuSimulation(p,new Vector2(13.6f,5.6f)))
                {
                    for(int i=0;i<120;i++)gpu.Step(1f/60);gpu.SynchronizeDiagnostics();while(gpu.TryDequeue(out _)){}
                    var samples=new List<double>();var watch=new System.Diagnostics.Stopwatch();
                    for(int batch=0;batch<20;batch++)
                    {
                        watch.Restart();for(int i=0;i<6;i++)gpu.Step(1f/60);gpu.SynchronizeDiagnostics();watch.Stop();
                        samples.Add(watch.Elapsed.TotalMilliseconds/6);while(gpu.TryDequeue(out _)){}
                    }
                    samples.Sort();var state=gpu.Particles;
                    Check(state.All(v=>!float.IsNaN(v.position.x)&&!float.IsInfinity(v.velocity.y)),"benchmark population stays finite");
                    string report="GPU completed work incl. submission/synchronization; "+SystemInfo.graphicsDeviceName+"; particles="+count+
                        "; distanceScaling="+scaleDistances+"; interactionRadius="+p.interactionRadius+"; 120 warmup + 120 measured ticks at 60Hz"+
                        "\nMedian batch ms/tick="+samples[10].ToString("F3")+"; p95="+samples[19].ToString("F3")+
                        "; bodies="+gpu.BodyCount+"; consumed="+gpu.Consumed+"; secreted="+gpu.Secreted;
                    Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/gpu-benchmark-"+count+(scaleDistances?"-scaled":"-fixed")+".txt",report);return report;
                }
            }
            finally{Object.DestroyImmediate(p);}
        }

        public static string LongRun(int count,int seconds)
        {
            var p=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EmergenceProfile>(EmergenceLevelSetup.ProfilePath));
            p.backend=EmergenceBackend.GPU;EmergenceProfileEditor.ScalePopulation(p,count,true);
            try
            {
                using(var gpu=new EmergenceGpuSimulation(p,new Vector2(13.6f,5.6f)))
                {
                    for(int i=0;i<seconds*60;i++)
                    {
                        gpu.Step(1f/60);
                        if(i%120==119){gpu.SynchronizeDiagnostics();while(gpu.TryDequeue(out _)){} }
                    }
                    var state=gpu.Particles;
                    double stored=state.Sum(v=>(double)v.reserve+v.waste),error=Math.Abs(gpu.Consumed-stored-gpu.ExportedMaterial);
                    Check(state.All(v=>!float.IsNaN(v.position.x)&&!float.IsInfinity(v.position.y)&&Mathf.Abs(v.position.x)<=gpu.HalfSize.x+.001f&&Mathf.Abs(v.position.y)<=gpu.HalfSize.y+.001f),"long GPU run stays finite and bounded");
                    Check(gpu.Consumed>0&&gpu.Replenished>0&&gpu.Secreted>0,"long GPU run completes feeding, waste and nutrient replenishment");
                    Check(error<.02,"long GPU run conserves material within 0.02 units across the full population");
                    string report="PASSED: count="+count+"; seconds="+seconds+"; consumed="+gpu.Consumed+"; replenished="+gpu.Replenished+
                        "; secreted="+gpu.Secreted+"; actual stored="+stored+"; exported="+gpu.ExportedMaterial+"; ledger error="+error;
                    File.WriteAllText(Folder+"/gpu-long-"+count+".txt",report);return report;
                }
            }
            finally{Object.DestroyImmediate(p);}
        }
    }
}
