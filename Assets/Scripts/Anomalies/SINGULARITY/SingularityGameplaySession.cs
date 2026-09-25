using System.Collections;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>Scene-local free-play wiring. Does not change the cabinet roster or load another level.</summary>
    public sealed class SingularityGameplaySession : MonoBehaviour
    {
        public MatchScoreService scoreService;
        public PlayerControllerScript[] players = new PlayerControllerScript[0];

        private IEnumerator Start()
        {
            // Match the existing roster's spawn timing without its menu-driven player activation.
            yield return null;
            if (scoreService)
            {
                foreach (var player in players) if (player) scoreService.RegisterPlayer(player);
                scoreService.OpenScoring();
            }
            foreach (var player in players)
            {
                if (!player || !player.gameObject.activeInHierarchy) continue;
                player.SetMatchInputLocked(false);
                player.BeginMatchStartSpawn();
            }
        }
    }
}
