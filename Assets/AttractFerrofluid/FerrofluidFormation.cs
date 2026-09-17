using UnityEngine;

namespace Massive.AttractStudy
{
    /// <summary>Reversible growth from a permanent reservoir. Each rounded lobe
    /// emerges inside the core, swells outward while attached, then folds into
    /// the growing surface. No incoming or detached particles are required.</summary>
    public static class FerrofluidFormation
    {
        public const int DropCount=8;
        public static float Core(float phase,float idleSize=.24f)=>Mathf.Lerp(Mathf.Clamp(idleSize,.12f,.4f),1,Mathf.SmoothStep(0,1,phase));
        public static void Evaluate(float phase,int seed,Vector4[] drops,float idleSize=.24f)
        {
            phase=Mathf.Clamp01(phase);
            for(int i=0;i<DropCount;i++)
            {
                float delay=.055f*((i*3+seed)%7);
                float age=Mathf.InverseLerp(delay,.94f,phase);
                float swelling=Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*age)),1.25f);
                float radius=(.12f+.018f*(i%3))*swelling;
                float emerge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.28f,age));
                float angle=i*2.39996323f+seed*.73f+(.5f-age)*.7f;
                float y=1-2*(i+.5f)/DropCount;
                float ring=Mathf.Sqrt(1-y*y);
                Vector3 direction=new Vector3(Mathf.Cos(angle)*ring,y,Mathf.Sin(angle)*ring);
                // Center remains inside the smallest possible core surface:
                // even at maximum relief every lobe stays attached to its fount.
                float distance=Mathf.Max(0,.445f*Core(phase,idleSize)-radius*.25f)*emerge;
                Vector3 center=direction*distance;
                drops[i]=new Vector4(center.x,center.y,center.z,radius);
            }
        }
    }
}
