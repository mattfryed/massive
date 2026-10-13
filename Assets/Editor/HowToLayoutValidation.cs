#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HowToLayoutValidation
{
    const string Key="MASSIVE.HowToLayoutValidation";
    static readonly List<string> checks=new(), errors=new();
    static double deadline;
    static int frame;
    static float start;
    static bool captured, capturedGoal, capturedBadge;
    static readonly List<float> goalTimes = new();
    static PlayerDemoDirector[] demos;
    static HowToLayoutValidation() { EditorApplication.update+=Begin; EditorApplication.playModeStateChanged+=State; }
    public static void RunBatch()
    {
        if(!Application.isBatchMode) throw new Exception("Run in the isolated How To project.");
        Directory.CreateDirectory(HowToLayoutAuthoring.Output);
        File.WriteAllText(HowToLayoutAuthoring.Output+"/report.txt","RUNNING");
        SessionState.SetBool(Key+".begin",true);
    }
    static void Begin()
    {
        if(!SessionState.GetBool(Key+".begin",false)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        SessionState.SetBool(Key+".begin",false);
        try { HowToLayoutAuthoring.Apply();
            var authored = TextLayoutSnapshot();
            File.WriteAllText(HowToLayoutAuthoring.Output+"/layout-authored.txt", authored);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(HowToLayoutAuthoring.ScenePath);
            File.WriteAllText(HowToLayoutAuthoring.Output+"/layout-reloaded.txt", TextLayoutSnapshot());
            if (TextLayoutSnapshot() != authored) throw new Exception("Saved instruction layout differs after scene reload.");
            ValidateTextLayout();
            // Supply the persistent input bootstrap normally carried from the attract scene; do not save it.
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab"));
            SessionState.SetBool(Key,true); EditorApplication.isPlaying=true; }
        catch(Exception e) { Directory.CreateDirectory(HowToLayoutAuthoring.Output); File.WriteAllText(HowToLayoutAuthoring.Output+"/report.txt",e.ToString()); EditorApplication.Exit(1); }
    }
    static void State(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground=true; Time.captureDeltaTime=0; // Keep gameplay and unscaled UI animation clocks in real time.
            Screen.SetResolution(1920,1080,false); Camera.main.aspect=16f/9f;
            foreach (var view in Object.FindObjectsByType<PlayerDemoView>(FindObjectsSortMode.None)) view.RefreshRect();
            deadline=EditorApplication.timeSinceStartup+300; frame=-1; start=Time.time; captured=false; capturedGoal=false; capturedBadge=false; goalTimes.Clear();
            checks.Clear();errors.Clear(); Application.logMessageReceived+=Log;
            demos=Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None);
            File.WriteAllText(HowToLayoutAuthoring.Output+"/captures.txt", "");
            var coreDemo=demos.Single(d=>d.Scenario.kind==PlayerDemoKind.AmplifierGoal);
            coreDemo.AmplifierCore.Captured += c=> {
                goalTimes.Add(Time.time);
                File.AppendAllText(HowToLayoutAuthoring.Output+"/captures.txt",
                    $"t={Time.time} captures={coreDemo.ConfirmedGoals} loops={coreDemo.SuccessfulLoops} transform={c.transform.position} body={c.Body.position}\n");
            };
            var controller=Object.FindFirstObjectByType<HowToPlayScreenController>();controller.StopAllCoroutines();controller.enabled=false;
            EditorApplication.update+=Tick;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key,false); EditorApplication.Exit(SessionState.GetBool(Key+".pass",false)?0:1); }
    }
    static void Log(string text,string stack,LogType kind)
    {if(kind is LogType.Error or LogType.Exception or LogType.Assert) { if(errors.Count<20 && !errors.Contains(text+"\n"+stack)) errors.Add(text+"\n"+stack); }}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return; frame=Time.frameCount;
        try
        {
            if(Time.time-start<1)return;
            var state=string.Join("\n",demos.Select(d=>$"{d.name}: loops={d.SuccessfulLoops}, phase={d.Phase}, failure={d.LastFailure}, player={d.Primary?.transform.position}, scale={d.Primary?.transform.lossyScale}, camera={d.PresentationCamera.orthographicSize}"));
            var amplifier=demos.Single(d=>d.Scenario.kind==PlayerDemoKind.AmplifierGoal);
            if(amplifier.AmplifierCore)
            {
                var c=amplifier.AmplifierCore;
                state+=$"\nCore: time={Time.time}, active={c.isActiveAndEnabled}, captured={c.HasBeenCaptured}, spawning={c.IsSpawning}, despawning={c.IsDespawning}, pos={c.transform.position}, goals={amplifier.ConfirmedGoals}, goal={amplifier.AmplifierGoal.CapturePoint.position}";
                foreach(var field in new[]{"_spawnLocalPosition","_externalRespawnManaged","respawnAfterCapture","respawnDelaySeconds","_lifecycleRoutine"})
                    state+=" "+field+"="+typeof(Massive.Multiplier.AmplifierCoreGameplay).GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(c);
            }
            File.WriteAllText(HowToLayoutAuthoring.Output+"/progress.txt",state);
            if(demos.Any(d=>d.FailedLoops>0))throw new Exception(state);
            if(!captured && Time.time-start>3)
            { Capture("layout.png",1920,1080); Capture("layout-4k.png",3840,2160); captured=true; }
            var goal=demos.Single(d=>d.Scenario.kind==PlayerDemoKind.AmplifierGoal);
            if(goal.ConfirmedGoals>0 && !capturedGoal){ Capture("goal.png",1920,1080); capturedGoal=true; }
            if(!capturedBadge && goalTimes.Count>0 && goal.AmplifierPresenter.DisplayedMultiplier==2 && !goal.AmplifierPresenter.GoalTransitionPlaying && Time.time-goalTimes[0]>1.6f)
            { ValidateBadge(goal); Capture("goal-settled.png",1920,1080); capturedBadge=true; }
            if(EditorApplication.timeSinceStartup>deadline || Time.time-start>130)throw new Exception("Demo timeout\n"+state);
            if(!demos.All(d=>d.SuccessfulLoops>=2))return;
            Check(true,"Instruction text and control transforms survive saved scene reload and remain inside their panels");
            Check(capturedBadge,"Complete native badge validated after scoring");
            Check(goalTimes.Count>=2 && goalTimes[1]-goalTimes[0]<13f,"Amplifier goals repeat in under 13 seconds");
            Check(demos.Length==6,"Six live canonical-player demonstrations");
            Check(demos.All(d=>d.FailedLoops==0),"Every demonstration completes two natural loops");
            Check(demos.All(d=>Mathf.Approximately(d.Primary.GetComponent<Massive.Player.PlayerScaleAdjuster>().WorldScale,
                d.Primary.GetComponent<Massive.Player.PlayerScaleAdjuster>().Size*2f)),"All demo players use exactly twice their shared gameplay size");
            Check(!goal.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.text.Contains("PLAYER x16")),"Personal multiplier caption is removed");
            Check(goal.AmplifierGoal && goal.GetComponentInChildren<ScoreVoidMetaballsVisual>().sdf.Count>0 &&
                goal.AmplifierTreatments && goal.AmplifierTreatments.isActiveAndEnabled,"Native goal surface and shared Amplifier effects are live");
            Check(goal.AmplifierPresenter && goal.AmplifierPresenter.GoalNumberSettings.enabled,"Native goal multiplier display uses shared settings");
            Check(goal.ConfirmedGoals<=goal.SuccessfulLoops+1,"One Amplifier credit per demonstrated shot; respawn cannot recapture");
            Check(goal.ConfirmedGoals>=2,"Real Amplifier shots score twice and respawn normally");
            Check(Math.Abs(goal.Primary.GetComponent<PlayerScoreChain>().CurrentMultiplier-16)<.001,"Amplifier player holds the actual 16x personal multiplier");
            Check(goal.Primary.visualsController.MultiplierShimmerStrength>.9f,"16x player uses the shared multiplier shimmer");
            var attack=demos.Single(d=>d.Scenario.kind==PlayerDemoKind.MovementAndCombo);
            var block=demos.Single(d=>d.Scenario.kind==PlayerDemoKind.Block);
            Check(Mathf.Approximately(attack.PresentationCamera.orthographicSize,block.PresentationCamera.orthographicSize),"Attack and defending actors have identical screen scale");
            Check(block.ConfirmedBlocks>=2,"Defending retains real synchronized shield contacts");
            var accelerator=demos.Single(d=>d.Scenario.powerUp is Massive.PowerUps.ParticleAcceleratorPowerUpDefinition);
            var decoherence=demos.Single(d=>d.Scenario.powerUp is Massive.PowerUps.DecoherencePowerUpDefinition);
            Check(Mathf.Approximately(accelerator.PresentationCamera.orthographicSize,decoherence.PresentationCamera.orthographicSize),"Particle Accelerator and Decoherence players have identical screen scale");
            Check(accelerator.ConfirmedKills>=2&&accelerator.ConfirmedRespawns>=2,"Wider Accelerator encounter still kills and respawns its target");
            Check(SceneSfxPolicy.IsSuppressed&&AudioListener.pause,"All How To SFX remain suppressed");
            Check(demos.All(d=>d.Grid && !d.Grid.registerAsDefault),"All six demos use their own responsive grid");
            Check(!MatchScoreService.Instance || MatchScoreService.Instance.GetTeamAmplifierMultiplier(1)==1,"Demonstration goal does not change match Amplifiers");
            var localGoal=goal.AmplifierGoal;
            var fixture=new GameObject("Unrelated goal fixture"); fixture.SetActive(false);
            fixture.AddComponent<SphereCollider>().isTrigger=true;
            var unrelatedGoal=fixture.AddComponent<Massive.Multiplier.AmplifierGoalCapture>();
            Check(localGoal.AcceptsCore(goal.AmplifierCore) && !unrelatedGoal.AcceptsCore(goal.AmplifierCore),"Demo Core only enters a goal in its own simulation");
            var matchCore=Object.Instantiate(goal.Scenario.amplifierCorePrefab, fixture.transform);
            Check(unrelatedGoal.AcceptsCore(matchCore) && !localGoal.AcceptsCore(matchCore),"Match Cores remain separate from demonstration goals");
            Check(!matchCore.TryCapture(unrelatedGoal),"Closed match scoring still rejects an ordinary goal capture");
            Object.Destroy(fixture);
            Capture("completed.png",1920,1080); Finish(true,null);
        }
        catch(Exception e){Capture("failure.png",1920,1080);Finish(false,e);}
    }
    static string TextLayoutSnapshot()
    {
        return string.Join("\n", UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
            .Where(g=>g.name=="Attack sequence" || g.name.Trim()=="Shield sequence" || g.name=="PU panel" || g.name=="Multiplier instructions")
            .SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
            .Select(t=>$"{t.name}|{t.position:F3}|{t.lossyScale:F3}|{t.rotation:F3}|{t.gameObject.activeSelf}|"+
                (t.TryGetComponent<TMPro.TMP_Text>(out var text)?$"{text.text}|{text.fontSize}|{text.alignment}|{text.rectTransform.sizeDelta:F3}|{text.rectTransform.pivot:F3}":"")));
    }
    static void ValidateBadge(PlayerDemoDirector goal)
    {
        var p=goal.AmplifierPresenter; var core=p.GoalBadgeCore; var disc=p.GoalBadgeDisc; var text=p.GoalNumberText;
        Check(core && disc && text && core.gameObject.activeInHierarchy && disc.gameObject.activeInHierarchy && text.gameObject.activeInHierarchy,
            "Goal indicator includes rainbow core, white disc and multiplier text");
        Check(core.sharedMaterial.renderQueue<disc.sharedMaterial.renderQueue && disc.sharedMaterial.renderQueue<text.fontSharedMaterial.renderQueue,
            "Goal badge uses gameplay layer order");
        Check(core.sharedMaterial.GetVector("_GoalOrigin")==text.fontSharedMaterial.GetVector("_GoalOrigin") &&
            disc.sharedMaterial.GetVector("_GoalShape")==text.fontSharedMaterial.GetVector("_GoalShape"), "All badge layers share the native goal clipping mask");
        var source=goal.Scenario.amplifierCorePrefab.GetComponentsInChildren<MeshRenderer>(true)
            .Single(r=>r.sharedMaterial && r.sharedMaterial.shader.name=="MASSIVE/Amplifier Core/Energy");
        Check(core.GetComponent<MeshFilter>().sharedMesh==source.GetComponent<MeshFilter>().sharedMesh && core.sharedMaterial.shader==source.sharedMaterial.shader,
            "Badge uses the canonical Core mesh and shader");
        Check(text.font==p.GoalNumberSettings.font && text.text==p.GoalNumberSettings.prefix+"2" && !p.GoalTransitionPlaying,
            "Shared font and normal animated x2 reveal settle correctly");
        float height=p.GoalNumberGlyphBounds.size.y*text.transform.lossyScale.y;
        Check(height<disc.transform.lossyScale.y && disc.transform.lossyScale.y<core.transform.lossyScale.y,
            "Native number, white disc and rainbow core retain gameplay proportions");
    }
    static void ValidateTextLayout()
    {
        foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if(root.name!="Attack sequence" && root.name.Trim()!="Shield sequence" && root.name!="PU panel" && root.name!="Multiplier instructions")continue;
            var border=root.transform.Find("Border").GetComponent<Shapes.Rectangle>();
            var texts=root.GetComponentsInChildren<TMPro.TMP_Text>(true);
            var title=texts.Single(t=>t.name.EndsWith("Title") || t.name=="PU");
            var body=texts.Single(t=>t.name.EndsWith("Description"));
            var a=GlyphBounds(title);var b=GlyphBounds(body);
            float left=border.transform.position.x-border.Width*.5f, right=left+border.Width;
            float top=border.transform.position.z+border.Height*.5f, bottom=top-border.Height;
            if(a.min.z<b.max.z+.08f || new[]{a,b}.Any(v=>v.min.x<left+.2f || v.max.x>right-.2f || v.max.z>top-.15f || v.min.z<bottom+.1f))
                throw new Exception("Instruction text exceeds its panel or overlaps: "+root.name+" title="+a+" body="+b);
        }
    }
    static Bounds GlyphBounds(TMPro.TMP_Text text)
    {
        text.ForceMeshUpdate(true,true);var bounds=new Bounds();bool first=true;
        foreach(var c in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
        {
            if(!c.isVisible)continue;
            var a=text.transform.TransformPoint(c.bottomLeft);var b=text.transform.TransformPoint(c.topRight);
            if(first){bounds=new Bounds(a,Vector3.zero);first=false;} bounds.Encapsulate(a);bounds.Encapsulate(b);
        }
        return bounds;
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static void Finish(bool pass,Exception e)
    {
        EditorApplication.update-=Tick; Application.logMessageReceived-=Log; Time.captureDeltaTime=0;
        pass &= errors.Count==0;
        File.WriteAllText(HowToLayoutAuthoring.Output+"/report.txt",(pass?"PASSED":"FAILED")+"\n"+string.Join("\n",checks)+"\n"+e+"\n"+string.Join("\n",errors));
        SessionState.SetBool(Key+".pass",pass);EditorApplication.isPlaying=false;
    }
    static void Capture(string name,int width,int height)
    {
        var cameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.gameObject.activeInHierarchy).OrderBy(c=>c.depth).ToArray();
        var output=new Texture2D(width,height,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        foreach(var c in cameras)
        {
            var rect=c.rect; int w=Mathf.Max(1,Mathf.RoundToInt(rect.width*width)),h=Mathf.Max(1,Mathf.RoundToInt(rect.height*height));
            int x=Mathf.RoundToInt(rect.x*width),y=Mathf.RoundToInt(rect.y*height);
            if(x<0||y<0||x+w>width||y+h>height)continue;
            var target=new RenderTexture(w,h,24);var old=c.targetTexture;
            c.targetTexture=target;c.rect=new Rect(0,0,1,1);c.Render();RenderTexture.active=target;
            output.ReadPixels(new Rect(0,0,w,h),x,y);c.targetTexture=old;c.rect=rect;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);
        }
        output.Apply();File.WriteAllBytes(HowToLayoutAuthoring.Output+"/"+name,output.EncodeToPNG());
        Object.DestroyImmediate(output);RenderTexture.active=previous;
    }
}
#endif