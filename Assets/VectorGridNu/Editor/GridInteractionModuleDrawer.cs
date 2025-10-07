// Assets/VectorGridNu/Editor/GridInteractionModuleDrawer.cs
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(GridInteractionModule))]
public class GridInteractionModuleDrawer : PropertyDrawer
{
    static float LH => EditorGUIUtility.singleLineHeight;
    static float SP => EditorGUIUtility.standardVerticalSpacing;

    public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
    {
        int lines = 0;
        var type = (GridModuleType)p.FindPropertyRelative("type").enumValueIndex;

        // Tag + Type
        lines += 2;

        // Common
        lines += 3; // radius, strength, inner

        // Directional (DirectionalWake only)
        if (type == GridModuleType.DirectionalWake) lines += 4; // useVel, pullBehind, directional, fixedDir

        // Scaling
        lines += 1; // scaleBySpeed
        if (p.FindPropertyRelative("scaleBySpeed").boolValue) lines += 2; // two curves

        // Type specific
        if (type == GridModuleType.Vortex)               lines += 1; // spin
        if (type == GridModuleType.Jiggle)               lines += 2; // noise amp/freq
        if (type == GridModuleType.BurstRadial ||
            type == GridModuleType.Pulse)                lines += 3; // duration, auto, loop + envelope(1 line)
        if (type == GridModuleType.TravelingWave)        lines += 2; // speed, thickness

        // Local tuning (ConstantRadial)
        if (type == GridModuleType.ConstantRadial)
        {
            lines += 1; // tuningEnabled
            if (p.FindPropertyRelative("tuningEnabled").boolValue)
                lines += 4; // springK, damping, falloffMode+exp, sharp+max, + blend
            lines += 1; // blend
        }

        return lines * (LH + SP) + 6;
    }

    public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
    {
        float y = r.y;
        float w = r.width;
        float x = r.x;

        // Grab props
        var pTag   = p.FindPropertyRelative("tag");
        var pType  = p.FindPropertyRelative("type");
        var pRad   = p.FindPropertyRelative("radius");
        var pStr   = p.FindPropertyRelative("strength");
        var pInner = p.FindPropertyRelative("innerFrac");

        var pDir    = p.FindPropertyRelative("directional");
        var pUseVel = p.FindPropertyRelative("useVelocity");
        var pPull   = p.FindPropertyRelative("pullAgainstVelocity");
        var pFixed  = p.FindPropertyRelative("fixedDirection");

        var pScale  = p.FindPropertyRelative("scaleBySpeed");
        var pRadOv  = p.FindPropertyRelative("radiusOverSpeed");
        var pStrOv  = p.FindPropertyRelative("strengthOverSpeed");

        var pSpin   = p.FindPropertyRelative("spinDegPerSec");
        var pNAmp   = p.FindPropertyRelative("noiseAmplitude");
        var pNFrq   = p.FindPropertyRelative("noiseFrequency");

        var pDur    = p.FindPropertyRelative("duration");
        var pAuto   = p.FindPropertyRelative("autoStart");
        var pLoop   = p.FindPropertyRelative("loopPulse");
        var pEnv    = p.FindPropertyRelative("envelope");
        var pRadTime = p.FindPropertyRelative("radiusOverTime");
        var pRepel  = p.FindPropertyRelative("repel");           // NEW


        var pWSpd   = p.FindPropertyRelative("waveSpeed");
        var pWThk   = p.FindPropertyRelative("waveThickness");

        var pTuneOn = p.FindPropertyRelative("tuningEnabled");
        var pSpring = p.FindPropertyRelative("tuningSpringK");
        var pDamp   = p.FindPropertyRelative("tuningDamping");
        var pFallM  = p.FindPropertyRelative("tuningFalloffMode");
        var pFallE  = p.FindPropertyRelative("tuningFalloffExp");
        var pSharp  = p.FindPropertyRelative("tuningSharpness");
        var pMaxSpd = p.FindPropertyRelative("tuningMaxSpeed");
        var pBlend  = p.FindPropertyRelative("tuningBlend");

        var type = (GridModuleType)pType.enumValueIndex;

        // Set a stable label width so nothing overlaps
        float oldLabel = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 140f;

        // Tag + Type
        EditorGUI.PropertyField(new Rect(x, y, w, LH), pTag);  y += LH + SP;
        EditorGUI.PropertyField(new Rect(x, y, w, LH), pType); y += LH + SP;

        // Common
        EditorGUI.PropertyField(new Rect(x, y, w, LH), pRad,   new GUIContent("Radius"));   y += LH + SP;
        EditorGUI.PropertyField(new Rect(x, y, w, LH), pStr,   new GUIContent("Strength")); y += LH + SP;
        EditorGUI.Slider      (new Rect(x, y, w, LH), pInner, 0f, 0.9f, new GUIContent("Inner Frac")); y += LH + SP;

        // Directional if needed
        if (type == GridModuleType.DirectionalWake)
        {
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pUseVel, new GUIContent("Use Velocity")); y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pPull,   new GUIContent("Pull Behind"));  y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pDir,    new GUIContent("Directional"));  y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pFixed,  new GUIContent("Fixed Direction")); y += LH + SP;
        }

        // Scaling
        EditorGUI.PropertyField(new Rect(x, y, w, LH), pScale, new GUIContent("Scale By Speed")); y += LH + SP;
        if (pScale.boolValue)
        {
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pRadOv, new GUIContent("Radius Over Speed"));   y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pStrOv, new GUIContent("Strength Over Speed")); y += LH + SP;
        }

        // Type-specific
        if (type == GridModuleType.Vortex)
        {
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pSpin, new GUIContent("Spin Deg / Sec")); y += LH + SP;
        }
        else if (type == GridModuleType.Jiggle)
        {
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pNAmp, new GUIContent("Noise Amplitude")); y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pNFrq, new GUIContent("Noise Frequency")); y += LH + SP;
        }
        else if (type == GridModuleType.BurstRadial || type == GridModuleType.Pulse)
        {
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pDur, new GUIContent("Duration (s)")); y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pAuto, new GUIContent("Auto Start")); y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pLoop, new GUIContent("Loop (Pulse)")); y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pEnv, new GUIContent("Envelope")); y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pRadTime, new GUIContent("Radius Over Time")); y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pRepel, new GUIContent("Repel (Outward)")); y += LH + SP;  // NEW

        }
        else if (type == GridModuleType.TravelingWave)
        {
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pWSpd, new GUIContent("Wave Speed")); y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pWThk, new GUIContent("Wave Thickness")); y += LH + SP;
        }

        // Local tuning
        if (type == GridModuleType.ConstantRadial)
        {
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pTuneOn, new GUIContent("Local Tuning Override")); y += LH + SP;
            if (pTuneOn.boolValue)
            {
                EditorGUI.PropertyField(new Rect(x, y, w, LH), pSpring, new GUIContent("Spring K")); y += LH + SP;
                EditorGUI.PropertyField(new Rect(x, y, w, LH), pDamp,   new GUIContent("Damping"));  y += LH + SP;

                // Falloff row
                Rect row = new Rect(x, y, w, LH);
                float half = (w - 10) * 0.5f;
                EditorGUI.IntPopup(new Rect(row.x, row.y, half, LH), pFallM,
                    new[] { new GUIContent("Linear"), new GUIContent("Smooth"), new GUIContent("Quadratic"), new GUIContent("Gaussian"), new GUIContent("InvSq") },
                    new[] { 0,1,2,3,4 }, new GUIContent("Falloff Mode"));
                EditorGUI.PropertyField(new Rect(row.x + half + 10, row.y, half - 10, LH), pFallE, new GUIContent("Falloff Exp"));
                y += LH + SP;

                EditorGUI.PropertyField(new Rect(x, y, w, LH), pSharp,  new GUIContent("Sharpness")); y += LH + SP;
                EditorGUI.PropertyField(new Rect(x, y, w, LH), pMaxSpd, new GUIContent("Max Speed")); y += LH + SP;

                EditorGUI.Slider(new Rect(x, y, w, LH), pBlend, 0f, 1f, new GUIContent("Tuning Blend")); y += LH + SP;
            }
        }

        EditorGUIUtility.labelWidth = oldLabel;
    }
}
