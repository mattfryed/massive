// Assets/VectorGridNu/Editor/GridInteractionModuleDrawer.cs
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(GridInteractionModule))]
public class GridInteractionModuleDrawer : PropertyDrawer
{
    static float LH => EditorGUIUtility.singleLineHeight;
    static float SP => EditorGUIUtility.standardVerticalSpacing;

    // --- Utility: draw a small bold section label ---
    static void SectionLabel(ref float y, Rect r, string text)
    {
        var style = EditorStyles.miniBoldLabel;
        EditorGUI.LabelField(new Rect(r.x, y, r.width, LH), text, style);
        y += LH + SP;
    }

    public override float GetPropertyHeight(SerializedProperty p, GUIContent label)
    {
        int lines = 0;
        var type = (GridModuleType)p.FindPropertyRelative("type").enumValueIndex;

        lines += 1; // Tag+Type
        lines += 1; // "Common" row
        if (type == GridModuleType.DirectionalWake) lines += 2; // section label + dir row

        // Scaling
        lines += 2; // section label + toggle
        if (p.FindPropertyRelative("scaleBySpeed").boolValue) lines += 2; // two curves

        // Type-specific
        if (type == GridModuleType.Vortex)               lines += 2; // section label + spin
        if (type == GridModuleType.Jiggle)               lines += 3; // section label + 2 fields
        if (type == GridModuleType.BurstRadial ||
            type == GridModuleType.Pulse)                lines += 3; // section label + duration/flags + envelope
        if (type == GridModuleType.TravelingWave)        lines += 3; // section label + speed/thickness

        // Local tuning (ConstantRadial only)
        if (type == GridModuleType.ConstantRadial)
        {
            lines += 2; // section label + enable toggle
            if (p.FindPropertyRelative("tuningEnabled").boolValue) lines += 4; // 3 rows + blend
        }

        return lines * (LH + SP) + 6;
    }

    public override void OnGUI(Rect r, SerializedProperty p, GUIContent label)
    {
        float x = r.x, y = r.y, w = r.width;

        var pTag   = p.FindPropertyRelative("tag");
        var pType  = p.FindPropertyRelative("type");
        var pRad   = p.FindPropertyRelative("radius");
        var pStr   = p.FindPropertyRelative("strength");
        var pInner = p.FindPropertyRelative("innerFrac");

        var pDir      = p.FindPropertyRelative("directional");
        var pUseVel   = p.FindPropertyRelative("useVelocity");
        var pPullBack = p.FindPropertyRelative("pullAgainstVelocity");
        var pFixed    = p.FindPropertyRelative("fixedDirection");

        var pScale    = p.FindPropertyRelative("scaleBySpeed");
        var pRadOver  = p.FindPropertyRelative("radiusOverSpeed");
        var pStrOver  = p.FindPropertyRelative("strengthOverSpeed");

        var pSpin     = p.FindPropertyRelative("spinDegPerSec");
        var pNAmp     = p.FindPropertyRelative("noiseAmplitude");
        var pNFrq     = p.FindPropertyRelative("noiseFrequency");

        var pDur      = p.FindPropertyRelative("duration");
        var pAuto     = p.FindPropertyRelative("autoStart");
        var pLoop     = p.FindPropertyRelative("loopPulse");
        var pEnv      = p.FindPropertyRelative("envelope");

        var pWSpd     = p.FindPropertyRelative("waveSpeed");
        var pWThk     = p.FindPropertyRelative("waveThickness");

        var pTuneOn   = p.FindPropertyRelative("tuningEnabled");
        var pSpringK  = p.FindPropertyRelative("tuningSpringK");
        var pDamping  = p.FindPropertyRelative("tuningDamping");
        var pFallMode = p.FindPropertyRelative("tuningFalloffMode");
        var pFallExp  = p.FindPropertyRelative("tuningFalloffExp");
        var pSharp    = p.FindPropertyRelative("tuningSharpness");
        var pMaxSpd   = p.FindPropertyRelative("tuningMaxSpeed");
        var pBlend    = p.FindPropertyRelative("tuningBlend");

        var type = (GridModuleType)pType.enumValueIndex;

        // Tag + Type
        float half = (w - 8) * 0.5f;
        EditorGUI.PropertyField(new Rect(x, y, half, LH), pTag);
        EditorGUI.PropertyField(new Rect(x + half + 8, y, half, LH), pType);
        y += LH + SP;

        // Common
        SectionLabel(ref y, r, "Common");
        float third = (w - 16) / 3f;
        EditorGUI.PropertyField(new Rect(x, y, third, LH), pRad);
        EditorGUI.PropertyField(new Rect(x + third + 8, y, third, LH), pStr);
        EditorGUI.Slider      (new Rect(x + (third + 8) * 2, y, third, LH), pInner, 0f, 0.9f, new GUIContent("Inner Frac"));
        y += LH + SP;

        // Direction (DirectionalWake only)
        if (type == GridModuleType.DirectionalWake)
        {
            SectionLabel(ref y, r, "Directional");
            float q = (w - 18) / 4f;
            EditorGUI.PropertyField(new Rect(x, y, q, LH), pUseVel, new GUIContent("Use Velocity"));
            EditorGUI.PropertyField(new Rect(x + q + 6, y, q, LH), pPullBack, new GUIContent("Pull Behind"));
            EditorGUI.PropertyField(new Rect(x + (q + 6)*2, y, q, LH), pDir, new GUIContent("Directional"));
            EditorGUI.PropertyField(new Rect(x + (q + 6)*3, y, q, LH), pFixed, new GUIContent("Fixed Dir"));
            y += LH + SP;
        }

        // Scaling
        SectionLabel(ref y, r, "Scaling");
        EditorGUI.PropertyField(new Rect(x, y, w, LH), pScale, new GUIContent("Scale By Speed"));
        y += LH + SP;
        if (pScale.boolValue)
        {
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pRadOver, new GUIContent("Radius Over Speed"));
            y += LH + SP;
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pStrOver, new GUIContent("Strength Over Speed"));
            y += LH + SP;
        }

        // Type-specific
        if (type == GridModuleType.Vortex)
        {
            SectionLabel(ref y, r, "Vortex");
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pSpin, new GUIContent("Spin Deg Per Sec"));
            y += LH + SP;
        }
        else if (type == GridModuleType.Jiggle)
        {
            SectionLabel(ref y, r, "Jiggle Noise");
            EditorGUI.PropertyField(new Rect(x, y, w * 0.5f - 6, LH), pNAmp, new GUIContent("Noise Amplitude"));
            EditorGUI.PropertyField(new Rect(x + w * 0.5f + 6, y, w * 0.5f - 6, LH), pNFrq, new GUIContent("Noise Frequency"));
            y += LH + SP;
        }
        else if (type == GridModuleType.BurstRadial || type == GridModuleType.Pulse)
        {
            SectionLabel(ref y, r, (type == GridModuleType.Pulse) ? "Pulse" : "Burst");
            float col = (w - 8) / 3f;
            EditorGUI.PropertyField(new Rect(x, y, col, LH), pDur,  new GUIContent("Duration"));
            EditorGUI.PropertyField(new Rect(x + col + 6, y, col, LH), pAuto, new GUIContent("Auto Start"));
            EditorGUI.PropertyField(new Rect(x + (col + 6) * 2, y, col, LH), pLoop, new GUIContent("Loop"));
            y += LH + SP;

            EditorGUI.PropertyField(new Rect(x, y, w, LH), pEnv, new GUIContent("Envelope"));
            y += LH + SP;
        }
        else if (type == GridModuleType.TravelingWave)
        {
            SectionLabel(ref y, r, "Traveling Wave");
            EditorGUI.PropertyField(new Rect(x, y, w * 0.5f - 6, LH), pWSpd, new GUIContent("Wave Speed"));
            EditorGUI.PropertyField(new Rect(x + w * 0.5f + 6, y, w * 0.5f - 6, LH), pWThk, new GUIContent("Wave Thickness"));
            y += LH + SP;
        }

        // Local tuning (ConstantRadial only)
        if (type == GridModuleType.ConstantRadial)
        {
            SectionLabel(ref y, r, "Local Tuning (ConstantRadial)");
            EditorGUI.PropertyField(new Rect(x, y, w, LH), pTuneOn, new GUIContent("Enable"));
            y += LH + SP;

            if (pTuneOn.boolValue)
            {
                float halfW = (w - 8) * 0.5f;
                EditorGUI.PropertyField(new Rect(x, y, halfW, LH), pSpringK, new GUIContent("Spring K"));
                EditorGUI.PropertyField(new Rect(x + halfW + 8, y, halfW, LH), pDamping, new GUIContent("Damping"));
                y += LH + SP;

                EditorGUI.IntPopup(new Rect(x, y, halfW, LH), pFallMode,
                    new[] { new GUIContent("Linear"), new GUIContent("Smooth"), new GUIContent("Quadratic"), new GUIContent("Gaussian"), new GUIContent("InvSq") },
                    new[] { 0,1,2,3,4 }, new GUIContent("Falloff Mode"));
                EditorGUI.PropertyField(new Rect(x + halfW + 8, y, halfW, LH), pFallExp, new GUIContent("Falloff Exp"));
                y += LH + SP;

                EditorGUI.PropertyField(new Rect(x, y, halfW, LH), pSharp, new GUIContent("Sharpness"));
                EditorGUI.PropertyField(new Rect(x + halfW + 8, y, halfW, LH), pMaxSpd, new GUIContent("Max Speed"));
                y += LH + SP;

                EditorGUI.Slider(new Rect(x, y, w, LH), pBlend, 0f, 1f, new GUIContent("Blend"));
                y += LH + SP;
            }
        }
    }
}
