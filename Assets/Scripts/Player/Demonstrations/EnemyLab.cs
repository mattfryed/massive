using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Massive.Scoring;

namespace Massive.Demonstrations
{
    [DefaultExecutionOrder(-550)]
    public sealed class EnemyLab : MonoBehaviour
    {
        public enum LabMode { Columns, EncounterTimeline }
        public LabMode mode;
        public EnemyEncounterLab timelinePreview;
        private LabMode appliedMode;
        public GameObject content;
        public GameObject[] encounters;
        public EnemyLabColumn[] columns;
        private readonly List<PlayerControllerScript> suppressed = new();
        private bool[] encounterStates;
        private readonly List<Behaviour> suspendedSpawners = new();
        private TMPro.TMP_Text title;
        private string originalTitle;
        private GameManagerScript match;
        private PlayerActionTestArena actionLab;
        private bool matchEnabled, actionEnabled, running;
        private void OnEnable() { if (Application.isPlaying) StartCoroutine(Begin()); }
        private IEnumerator Begin()
        {
            // A serialized active child can otherwise start its preview before the
            // roster has finished spawning and before the Lab suspends that roster.
            if (content) content.SetActive(false);
            if (timelinePreview) timelinePreview.gameObject.SetActive(false);
            actionLab = FindFirstObjectByType<PlayerActionTestArena>();
            if (actionLab) { actionEnabled = actionLab.enabled; actionLab.enabled = false; }
            var gallery = FindFirstObjectByType<PlayerDemoGallery>(); if (gallery) gallery.Stop();
            encounterStates = new bool[encounters.Length];
            for (int i = 0; i < encounters.Length; i++)
                if (encounters[i]) { encounterStates[i] = encounters[i].activeSelf; encounters[i].SetActive(false); }
            foreach (var spawner in FindObjectsByType<Massive.Multiplier.AmplifierResonanceSpawner>(FindObjectsSortMode.None))
                if (spawner.enabled) { suspendedSpawners.Add(spawner); spawner.enabled = false; }
            match = FindFirstObjectByType<GameManagerScript>();
            while (match && match.Phase != MatchRuntimePhase.Regulation) yield return null;
            matchEnabled = match && match.enabled; if (match) match.enabled = false;
            foreach (var player in PlayerControllerScript.ActivePlayers)
                if (player && !player.IsPseudoPlayer && player.gameObject.scene == gameObject.scene)
                { player.SetWorldGameplaySuppressed(true); suppressed.Add(player); }
            // Hide alone leaves roots registered as live players (including spawn blockers).
            // Deactivate after collecting: OnDisable removes them from ActivePlayers.
            foreach (var player in suppressed) player.gameObject.SetActive(false);
            var context = FindFirstObjectByType<Massive.Levels.LevelSceneContext>();
            title = context ? context.stageTitleText : null;
            if (title) { originalTitle = title.text; title.text = "ENEMY LAB"; }
            running = true; ApplyMode();
        }
        private void Update() { if (running && mode != appliedMode) ApplyMode(); }
        private void ApplyMode()
        {
            if (content) content.SetActive(mode == LabMode.Columns);
            if (timelinePreview) timelinePreview.gameObject.SetActive(mode == LabMode.EncounterTimeline);
            if (title) title.text = mode == LabMode.Columns ? "ENEMY LAB" : "ENCOUNTER LAB";
            appliedMode = mode;
        }
        public void Isolate(EnemyLabColumn owner, GameObject member)
        {
            foreach (var column in columns)
            {
                if (!column || column == owner || !column.Session) continue;
                foreach (var a in member.GetComponentsInChildren<Collider>(true))
                    foreach (var b in column.Session.GetComponentsInChildren<Collider>(true))
                        if (a && b) Physics.IgnoreCollision(a, b);
            }
        }
        private void OnDisable()
        {
            StopAllCoroutines(); if (content) content.SetActive(false);
            if (timelinePreview) timelinePreview.gameObject.SetActive(false);
            if (encounterStates != null)
                for (int i = 0; i < encounters.Length; i++) if (encounters[i]) encounters[i].SetActive(encounterStates[i]);
            foreach (var player in suppressed)
                if (player) { player.gameObject.SetActive(true); player.SetWorldGameplaySuppressed(false); }
            suppressed.Clear();
            foreach (var spawner in suspendedSpawners) if (spawner) spawner.enabled = true;
            suspendedSpawners.Clear(); if (title) title.text = originalTitle;
            if (running && match) match.enabled = matchEnabled;
            if (actionLab) actionLab.enabled = actionEnabled;
            running = false;
        }
    }
}
