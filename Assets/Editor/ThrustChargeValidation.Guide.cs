#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Massive.Player;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class ThrustChargeValidation
{
    static PlayerInputFrame Moving(bool down=false,bool held=false) => new() {
        moveInput=Vector2.right,attackDown=down,attackHeld=held,hasAimDirWS=true,aimDirWS=Vector3.right };
    static int VisibleArrows(PlayerThrustChargeGuide guide)
    { int count=0;for(int i=0;i<guide.ArrowCount;i++)if(guide.TryGetArrow(i,out _,out _,out _,out _))count++;return count; }
    static IEnumerator GuideChecks(GameObject root)
    {
        var guide=actor.GetComponent<PlayerThrustChargeGuide>();
        Check(guide && !guide.IsVisible,"Canonical actor automatically binds a runtime charge guide; idle has no arrows");
        Check(tuning.combat.thrustChargeMinMoveMultiplier==.25f && tuning.combat.thrustChargeGuideArrowCount==5 &&
            tuning.combat.thrustChargeGuideDistanceFraction==.5f,"Guide defaults: 25% movement, five arrows, half maximum range");
        yield return Ready(); actor.SetScriptedInput(Moving()); yield return Seconds(2f);
        float ordinary=body.linearVelocity.magnitude;
        actor.SetScriptedInput(Moving(true,true)); yield return Until(()=>attack.ChargeElapsed>=.025f,"First arrow starts");
        Check(VisibleArrows(guide)==1,"Only the nearest arrow starts appearing on a new charge");
        guide.TryGetArrow(0,out _,out _,out _,out float firstWidth);
        yield return Seconds(.04f); guide.TryGetArrow(0,out _,out _,out _,out float secondWidth);
        Check(secondWidth>firstWidth && firstWidth>0 && secondWidth<attack.Effective_thrustChargeGuideStrokeWidth,
            "First chevron stroke grows continuously from zero toward full width");
        float early=body.linearVelocity.magnitude;
        yield return Until(()=>attack.ChargeElapsed>=.78f,"Two arrows revealed");
        Check(VisibleArrows(guide)==2,"Chevrons appear outward in charge order"); CaptureGuide(guide,"guide-two.png",false);
        yield return Until(()=>attack.ChargeElapsed>=1.5f,"Half-charge movement"); float middle=body.linearVelocity.magnitude;
        Check(VisibleArrows(guide)==3,"Half charge shows three chevrons");
        yield return Until(()=>attack.ChargeElapsed>=2.8f,"Full guide"); float late=body.linearVelocity.magnitude;
        Check(VisibleArrows(guide)==5,"All five chevrons are visible before automatic launch");
        Check(ordinary>early && early>middle && middle>late && late<ordinary*.4f,
            "Measured speed progressively falls (normal/early/half/late: "+ordinary.ToString("0.00")+" / "+early.ToString("0.00")+" / "+middle.ToString("0.00")+" / "+late.ToString("0.00")+")");
        guide.TryGetArrow(4,out var left,out var tip,out var right,out float width);
        Vector3 offset=tip-actor.transform.position;offset.y=0;
        Check(Mathf.Abs(offset.magnitude-3f)<.002f,"Last arrow tip is half the six-unit maximum charged travel from player center");
        Check(Mathf.Abs(width-attack.Effective_thrustChargeGuideStrokeWidth)<.001f,"Revealed arrows reach full stroke width");
        Check(Vector3.Dot(left-tip,Vector3.right)<0 && Vector3.Dot(right-tip,Vector3.right)<0,"Both open triangle legs point forward");
        CaptureGuide(guide,"guide-five.png",true);
        var size=actor.GetComponent<PlayerScaleAdjuster>();size.Size=1.5f;
        guide.TryGetArrow(4,out _,out var largerTip,out _,out var largerStroke);
        Vector3 larger=largerTip-actor.transform.position;larger.y=0;
        Check(Mathf.Abs(larger.magnitude-4.5f)<.005f && Mathf.Abs(largerStroke/width-1.5f)<.005f,"Range and stroke follow scaled player reach and size");size.Size=1;
        actor.SetScriptedInput(new PlayerInputFrame {attackHeld=true,hasAimDirWS=true,aimDirWS=Vector3.forward});yield return null;yield return null;
        guide.TryGetArrow(4,out _,out var turnedTip,out _,out _);Vector3 turned=turnedTip-actor.transform.position;turned.y=0;
        Check(Vector3.Angle(turned,Vector3.forward)<.01f,"Guide follows live aim without accumulating charge-vibration offsets");
        actor.SetScriptedInput(new PlayerInputFrame {attackUp=true,hasAimDirWS=true,aimDirWS=Vector3.forward});yield return null;yield return null;
        Check(!guide.IsVisible && attack.ChargeMovementMultiplier==1,"Release removes guide and charging slowdown immediately");
        yield return Ready();yield return StartHold();yield return Seconds(.7f);
        attack.CancelAttack();Check(!guide.IsVisible && VisibleArrows(guide)==0 && attack.ChargeMovementMultiplier==1,"Cancel clears arrows and slowdown without lingering geometry");
        yield return Ready();yield return StartHold();yield return Seconds(.03f);
        Check(VisibleArrows(guide)==1,"Repeated charge restarts its arrow reveal from the beginning");
        tuning.combat.thrustChargeGuideEnabled=false;Check(!guide.IsVisible && attack.IsCharging,"Guide toggle hides only the telegraph");tuning.combat.thrustChargeGuideEnabled=true;
        attack.enabled=false;Check(!guide.IsVisible && attack.ChargeMovementMultiplier==1,"Disable clears guide and movement penalty");attack.enabled=true;
        yield return Ready(); bool clamped=tuning.movement.clampSpeed;tuning.movement.clampSpeed=false;
        actor.SetScriptedInput(Moving(true,true));yield return Until(()=>attack.ChargeElapsed>=2.8f,"Charge with ordinary speed cap disabled");
        Check(body.linearVelocity.magnitude<actor.Effective_maxMoveSpeed*.42f,"Charging still slows existing momentum when ordinary speed clamping is disabled");
        tuning.movement.clampSpeed=clamped;yield return Ready();
        // Check the live preview against the actual aim-assistance selection on release.
        var targetRoot=new GameObject("Guide aim target");targetRoot.transform.SetParent(root.transform);targetRoot.SetActive(false);
        var targetGo=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"),targetRoot.transform);
        var target=targetGo.GetComponent<PlayerControllerScript>();target.ConfigureDemonstration(root.transform,2,2);
        targetGo.transform.position=Origin+Quaternion.AngleAxis(20,Vector3.up)*Vector3.right*2.5f;
        targetGo.GetComponent<Rigidbody>().useGravity=false;targetRoot.SetActive(true);tuning.combat.lockOnEnabled=true;
        yield return StartHold();yield return Until(()=>attack.ChargeElapsed>=1.2f,"Assisted preview");
        attack.GetChargeGuidePath(out _,out var preview,out _);
        Check(Vector3.Angle(preview,Vector3.right)>10f,"Guide includes configured aim attraction toward a target");
        actor.SetScriptedInput(Frame(up:true));yield return Until(()=>attack.IsAttacking,"Assisted release");
        Check(Vector3.Angle(preview,attack.CurrentAttackDirectionWS)<.05f,"Telegraphed heading agrees with actual assisted launch");
        tuning.combat.lockOnEnabled=false;Object.Destroy(targetRoot);yield return Ready();
    }
    static void CaptureGuide(PlayerThrustChargeGuide guide,string name,bool validate)
    {
        var go=new GameObject("Charge guide capture");var cam=go.AddComponent<Camera>();cam.enabled=false;
        cam.cameraType=CameraType.Preview;cam.orthographic=true;cam.orthographicSize=1.55f;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.03f,.045f);cam.allowHDR=false;
        cam.transform.SetPositionAndRotation(actor.transform.position+Vector3.right*1.45f+Vector3.up*20,
            Quaternion.LookRotation(Vector3.down,Vector3.forward));
        var rt=new RenderTexture(1200,650,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;
        var pixels=new Texture2D(1200,650,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
        Color original=tuning.combat.thrustChargeGuideColor;
        try {
            guide.DrawShapes(cam);cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1200,650),0,0);pixels.Apply();
            File.WriteAllBytes(Output+"/"+name,pixels.EncodeToPNG());
            if(validate) {
                tuning.combat.thrustChargeGuideColor=Color.cyan;guide.DrawShapes(cam);cam.Render();RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,1200,650),0,0);pixels.Apply();
                File.WriteAllBytes(Output+"/guide-pixel-check.png",pixels.EncodeToPNG());
                guide.TryGetArrow(4,out var left,out var tip,out var right,out _);
                bool CyanNear(Vector3 world) {
                    var uv=cam.WorldToViewportPoint(world);int x=Mathf.RoundToInt(uv.x*1200),y=Mathf.RoundToInt(uv.y*650);
                    for(int iy=-3;iy<=3;iy++)for(int ix=-3;ix<=3;ix++) {
                        var c=pixels.GetPixel(Mathf.Clamp(x+ix,0,1199),Mathf.Clamp(y+iy,0,649));if(c.r<.3f && c.g>.55f && c.b>.55f)return true; }
                    return false;
                }
                Check(CyanNear(Vector3.Lerp(left,tip,.5f)) && CyanNear(Vector3.Lerp(tip,right,.5f)),"Rendered chevron has both visible forward legs");
                Check(!CyanNear((left+right)*.5f),"Rendered triangle remains open at the back");
            }
        }
        finally { tuning.combat.thrustChargeGuideColor=original;RenderTexture.active=previous;cam.targetTexture=null;
            rt.Release();Object.Destroy(rt);Object.Destroy(pixels);Object.Destroy(go); }
    }
}
#endif
