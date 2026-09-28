using Massive.AttractStudy;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttractFerrofluidStudy)),CanEditMultipleObjects]
public sealed class AttractFerrofluidInspector : Editor
{
    bool showSurface,showInput,showBindings;
    static readonly GUIContent MsaaLabel=new GUIContent("Game MSAA", "Changes anti-aliasing for the active Quality preset across the game. Saved as a project setting.");
    static readonly string[] MsaaNames={"4x MSAA", "8x MSAA"};
    static void DrawMsaaControl()
    {
        int samples=QualitySettings.antiAliasing;
        int selected=samples==4?0:samples==8?1:-1;
        EditorGUI.BeginChangeCheck();
        int next=EditorGUILayout.Popup(MsaaLabel,selected,MsaaNames);
        if(EditorGUI.EndChangeCheck() && next>=0)SetMsaa(next==0?4:8);
        EditorGUILayout.LabelField("Quality preset",QualitySettings.names[QualitySettings.GetQualityLevel()]);
    }
    public static void SetMsaa(int samples)
    {
        if(samples!=4 && samples!=8)throw new System.ArgumentOutOfRangeException(nameof(samples));
        var quality=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
        Undo.RecordObject(quality,"Change game MSAA");
        QualitySettings.antiAliasing=samples;
        EditorUtility.SetDirty(quality);
        AssetDatabase.SaveAssetIfDirty(quality);
    }

    void Field(string name,string label,string tooltip=null)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(label,tooltip));
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Attract sphere",EditorStyles.boldLabel);
        Field("useFerrofluid","Use Ferrofluid Sphere","Off restores the existing sphere. Settings are retained.");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lettering",EditorStyles.boldLabel);
        Field("embeddedLogo","Embed MASSIVE Lettering","Show or hide the white lettering embedded in the sphere.");
        Field("logoScale","Lettering Scale","Uniform scale on the sphere; 1 is the approved study size. Range 0.25–1.3.");
        Field("logoLetterSpacing","Letter Spacing","Adds space between the seven letters without stretching them. Zero retains the original spacing; negative values tighten it.");
        Field("logoRecess","Embed Depth","Depth below the reference surface, in sphere-local units.");
        Field("logoElevation","Vertical Position","Vertical angle of the lettering around the sphere.");
        Field("logoSurfaceFlow","Ridge Undulation","How strongly the mounds move the outer ridge where it joins the liquid.");
        Field("logoLipFlow","Outer Lip Flow","0 retains the original ridge. 1 lets the outer lip follow the mound heights and slopes more strongly. Uses Ridge Undulation; preserves the inner white outline.");
        Field("logoTypeFlow","Type Undulation","0 keeps the white letter faces steady. 1 follows the full mound motion. Independent of Ridge Undulation.");
        EditorGUILayout.Space();
        Field("animateLogoArrival","Animate Lettering Arrival","Uncovers the embedded title from the liquid when the scene or sphere starts.");
        if(serializedObject.FindProperty("animateLogoArrival").boolValue)
        {
            Field("logoArrivalDelay","Arrival Delay","Seconds the sphere stays completely covered before the liquid recedes.");
            Field("logoArrivalDuration","Reveal Duration","Seconds for the liquid to withdraw from the centers of the letter strokes.");
            using(new EditorGUI.DisabledScope(!Application.isPlaying))
                if(GUILayout.Button("Replay Lettering Arrival"))
                    foreach(var item in targets)((AttractFerrofluidStudy)item).ReplayLogoArrival();
        }
        EditorGUILayout.HelpBox("Controls update live in Play Mode. Set them before entering Play Mode to save your preferred scene defaults. Embedded lettering is used when the ferrofluid sphere is on.",MessageType.Info);
        EditorGUILayout.Space();
        DrawMsaaControl();
        EditorGUILayout.HelpBox("MSAA changes the active Quality preset for the whole game, including future builds. This project setting is saved even in Play Mode.",MessageType.None);
        showSurface=EditorGUILayout.Foldout(showSurface,"Surface and lighting",true);
        if(showSurface)
        {
            Field("surfaceDensity","Surface Density");Field("surfaceRelief","Surface Relief");
            Field("motionSpeed","Motion Speed");Field("wetness","Highlight Coverage");
            Field("rimWidth","Rim Width");Field("rimAngle","Rim Angle");
            Field("faceResolution","Mesh Resolution","Applies the next time the ferrofluid sphere is enabled.");
            Field("optimizeBodyGeometry","Optimize Body Geometry","Caps body resolution at 128 while retaining lettering detail. This can make reflection borders more angular. Applies on enable; disable to use the full Mesh Resolution.");
            int requested=Mathf.Clamp(serializedObject.FindProperty("faceResolution").intValue,64,192);
            int body=serializedObject.FindProperty("optimizeBodyGeometry").boolValue?Mathf.Min(requested,128):requested;
            EditorGUILayout.LabelField("Body Resolution on Enable",body.ToString());
            Field("trimHiddenRear","Trim Hidden Rear","Keeps the visible surface and a conservative rear skirt. Applies on enable. Expands coverage if relief changes; restores the full shell if the viewing direction changes.");
        }
        showInput=EditorGUILayout.Foldout(showInput,"Joystick response",true);
        if(showInput)
        {
            Field("joystickAttraction","Joystick Attraction");Field("joystickDeadzone","Joystick Deadzone");
            Field("attractionSmoothTime","Response Smoothing");Field("attractionReach","Attraction Reach");
        }
        showBindings=EditorGUILayout.Foldout(showBindings,"Asset references and advanced layout",true);
        if(showBindings)
        {
            Field("surfaceShader","Surface Shader");Field("logoDistanceField","Lettering Shape");
            Field("logoGlyphDistanceFields","Isolated Letter Fields");Field("attractionCamera","Input Camera");
            Field("logoFieldShader","Letter Field Composer");
            Field("logoArcWidth","Base Lettering Arc","Angular width at scale 1. The default is 2.2 radians.");
        }
        serializedObject.ApplyModifiedProperties();
    }
}
