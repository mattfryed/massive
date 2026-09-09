using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Massive.Resonance.Editor
{
    public static class ResonanceParticleValidation
    {
        [MenuItem("MASSIVE/Resonance/Validate Particle Option B")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            var passed=new List<string>();
            var scene=EditorSceneManager.NewPreviewScene();
            var definition=ScriptableObject.CreateInstance<ResonancePatternDefinition>();
            GameObject root=null,other=null,controls=null;
            var material=new Material(Shader.Find("MASSIVE/Resonance/Particles"));
            try
            {
                Check(ShaderUtil.GetShaderMessages(material.shader).Length==0,"particle shader has no compile messages",passed);
                definition.Set345HzDefaults(); root=new GameObject("Particle renderer test"); SceneManager.MoveGameObjectToScene(root,scene);
                var p=root.AddComponent<ResonancePatternController>(); p.definition=definition;
                p.segmentMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
                p.particleMaterial=material; p.Rebuild();
                int contacts=p.GeneratedRoot.GetComponentsInChildren<Collider>().Length, samples=p.GridSampleCount;
                string serialized=JsonUtility.ToJson(definition);
                float gapA,env; Vector3 normal;
                p.InteractiveSegments[0].NearestContact(new Vector3(1,0,1),out normal,out gapA,out env);
                Check(p.ParticleCount==0 && p.GeneratedRoot.GetComponentsInChildren<ParticleSystem>().Length==0,"Option A does not allocate particle systems",passed);
                p.rendering=ResonanceRendering.OptionBParticles; p.Rebuild();
                var systems=p.GeneratedRoot.GetComponentsInChildren<ParticleSystem>(true);
                Check(systems.Length==27,"three arc layers plus separate full-pattern and center populations",passed);
                Check(p.GeneratedRoot.GetComponentsInChildren<MeshRenderer>().Length==0,"Option B has no continuous-line renderers",passed);
                Check(systems.Sum(x=>x.particleCount)==p.ParticleCount && p.ParticleCount>1000,"real Particle Systems contain the authored grain pool",passed);
                Check(p.ParticleCount<=p.particles.particleBudget,"combined pool stays within hard budget",passed);
                Check(p.GeneratedRoot.GetComponentsInChildren<Collider>().Length==contacts && p.GridSampleCount==samples,"renderer swap preserves collisions and grid samples",passed);
                float gapB; p.InteractiveSegments[0].NearestContact(new Vector3(1,0,1),out normal,out gapB,out env);
                Check(Mathf.Abs(gapA-gapB)<.00001f,"renderer swap preserves nearest-contact math",passed);
                Check(JsonUtility.ToJson(definition)==serialized,"renderer swap never edits pattern definition",passed);
                Check(systems.All(x=>!x.collision.enabled && !x.emission.enabled && x.isPaused),"particle simulation adds no collisions or emission authority",passed);
                var streams=new List<ParticleSystemVertexStream>();
                systems[0].GetComponent<ParticleSystemRenderer>().GetActiveVertexStreams(streams);
                Check(streams.Contains(ParticleSystemVertexStream.Custom1XYZW) && streams.Contains(ParticleSystemVertexStream.Custom2XYZW),"arc samples and basis reach the particle shader",passed);
                var custom=new List<Vector4>(); systems[0].GetCustomParticleData(custom,ParticleSystemCustomData.Custom1);
                Check(custom.Count==systems[0].particleCount && custom.All(x=>x.x>=0 && x.y>=0 && x.y<=1),"custom per-grain data is bounded and aligned to pool",passed);
                var ghost=p.GeneratedRoot.transform.Find("Full pattern — impact reveal (visual only)");
                Check(ghost.GetComponentsInChildren<Collider>().Length==0 && ghost.GetComponentsInChildren<ParticleSystemRenderer>().All(x=>!x.enabled),"full-pattern particles start hidden and non-colliding",passed);
                p.PreviewImpact();
                Check(ghost.GetComponentsInChildren<ParticleSystemRenderer>().All(x=>x.enabled),"Core reveal activates the complete particle pattern",passed);
                p.PreviewLocalContact(false);
                var block=new MaterialPropertyBlock(); var seg=p.InteractiveSegments[0];
                foreach (var renderer in seg.GetComponentsInChildren<ParticleSystemRenderer>())
                {
                    renderer.GetPropertyBlock(block);
                    Check(block.GetInt("_ContactPlayerCount")==1,"player wake bound to particle layer "+block.GetFloat("_ParticleLayer"),passed);
                }
                p.particles.particleBudget=1024; p.Rebuild();
                Check(p.ParticleCount>900 && p.ParticleCount<=1024,"budget reduction scales all populations proportionally",passed);
                p.enabled=false;
                Check(p.ParticleCount==0 && p.GeneratedRoot==null,"disable releases all transient populations",passed);
                p.enabled=true; p.Rebuild();
                Check(p.ParticleCount<=1024 && root.transform.childCount==1,"reenable does not duplicate particle systems",passed);
                other=new GameObject("Option A test"); SceneManager.MoveGameObjectToScene(other,scene);
                var a=other.AddComponent<ResonancePatternController>(); a.definition=definition; a.segmentMaterial=p.segmentMaterial;
                controls=new GameObject("Comparison test"); SceneManager.MoveGameObjectToScene(controls,scene);
                var pair=controls.AddComponent<ResonanceRenderComparison>(); pair.optionA=a; pair.optionB=p;
                pair.Select(ResonanceRendering.OptionBParticles);
                Check(!a.gameObject.activeSelf && p.gameObject.activeSelf,"B selection deactivates A simulation",passed);
                pair.Select(ResonanceRendering.OptionAContinuous);
                Check(a.gameObject.activeSelf && !p.gameObject.activeSelf,"A selection deactivates B simulation",passed);
                return passed.Count+" particle checks passed:\n"+string.Join("\n",passed);
            }
            finally
            {
                if(controls!=null) UnityEngine.Object.DestroyImmediate(controls);
                if(root!=null) UnityEngine.Object.DestroyImmediate(root);
                if(other!=null) UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(definition);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Accepts a transient shader compiled from the pre-extraction source. No baseline asset is saved.
        public static string CompareOptionA(Shader baseline)
        {
            var scene=EditorSceneManager.NewPreviewScene(); var def=ScriptableObject.CreateInstance<ResonancePatternDefinition>();
            GameObject root=null; Material old=null;
            int maximumDifference=0, comparisons=0;
            try
            {
                def.Set345HzDefaults(); root=new GameObject("Option A pixel regression"); SceneManager.MoveGameObjectToScene(root,scene);
                var p=root.AddComponent<ResonancePatternController>(); p.definition=def;
                p.segmentMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
                old=new Material(baseline);
                for(int blend=0;blend<5;blend++)
                {
                    p.blending=(ResonanceBlend)blend; p.Rebuild();
                    var segment=p.InteractiveSegments[0]; var renderer=segment.GetComponent<MeshRenderer>();
                    var mesh=segment.GetComponent<MeshFilter>().sharedMesh; var block=new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block); block.SetFloat("_ResonanceTime",1.234f); block.SetFloat("_ContactNoiseTime",2.345f);
                    block.SetFloat("_ArcIntensity",.4f); // Keep multiply visible, rather than HDR-white on white.
                    block.SetInt("_ContactCoreCount",1); block.SetVectorArray("_ContactCore",new[] {new Vector4(segment.PathLength*.5f,.12f,.8f,.3f),Vector4.zero,Vector4.zero,Vector4.zero});
                    old.SetInt("_SrcBlend",renderer.sharedMaterial.GetInt("_SrcBlend")); old.SetInt("_DstBlend",renderer.sharedMaterial.GetInt("_DstBlend")); old.SetFloat("_BlendStyle",blend);
                    for(int ghost=0;ghost<2;ghost++)
                    {
                        block.SetFloat("_Ghost",ghost); block.SetFloat("_Reveal",.7f);
                        var current=renderer.sharedMaterial;
                        var before=Render(renderer,old,block); var after=Render(renderer,current,block);
                        int changed=0;
                        for(int i=0;i<before.Length;i++)
                        {
                            int diff=Mathf.Max(Mathf.Abs(before[i].r-after[i].r),Mathf.Abs(before[i].g-after[i].g),Mathf.Abs(before[i].b-after[i].b),Mathf.Abs(before[i].a-after[i].a));
                            maximumDifference=Mathf.Max(maximumDifference,diff);
                            if(Mathf.Abs(before[i].r-before[0].r)>2 || Mathf.Abs(before[i].g-before[0].g)>2 || Mathf.Abs(before[i].b-before[0].b)>2) changed++;
                        }
                        if(changed<10) throw new Exception("Pixel comparison was blank: blend="+blend+", ghost="+ghost);
                        comparisons++;
                    }
                }
                if(maximumDifference>1) throw new Exception("Option A pixel difference: "+maximumDifference);
                return comparisons+" Option A GPU comparisons passed; maximum channel difference="+maximumDifference+"/255";
            }
            finally { if(root!=null) UnityEngine.Object.DestroyImmediate(root); if(old!=null) UnityEngine.Object.DestroyImmediate(old); UnityEngine.Object.DestroyImmediate(def); EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static Color32[] Render(Renderer renderer,Material material,MaterialPropertyBlock block)
        {
            var rt=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var texture=new Texture2D(128,128,TextureFormat.RGBA32,false,true);
            var previous=RenderTexture.active; var previousMaterial=renderer.sharedMaterial;
            var go=new GameObject("Regression camera"); SceneManager.MoveGameObjectToScene(go,renderer.gameObject.scene);
            try
            {
                var camera=go.AddComponent<Camera>(); camera.enabled=false; camera.scene=renderer.gameObject.scene;
                camera.orthographic=true; camera.orthographicSize=1; camera.aspect=1;
                go.transform.SetPositionAndRotation(new Vector3(.9f,10,.9f),Quaternion.Euler(90,0,0));
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.4f,.4f,.4f,1);
                camera.allowHDR=false; camera.allowMSAA=false; camera.targetTexture=rt;
                renderer.sharedMaterial=material; renderer.SetPropertyBlock(block); camera.Render();
                RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,128,128),0,0); texture.Apply(); return texture.GetPixels32();
            }
            finally { renderer.sharedMaterial=previousMaterial; UnityEngine.Object.DestroyImmediate(go); RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture); }
        }
        private static void Check(bool condition,string name,List<string> passed)
        { if(!condition) throw new InvalidOperationException("Particle check failed: "+name); passed.Add(name); }
    }
}
