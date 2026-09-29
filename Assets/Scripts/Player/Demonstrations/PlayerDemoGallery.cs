using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Massive.Demonstrations
{
    /// <summary>Presents independent, real gameplay demos on the action arena's shared grid.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDemoGallery : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private PlayerDemoDirector[] demos;
        [SerializeField] private TMP_Text[] statusLabels;
        [SerializeField] private GameObject[] encounters;
        private PlayerControllerScript[] matchPlayers;
        private GameManagerScript match;
        private bool matchWasEnabled;
        private bool[] encounterWasActive;
        private float nextLabelUpdate;
        public bool IsPlaying { get; private set; }
        public IReadOnlyList<PlayerDemoDirector> Demos => demos;

        public void Play(GameManagerScript activeMatch, PlayerControllerScript first, PlayerControllerScript second)
        {
            if (IsPlaying || !content) return;
            match = activeMatch; matchWasEnabled = match && match.enabled;
            if (match) match.enabled = false; // Freeze the match timer while the gallery loops.
            matchPlayers = new[] { first, second };
            foreach (var player in matchPlayers) if (player) player.SetWorldGameplaySuppressed(true);
            encounterWasActive = new bool[encounters.Length];
            for (int i = 0; i < encounters.Length; i++)
            {
                if (!encounters[i]) continue;
                encounterWasActive[i] = encounters[i].activeSelf;
                encounters[i].SetActive(false);
            }
            IsPlaying = true;
            content.SetActive(true);
            IsolateBodies();
            foreach (var demo in demos)
            {
                if (demo.Primary) demo.Primary.RespawnCompleted += OnRespawn;
                if (demo.Partner) demo.Partner.RespawnCompleted += OnRespawn;
            }
            nextLabelUpdate = 0f;
        }

        private void OnRespawn(PlayerControllerScript _) { IsolateBodies(); }
        private void IsolateBodies()
        {
            var groups = new List<Collider[]>();
            foreach (var demo in demos)
            {
                if (!demo || !demo.Primary) continue;
                var colliders = new List<Collider>(demo.Primary.GetComponentsInChildren<Collider>(true));
                if (demo.Partner) colliders.AddRange(demo.Partner.GetComponentsInChildren<Collider>(true));
                groups.Add(colliders.ToArray());
            }
            for (int i = 0; i < groups.Count; i++)
                for (int j = i + 1; j < groups.Count; j++)
                    foreach (var a in groups[i]) foreach (var b in groups[j])
                        if (a && b) Physics.IgnoreCollision(a, b, true);
        }

        private void Update()
        {
            if (!IsPlaying || Time.unscaledTime < nextLabelUpdate) return;
            nextLabelUpdate = Time.unscaledTime + .1f;
            for (int i = 0; i < demos.Length && i < statusLabels.Length; i++)
            {
                if (!demos[i] || !statusLabels[i]) continue;
                var demo = demos[i];
                statusLabels[i].text = demo.LastFailure != null ? demo.LastFailure :
                    demo.Phase + "   |   Loops " + demo.SuccessfulLoops;
                statusLabels[i].color = demo.LastFailure != null ? new Color(1f, .4f, .35f) : new Color(.72f, .83f, .89f);
            }
        }

        public void Stop()
        {
            if (!IsPlaying) return;
            foreach (var demo in demos)
            {
                if (demo.Primary) demo.Primary.RespawnCompleted -= OnRespawn;
                if (demo.Partner) demo.Partner.RespawnCompleted -= OnRespawn;
            }
            content.SetActive(false); // Directors stop and remove their owned actors, beams and VFX.
            IsPlaying = false;
            for (int i = 0; i < encounters.Length; i++)
                if (encounters[i]) encounters[i].SetActive(encounterWasActive[i]);
            if (matchPlayers != null)
                foreach (var player in matchPlayers) if (player) player.SetWorldGameplaySuppressed(false);
            if (match) match.enabled = matchWasEnabled;
            matchPlayers = null; match = null;
        }
        private void OnDisable() { Stop(); }
    }
}
