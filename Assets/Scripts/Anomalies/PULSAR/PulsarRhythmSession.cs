using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using Dypsloom.RhythmTimeline.Core.Managers;
using Dypsloom.RhythmTimeline.Core.Playables;
using Dypsloom.RhythmTimeline;
using Dypsloom.RhythmTimeline.Core;



[DisallowMultipleComponent]
public class PulsarRhythmSession : MonoBehaviour
{
    public static PulsarRhythmSession Instance { get; private set; }

    [Header("Rhythm Timeline 2")]
    [SerializeField] private RhythmDirector rhythmDirector;
    [SerializeField] private RhythmTimelineAsset rhythmTimelineAsset;
    [SerializeField] private PlayableDirector playableDirector; // assign the same one RhythmDirector uses

    [Header("Gameplay")]
    [SerializeField] private Transform pulsarCenter;
    [SerializeField] private MonoBehaviour inputSourceBehaviour; // must implement IPulsarInputSource
    [SerializeField] private PulsarRhythmConfig config;

    [Header("Session Hooks")]
    public UnityEvent onSessionStarted;
    public UnityEvent onSessionEnded;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private KeyCode debugStartKey = KeyCode.P;

    // Events you’ll hook to MASSIVE scoring/mass VFX later:
    public event Action<int, float, float> OnTeamBeatResolved; // (teamIndex, deltaMass, resonance01)
    public event Action<int> OnBeatTick;                       // beat counter within the session
    public event Action<float, float> OnSessionTotals;         // (team0Total, team1Total)

    public Transform PulsarCenter => pulsarCenter;
    public PulsarRhythmConfig Config => config;
    public bool IsRunning => _running;
    public int PlayerCount => _players;

    private IPulsarInputSource _input;
    private RhythmProcessor _processor;

    private bool _running;
    private int _players;
    private int[] _playerToTeam = Array.Empty<int>();

    // Active notes (unison): we keep track of what is currently hittable.
    private readonly System.Collections.Generic.List<PulsarUnisonNote> _activeSword = new();
    private readonly System.Collections.Generic.List<PulsarUnisonNote> _activeShield = new();
    private readonly System.Collections.Generic.List<PulsarUnisonNote> _activeBoth = new();

    // Chord detection per player
    private double[] _lastSwordDsp = Array.Empty<double>();
    private double[] _lastShieldDsp = Array.Empty<double>();

    // Resonance
    private int[] _teamCombo = new int[2];
    private float[] _teamRes01 = new float[2];
    private float _team0Total, _team1Total;
    private int _beatCounter;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _input = inputSourceBehaviour as IPulsarInputSource;
        if (_input == null && inputSourceBehaviour != null)
            Debug.LogError($"{name}: inputSourceBehaviour must implement IPulsarInputSource.");

        if (!playableDirector && rhythmDirector)
            playableDirector = rhythmDirector.GetComponent<PlayableDirector>();

        if (playableDirector)
            playableDirector.stopped += OnPlayableStopped;
    }

    private void OnDestroy()
    {
        if (playableDirector)
            playableDirector.stopped -= OnPlayableStopped;

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_running)
        {
            if (debugStartKey != KeyCode.None && Input.GetKeyDown(debugStartKey))
                StartSession();

            return;
        }

        HandleInputs();
    }

    public void StartSession()
    {
        if (_running) return;

        if (!rhythmDirector || !rhythmTimelineAsset || !config || !pulsarCenter)
        {
            Debug.LogError($"{name}: Missing refs (rhythmDirector / rhythmTimelineAsset / config / pulsarCenter).");
            return;
        }

        _processor = rhythmDirector.RhythmProcessor; // per docs: RhythmDirector exposes RhythmProcessor :contentReference[oaicite:10]{index=10}
        if (_processor == null)
        {
            Debug.LogError($"{name}: RhythmDirector.RhythmProcessor is null.");
            return;
        }

        _players = Mathf.Clamp(_input?.PlayerCount ?? 0, 1, 4);
        BuildDefaultTeams(_players);

        _lastSwordDsp = new double[_players];
        _lastShieldDsp = new double[_players];
        for (int i = 0; i < _players; i++) { _lastSwordDsp[i] = double.NaN; _lastShieldDsp[i] = double.NaN; }

        _teamCombo[0] = _teamCombo[1] = 0;
        _teamRes01[0] = _teamRes01[1] = 0f;
        _team0Total = _team1Total = 0f;
        _beatCounter = 0;

        _activeSword.Clear();
        _activeShield.Clear();
        _activeBoth.Clear();

        _running = true;
        onSessionStarted?.Invoke();

        // Start song (RhythmDirector API) :contentReference[oaicite:11]{index=11}
        rhythmDirector.PlaySong(rhythmTimelineAsset);

        if (debugLogs)
            Debug.Log($"PULSAR Session START | players={_players}");
    }

    public void StopSession()
    {
        if (!_running) return;

        // Stop first so notes deactivating due to EndSong don’t resolve beats after shutdown.
        _running = false;

        // End song :contentReference[oaicite:12]{index=12}
        if (rhythmDirector) rhythmDirector.EndSong();

        _activeSword.Clear();
        _activeShield.Clear();
        _activeBoth.Clear();

        onSessionEnded?.Invoke();
        OnSessionTotals?.Invoke(_team0Total, _team1Total);

        if (debugLogs)
            Debug.Log($"PULSAR Session END | team0={_team0Total:0.##} team1={_team1Total:0.##}");
    }

    private void OnPlayableStopped(PlayableDirector dir)
    {
        // Natural end of timeline
        if (_running)
            StopSession();
    }

    // Called by PulsarUnisonNote
    internal void RegisterActive(PulsarUnisonNote note)
    {
        if (!_running || note == null) return;

        var list = GetActiveList(note.PromptType);
        if (!list.Contains(note))
            list.Add(note);
    }

    // Called by PulsarUnisonNote
    internal void UnregisterActive(PulsarUnisonNote note)
    {
        if (note == null) return;
        GetActiveList(note.PromptType).Remove(note);
    }

    // Called by PulsarUnisonNote when its perfect moment passes (beat tick)
    internal void NotifyBeatTick()
    {
        if (!_running) return;
        OnBeatTick?.Invoke(_beatCounter);
        _beatCounter++;
    }

    // Called by PulsarUnisonNote when its window ends
    internal void ResolveBeat(PulsarJudgement[] perPlayer)
    {
        if (!_running || perPlayer == null || perPlayer.Length != _players) return;

        float pos0 = 0, neg0 = 0, q0 = 0; int c0 = 0;
        float pos1 = 0, neg1 = 0, q1 = 0; int c1 = 0;

        for (int p = 0; p < _players; p++)
        {
            int team = _playerToTeam[p];
            float m = JudgementToMass(perPlayer[p]);
            float q = JudgementToQuality(perPlayer[p]);

            if (team == 0) { q0 += q; c0++; if (m >= 0) pos0 += m; else neg0 += m; }
            else           { q1 += q; c1++; if (m >= 0) pos1 += m; else neg1 += m; }
        }

        float avgQ0 = (c0 > 0) ? (q0 / c0) : 0f;
        float avgQ1 = (c1 > 0) ? (q1 / c1) : 0f;

        UpdateResonance(0, avgQ0);
        UpdateResonance(1, avgQ1);

        float mult0 = 1f + (_teamRes01[0] * config.maxBonusMultiplier);
        float mult1 = 1f + (_teamRes01[1] * config.maxBonusMultiplier);

        // Only boost positive mass; penalties remain penalties.
        float delta0 = pos0 * mult0 + neg0;
        float delta1 = pos1 * mult1 + neg1;

        _team0Total += delta0;
        _team1Total += delta1;

        OnTeamBeatResolved?.Invoke(0, delta0, _teamRes01[0]);
        OnTeamBeatResolved?.Invoke(1, delta1, _teamRes01[1]);

        if (debugLogs)
            Debug.Log($"Beat {_beatCounter - 1:00} | T0 {delta0,6:0.##} (R{_teamRes01[0]:0.00}) | T1 {delta1,6:0.##} (R{_teamRes01[1]:0.00})");
    }

    private void HandleInputs()
    {
        if (_input == null || _processor == null) return;

        double now = AudioSettings.dspTime;

        // If a BOTH prompt is currently active, we prioritize chord detection.
        var bothNote = GetBestActive(PulsarPromptType.Both);

        for (int p = 0; p < _players; p++)
        {
            bool swordDown = _input.GetSwordDown(p);
            bool shieldDown = _input.GetShieldDown(p);

            if (swordDown) _lastSwordDsp[p] = now;
            if (shieldDown) _lastShieldDsp[p] = now;

            if (bothNote != null)
            {
                TrySendChord(bothNote, p);
                continue; // Don’t send singles during BOTH prompts
            }

            if (swordDown)
            {
                var swordNote = GetBestActive(PulsarPromptType.Sword);
                if (swordNote != null) SendInput(swordNote, p, PulsarPromptType.Sword, now);
            }

            if (shieldDown)
            {
                var shieldNote = GetBestActive(PulsarPromptType.Shield);
                if (shieldNote != null) SendInput(shieldNote, p, PulsarPromptType.Shield, now);
            }
        }
    }

    private void TrySendChord(PulsarUnisonNote bothNote, int playerIndex)
    {
        double s = _lastSwordDsp[playerIndex];
        double h = _lastShieldDsp[playerIndex];
        if (double.IsNaN(s) || double.IsNaN(h)) return;

        if (Math.Abs(s - h) <= config.chordSeparationSec)
        {
            double avg = 0.5 * (s + h);
            SendInput(bothNote, playerIndex, PulsarPromptType.Both, avg);

            // Clear so we don’t double-trigger
            _lastSwordDsp[playerIndex] = double.NaN;
            _lastShieldDsp[playerIndex] = double.NaN;
        }
    }

    private void SendInput(PulsarUnisonNote note, int playerIndex, PulsarPromptType attemptType, double dspTime)
    {
        if (!_running || note == null) return;

        var evt = new PulsarInputEventData
        {
            Note = note,
            PlayerIndex = playerIndex,
            DspTime = dspTime,
            AttemptType = attemptType,

            // These fields exist in the docs snippet; we set them minimally. :contentReference[oaicite:13]{index=13}
            TrackID = (int)attemptType,
            InputID = (int)attemptType,
            Direction = Vector2.zero
        };

        // Send into the system the “official” way. :contentReference[oaicite:14]{index=14}
        _processor.TriggerInput(evt);
    }

    private PulsarUnisonNote GetBestActive(PulsarPromptType type)
    {
        var list = GetActiveList(type);
        if (list.Count == 0) return null;

        // If multiple overlap, choose closest-to-perfect.
        PulsarUnisonNote best = null;
        float bestAbs = float.MaxValue;

        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            if (!n) continue;

            float abs = Mathf.Abs(n.SignedTimeToPerfect);
            if (abs < bestAbs)
            {
                bestAbs = abs;
                best = n;
            }
        }

        return best;
    }

    private System.Collections.Generic.List<PulsarUnisonNote> GetActiveList(PulsarPromptType type)
    {
        return type switch
        {
            PulsarPromptType.Sword => _activeSword,
            PulsarPromptType.Shield => _activeShield,
            _ => _activeBoth
        };
    }

    private void BuildDefaultTeams(int players)
    {
        _playerToTeam = new int[players];

        // 2 players: 0 vs 1
        // 4 players: (0,1) vs (2,3)
        if (players <= 2)
        {
            _playerToTeam[0] = 0;
            if (players == 2) _playerToTeam[1] = 1;
        }
        else
        {
            for (int i = 0; i < players; i++)
                _playerToTeam[i] = (i < players / 2) ? 0 : 1;
        }
    }

    private void UpdateResonance(int team, float avgQuality01)
    {
        bool success = avgQuality01 >= config.comboSuccessQualityThreshold;

        if (success) _teamCombo[team] += 1;
        else _teamCombo[team] = 0;

        float combo = Mathf.Max(0, _teamCombo[team]);
        _teamRes01[team] = combo / (combo + Mathf.Max(0.01f, config.comboK));
    }

    private float JudgementToQuality(PulsarJudgement j) => j switch
    {
        PulsarJudgement.Perfect => 1f,
        PulsarJudgement.Good => 0.5f,
        _ => 0f
    };

    private float JudgementToMass(PulsarJudgement j) => j switch
    {
        PulsarJudgement.Perfect => config.massPerfect,
        PulsarJudgement.Good => config.massGood,
        _ => config.massMiss
    };
}

public enum PulsarJudgement
{
    Perfect,
    Good,
    Miss
}
