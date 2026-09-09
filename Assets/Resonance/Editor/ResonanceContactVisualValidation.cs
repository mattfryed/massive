using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Resonance.Editor
{
    public static class ResonanceContactVisualValidation
    {
        [MenuItem("MASSIVE/Resonance/Validate Local Contact Effects")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            var passed=new List<string>(); AppendChecks(passed);
            return passed.Count+" local contact checks passed:\n"+string.Join("\n",passed);
        }
        public static void AppendChecks(List<string> passed)
        {
            var path=new[] { new Vector4(-2,0,1,0),new Vector4(0,0,1,2),new Vector4(2,0,1,4) };
            var effects=new ResonanceContactVisuals(path,3,false);
            var core=new ResonanceCoreVisualSettings(); var player=new ResonancePlayerVisualSettings();
            var block=new MaterialPropertyBlock();
            var contact=effects.Nearest(new Vector2(.5f,1),new Vector2(.5f,1));
            Check(Near(contact.along,2.5f) && Near(contact.distance,1f),"nearest visual point uses rendered arc distance",passed);
            contact=effects.Nearest(new Vector2(.5f,-5),new Vector2(.5f,5));
            Check(Near(contact.distance,0f) && Near(contact.along,2.5f),"fast swept passage hits a thin arc",passed);
            Check(Near(effects.Nearest(new Vector2(3,-2),new Vector2(3,2)).distance,1f),"sweep respects finite arc ends",passed);
            Check(Near(effects.Nearest(new Vector2(-1,1),new Vector2(1,1)).distance,1f),"parallel sweep remains finite",passed);
            var bent=new ResonanceContactVisuals(new[] { new Vector4(0,0,1,0),new Vector4(0,2,1,2),new Vector4(2,2,1,4) },3,false);
            Check(Near(bent.Nearest(new Vector2(1,2),new Vector2(1,2)).along,3f),"pulse location follows bent curve length, not radial distance",passed);
            float direct=ResonanceContactVisuals.ImpactStrength(Vector2.up*8,Vector2.right,8);
            Check(direct>ResonanceContactVisuals.ImpactStrength(Vector2.right*8,Vector2.right,8),"direct hit stronger than glancing hit",passed);
            Check(direct>ResonanceContactVisuals.ImpactStrength(Vector2.up,Vector2.right,8),"faster hit increases bounded strength",passed);
            effects.Approach(1,contact,Vector2.up*8,1,.5f,10);
            effects.Apply(block,10,core,player);
            Check(effects.ActiveCoreCount==1 && block.GetVectorArray("_ContactCore")[0].y<0,"braking is compression, not an impact",passed);
            Check(effects.Impact(1,contact,Vector2.up*8,1,10.1,.12f),"turnaround upgrades approach to impact",passed);
            Check(!effects.Impact(1,contact,Vector2.up*8,1,10.11,.12f),"compound wall contacts are deduplicated per body and arc",passed);
            effects.Apply(block,10.2,core,player);
            Check(effects.ActiveCoreCount==1 && Near(block.GetVectorArray("_ContactCore")[0].y,.1f),"turnaround starts independent pulse age",passed);
            effects.Impact(2,contact,Vector2.right*8,1,10.2,.12f);
            effects.Apply(block,10.25,core,player);
            Check(effects.ActiveCoreCount==2 && block.GetVectorArray("_ContactCore")[1].w>.99f,"second Core coexists and preserves tangential bias",passed);
            effects.Apply(block,12,core,player);
            Check(effects.ActiveCoreCount==0 && block.GetVectorArray("_ContactCore")[0]==Vector4.zero,"expired impacts clear shader slots",passed);
            for (int i=0;i<9;i++) effects.Impact(i,contact,Vector2.up,1,20+i*.001f,0);
            effects.Apply(block,20.01,core,player);
            Check(effects.ActiveCoreCount==4 && block.GetVectorArray("_ContactCore").Length==4,"Core effect storage stays bounded under burst load",passed);
            core.enabled=false; effects.Apply(block,20.01,core,player);
            Check(effects.ActiveCoreCount==0,"Core visual toggle suppresses presentation",passed); core.enabled=true;
            // Continuous touches represent a slow or stationary actor in sludge.
            for (int i=0;i<100;i++) effects.Passage(1,contact,Vector2.up,1,30+i*.02,.02f,player);
            effects.Apply(block,31.98,core,player);
            float sustained=block.GetVectorArray("_ContactPlayer")[0].y;
            Check(effects.ActivePlayerCount==1 && sustained>.95f,"sludge sustains one smooth wake instead of re-triggering flashes",passed);
            Check(block.GetVectorArray("_ContactPlayer")[0].w>.99f,"wake follows signed crossing direction",passed);
            effects.Apply(block,32.2,core,player);
            float recovery=block.GetVectorArray("_ContactPlayer")[0].y;
            Check(recovery>0 && recovery<sustained,"exit recovers smoothly without an instant snap",passed);
            effects.Apply(block,33,core,player);
            Check(effects.ActivePlayerCount==0,"exited or removed actors leave no lasting disturbance",passed);
            for (int i=0;i<9;i++) effects.Passage(i,contact,Vector2.up,1,40+i*.001,.02f,player);
            effects.Apply(block,40.01,core,player);
            Check(effects.ActivePlayerCount==4,"player effect storage stays bounded under burst load",passed);
            player.enabled=false; effects.Apply(block,40.01,core,player);
            Check(effects.ActivePlayerCount==0,"player visual toggle suppresses presentation",passed); player.enabled=true;
            Check(Near(ResonanceContactVisuals.Recovery(0,1),1) && Near(ResonanceContactVisuals.Recovery(1,1),0)
                && ResonanceContactVisuals.Recovery(.01f,1)>.999f,"quintic recovery has smooth endpoints",passed);
            var loop=new ResonanceContactVisuals(path,3,true); loop.Apply(block,0,core,player);
            Check(block.GetVector("_ContactPath").y==1 && block.GetVector("_ContactPath").x==4,"closed arc exposes wrap length to pulse shader",passed);

            var scene=EditorSceneManager.NewPreviewScene();
            var def=ScriptableObject.CreateInstance<ResonancePatternDefinition>(); GameObject root=null;
            try
            {
                def.Set345HzDefaults(); root=new GameObject("Local contact isolation test"); SceneManager.MoveGameObjectToScene(root,scene);
                var p=root.AddComponent<ResonancePatternController>(); p.definition=def;
                p.segmentMaterial=AssetDatabase.LoadAssetAtPath<Material>(ResonancePrototypeSetup.Folder+"/Resonance Ribbon.mat"); p.Rebuild();
                int collisions=root.GetComponentsInChildren<Collider>().Length, samples=p.GridSampleCount;
                p.playerResponse=ResonancePlayerResponse.PassThrough;
                var seg=p.InteractiveSegments[0]; float u;
                Vector3 point=seg.transform.TransformPoint(seg.SampleDistance(seg.PathLength*.5f,out u));
                p.NotifyPlayerPassage(7,point-Vector3.forward*2,point+Vector3.forward*2,Vector3.forward*10,.1f,.02f);
                seg.ContactVisuals.Apply(block,p.ContactClock,p.coreContactVisuals,p.playerPassageVisuals);
                Check(seg.ContactVisuals.ActivePlayerCount==1,"pass-through policy receives localized feedback",passed);
                Check(p.CurrentReveal==0,"player passage never reveals full pattern",passed);
                p.NotifyCoreContact(null,seg,point,Vector3.right,true);
                Check(seg.ContactVisuals.ImpactCount==0,"invalid Core cannot trigger local impact",passed);
                p.PreviewLocalContact(false);
                var ghost=p.GeneratedRoot.transform.Find("Full pattern — impact reveal (visual only)");
                foreach (var renderer in ghost.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.GetPropertyBlock(block);
                    if (block.GetInt("_ContactCoreCount")!=0 || block.GetInt("_ContactPlayerCount")!=0) throw new Exception("Local effect leaked to ghost");
                }
                passed.Add("full-pattern renderers never inherit localized effects");
                Check(collisions==root.GetComponentsInChildren<Collider>().Length && samples==p.GridSampleCount,"preview preserves collision topology and grid sampling",passed);
                p.coreContactVisuals.strength=3; p.playerPassageVisuals.wakeStretch=3; p.Rebuild();
                Check(collisions==root.GetComponentsInChildren<Collider>().Length && samples==p.GridSampleCount,"maximum visual tuning cannot expand physics or grid forces",passed);
                Check(p.InteractiveSegments[0].ContactVisuals.ImpactCount==0,"rebuild discards transient contact state",passed);
                p.enabled=false;
                Check(p.GeneratedRoot==null && p.InteractiveSegments.Count==0,"disable cleans up effect-owned renderers and state",passed);
            }
            finally
            { if(root!=null) UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(def); EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static bool Near(float a,float b) { return Mathf.Abs(a-b)<.001f; }
        private static void Check(bool condition,string name,List<string> passed)
        { if(!condition) throw new InvalidOperationException("Contact visual check failed: "+name); passed.Add(name); }
    }
}
