/// ---------------------------------------------
/// Rhythm Timeline
/// Copyright (c) Dyplsoom. All Rights Reserved.
/// https://www.dypsloom.com
/// ---------------------------------------------

namespace Dypsloom.RhythmTimeline.Scoring
{
    using Dypsloom.RhythmTimeline.Core;
    using Dypsloom.RhythmTimeline.Core.Managers;
    using Dypsloom.RhythmTimeline.Core.Notes;
    using Dypsloom.RhythmTimeline.UI;
    using Dypsloom.Shared;
    using Dypsloom.Shared.Utility;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class ScoreManager : MonoBehaviour
    {
        protected enum AccuracySpawnPointOption
        {
            None,
            OnTrackEnd,
            OnTrackStart,
            OnNote,
            OnMouse,
            OnAccuracySpawnPointTransform,
        }
        
        public event Action<RhythmTimelineAsset> OnNewHighScore;
        public event Action<Note, NoteAccuracy> OnNoteScore;
        public event Action OnBreakChain;
        public event Action<int> OnContinueChain;
        public event Action<float> OnScoreChange;

        [Tooltip("For local multiplayer you may have multiple Rhythm tracks playing, match the PlayerIds for all relevant components.")]
        [SerializeField] protected uint m_PlayerID = 0;
        [Tooltip("The Rhythm Director.")]
        [SerializeField] protected RhythmDirector m_RhythmDirector;
        [Tooltip("The score settings.")]
        [SerializeField] protected ScoreSettings m_ScoreSettings;
        [Tooltip("The score settings.")]
        [SerializeField] protected bool m_AddNoteScoreOnTrigger = true;
        [Tooltip("The score text.")]
        [SerializeField] protected TMPro.TextMeshProUGUI m_ScoreTmp;
        [Tooltip("The score multiplier text.")]
        [SerializeField] protected TMPro.TextMeshProUGUI m_ScoreMultiplierTmp;
        [Tooltip("The chain text.")]
        [SerializeField] protected TMPro.TextMeshProUGUI m_ChainTmp;
        [Tooltip("The rank slider.")]
        [SerializeField] protected RankSlider m_RankSlider;
        [Tooltip("(Optional) the spawn point for the accuracy pop ups.")]
        [SerializeField] protected AccuracySpawnPointOption m_AccuracySpawnPointOption = AccuracySpawnPointOption.OnTrackEnd;
        [Tooltip("(Optional) the spawn point for the accuracy pop ups.")]
        [SerializeField] protected Transform m_AccuracySpawnPoint;
        [Tooltip("The spawn point for the accuracy pop ups.")]
        [SerializeField] protected Vector3 m_AccuracySpawnPointOffset;
    
        protected RhythmTimelineAsset m_CurrentSong;
        protected float m_CurrentScore;
        protected List<int> m_CurrentAccuracyIDHistogram;
        protected int m_CurrentMaxChain;
        protected int m_CurrentChain;
        protected float m_CurrentMaxPossibleScore;

        protected float m_ScoreMultiplier = 1;
        protected Coroutine m_ScoreMultiplierCoroutine;
        private Camera m_Camera;
    
        public uint PlayerID => m_PlayerID;
        public ScoreSettings ScoreSettings => m_ScoreSettings;
        public RhythmTimelineAsset CurrentSong => m_CurrentSong;

        protected void Awake()
        {
            Toolbox.Set(this, m_PlayerID);
        }

        private void Start()
        {
            if (m_RhythmDirector == null) {
                m_RhythmDirector = Toolbox.Get<RhythmDirector>(m_PlayerID);
            }
            
            m_Camera = Camera.main;
            
            UpdateScoreVisual();
            m_RhythmDirector.OnSongPlay += HandleOnSongPlay;
            m_RhythmDirector.OnSongEnd += HandleOnSongEnd;
            m_RhythmDirector.RhythmProcessor.OnNoteTriggerEvent += HandleOnNoteTriggerEvent;

            if (m_RhythmDirector.IsPlaying) {
                HandleOnSongPlay();
            }
        }

        protected virtual void HandleOnNoteTriggerEvent(NoteTriggerEventData noteTriggerEventData)
        {
            if (m_AddNoteScoreOnTrigger == false) { return; }
            
            var noteAccuracy = AddNoteAccuracyScore(
                noteTriggerEventData.Note,
                noteTriggerEventData.DspTimeDifferencePercentage,
                noteTriggerEventData.Good,
                noteTriggerEventData.Miss);

            Transform spawnTransform;
            switch (m_AccuracySpawnPointOption) {
                case AccuracySpawnPointOption.None:
                    return;
                case AccuracySpawnPointOption.OnTrackEnd:
                    spawnTransform = noteTriggerEventData.Note.RhythmClipData.TrackObject.EndPoint;
                    break;
                case AccuracySpawnPointOption.OnTrackStart:
                    spawnTransform = noteTriggerEventData.Note.RhythmClipData.TrackObject.StartPoint;
                    break;
                case AccuracySpawnPointOption.OnNote:
                    spawnTransform = noteTriggerEventData.Note.transform;
                    break;
                case AccuracySpawnPointOption.OnAccuracySpawnPointTransform:
                    spawnTransform = m_AccuracySpawnPoint;
                    break;
                case AccuracySpawnPointOption.OnMouse:
#if ENABLE_INPUT_SYSTEM
                    var mousePosition =  UnityEngine.InputSystem.Mouse.current.position.value;
                    #else
                    var mousePosition = Input.mousePosition;
#endif
                    Vector3 mouseWorldPoint = new Vector3(mousePosition.x, mousePosition.y, 10);
                    mouseWorldPoint = m_Camera.ScreenToWorldPoint(mouseWorldPoint);
                    var spawnPosition = m_AccuracySpawnPointOffset+mouseWorldPoint;
                    ScorePopup(null, noteAccuracy, spawnPosition);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
           
            ScorePopup(spawnTransform, noteAccuracy, m_AccuracySpawnPointOffset);
        }

        private void HandleOnSongEnd()
        {
            OnSongEnd(m_RhythmDirector.SongTimelineAsset);
        }

        private void HandleOnSongPlay()
        {
            SetSong(m_RhythmDirector.SongTimelineAsset);
        }

        public void SetSong(RhythmTimelineAsset song)
        {
            m_CurrentSong = song;
            m_CurrentScore = m_CurrentSong.StartScore;

            m_CurrentMaxPossibleScore = m_CurrentSong.MaxScore;
            if (m_CurrentMaxPossibleScore < 0) {
                m_CurrentMaxPossibleScore = song.RhythmClipCount * m_ScoreSettings.MaxNoteScore;
            }
            
            m_CurrentAccuracyIDHistogram = new List<int>(m_CurrentSong.RhythmClipCount);
            m_CurrentMaxChain = 0;
            m_CurrentChain = 0;

            m_ScoreMultiplier = 1;
        
            UpdateScoreVisual();
        }

        public void OnSongEnd(RhythmTimelineAsset song)
        {
            if (song == null)
            {
                Debug.LogError("The song cannot be null");
                return;
            }
            
            if (m_CurrentAccuracyIDHistogram == null) {
                SetSong(song);
            }
            
            var newScore = new ScoreData(m_CurrentAccuracyIDHistogram.ToArray(), m_CurrentScore, m_CurrentMaxChain,
                m_ScoreSettings, song);

            if (newScore.FullScore > song.HighScore.FullScore) {
                song.SetHighScore(newScore);
                OnNewHighScore?.Invoke(song);
            }
        }

        public ScoreData GetScoreData()
        {
            return new ScoreData(m_CurrentAccuracyIDHistogram.ToArray(),m_CurrentScore,m_CurrentMaxChain,m_ScoreSettings,m_CurrentSong);
        }

        public NoteAccuracy GetAccuracy(float offsetPercentage, bool good, bool miss)
        {
            if (miss) { return GetMissAccuracy(); }

            return GetAccuracy(offsetPercentage, good);
        }
        
        public virtual NoteAccuracy GetAccuracy(float offsetPercentage, bool good)
        {
            var noteAccuracy = good ? m_ScoreSettings.GetGoodNoteAccuracy(offsetPercentage) : m_ScoreSettings.GetBadNoteAccuracy(offsetPercentage);
            
            if (noteAccuracy == null) {
                Debug.LogWarningFormat("Note Accuracy could not be found for offset ({0}), make sure the score settings are correctly set up", offsetPercentage);
                return null;
            }

            return noteAccuracy;
        }
        
        public virtual NoteAccuracy GetMissAccuracy()
        {
            return  m_ScoreSettings.GetMissAccuracy();
        }
        
        public virtual NoteAccuracy AddNoteAccuracyScore(Note note, float offsetPercentage, bool good, bool miss)
        {
            NoteAccuracy noteAccuracy = GetAccuracy(offsetPercentage, good, miss);

            AddNoteAccuracyScore(note, noteAccuracy);

            return noteAccuracy;
        }

        public virtual void AddNoteAccuracyScore(Note note, NoteAccuracy noteAccuracy)
        {
            
            if (noteAccuracy == null) {
                Debug.LogWarningFormat("Note Accuracy is null");
                return;
            }

            //Chain
            if (noteAccuracy.breakChain) {
                m_CurrentChain = 0;
                OnBreakChain?.Invoke();
            } else {
                m_CurrentChain++;
                m_CurrentMaxChain = Mathf.Max(m_CurrentChain, m_CurrentMaxChain);
                OnContinueChain?.Invoke(m_CurrentChain);
            }
            
            AddScore(noteAccuracy.score);
            OnNoteScore?.Invoke(note,noteAccuracy);

            if (m_CurrentSong == null) { return; }

            m_CurrentAccuracyIDHistogram.Add(m_ScoreSettings.GetID(noteAccuracy));
        }

        public virtual void AddScore(float score)
        {
            if (score < 0) {
                m_CurrentScore += score;
            } else {
                m_CurrentScore += m_ScoreMultiplier*score;
            }

            if (m_CurrentSong.PreventMaxScoreOvershoot) {
                m_CurrentScore = Mathf.Min(m_CurrentScore, m_CurrentMaxPossibleScore);
            }
            
            if (m_CurrentSong.PreventMinScoreOvershoot) {
                m_CurrentScore = Mathf.Max(m_CurrentScore, m_CurrentSong.MinScore);
            }
            
            OnScoreChange?.Invoke(m_CurrentScore);
            UpdateScoreVisual();
        }

        public void SetMultiplier(float multiplier)
        {
            m_ScoreMultiplier = multiplier;
            UpdateScoreVisual();
        }
        
        public void SetMultiplier(float multiplier,float time)
        {
            SetMultiplier(multiplier);

            if (m_ScoreMultiplierCoroutine != null) { StopCoroutine(m_ScoreMultiplierCoroutine); }
            m_ScoreMultiplierCoroutine = StartCoroutine(ResetMultiplierDelayed(time));
        }

        public IEnumerator ResetMultiplierDelayed(float delay)
        {
            var start = DspTime.AdaptiveTime;
            while (start + delay > DspTime.AdaptiveTime) { yield return null; }
            SetMultiplier(1);
        }
        
        public int GetChain()
        {
            return m_CurrentChain;
        }
        
        public float GetChainPercentage()
        {
            var maxScore = m_CurrentSong.RhythmClipCount;
            var percentage = 100 * m_CurrentChain / maxScore;
            return percentage;
        }
        
        public float GetMaxChain()
        {
            return m_CurrentMaxChain;
        }
        
        public float GetMaxChainPercentage()
        {
            var maxScore = m_CurrentSong.RhythmClipCount;
            var percentage = 100 * m_CurrentMaxChain / maxScore;
            return percentage;
        }
        
        public float GetScore()
        {
            return m_CurrentScore;
        }

        public float GetScorePercentage()
        {
            var percentage = m_CurrentScore * 100 / m_CurrentMaxPossibleScore;
            return percentage;
        }
        
        public ScoreRank GetRank()
        {
            return m_ScoreSettings.GetRank(GetScorePercentage());
        }

        public void UpdateScoreVisual()
        {
            if (m_ScoreTmp != null) {
                m_ScoreTmp.text = m_CurrentScore.ToString();
            }
            if (m_ScoreMultiplierTmp != null) {
                m_ScoreMultiplierTmp.text = m_ScoreMultiplier == 1 ? "" : $"X{m_ScoreMultiplier}";
            }
            if (m_ChainTmp != null) {
                m_ChainTmp.text = m_CurrentChain.ToString();
            }

            if (m_RankSlider != null) {
                if (m_CurrentSong == null) {
                    m_RankSlider.SetRank(0, m_ScoreSettings.GetRank(0));
                } else {
                    var percentage =  GetScorePercentage();
                    m_RankSlider.SetRank(percentage, m_ScoreSettings.GetRank(percentage));
                }
            }
        }

        public virtual void ScorePopup(Transform spawnPoint, NoteAccuracy noteAccuracy, Vector3 offset)
        {
            Pop(noteAccuracy.popPrefab, spawnPoint, offset);
        }

        public virtual void Pop(GameObject prefab, Transform spawnPoint, Vector3 offset)
        {
            if (prefab == null) {
                Debug.LogError("Prefab for score accuracy cannot be Null");
                return;
            }
            if (spawnPoint == null) {
                PoolManager.Instantiate(prefab, offset, prefab.transform.rotation);
            } else {
                PoolManager.Instantiate(prefab, spawnPoint.position+offset, spawnPoint.rotation);
            }
            
        }
    }
}
