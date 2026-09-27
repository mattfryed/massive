using UnityEngine;

namespace Massive.AttractStudy
{
    // Compose spacing once, not seven letter lookups at every tessellation/MSAA
    // sample. Flow, arrival, scale and depth all reuse this unanimated shape.
    sealed class FerrofluidLogoField
    {
        const int Padding=512;
        public static readonly Vector4 Domain=new Vector4(FerrofluidLogoLayout.Width+2*Padding,FerrofluidLogoLayout.Height+2*Padding,Padding,Padding);
        readonly Material composer;
        RenderTexture texture;
        Texture2DArray source;
        float spacing=float.NaN;

        public FerrofluidLogoField(Shader shader)
        {
            composer=new Material(shader){name="Letter field composer — transient",hideFlags=HideFlags.DontSave};
            composer.SetVectorArray("_LogoGlyphRects",FerrofluidLogoLayout.GlyphRects);
            composer.SetVector("_LogoFieldLayout",new Vector4(FerrofluidLogoLayout.Width,FerrofluidLogoLayout.Height,FerrofluidLogoLayout.Span,FerrofluidLogoLayout.DistanceRange));
            composer.SetVector("_LogoFieldDomain",Domain);
        }

        public RenderTexture Update(Texture2DArray glyphs,float letterSpacing)
        {
            if(glyphs==null)return null;
            if(texture==null)
                texture=new RenderTexture((int)Domain.x,(int)Domain.y,0,RenderTextureFormat.RHalf,RenderTextureReadWrite.Linear)
                {name="Spaced MASSIVE distance — transient",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,useMipMap=false,autoGenerateMips=false};
            bool rebuild=!texture.IsCreated();
            if(rebuild)texture.Create();
            if(rebuild || source!=glyphs || spacing!=letterSpacing)
            {
                composer.SetTexture("_LogoGlyphFields",glyphs);
                composer.SetFloat("_LogoSpacing",letterSpacing);
                var active=RenderTexture.active;bool srgb=GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite=false;
                    Graphics.Blit(null,texture,composer,0);
                    source=glyphs;spacing=letterSpacing;
                }
                finally{RenderTexture.active=active;GL.sRGBWrite=srgb;}
            }
            return texture;
        }

        public void Dispose()
        {
            if(texture!=null)texture.Release();
            Destroy(texture);Destroy(composer);texture=null;source=null;
        }
        static void Destroy(Object item)
        {
            if(item==null)return;
            if(Application.isPlaying)Object.Destroy(item);else Object.DestroyImmediate(item);
        }
    }
}
