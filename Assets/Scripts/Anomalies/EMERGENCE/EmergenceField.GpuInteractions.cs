using Massive.Player;
using UnityEngine;

namespace Massive.Emergence
{
    public sealed partial class EmergenceField
    {
        readonly EmergenceGpuSimulation.Influence[] influences = new EmergenceGpuSimulation.Influence[4];
        static Vector4 V4(Vector3 v, float w) => new Vector4(v.x,v.y,v.z,w);
        void ApplyGpuInteractions(EmergenceGpuSimulation gpu,float dt)
        {
            int count=Mathf.Min(4,bindings.Length);
            for(int i=0;i<count;i++)
            {
                influences[i]=default;var b=bindings[i];
                if(b==null||!b.player||!b.player.isActiveAndEnabled||b.player.IsPseudoPlayer||b.player.temporarilyEliminated||b.player.IsMatchInputLocked)continue;
                if(b.attack && (b.stage!=b.attack.CurrentStage||b.attack.TimeInStage<b.time))
                { b.stage=b.attack.CurrentStage; b.swordSequence++; if(b.swordSequence==0)b.swordSequence=1; }
                b.time=b.attack?b.attack.TimeInStage:0;
                bool pulse=b.repulsor && b.repulsor.IsPulseActive;
                if(pulse&&!b.pulse){b.pulseSequence++;if(b.pulseSequence==0)b.pulseSequence=1;}b.pulse=pulse;
                Vector2 origin=PlanePosition(b.player.transform.position),drift=b.body?PlaneVector(b.body.linearVelocity)*dt*2:Vector2.zero;
                Vector2 pulseOrigin=pulse?PlanePosition(b.repulsor.OriginWorld):origin;
                var f=new EmergenceGpuSimulation.Influence
                {
                    active=1, wake=new Vector4(origin.x,origin.y,PlayerScaleAdjuster.BodyRadiusOf(b.player)+.28f,profile.wakeStrength*dt),
                    drift=new Vector4(drift.x,drift.y,pulseOrigin.x,pulseOrigin.y),
                    pulse=new Vector4(pulse?b.repulsor.RadiusWorld:0,profile.repulsorImpulse,profile.disruptedSeconds,0),
                    pulseSequence=pulse?b.pulseSequence:0
                };
                bool sword=b.attack && b.attack.IsAttacking && b.stage!=null && b.stage.StageType!=AttackStageType.FinisherRepulsor && b.sword && b.sword.enabled && b.sword.gameObject.activeInHierarchy;
                if(sword)
                {
                    Transform t=b.sword.transform;Vector3 scale=t.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                    f.swordX=V4(t.right,0);f.swordY=V4(t.up,0);f.swordZ=V4(t.forward,0);
                    if(b.sword is CapsuleCollider capsule)
                    {
                        Vector3 direction=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
                        float along=scale[capsule.direction],across=Mathf.Max(scale[(capsule.direction+1)%3],scale[(capsule.direction+2)%3]);
                        float radius=capsule.radius*across;
                        f.shape=2;f.swordCenter=V4(t.TransformPoint(capsule.center),0);f.swordY=V4(t.TransformDirection(direction),0);
                        f.swordShape=new Vector4(radius,Mathf.Max(0,capsule.height*along*.5f-radius),0,0);
                    }
                    else if(b.sword is BoxCollider box)
                    {
                        f.shape=1;f.swordCenter=V4(t.TransformPoint(box.center),0);Vector3 half=Vector3.Scale(box.size,scale)*.5f;
                        f.swordX.w=half.x;f.swordY.w=half.y;f.swordZ.w=half.z;
                    }
                    else if(b.sword is SphereCollider sphere)
                    {f.shape=3;f.swordCenter=V4(t.TransformPoint(sphere.center),0);f.swordShape.x=sphere.radius*Mathf.Max(scale.x,Mathf.Max(scale.y,scale.z));}
                    else if(!reportedError){Debug.LogWarning("[EMERGENCE] GPU melee requires a capsule, box or sphere collider.",b.sword);reportedError=true;}
                    if(f.shape!=0)
                    {
                        f.swordSequence=b.swordSequence;
                        Vector2 direction=PlaneVector(b.attack.CurrentAttackVisualDirectionWS);
                        f.attack=new Vector4(direction.x,direction.y,profile.attackImpulse,profile.disruptedSeconds);
                    }
                }
                influences[i]=f;
            }
            gpu.SetInfluences(influences,count);
        }
    }
}
