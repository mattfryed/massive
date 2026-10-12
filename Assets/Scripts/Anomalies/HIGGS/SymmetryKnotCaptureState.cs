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
    public int IncomeTicks { get; private set; }
    // The last Tick's award survives ownership loss at the exact end of forgiveness.
    public int AwardTeam { get; private set; } = -1;
    public int FirstAwardTickIndex { get; private set; }
    public double TickSeconds => interval;

    private readonly double formation, claimed, ramp, interval, outro, vacancyForgiveness;
    private double age, expiresAt, hold, retiringAge;
    private double tickTime;
    private double forgivenessRemaining;
    private int lastClaimedTeam = -1;
    private readonly bool resetIncomeOnContest;

    public SymmetryKnotCaptureState(float formationSeconds, float unclaimedSeconds,
        float claimedSeconds, float rampSeconds, float tickSeconds, float outroSeconds,
        bool resetIncomeOnContest = false, float vacancyForgivenessSeconds = 1.5f)
    {
        formation = Math.Max(0, formationSeconds);
        expiresAt = formation + Math.Max(.01f, unclaimedSeconds);
        claimed = Math.Max(0, claimedSeconds);
        ramp = Math.Max(0, rampSeconds);
        interval = Math.Max(.02f, tickSeconds);
        outro = Math.Max(.01f, outroSeconds);
        this.resetIncomeOnContest = resetIncomeOnContest;
        vacancyForgiveness = Math.Max(0, vacancyForgivenessSeconds);
        Phase = formation > 0 ? Stage.Forming : Stage.Active;
    }

    // teamMask: 0 empty, 1 Light, 2 Dark, 3 contested. Extra players cannot increase income.
    public int Tick(float dt, int teamMask, bool clockRunning)
    {
        AwardTeam = -1; FirstAwardTickIndex = 0;
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
        bool forgivenVacancy = teamMask == 0 && OwnerTeam > 0 && forgivenessRemaining > 0;
        if (forgivenVacancy) candidate = OwnerTeam;
        // Only uncontested physical presence renews grace. An opponent cancels
        // it immediately, so an emptied contest cannot resume remote control.
        else forgivenessRemaining = candidate > 0 ? vacancyForgiveness : 0;
        if (teamMask != 0 && FirstArrivalSeconds < 0) FirstArrivalSeconds = (float)age;
        if (!Contested && candidate != OwnerTeam)
        {
            OwnerTeam = candidate; hold = 0; tickTime = 0; IncomeTicks = 0;
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
            if (resetIncomeOnContest) IncomeTicks = 0;
        }
        else if (OwnerTeam > 0)
        {
            double controlDt = forgivenVacancy ? Math.Min(activeDt, forgivenessRemaining) : activeDt;
            UncontestedSeconds += (float)controlDt;
            double warmup = Math.Min(controlDt, Math.Max(0, ramp - hold));
            hold += controlDt;
            tickTime += controlDt - warmup;
            ticks = (int)Math.Floor((tickTime + 0.000001) / interval);
            tickTime = Math.Max(0, tickTime - ticks * (double)interval);
            AwardTeam = OwnerTeam; FirstAwardTickIndex = IncomeTicks;
            IncomeTicks += ticks;
            if (forgivenVacancy)
            {
                forgivenessRemaining = Math.Max(0, forgivenessRemaining - activeDt);
                if (forgivenessRemaining <= .000001)
                {
                    forgivenessRemaining = 0;
                    OwnerTeam = -1; hold = 0; tickTime = 0; IncomeTicks = 0;
                }
            }
        }
        age += activeDt;
        // Caller delivers ticks earned before the exact deadline, then retires presentation.
        if (age >= expiresAt) Phase = Stage.Retiring;
        return ticks;
    }

    public void Retire()
    {
        if (Phase == Stage.Finished || Phase == Stage.Retiring) return;
        Phase = Stage.Retiring; OwnerTeam = -1; Contested = false; hold = 0; tickTime = 0; IncomeTicks = 0;
        forgivenessRemaining = 0;
    }
}
