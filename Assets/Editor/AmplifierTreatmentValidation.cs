using System;
using Massive.Multiplier;
using UnityEngine;
using UnityEditor;

public static class AmplifierTreatmentValidation
{
    [MenuItem("MASSIVE/Validate Amplifier Treatments")]
    public static void Run()
    {
        var t = UnityEngine.Object.FindFirstObjectByType<AmplifierGoalTreatments>();
        if (t == null) throw new InvalidOperationException("Install treatment workbench first.");
        string saved = JsonUtility.ToJson(t.Settings); float oldClock = t.Clock; float speed = t.PlaybackSpeed;
        int oldPreset=t.PresetIndex, oldLevel=t.HeldLevel, oldTeam=t.PreviewTeam; bool oldPreview=t.Preview;
        int assertions = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); assertions++; };
        try
        {
            t.ApplyPreset(0); t.Settings.heldCharge = false; t.Settings.emissions = 1;
            t.ResetPreview(); t.Capture(1);
            t.SetPreviewTime(0.1f);
            check(t.BoundaryOffset(1,1,0,out _).sqrMagnitude == 0, "Packets preceded absorption.");
            t.SetPreviewTime(0.82f);
            Vector2 up=t.BoundaryOffset(1.7f,1,0,out float strengthUp), down=t.BoundaryOffset(-1.7f,1,0,out float strengthDown);
            check(Mathf.Abs(up.x-down.x)<0.0001f && Mathf.Abs(up.y+down.y)<0.0001f, "Opposed packets lost symmetry.");
            check(strengthUp>0.05f && Mathf.Abs(strengthUp-strengthDown)<0.0001f, "Packet failed to propagate.");
            t.Settings.capturePackets=false;
            check(t.BoundaryOffset(1.7f,1,0,out _).sqrMagnitude==0, "Capture packet toggle leaked displacement.");
            t.Settings.capturePackets=true; t.SetPreviewTime(10);
            check(t.BoundaryOffset(1,1,0,out _).sqrMagnitude==0, "Capture did not settle.");
            t.Settings.heldCharge=true;t.Settings.travellingWave=true;t.SetPreviewTime(10.1f);
            check(t.BoundaryOffset(1,1,0,out _).sqrMagnitude==0, "Neutral x1 is charged.");
            check(t.BoundaryOffset(1,1,3,out _).sqrMagnitude>0, "Held x8 stopped moving.");
            var before=t.BoundaryOffset(1,1,3,out _);t.PlaybackSpeed=4;
            check((before-t.BoundaryOffset(1,1,3,out _)).sqrMagnitude==0, "Playback altered wave parameters at fixed time.");
            t.Settings.gridCoupling=0;
            check(t.BoundaryOffset(1,1,3,out _).sqrMagnitude==0,"Grid coupling zero leaked displacement.");
            var gridBlock=new MaterialPropertyBlock();t.WriteGridProperties(gridBlock);
            foreach(var state in gridBlock.GetVectorArray("_AmpStates")) check(state==Vector4.zero,"Grid coupling failed to mute a grid layer.");
            t.ApplyPreset(0);t.Preview=true;t.PreviewTeam=1;t.HeldLevel=2;t.RenderTreatment();
            var serialized=new SerializedObject(t);var surfaces=serialized.FindProperty("massSurfaces");
            for(int side=0;side<2;side++)
            {
                var surface=surfaces.GetArrayElementAtIndex(side).objectReferenceValue as MetaballSDFInstance;
                check(surface!=null && surface.TargetRenderer!=null,"Missing goal surface.");
                var block=new MaterialPropertyBlock();surface.TargetRenderer.GetPropertyBlock(block);
                int ballCount=block.GetInt("_BallCount");var balls=block.GetVectorArray("_Balls");
                check(ballCount>0,"Empty mass surface.");
                t.Settings.wholeGoalPulse=false;t.Settings.corona=false;t.Settings.branching=true;t.RenderTreatment();
                surface.TargetRenderer.GetPropertyBlock(block);
                check(block.GetVector("_AmpGoalPulse").y==0,"Whole goal toggle leaked deformation.");
                check(block.GetVector("_AmpGoalMode").z==0 && block.GetVector("_AmpGoalMode").w==1,"Corona and branches cannot be isolated.");
                check(block.GetInt("_BallCount")==ballCount,"Treatment changed mass count.");
                var afterBalls=block.GetVectorArray("_Balls");
                for(int b=0;b<ballCount;b++)check(afterBalls[b]==balls[b],"Treatment changed mass geometry data.");
                t.enabled=false;surface.TargetRenderer.GetPropertyBlock(block);
                check(block.GetFloat("_AmpGoalEnabled")==0,"Disabled treatment left surface effects active.");t.enabled=true;
            }
            for(int preset=0;preset<4;preset++)
            {
                t.ApplyPreset(preset);t.ResetPreview();t.Capture(1);
                for(int level=0;level<4;level++)
                for(int sample=0;sample<24;sample++)
                {
                    t.SetPreviewTime(sample*0.2f);
                    var p=t.BoundaryOffset(-5.5f+sample*0.45f,1,level,out _);
                    check(!float.IsNaN(p.x)&&!float.IsInfinity(p.y),"Non-finite packet.");
                }
            }
            Debug.Log("Amplifier validation PASS: "+assertions+" assertions (absorption gate, opposed travel, isolation, decay, held levels, independent playback, all presets).");
        }
        finally { t.ApplyPreset(oldPreset);t.RestoreSettings(saved);t.PlaybackSpeed=speed;t.HeldLevel=oldLevel;t.PreviewTeam=oldTeam;t.Preview=oldPreview;t.ResetPreview();t.SetPreviewTime(oldClock); }
    }
}
