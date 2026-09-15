using UnityEngine;

namespace Massive.AttractStudy
{
    /// <summary>Reversible droplet paths. Phase is independent of frame rate and
    /// motion speed, so rapid menu reversals keep the current liquid shape.</summary>
    public static class FerrofluidFormation
    {
        public const int DropCount=8;
        public static float Core(float phase)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.91f,phase));
        public static void Evaluate(float phase,int seed,Vector4[] drops)
        {
            phase=Mathf.Clamp01(phase);
            for(int i=0;i<DropCount;i++)
            {
                float delay=.025f*((i*3+seed)%5);
                float birth=Mathf.SmoothStep(0,1,Mathf.InverseLerp(delay,delay+.19f,phase));
                float gather=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f+delay,.83f+delay,phase));
                float angle=i*2.39996323f+seed*.73f+(1-gather)*.85f;
                float y=1-2*(i+.5f)/DropCount;
                float ring=Mathf.Sqrt(1-y*y);
                Vector3 direction=new Vector3(Mathf.Cos(angle)*ring,y,Mathf.Sin(angle)*ring);
                float distance=Mathf.Lerp(.64f,.06f,gather);
                Vector3 center=direction*distance;
                float radius=(.083f+.012f*(i%3))*birth*(1-.42f*gather);
                drops[i]=new Vector4(center.x,center.y,center.z,radius);
            }
        }
    }
}
