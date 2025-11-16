#ifndef BLOB_DEFORM_INCLUDED
#define BLOB_DEFORM_INCLUDED

static const float MEAN_PROJ = 0.32; // avg of max(dot,0) around circle (≈1/π)

float2 NormSafe(float2 v){ float l = max(length(v), 1e-6); return v/l; }

float SymIdle(float idleWobble, float noisePhase, float ang){
    float band = sin(noisePhase*2.3 + ang*5.0)*0.6
               + sin(noisePhase*1.3 + ang*9.0)*0.4;
    return idleWobble * 0.08 * band;
}

float HitRipple(float hitImpulse, float noisePhase, float ang){
    return hitImpulse * sin(noisePhase*20.0 + ang*8.0) * 0.10;
}

float DirStretch(float deformAmt, float2 deformDir, float frontGain, float backGain, float areaKeep, float2 dirUnit){
    if (deformAmt <= 1e-6) return 0.0;
    float2 ax = NormSafe(deformDir);
    float f = max(dot(dirUnit,  ax), 0.0);
    float b = max(dot(dirUnit, -ax), 0.0);
    float sRaw = f*frontGain - b*backGain;
    float sZeroMean = sRaw - areaKeep * MEAN_PROJ * (frontGain - backGain);
    return deformAmt * sZeroMean;
}

float ExpectedRadius(
    float baseR, float idleWobble, float deformAmt, float2 deformDir,
    float frontGain, float backGain, float areaKeep,
    float hitImpulse, float noisePhase,
    float ang)
{
    float2 u = float2(cos(ang), sin(ang));
    float sSym = 1.0 + SymIdle(idleWobble, noisePhase, ang) + HitRipple(hitImpulse, noisePhase, ang);
    float sDir = DirStretch(deformAmt, deformDir, frontGain, backGain, areaKeep, u);
    return baseR * (sSym + sDir);
}

#endif
