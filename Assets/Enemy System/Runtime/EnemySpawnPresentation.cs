using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Replays native enemy draw-in effects together in a presentation without enabling gameplay AI.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawnPresentation : MonoBehaviour
    {
        [Tooltip("Seconds for the displayed enemies to trace their facets and reveal their cores each time this group opens.")]
        [Min(.05f), SerializeField] private float drawInSeconds = 3f;

        private float elapsed;
        public float Progress01 => Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.05f, drawInSeconds));
        public bool IsComplete => elapsed >= Mathf.Max(.05f, drawInSeconds);

        private void OnEnable() { elapsed = 0f; }
        private void Update()
        {
            if (!IsComplete) elapsed += Time.deltaTime;
        }
    }
}
