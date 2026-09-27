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
        public static float BoundingRadius(float core,float relief,Vector4[] drops)
        {
            // Gradient noise is bounded by .5 +/- 1.2 (corner dot products
            // are bounded by +/-2). .48 safely bounds the .455 body + noise;
            // the positive soft-saturated mound contribution is <= .72*relief.
            float bound=(.48f+.72f*Mathf.Clamp(relief,.03f,.16f))*core;
            float k=.055f+.045f*core;
            for(int i=0;i<DropCount;i++)
            {
                var drop=drops[i];if(drop.w<=.0001f)continue;
                float extent=new Vector3(drop.x,drop.y,drop.z).magnitude+drop.w;
                // Smooth max of enclosing radii bounds the smooth min of the
                // signed fields, including the union's outward expansion.
                float h=Mathf.Max(k-Mathf.Abs(bound-extent),0)/k;
                bound=Mathf.Max(bound,extent)+h*h*k*.25f;
            }
            return bound+.002f; // Tracing tolerance and floating-point clearance.
        }
        public static Mesh BuildProxy()
        {
            var vertices=new Vector3[24];var indices=new int[36];
            Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            for(int face=0;face<6;face++)
            {
                Vector3 axis=axes[face],u=Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.Cross(Vector3.up,axis),v=Vector3.Cross(axis,u);
                int first=face*4,t=face*6;
                vertices[first]=(axis-u-v)*.5f;vertices[first+1]=(axis+u-v)*.5f;
                vertices[first+2]=(axis-u+v)*.5f;vertices[first+3]=(axis+u+v)*.5f;
                indices[t]=first;indices[t+1]=first+1;indices[t+2]=first+2;
                indices[t+3]=first+1;indices[t+4]=first+3;indices[t+5]=first+2;
            }
            var result=new Mesh{name="Ferrofluid formation bounds",hideFlags=HideFlags.DontSave};
            result.vertices=vertices;result.triangles=indices;
            result.bounds=new Bounds(Vector3.zero,Vector3.one*1.6f);
            result.UploadMeshData(false);return result;
        }
    }
}
