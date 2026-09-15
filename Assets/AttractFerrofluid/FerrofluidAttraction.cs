using System.Collections.Generic;
using UnityEngine;

namespace Massive.AttractStudy
{
    /// <summary>Bounded, shared joystick intent and a critically damped attraction point.</summary>
    public sealed class FerrofluidAttraction
    {
        public Vector2 Position {get;private set;}
        Vector2 velocity;

        public static Vector2 Average(IReadOnlyList<Vector2> sticks,float deadzone,out int activePlayers)
        {
            activePlayers=0;Vector2 sum=Vector2.zero;
            if(sticks==null)return sum;
            deadzone=Mathf.Clamp(deadzone,0,.95f);
            for(int i=0;i<sticks.Count;i++)
            {
                Vector2 raw=sticks[i];
                if(!Finite(raw.x) || !Finite(raw.y))continue;
                raw=Vector2.ClampMagnitude(raw,1);
                float magnitude=raw.magnitude;
                if(magnitude<=deadzone || magnitude==0)continue;
                // A radial deadzone with a continuous start, preserving direction.
                sum+=raw/magnitude*((magnitude-deadzone)/(1-deadzone));
                activePlayers++;
            }
            return activePlayers==0?Vector2.zero:Vector2.ClampMagnitude(sum/activePlayers,1);
        }

        public Vector2 Step(Vector2 target,float deltaTime,float smoothTime)
        {
            if(!Finite(deltaTime) || deltaTime<=0)return Position;
            target=Vector2.ClampMagnitude(target,1);
            float omega=2/Mathf.Max(.05f,smoothTime);
            float decay=Mathf.Exp(-omega*deltaTime);
            Vector2 offset=Position-target,impulse=velocity+omega*offset;
            Position=Vector2.ClampMagnitude(target+(offset+impulse*deltaTime)*decay,1);
            velocity=(velocity-omega*impulse*deltaTime)*decay;
            return Position;
        }

        public void Reset(){Position=Vector2.zero;velocity=Vector2.zero;}
        static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}
