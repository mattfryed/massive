#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Massive.Levels;
using Massive.Scoring;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Lattice.EditorTools
{
    public static partial class LatticeValidation
    {
        static IEnumerator SceneSystemChecks(Scene scene)
        {
            var context = LatticeSetup.InScene<LevelSceneContext>(scene).Single();
            var match = context.match;
            var score = LatticeSetup.InScene<MatchScoreService>(scene).Single();
            Check(context.spawners && context.spawners.transform.parent.CompareTag("GameplayObjects"), "Level context owns the standard spawner group");
            Check(context.spawners.activeSelf == (match.Phase == MatchRuntimePhase.Regulation), "Spawners respect preparation/countdown gating");
            Check(context.stageTitleText.gameObject.activeInHierarchy && context.stageTitleText.text == "LATTICE" &&
                context.stageNumberText.gameObject.activeInHierarchy && context.stageNumberText.text == "STAGE_001", "Context drives visible stage labels");
            yield return Until(() => match.Phase == MatchRuntimePhase.Regulation || match.IsStartupBlocked, "Standard roster startup completes", 18);
            Check(!match.IsStartupBlocked && context.roster.IsRosterReady && context.spawners.activeInHierarchy, "Roster reaches regulation and enables spawners");
            Check(match.ScoreService == score && MatchScoreService.Instance == score, "One explicit scene score authority serves the match");
            Check(Rewired.ReInput.isReady && Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length == 1, "One ready global input manager after direct launch");
            Check(BGMManager.Instance && Object.FindObjectsByType<BGMManager>(FindObjectsSortMode.None).Length == 1 &&
                GameFlowContext.Instance.SelectedLevel == context.level, "Global music and session bootstrap retain LATTICE identity");
            var toast = LatticeSetup.InScene<TeamAmplifierToastPresenter>(scene).Single();
            score.AdvanceTeamAmplifier(1); yield return null;
            Check(toast.ActiveToastCount == 1, "Shared amplifier capture event displays team feedback");
            score.ResetForMatch(true); yield return null;
            Check(toast.ActiveToastCount == 0 && score.IsScoringOpen, "Match reset clears team feedback while keeping regulation scoring open");
        }
    }
}
#endif
