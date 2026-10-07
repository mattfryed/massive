using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        private GameManagerScript timingMatch;
        private Massive.Demonstrations.EnemyEncounterLab timingLab;
        private bool timingContextFound;
        public float TimelineTimeScale { get; private set; } = 1f;
        public float TimelineDurationSeconds { get; private set; }

        public float TimelineRegulationSeconds()
        {
            if (!timingContextFound || !timingMatch && !timingLab)
            {
                timingLab = GetComponent<Massive.Demonstrations.EnemyEncounterLab>();
                foreach (var candidate in FindObjectsByType<GameManagerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { timingMatch = candidate; break; }
                timingContextFound = true;
            }
            if (timingLab) return Mathf.Max(1f, timingLab.previewRegulationSeconds);
            return timingMatch ? timingMatch.EncounterRegulationDurationSeconds : 0f;
        }
        public float PreviewTimelineTimeScale(EnemyEncounterTimeline value) => value
            ? value.TimeScaleFor(TimelineRegulationSeconds()) : 1f;
    }
}
