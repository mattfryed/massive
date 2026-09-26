using System;

/// <summary>Finite, frame-rate independent rules for one territory-control opportunity.</summary>
public sealed class SymmetryKnotCaptureState
{
    public enum Stage { Forming, Active, Retiring, Finished }
    public Stage Phase { get; private set; }
    public int OwnerTeam { get; private set; } = -1;
    public bool Contested { get; private set; }
    public bool HasBeenClaimed { get; private set; }
    public float Hold01 => ramp <= 0 ? (OwnerTeam > 0 ? 1 : 0) : (float)Math.Min(1, hold / ramp);
    public float SecondsRemaining => (float)Math.Max(0, expiresAt - age);
    public float UncontestedSeconds { get; private set; }
    public float ContestedSeconds { get; private set; }
    public float FirstArrivalSeconds { get; private set; } = -1;
    public int OwnershipChanges { get; private set; }

    private readonly double formation, claimed, ramp, interval, outro;
    private double age, expiresAt, hold, retiringAge;
    private double tickTime;
    private int lastClaimedTeam = -1;

    public SymmetryKnotCaptureState(float formationSeconds, float unclaimedSeconds,
        float claimedSeconds, float rampSeconds, float tickSeconds, float outroSeconds)
    {
        formation = Math.Max(0, formationSeconds);
        expiresAt = formation + Math.Max(.01f, unclaimedSeconds);
        claimed = Math.Max(0, claimedSeconds);
        ramp = Math.Max(0, rampSeconds);
        interval = Math.Max(.02f, tickSeconds);
        outro = Math.Max(.01f, outroSeconds);
        Phase = formation > 0 ? Stage.Forming : Stage.Active;
    }

    // teamMask: 0 empty, 1 Light, 2 Dark, 3 contested. Extra players cannot increase income.
    public int Tick(float dt, int teamMask, bool clockRunning)
    {
        if (!clockRunning || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt) || Phase == Stage.Finished) return 0;
        if (Phase == Stage.Retiring)
        {
            retiringAge += dt;
            if (retiringAge >= outro) Phase = Stage.Finished;
            return 0;
        }
        double remainingDt = dt;
        if (Phase == Stage.Forming)
        {
            double used = Math.Min(remainingDt, Math.Max(0, formation - age));
            age += used; remainingDt -= used;
            if (age < formation) return 0;
            Phase = Stage.Active;
        }
        if (age >= expiresAt) { Retire(); return 0; }
        teamMask &= 3;
        Contested = teamMask == 3;
        int candidate = teamMask == 1 ? 1 : teamMask == 2 ? 2 : -1;
        if (teamMask != 0 && FirstArrivalSeconds < 0) FirstArrivalSeconds = (float)age;
        if (!Contested && candidate != OwnerTeam)
        {
            OwnerTeam = candidate; hold = 0; tickTime = 0;
            if (candidate > 0)
            {
                if (lastClaimedTeam > 0 && lastClaimedTeam != candidate) OwnershipChanges++;
                lastClaimedTeam = candidate;
                if (!HasBeenClaimed)
                {
                    HasBeenClaimed = true;
                    expiresAt = Math.Max(expiresAt, age + claimed);
                }
            }
        }
        double activeDt = Math.Min(remainingDt, Math.Max(0, expiresAt - age));
        int ticks = 0;
        if (Contested)
        {
            ContestedSeconds += (float)activeDt;
            // Contest pauses the ramp, but never carries a fractional award through a contest.
            tickTime = 0;
        }
        else if (OwnerTeam > 0)
        {
            UncontestedSeconds += (float)activeDt;
            double warmup = Math.Min(activeDt, Math.Max(0, ramp - hold));
            hold += activeDt;
            tickTime += activeDt - warmup;
            ticks = (int)Math.Floor((tickTime + 0.000001) / interval);
            tickTime = Math.Max(0, tickTime - ticks * (double)interval);
        }
        age += activeDt;
        // Caller delivers ticks earned before the exact deadline, then retires presentation.
        if (age >= expiresAt) Phase = Stage.Retiring;
        return ticks;
    }

    public void Retire()
    {
        if (Phase == Stage.Finished || Phase == Stage.Retiring) return;
        Phase = Stage.Retiring; OwnerTeam = -1; Contested = false; hold = 0; tickTime = 0;
    }
}
