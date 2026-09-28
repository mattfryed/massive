using UnityEngine;

namespace Massive.AttractStudy
{
    // Source coordinates of the approved, smoothed MASSIVE distance field.
    // Each array slice contains only one letter, including its outer recess.
    public static class FerrofluidLogoLayout
    {
        public const int Width=2048,Height=423,Resolution=768,Span=1024,DistanceRange=1024;
        public static readonly Vector4[] GlyphRects={
            new Vector4(201,212,0,402),new Vector4(574,212,402,745.5f),
            new Vector4(887,212,745.5f,1028.5f),new Vector4(1179,212,1028.5f,1329.5f),
            new Vector4(1390,212,1329.5f,1449.5f),new Vector4(1613,212,1449.5f,1777),
            new Vector4(1912,212,1777,2048)};

        // The baker constrains contours to GlyphRects x [0,Height]. Spacing only
        // translates them. A .25-distance margin exceeds every shader influence
        // (largest normal band <.202 at width 2.9, resolution 64, recess .03).
        // Four source pixels cover both bilinear filters and distance encoding.
        // Keep this bound in sync if the shader influence or bake layout changes.
        public static Vector4 ConservativeRegion(float width,float height,float elevation,float spacing)
        {
            if(width<=0 || height<=0)return new Vector4(-1e20f,1e20f,-1,1);
            float padding=.25f*Width/width+4;
            float left=float.PositiveInfinity,right=float.NegativeInfinity;
            for(int glyph=0;glyph<GlyphRects.Length;glyph++)
            {
                float shift=(glyph-3)*spacing*Width;
                left=Mathf.Min(left,GlyphRects[glyph].z+shift);
                right=Mathf.Max(right,GlyphRects[glyph].w+shift);
            }
            left=((left-padding)/Width-.5f)*width;
            right=((right+padding)/Width-.5f)*width;
            float bottom=elevation+(-padding/Height-.5f)*height;
            float top=elevation+(padding/Height+.5f)*height;
            // For front>0, longitude comparisons use slopes instead of atan2;
            // latitude comparisons use sin because asin is monotone in [-1,1].
            float halfPi=Mathf.PI*.5f;
            return new Vector4(left<=-halfPi?-1e20f:Mathf.Tan(left),right>=halfPi?1e20f:Mathf.Tan(right),
                Mathf.Sin(Mathf.Clamp(bottom,-halfPi,halfPi)),Mathf.Sin(Mathf.Clamp(top,-halfPi,halfPi)));
        }
    }
}
