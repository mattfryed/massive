using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Massive.AttractStudy;

public static class AttractLetterRegionValidation
{
    const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("MASSIVE/Attract/Validate Lettering Region")]
    public static void RunMenu()=>Debug.Log(Run());

    public static string Run(string outputDirectory=null,Shader referenceShader=null)
    {
        if(Application.isPlaying)throw new InvalidOperationException("Run lettering-region validation in Edit Mode.");
        var source=UnityEngine.Object.FindFirstObjectByType<AttractFerrofluidStudy>();
        if(source==null)throw new InvalidOperationException("Open S-0_ATTRACT before validation.");
        var passed=new List<string>();
        var scene=EditorSceneManager.NewPreviewScene();
        GameObject sphere=null,cameraObject=null;
        RenderTexture target=null;Texture2D readback=null;Material reference=null;
        var previousTarget=RenderTexture.active;
        try
        {
            sphere=new GameObject("Letter-region validation"){hideFlags=HideFlags.HideAndDontSave,layer=31};
            cameraObject=new GameObject("Letter-region camera"){hideFlags=HideFlags.HideAndDontSave};
            SceneManager.MoveGameObjectToScene(sphere,scene);SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
            camera.orthographic=true;camera.orthographicSize=.66f;
            camera.transform.SetPositionAndRotation(new Vector3(0,2,0),Quaternion.LookRotation(Vector3.down,Vector3.forward));
            camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.17f,.27f,.38f,1);camera.allowMSAA=true;
            var title=sphere.AddComponent<AttractFerrofluidStudy>();
            string sourceSettings=EditorJsonUtility.ToJson(source);
            EditorJsonUtility.FromJsonOverwrite(sourceSettings,title);
            title.attractionCamera=camera;Invoke(title,"CreateSurface");
            Require(title.SurfaceRenderer!=null,"title surface created",passed);
            reference=new Material(referenceShader!=null?referenceShader:title.surfaceShader){hideFlags=HideFlags.HideAndDontSave};
            reference.SetVector("_LogoSourceSize",new Vector4(FerrofluidLogoLayout.Width,FerrofluidLogoLayout.Height,0,0));
            reference.SetVector("_LogoFieldDomain",new Vector4(3072,1447,512,512));
            target=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32){antiAliasing=8};
            readback=new Texture2D(768,768,TextureFormat.RGBA32,false);camera.targetTexture=target;
            Action<string> compare=name=>Compare(camera,title,reference,target,readback,name,passed,outputDirectory,false);
            foreach(float progress in new[]{0,.1f,.2f,.3f,.5f,.7f,.9f,1f})
            {
                Set(title,"logoArrivalElapsed",title.logoArrivalDelay+title.logoArrivalDuration*progress);
                Set(title,"<AnimationTime>k__BackingField",progress*13);Invoke(title,"Apply");
                compare("arrival-"+progress);
            }
            foreach(float time in new[]{3f,7f,13f,19f})
            {
                Set(title,"<AnimationTime>k__BackingField",time);Invoke(title,"Apply");compare("flow-"+time);
            }
            foreach(var offset in new[]{new Vector3(.25f,0,0),new Vector3(-.25f,0,0),new Vector3(0,0,.25f),new Vector3(0,0,-.25f)})
            {
                Set(title,"<AttractionOffset>k__BackingField",offset);Invoke(title,"Apply");compare("attraction-"+offset);
            }
            int combination=0;
            foreach(float scale in new[]{.25f,.632f,1.3f})
            foreach(float spacing in new[]{-.008f,.06f})
            foreach(float elevation in new[]{-.4f,.4f})
            {
                title.logoScale=scale;title.logoLetterSpacing=spacing;title.logoElevation=elevation;
                title.logoArcWidth=scale<1?1.5f:2.9f;
                title.surfaceRelief=.16f;title.logoRecess=.03f;title.logoTypeFlow=1;title.logoSurfaceFlow=1;
                Set(title,"<AnimationTime>k__BackingField",combination*1.73f);
                Set(title,"logoArrivalElapsed",100f);Invoke(title,"Apply");compare("layout-extreme-"+combination+"-rest");
                Set(title,"logoArrivalElapsed",title.logoArrivalDelay+title.logoArrivalDuration*.75f);Invoke(title,"Apply");
                compare("layout-extreme-"+combination+"-arrival");combination++;
            }
            title.embeddedLogo=false;Invoke(title,"Apply");compare("lettering-disabled");
            title.embeddedLogo=true;title.surfaceRelief=.03f;title.logoRecess=0;title.logoTypeFlow=0;title.logoSurfaceFlow=0;
            Invoke(title,"Apply");compare("shallow-unmoving-letter-floor");

            // Lower tessellation resolution has the widest geometry guard.
            Invoke(title,"ReleaseSurface");EditorJsonUtility.FromJsonOverwrite(sourceSettings,title);
            title.faceResolution=64;title.logoScale=1.3f;title.logoArcWidth=2.9f;title.logoRecess=.03f;
            title.logoLetterSpacing=.06f;title.attractionCamera=camera;Invoke(title,"CreateSurface");
            Set(title,"logoArrivalElapsed",100f);Set(title,"<AnimationTime>k__BackingField",11f);Invoke(title,"Apply");
            compare("widest-geometry-guard");

            // Current authored surface at native output resolution.
            Invoke(title,"ReleaseSurface");EditorJsonUtility.FromJsonOverwrite(sourceSettings,title);
            title.attractionCamera=camera;Invoke(title,"CreateSurface");
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);
            target=new RenderTexture(3840,2160,24,RenderTextureFormat.ARGB32){antiAliasing=8};
            readback=new Texture2D(3840,2160,TextureFormat.RGBA32,false);camera.targetTexture=target;
            foreach(float progress in new[]{.7f,1f})
            {
                Set(title,"logoArrivalElapsed",title.logoArrivalDelay+title.logoArrivalDuration*progress);
                Set(title,"<AnimationTime>k__BackingField",7f);Invoke(title,"Apply");
                Compare(camera,title,reference,target,readback,"native-4k-"+progress,passed,outputDirectory,true);
            }
            Require(!ShaderUtil.GetShaderMessages(title.surfaceShader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"optimized shader compiles",passed);
            Require(!ShaderUtil.GetShaderMessages(reference.shader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"reference shader compiles",passed);
            return string.Join("\n",passed.Select(p=>"PASS "+p));
        }
        finally
        {
            RenderTexture.active=previousTarget;
            if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
            if(sphere!=null)UnityEngine.Object.DestroyImmediate(sphere);
            if(reference!=null)UnityEngine.Object.DestroyImmediate(reference);
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            if(readback!=null)UnityEngine.Object.DestroyImmediate(readback);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static void Compare(Camera camera,AttractFerrofluidStudy title,Material reference,RenderTexture target,Texture2D readback,string name,List<string> passed,string output,bool save)
    {
        var renderer=title.SurfaceRenderer;var optimized=renderer.sharedMaterial;
        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
        Vector4 bounds=block.GetVector("_LogoRegion");
        renderer.sharedMaterial=reference;block.SetVector("_LogoRegion",new Vector4(-1e20f,1e20f,-1,1));renderer.SetPropertyBlock(block);
        var before=Capture(camera,target,readback);
        byte[] beforePng=output!=null?readback.EncodeToPNG():null;
        renderer.sharedMaterial=optimized;block.SetVector("_LogoRegion",bounds);renderer.SetPropertyBlock(block);
        var after=Capture(camera,target,readback);
        int differences=0,white=0,black=0;
        for(int i=0;i<before.Length;i++)
        {
            if(!before[i].Equals(after[i]))differences++;
            if(before[i].r>250 && before[i].g>250)white++;
            if(before[i].r<5 && before[i].g<5)black++;
        }
        if(output!=null)
        {
            File.AppendAllText(Path.Combine(output,"pairs.txt"),name+": changed="+differences+", white="+white+", black="+black+", region="+bounds+"\n");
            if(save || differences>0)
            {
                File.WriteAllBytes(Path.Combine(output,name+"-before.png"),beforePng);
                File.WriteAllBytes(Path.Combine(output,name+"-after.png"),readback.EncodeToPNG());
            }
        }
        Require(white>100 && black>100,name+" renders nonempty surface",passed);
        Require(differences==0,name+": zero changed pixels",passed);
    }
    static Color32[] Capture(Camera camera,RenderTexture target,Texture2D readback)
    {
        camera.Render();RenderTexture.active=target;
        readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0);readback.Apply();return readback.GetPixels32();
    }
    static void Invoke(AttractFerrofluidStudy title,string method)=>typeof(AttractFerrofluidStudy).GetMethod(method,PrivateInstance).Invoke(title,null);
    static void Set(AttractFerrofluidStudy title,string field,object value)=>typeof(AttractFerrofluidStudy).GetField(field,PrivateInstance).SetValue(title,value);
    static void Require(bool condition,string name,List<string> passed)
    { if(!condition)throw new InvalidOperationException("FAIL "+name);passed.Add(name); }
}