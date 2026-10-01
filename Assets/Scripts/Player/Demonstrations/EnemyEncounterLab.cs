using System.Collections.Generic;
using Massive.Enemies;
using Massive.Player;
using TMPro;
using UnityEngine;

namespace Massive.Demonstrations
{
    public sealed class EnemyEncounterLab : MonoBehaviour
    {
        public enum LayoutPreset { Open, BlockedCenter, SideWallMounts }
        public EnemyDirector director;
        public EnemyArenaLayout layout;
        public GameObject playerPrefab, centerObstacle;
        public TMP_Text status;
        public LayoutPreset preset;
        public bool loop = true;
        [Tooltip("Preview actors restore damage after a hit so the authored schedule can run continuously.")]
        public bool restorePlayerMass = true;
        public bool actorsMove = true, actorsAttack = true;
        public int CompletedLoops { get; private set; }
        public IReadOnlyList<PlayerControllerScript> Players => players;
        private readonly List<PlayerControllerScript> players = new();
        private Transform session;
        private LayoutPreset appliedPreset;
        private float nextAttack, releaseAt, nextStatus;
        private int appliedCue, appliedSeed;
        private EnemyEncounterTimeline appliedTimeline;

        private void OnEnable() { if (Application.isPlaying) RestartPreview(); }
        public void ApplyLayout()
        {
            if (!layout) return;
            if (centerObstacle) centerObstacle.SetActive(preset != LayoutPreset.Open);
            foreach (var socket in layout.sockets) socket.enabled = socket.id.StartsWith("Side") ? preset == LayoutPreset.SideWallMounts : preset != LayoutPreset.SideWallMounts;
            appliedPreset = preset;
        }
        public void RestartPreview()
        {
            if (!Application.isPlaying || !director || !layout || !playerPrefab) return;
            director.RestartTimeline(true);
            if (session) { session.gameObject.SetActive(false); Destroy(session.gameObject); }
            players.Clear(); ApplyLayout();
            var go = new GameObject("Timeline actors"); go.transform.SetParent(transform, false); go.SetActive(false); session = go.transform;
            director.enemyRoot = session; director.ConfigureDemonstration(session);
            for (int i = 0; i < 2; i++)
            {
                var actor = Instantiate(playerPrefab, layout.World(new Vector2(i == 0 ? -.42f : .42f, -.15f)), Quaternion.identity, session);
                actor.name = "Preview player " + (i + 1);
                var player = actor.GetComponent<PlayerControllerScript>(); player.ConfigureDemonstration(session, i, i + 1);
                foreach (var interactor in actor.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = layout.arena.Grid;
                foreach (var pulse in actor.GetComponentsInChildren<PlayerRepulsorGridPulse>(true)) pulse.BindGrid(layout.arena.Grid);
                if (actor.TryGetComponent<PlayerScaleAdjuster>(out var scale)) scale.ApplyScale();
                player.HitAccepted += hit => { if (restorePlayerMass && player) player.ApplyExternalMassDelta(hit.massLost01); };
                players.Add(player);
            }
            go.SetActive(true); nextAttack = Time.time + 3f; releaseAt = -1f;
            appliedCue = director.previewCue; appliedSeed = director.encounterSeed; appliedTimeline = director.encounterTimeline;
            director.RestartTimeline();
        }
        private void Update()
        {
            if (!director) return;
            if (preset != appliedPreset || appliedCue != director.previewCue || appliedSeed != director.encounterSeed || appliedTimeline != director.encounterTimeline)
                RestartPreview();
            if (director.TimelineFinished && loop) { CompletedLoops++; RestartPreview(); }
            if (status && Time.unscaledTime >= nextStatus)
            {
                nextStatus = Time.unscaledTime + .15f;
                string cue = "Waiting";
                foreach (var item in director.CueStates)
                    if (item.state != "Waiting") cue = item.label + ": " + item.state + (string.IsNullOrEmpty(item.reason) ? "" : " (" + item.reason + ")");
                status.text = $"{director.GameplayAge:00.0}s  |  {preset}  |  {cue}  |  Alive {director.AliveCount} / Reserved {director.TimelinePendingCount}  |  Pressure {director.CurrentPressure:0}";
            }
            if (!session) return;
            bool attack = actorsAttack && !director.timelinePaused && Time.time >= nextAttack;
            bool release = releaseAt >= 0 && Time.time >= releaseAt;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i]; if (!player) continue;
                Vector3 aim = Vector3.forward; float nearest = float.MaxValue;
                foreach (var enemy in EnemyBase.ActiveEnemies)
                {
                    if (!enemy || enemy.IsDead || enemy.SimulationRoot != session) continue;
                    var delta = enemy.transform.position - player.transform.position;
                    if (delta.sqrMagnitude < nearest) { nearest = delta.sqrMagnitude; aim = delta; }
                }
                float side = i == 0 ? -1 : 1;
                var target = layout.World(new Vector2(side * (.42f + Mathf.Sin(director.GameplayAge * .2f) * .06f),
                    Mathf.Sin(director.GameplayAge * .3f + i * Mathf.PI) * .23f));
                var move = target - player.transform.position;
                player.SetScriptedInput(new PlayerInputFrame { moveInput = actorsMove && !director.timelinePaused ? Vector2.ClampMagnitude(new Vector2(move.x, move.z), .4f) : Vector2.zero,
                    hasAimDirWS = true, aimDirWS = aim, attackDown = attack, attackHeld = attack, attackUp = release });
            }
            if (release) releaseAt = -1;
            if (attack) { nextAttack = Time.time + 2.8f; releaseAt = Time.time; }
        }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (director) director.RestartTimeline(true);
            if (session) Destroy(session.gameObject); session = null; players.Clear();
        }
    }
}
