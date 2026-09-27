using System;
using System.Collections.Generic;
using System.IO;
using Massive.AttractStudy;
using UnityEditor;
using UnityEngine;

// Isolate the existing smoothed outlines, rather than rebaking the old faceted
// mesh or retaining scene lettering just to regenerate this rendering asset.
public static class FerrofluidLogoFieldBaker
{
    public const string AssetPath="Assets/AttractFerrofluid/MassiveLogoGlyphs.asset";
    const string SourcePath="Assets/AttractFerrofluid/MassiveLogoDistance.png";
    struct Segment { public Vector2 a,b; public Segment(Vector2 a,Vector2 b){this.a=a;this.b=b;} }
    sealed class Node
    {
        public Vector2 min,max;
        public int start,count;
        public Node left,right;
    }
    static float BoxDistance(Vector2 p,Vector2 min,Vector2 max)
    {
        float x=Mathf.Max(Mathf.Max(min.x-p.x,0),p.x-max.x),y=Mathf.Max(Mathf.Max(min.y-p.y,0),p.y-max.y);
        return x*x+y*y;
    }
    static Node Build(Segment[] edges,int start,int count)
    {
        var node=new Node{min=Vector2.one*float.MaxValue,max=Vector2.one*float.MinValue,start=start,count=count};
        for(int i=start;i<start+count;i++){node.min=Vector2.Min(node.min,Vector2.Min(edges[i].a,edges[i].b));node.max=Vector2.Max(node.max,Vector2.Max(edges[i].a,edges[i].b));}
        if(count<=8)return node;
        bool x=node.max.x-node.min.x>node.max.y-node.min.y;
        Array.Sort(edges,start,count,Comparer<Segment>.Create((a,b)=>(x?a.a.x+a.b.x:a.a.y+a.b.y).CompareTo(x?b.a.x+b.b.x:b.a.y+b.b.y)));
        int half=count/2;node.left=Build(edges,start,half);node.right=Build(edges,start+half,count-half);return node;
    }
    static void Closest(Node node,Segment[] edges,Vector2 p,ref float best)
    {
        if(BoxDistance(p,node.min,node.max)>=best)return;
        if(node.left==null)
        {
            for(int i=node.start;i<node.start+node.count;i++)
            {
                Vector2 d=edges[i].b-edges[i].a;
                float t=Mathf.Clamp01(Vector2.Dot(p-edges[i].a,d)/d.sqrMagnitude);
                best=Mathf.Min(best,(p-edges[i].a-d*t).sqrMagnitude);
            }
            return;
        }
        bool left=BoxDistance(p,node.left.min,node.left.max)<BoxDistance(p,node.right.min,node.right.max);
        Closest(left?node.left:node.right,edges,p,ref best);Closest(left?node.right:node.left,edges,p,ref best);
    }
    static float Sample(float[] field,float x,float y)
    {
        if(x<.5f || y<.5f || x>FerrofluidLogoLayout.Width-.5f || y>FerrofluidLogoLayout.Height-.5f)return -128;
        x-=.5f;y-=.5f;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
        int nx=Mathf.Min(ix+1,FerrofluidLogoLayout.Width-1),ny=Mathf.Min(iy+1,FerrofluidLogoLayout.Height-1);
        return Mathf.Lerp(Mathf.Lerp(field[ix+iy*FerrofluidLogoLayout.Width],field[nx+iy*FerrofluidLogoLayout.Width],x-ix),Mathf.Lerp(field[ix+ny*FerrofluidLogoLayout.Width],field[nx+ny*FerrofluidLogoLayout.Width],x-ix),y-iy);
    }
    static Segment[] Outline(float[] field,Vector4 rect)
    {
        var edges=new List<Segment>();var crossings=new Vector2[4];var p=new Vector2[4];var d=new float[4];
        for(int y=0;y<FerrofluidLogoLayout.Height-1;y++)
        for(int x=Mathf.Max(0,Mathf.FloorToInt(rect.z));x<Mathf.Min(FerrofluidLogoLayout.Width-1,Mathf.CeilToInt(rect.w));x++)
        {
            p[0]=new Vector2(x+.5f,y+.5f);p[1]=p[0]+Vector2.right;p[2]=p[1]+Vector2.up;p[3]=p[0]+Vector2.up;
            d[0]=field[x+y*FerrofluidLogoLayout.Width];d[1]=field[x+1+y*FerrofluidLogoLayout.Width];d[2]=field[x+1+(y+1)*FerrofluidLogoLayout.Width];d[3]=field[x+(y+1)*FerrofluidLogoLayout.Width];
            int count=0;
            for(int i=0;i<4;i++){int j=(i+1)%4;if((d[i]>0)!=(d[j]>0))crossings[count++]=Vector2.Lerp(p[i],p[j],d[i]/(d[i]-d[j]));}
            if(count==2)edges.Add(new Segment(crossings[0],crossings[1]));
            else if(count==4)
            {
                bool center=(d[0]+d[1]+d[2]+d[3])>0;
                if(center==(d[0]>0)){edges.Add(new Segment(crossings[0],crossings[1]));edges.Add(new Segment(crossings[2],crossings[3]));}
                else {edges.Add(new Segment(crossings[0],crossings[3]));edges.Add(new Segment(crossings[1],crossings[2]));}
            }
        }
        if(edges.Count<50)throw new InvalidOperationException("Missing letter outline.");
        return edges.ToArray();
    }
    [MenuItem("MASSIVE/Attract Ferrofluid/Rebuild isolated letter fields")]
    public static void Bake()
    {
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        Texture2DArray result=null;
        try
        {
            source.LoadImage(File.ReadAllBytes(SourcePath));
            if(source.width!=FerrofluidLogoLayout.Width || source.height!=FerrofluidLogoLayout.Height)throw new InvalidOperationException("Unexpected source lettering size.");
            var pixels=source.GetPixels32();var field=new float[pixels.Length];
            for(int i=0;i<pixels.Length;i++)field[i]=((pixels[i].r*256+pixels[i].g)/65535f-.5f)*256;
            int size=FerrofluidLogoLayout.Resolution;
            result=new Texture2DArray(size,size,7,TextureFormat.R16,false,true){name="MASSIVE isolated letter distances",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=1};
            for(int glyph=0;glyph<7;glyph++)
            {
                var rect=FerrofluidLogoLayout.GlyphRects[glyph];var edges=Outline(field,rect);var tree=Build(edges,0,edges.Length);var data=new ushort[size*size];
                // The tree prunes distant contour segments, so the offline bake
                // scales with pixels rather than pixels times all outline edges.
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    var p=new Vector2(rect.x+((x+.5f)/size-.5f)*FerrofluidLogoLayout.Span,rect.y+((y+.5f)/size-.5f)*FerrofluidLogoLayout.Span);
                    float best=float.MaxValue;Closest(tree,edges,p,ref best);
                    bool inside=p.x>=rect.z && p.x<=rect.w && Sample(field,p.x,p.y)>0;
                    float distance=Mathf.Sqrt(best)*(inside?1:-1);
                    data[x+y*size]=(ushort)Mathf.RoundToInt(Mathf.Clamp01(.5f+distance/FerrofluidLogoLayout.DistanceRange)*65535);
                }
                result.SetPixelData(data,0,glyph);
            }
            result.Apply(false,true);
            var existing=AssetDatabase.LoadAssetAtPath<Texture2DArray>(AssetPath);
            if(existing==null){AssetDatabase.CreateAsset(result,AssetPath);result=null;}
            else {EditorUtility.CopySerialized(result,existing);EditorUtility.SetDirty(existing);}
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Texture2DArray>(AssetPath));
            Debug.Log("[Attract Lettering] Baked seven independent 16-bit letter fields from the approved contours.");
        }
        finally{UnityEngine.Object.DestroyImmediate(source);if(result!=null)UnityEngine.Object.DestroyImmediate(result);}
    }
}
