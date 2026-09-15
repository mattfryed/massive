using Massive.AttractStudy;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttractFerrofluidStudy)),CanEditMultipleObjects]
public sealed class AttractFerrofluidInspector : Editor
{
    bool showSurface,showInput,showBindings;
    void Field(string name,string label,string tooltip=null)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(label,tooltip));
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Attract sphere",EditorStyles.boldLabel);
        Field("useFerrofluid","Use Ferrofluid Sphere","Off restores the existing sphere and floating title. Settings are retained.");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lettering",EditorStyles.boldLabel);
        Field("embeddedLogo","Embed MASSIVE Lettering","On places white lettering in the sphere. Off restores the original floating title.");
        Field("logoScale","Lettering Scale","Uniform scale on the sphere; 1 is the approved study size. Range 0.25–1.3.");
        Field("logoRecess","Embed Depth","Depth below the reference surface, in sphere-local units.");
        Field("logoElevation","Vertical Position","Vertical angle of the lettering around the sphere.");
        Field("logoSurfaceFlow","Ridge Undulation","How strongly the mounds move the outer ridge where it joins the liquid.");
        Field("logoTypeFlow","Type Undulation","0 keeps the white letter faces steady. 1 follows the full mound motion. Independent of Ridge Undulation.");
        EditorGUILayout.HelpBox("Controls update live in Play Mode. Set them before entering Play Mode to save your preferred scene defaults. Embedded lettering is used when the ferrofluid sphere is on.",MessageType.Info);
        showSurface=EditorGUILayout.Foldout(showSurface,"Surface and lighting",true);
        if(showSurface)
        {
            Field("surfaceDensity","Surface Density");Field("surfaceRelief","Surface Relief");
            Field("motionSpeed","Motion Speed");Field("wetness","Highlight Coverage");
            Field("rimWidth","Rim Width");Field("rimAngle","Rim Angle");
            Field("faceResolution","Mesh Resolution","Applies the next time the ferrofluid sphere is enabled.");
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
            Field("originalLogo","Original Title");Field("attractionCamera","Input Camera");
            Field("logoArcWidth","Base Lettering Arc","Angular width at scale 1. The default is 2.2 radians.");
        }
        serializedObject.ApplyModifiedProperties();
    }
}
