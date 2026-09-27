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
    }
}
