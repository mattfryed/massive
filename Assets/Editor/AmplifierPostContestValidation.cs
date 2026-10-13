#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Massive.Multiplier;
using Massive.Settings;
using UnityEditor;
using UnityEngine;

namespace Massive.EditorTools
{
    public static class AmplifierPostContestValidation
    {
        public static string Run()
        {
            var checks = new List<string>();
            void Check(bool pass, string label) { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
            var s = new AmplifierPossessionSettings { postContestGraceSeconds = .2f, postContestReleaseSeconds = .1f, exitSeconds = .32f };
            foreach (float fps in new[] { 30f, 60f, 144f })
            {
                var gate = new AmplifierPostContestInputGate(); gate.Begin(0f, s);
                Check(Mathf.Abs(gate.UnlockAt - .52f) < .0001f, "Protection includes exit plus grace at " + fps + " fps");
                bool fired = false;
                for (int frame = 0; frame < fps * 2; frame++)
                {
                    // Alternating buttons with one-frame neutral gaps, including after protection.
                    bool attack = frame % 4 == 0, shield = frame % 4 == 2;
                    fired |= gate.TryStartCharge(frame / fps, attack, attack, shield, shield);
                }
                Check(!fired && !gate.ReadyForPress, "Rapid contest tapping cannot arm a shot at " + fps + " fps");
                for (float t = 2f; t < 2.15f; t += 1f / fps) gate.TryStartCharge(t, false, false, false, false);
                Check(gate.ReadyForPress && gate.TryStartCharge(2.16f, true, true, false, false), "Stable release followed by a fresh press starts charge at " + fps + " fps");
            }
            {
                var gate = new AmplifierPostContestInputGate(); gate.Begin(0f, s);
                gate.TryStartCharge(.01f, false, false, false, false);
                gate.TryStartCharge(.2f, false, false, false, false);
                Check(!gate.TryStartCharge(.3f, true, true, false, false) && !gate.TryStartCharge(.8f, false, true, false, false),
                    "An Attack pressed during protection is discarded even if held beyond it");
                gate.TryStartCharge(1f, false, false, false, false); gate.TryStartCharge(1.12f, false, false, false, false);
                Check(!gate.TryStartCharge(1.13f, false, true, false, false), "Held-only input cannot substitute for a fresh Attack edge");
                gate.TryStartCharge(1.2f, false, false, false, false); gate.TryStartCharge(1.32f, false, false, false, false);
                Check(!gate.TryStartCharge(1.33f, true, true, true, true), "Pressing Attack and Shield together cannot start charge");
                gate.TryStartCharge(1.4f, false, false, false, false); gate.TryStartCharge(1.52f, false, false, false, false);
                Check(gate.TryStartCharge(1.53f, true, false, false, false), "A deliberate quick tap is accepted after release; no minimum charge hold is imposed");
                gate.Begin(2f, s); gate.TryStartCharge(2.01f, false, false, false, false); gate.BreakRelease();
                Check(!gate.TryStartCharge(3f, true, true, false, false), "Missing input cannot count as a stable button release");
                s.animatePlayerTransit = false; gate.Begin(4f, s);
                Check(Mathf.Abs(gate.UnlockAt - 4.2f) < .0001f, "Disabling transit removes exit duration from protection");
                s.postContestGraceSeconds = s.postContestReleaseSeconds = 0f; gate.Begin(5f, s);
                Check(!gate.TryStartCharge(5f, true, true, false, false), "Zero timing settings still require release before accepting Attack");
                gate.TryStartCharge(5.01f, false, false, false, false);
                Check(gate.TryStartCharge(5.02f, true, true, false, false), "Zero timings allow immediate fresh input after release");
            }
            var source = SharedSettingsRuntime.Load<AmplifierSharedProfile>();
            string before = JsonUtility.ToJson(source);
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                copy.core.possession.postContestGraceSeconds = .2f; copy.core.possession.postContestReleaseSeconds = .1f;
                copy.core.possession.animatePlayerTransit = true; copy.core.possession.exitSeconds = .32f;
                copy.core.possession.winnerAimSeconds = 2.5f;
                using (var v = new AmplifierCoreInteractionPreview(copy))
                {
                    v.Charge = 1f;
                    foreach (var look in new[] { AmplifierCoreInteractionPreview.Look.PostContestLight, AmplifierCoreInteractionPreview.Look.PostContestDark })
                    {
                        v.SetAppearance(look); v.Advance(1.1f);
                        Check(v.PostContestProtectionRemaining > .15f && v.DisplayCharge == 0f && Mathf.Abs(v.DisplayControlSecondsRemaining - 2.5f) < .001f,
                            "Preview holds charge and countdown through the protected handoff: " + look);
                        v.Advance(.26f);
                        Check(v.PostContestWaitingForRelease && v.DisplayCharge == 0f && v.DisplayControlSecondsRemaining < 2.5f,
                            "Preview counts aiming time after fixed protection while showing button release: " + look);
                        v.Advance(.2f);
                        Check(!v.PostContestWaitingForRelease && v.DisplayCharge > 0f, "Preview starts charge only after the handoff: " + look);
                        v.Reset();
                        Check(v.DisplayCharge == 0f && v.PostContestProtectionRemaining == 0f, "Replaying the state resets its handoff: " + look);
                    }
                    copy.core.possession.postContestGraceSeconds = 1f; v.SyncSettings(true);
                    v.SetAppearance(AmplifierCoreInteractionPreview.Look.PostContestLight); v.Advance(1.8f);
                    Check(v.PostContestProtectionRemaining > .3f && v.DisplayCharge == 0f, "Longer grace updates the edit-mode preview");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            Check(JsonUtility.ToJson(source) == before, "Checks preserve shared tuning");
            return "PASSED (" + checks.Count + " post-contest input / preview checks)\n" + string.Join("\n", checks);
        }
    }
}

public static partial class AmplifierPossessionValidation
{
    public static void RunPostContestChecks()
    {
        Run(); SessionState.SetBool(Key + ".PostContestOnly", true);
    }

    static IEnumerator WinForInputCheck(PlayerControllerScript winner)
    {
        yield return ResetCore(); yield return Enter(light); yield return Enter(dark, 1);
        Input(light); Input(dark); yield return Wait(.08f);
        Input(winner, attack: true);
        yield return Until(() => core.IsContestWinnerAiming, .5f, "Alternating contest input awards winner possession");
    }

    static IEnumerator PostContestInputChecks()
    {
        string saved = JsonUtility.ToJson(core.Possession);
        try
        {
            var s = core.Possession;
            s.contestSeconds = 6f; s.progressPerPress = .5f;
            s.postContestGraceSeconds = .2f; s.postContestReleaseSeconds = .1f;
            s.exitSeconds = .32f; s.animatePlayerTransit = true; s.winnerAimSeconds = 2.5f; s.fullChargeSeconds = .35f;
            foreach (var winner in new[] { light, dark })
            {
                yield return WinForInputCheck(winner);
                Check(core.Occupant == winner && core.PostContestInputProtected && core.PostContestGraceRemaining > .1f,
                    "Winner begins with ejection/grace protection: team " + winner.teamID);
                Check(Mathf.Abs(core.ControlSecondsRemaining - s.winnerAimSeconds) < .02f, "Winner receives their full aiming time after protection");
                // The winning Attack remains held through the entire lockout.
                Input(winner, attack: true, aim: Vector3.forward);
                yield return Wait(core.PostContestGraceRemaining + .08f);
                Check(!core.IsLaunched && core.Charge01 == 0f && core.PostContestInputProtected, "Attack held across protection never charges or fires");
                Check(Vector3.Dot(core.AimDirection, Vector3.forward) > .99f, "Winner can aim while attack input is protected");
                for (int i = 0; i < 4; i++)
                {
                    Input(winner, attack: i % 2 == 0, shield: i % 2 != 0); yield return null; yield return null;
                    Check(!core.IsLaunched && core.Charge01 == 0f, "Continued contest presses cannot fire after grace");
                }
                Input(winner); yield return Wait(s.postContestReleaseSeconds + .05f);
                Check(!core.PostContestInputProtected, "Sustained button release arms the fresh-press requirement");
                winner.SetScriptedInput(new PlayerInputFrame { attackHeld = true, hasAimDirWS = true, aimDirWS = Vector3.right });
                yield return Wait(.04f);
                Check(core.Charge01 == 0f && !core.IsLaunched, "Held input without a new press cannot start the winner's shot");
                Input(winner); yield return Wait(s.postContestReleaseSeconds + .05f);
                if (winner == light)
                {
                    Input(winner, attack: true); yield return Wait(.1f);
                    Check(core.Charge01 > 0f && !core.IsLaunched, "Fresh winner Attack starts normal charging");
                    yield return Until(() => core.IsLaunched, .5f, "Full-charge auto-fire still works after a contest");
                }
                else yield return Fire(winner, .03f, Vector3.right);
            }
            s.winnerAimSeconds = .3f;
            yield return WinForInputCheck(light); Input(light, attack: true);
            yield return Until(() => core.ControlState == AmplifierControlState.Available, 1.5f, "Never releasing the buttons still times out and ejects the winner");
            Check(core.Body.isKinematic && !core.IsLaunched && core.ShotVelocity == Vector3.zero, "Timeout during input protection does not create a shot");
            yield return ResetCore(); yield return Enter(light);
            Check(!core.IsContestWinnerAiming && !core.PostContestInputProtected, "Ordinary entry has no leftover contest protection");
            yield return Fire(light, .03f, Vector3.right);
            yield return ResetCore();
            Check(!core.PostContestInputProtected && core.PostContestGraceRemaining == 0f, "Reset removes every post-contest input restriction");
        }
        finally { JsonUtility.FromJsonOverwrite(saved, core.Possession); }
    }
}
#endif
