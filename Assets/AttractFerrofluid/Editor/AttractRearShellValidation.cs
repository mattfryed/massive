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

public static class AttractRearShellValidation
{
    const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("MASSIVE/Attract/Validate Rear Shell")]
    public static void RunMenu()=>Debug.Log(Run());

    public static string Run(string outputDirectory=null)
    {
        if(Application.isPlaying)throw new InvalidOperationException("Run rear-shell validation in Edit Mode.");
        var source=UnityEngine.Object.FindFirstObjectByType<AttractFerrofluidStudy>();
        if(source==null)throw new InvalidOperationException("Open S-0_ATTRACT before validation.");
        var passed=new List<string>();
        var scene=EditorSceneManager.NewPreviewScene();
        GameObject sphere=null,cameraObject=null;
        Mesh full=null;
        RenderTexture target=null;
        Texture2D readback=null;
        var previousTarget=RenderTexture.active;
        try
        {
            sphere=new GameObject("Rear-shell validation"){hideFlags=HideFlags.HideAndDontSave,layer=31};
            cameraObject=new GameObject("Rear-shell camera"){hideFlags=HideFlags.HideAndDontSave};
            SceneManager.MoveGameObjectToScene(sphere,scene);SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
            camera.orthographic=true;camera.orthographicSize=.66f;
            camera.transform.SetPositionAndRotation(new Vector3(0,2,0),Quaternion.LookRotation(Vector3.down,Vector3.forward));
            camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.17f,.27f,.38f,1);camera.allowMSAA=true;
            var title=sphere.AddComponent<AttractFerrofluidStudy>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source),title);
            title.attractionCamera=camera;title.trimHiddenRear=true;
            Invoke(title,"CreateSurface");
            Require(title.SurfaceRenderer!=null && title.RearGeometryTrimmed,"trim enabled for the fixed orthographic title camera",passed);
            Require(!ShaderUtil.GetShaderMessages(title.surfaceShader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"surface shader compiles",passed);
            int resolution=title.optimizeBodyGeometry?Mathf.Min(title.faceResolution,128):title.faceResolution;
            full=AttractFerrofluidStudy.BuildSphere(resolution);
            int fullTriangles=(int)full.GetIndexCount(0)/3,trimmedTriangles=(int)title.SurfaceMesh.GetIndexCount(0)/3;
            Require(trimmedTriangles<fullTriangles && title.SurfaceMesh.vertexCount<full.vertexCount,
                "mesh reduced: triangles "+fullTriangles+" -> "+trimmedTriangles+", vertices "+full.vertexCount+" -> "+title.SurfaceMesh.vertexCount,passed);
            var block=new MaterialPropertyBlock();title.SurfaceRenderer.GetPropertyBlock(block);
            Require(Mathf.Abs(block.GetFloat("_LogoDetail")-4f*title.faceResolution/resolution)<.0001f,"lettering tessellation density is unchanged",passed);
            int bakes=title.GeometryBuildCount;
            for(int i=0;i<200;i++){Set(title,"<AnimationTime>k__BackingField",i*.1f);Invoke(title,"Apply");}
            Require(title.GeometryBuildCount==bakes,"animation does not rebuild geometry",passed);

            target=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32){antiAliasing=8};
            readback=new Texture2D(1024,1024,TextureFormat.RGBA32,false);camera.targetTexture=target;
            var filter=title.SurfaceRenderer.GetComponent<MeshFilter>();
            float[] times={0,1,3,7,13,19};
            foreach(float time in times)
            {
                Set(title,"<AnimationTime>k__BackingField",time);Set(title,"logoArrivalElapsed",time);
                Invoke(title,"Apply");Compare(camera,filter,full,title.SurfaceMesh,target,readback,"arrival/flow t="+time,passed);
            }
            foreach(var offset in new[]{new Vector3(.25f,0,0),new Vector3(-.25f,0,0),new Vector3(0,0,.25f),new Vector3(0,0,-.25f)})
            {
                Set(title,"<AttractionOffset>k__BackingField",offset);Invoke(title,"Apply");
                Compare(camera,filter,full,title.SurfaceMesh,target,readback,"maximum joystick offset "+offset,passed);
            }
            title.surfaceRelief=.16f;title.logoRecess=.03f;title.logoTypeFlow=1;
            Invoke(title,"Apply");
            Require(title.GeometryBuildCount>bakes && title.RearGeometryTrimmed,"larger relief/recess expands the retained skirt",passed);
            Compare(camera,filter,full,title.SurfaceMesh,target,readback,"maximum relief, recess and type flow",passed);
            title.surfaceRelief=.03f;title.logoTypeFlow=0;Invoke(title,"Apply");
            Compare(camera,filter,full,title.SurfaceMesh,target,readback,"minimum relief with deep lettering",passed);

            // Fresh current-scene settings, then a native-size render including the profile.
            Invoke(title,"ReleaseSurface");
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source),title);
            title.attractionCamera=camera;title.trimHiddenRear=true;Invoke(title,"CreateSurface");
            Set(title,"<AnimationTime>k__BackingField",7f);Set(title,"logoArrivalElapsed",7f);Invoke(title,"Apply");
            filter=title.SurfaceRenderer.GetComponent<MeshFilter>();
            target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);
            target=new RenderTexture(3840,2160,24,RenderTextureFormat.ARGB32){antiAliasing=8};
            readback=new Texture2D(3840,2160,TextureFormat.RGBA32,false);camera.targetTexture=target;
            Compare(camera,filter,full,title.SurfaceMesh,target,readback,"native 4K profile and lettering",passed,outputDirectory);

            camera.transform.rotation=Quaternion.Euler(85,0,0);Invoke(title,"Apply");
            Require(!title.RearGeometryTrimmed && title.SurfaceMesh.GetIndexCount(0)==full.GetIndexCount(0),"changed viewing direction restores full shell",passed);
            Invoke(title,"ReleaseSurface");camera.orthographic=false;Invoke(title,"CreateSurface");
            Require(!title.RearGeometryTrimmed && title.SurfaceMesh.GetIndexCount(0)==full.GetIndexCount(0),"perspective camera uses full shell",passed);
            var released=title.SurfaceMesh;Invoke(title,"ReleaseSurface");
            Require(released==null && title.SurfaceRenderer==null && sphere.GetComponent<MeshRenderer>().enabled,"cleanup releases generated mesh and restores original renderer",passed);
            var preview=AttractFerrofluidStudy.BuildSphere(32);
            try{Require(preview.GetIndexCount(0)/3==12288,"player preview spheres remain complete",passed);}
            finally{UnityEngine.Object.DestroyImmediate(preview);}
            return string.Join("\n",passed.Select(p=>"PASS "+p));
        }
        finally
        {
            RenderTexture.active=previousTarget;
            if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);
            if(sphere!=null)UnityEngine.Object.DestroyImmediate(sphere);
            if(full!=null)UnityEngine.Object.DestroyImmediate(full);
            if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
            if(readback!=null)UnityEngine.Object.DestroyImmediate(readback);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static void Compare(Camera camera,MeshFilter filter,Mesh full,Mesh trimmed,RenderTexture target,Texture2D readback,string name,List<string> passed,string output=null)
    {
        filter.sharedMesh=full;var reference=Capture(camera,target,readback);
        if(output!=null)File.WriteAllBytes(Path.Combine(output,"full-native.png"),readback.EncodeToPNG());
        filter.sharedMesh=trimmed;var result=Capture(camera,target,readback);
        if(output!=null)File.WriteAllBytes(Path.Combine(output,"trimmed-native.png"),readback.EncodeToPNG());
        int differences=0,white=0,black=0;
        for(int i=0;i<reference.Length;i++)
        {
            if(!reference[i].Equals(result[i]))differences++;
            if(reference[i].r>250 && reference[i].g>250)white++;
            if(reference[i].r<5 && reference[i].g<5)black++;
        }
        Require(white>100 && black>100,name+" renders nonempty black/white surface",passed);
        Require(differences==0,name+": "+differences+" changed pixels",passed);
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
