/// ---------------------------------------------
/// Rhythm Timeline
/// Copyright (c) Dyplsoom. All Rights Reserved.
/// https://www.dypsloom.com
/// ---------------------------------------------

namespace Dypsloom.RhythmTimeline.Scoring
{
    using Dypsloom.RhythmTimeline.Core.Managers;
    using Dypsloom.Shared;
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Serialization;

    [System.Serializable]
    public class NoteAccuracy
    {
        public string name;
        public bool breakChain;
        public float percentageTheshold;
        public float score;
        public Sprite icon;
        public GameObject popPrefab;
    }

    [System.Serializable]
    public class ScoreRank
    {
        public string name;
        public Sprite icon;
        public float percentageTheshold;
    }

    [CreateAssetMenu(fileName = "My Score Setting", menuName = "Dypsloom/Rhythm Timeline/Score Setting", order = 1)]
    public class ScoreSettings : ScriptableObject
    {
        [Tooltip("The default accuracy when missing.")]
        [SerializeField] protected NoteAccuracy m_MissAccuracy;
        [Tooltip("The accuracy table for doing bad notes, higher percentage means worse.")]
        [SerializeField] protected NoteAccuracy[] m_BadAccuracyTable;
        [FormerlySerializedAs("m_AccuracyTable")]
        [Tooltip("The accuracy table for doing good notes, higher percentage means better.")]
        [SerializeField] protected NoteAccuracy[] m_GoodAccuracyTable;
        [Tooltip("The rank table.")]
        [SerializeField] protected ScoreRank[] m_RankTable;
    
        protected Dictionary<string, NoteAccuracy> m_BadAccuracyDictionary;
        protected Dictionary<string, NoteAccuracy> m_GoodAccuracyDictionary;
        protected Dictionary<string, ScoreRank> m_RankDictionary;

        //Ordered Good, Bad, Miss
        [NonSerialized] protected List<NoteAccuracy> m_OrderedAllAccuracyList = new List<NoteAccuracy>();
        
        protected float m_MaxNoteScore;

        public float MaxNoteScore => m_MaxNoteScore;

        public virtual IReadOnlyDictionary<string, NoteAccuracy> GoodAccuracyDictionary
        {
            get
            {
                if (!m_Initialized) { Initialize(); }

                return m_GoodAccuracyDictionary;
            }
        }
        public virtual IReadOnlyList<NoteAccuracy> OrderedGoodAccuracyTable
        {
            get
            {
                if (!m_Initialized) { Initialize(); }

                return m_GoodAccuracyTable;
            }
        }
        public virtual IReadOnlyDictionary<string, NoteAccuracy> BadAccuracyDictionary
        {
            get
            {
                if (!m_Initialized) { Initialize(); }

                return m_BadAccuracyDictionary;
            }
        }
        public virtual IReadOnlyList<NoteAccuracy> OrderedBadAccuracyTable
        {
            get
            {
                if (!m_Initialized) { Initialize(); }

                return m_BadAccuracyTable;
            }
        }
        
        public virtual IReadOnlyDictionary<string, ScoreRank> RankDictionary
        {
            get
            {
                if (!m_Initialized) { Initialize(); }

                return m_RankDictionary;
            }
        }
        public virtual IReadOnlyList<ScoreRank> OrderedRankTable
        {
            get
            {
                if (!m_Initialized) { Initialize(); }

                return m_RankTable;
            }
        }
        public virtual IReadOnlyList<NoteAccuracy> OrderedAllAccuracyList
        {
            get
            {
                if (!m_Initialized) { Initialize(); }

                return m_OrderedAllAccuracyList;
            }
        }

        [NonSerialized] protected bool m_Initialized = false;
        
    
        public void Initialize()
        {
            //Good Note Accuracy
            Array.Sort(m_GoodAccuracyTable, new Comparison<NoteAccuracy>( 
                (x, y) => x.percentageTheshold.CompareTo(y.percentageTheshold))); 
        
            m_GoodAccuracyDictionary = new Dictionary<string, NoteAccuracy>();
            for (int i = 0; i < m_GoodAccuracyTable.Length; i++) {
                m_GoodAccuracyDictionary.Add(m_GoodAccuracyTable[i].name, m_GoodAccuracyTable[i]);
            }
            
            //Bad Note Accuracy
            Array.Sort(m_BadAccuracyTable, new Comparison<NoteAccuracy>( 
                (x, y) => x.percentageTheshold.CompareTo(y.percentageTheshold))); 
        
            m_BadAccuracyDictionary = new Dictionary<string, NoteAccuracy>();
            for (int i = 0; i < m_BadAccuracyTable.Length; i++) {
                m_BadAccuracyDictionary.Add(m_BadAccuracyTable[i].name, m_BadAccuracyTable[i]);
            }
        
            //Rank
            Array.Sort(m_RankTable, new Comparison<ScoreRank>( 
                (x, y) => x.percentageTheshold.CompareTo(y.percentageTheshold))); 
        
            m_RankDictionary = new Dictionary<string, ScoreRank>();
            for (int i = 0; i < m_RankTable.Length; i++) {
                m_RankDictionary.Add(m_RankTable[i].name, m_RankTable[i]);
            }

            m_MaxNoteScore = GetGoodNoteAccuracy(0).score;
            
            m_OrderedAllAccuracyList.Clear();
            m_OrderedAllAccuracyList.AddRange(m_GoodAccuracyTable);
            m_OrderedAllAccuracyList.AddRange(m_BadAccuracyTable);
            m_OrderedAllAccuracyList.Add(m_MissAccuracy);

            m_Initialized = true;
        }

        public virtual NoteAccuracy GetGoodNoteAccuracy(float offsetPercentage)
        {
            NoteAccuracy last = null;
            
            for (int i = 0; i < m_GoodAccuracyTable.Length; i++) {
                
                if (offsetPercentage <= m_GoodAccuracyTable[i].percentageTheshold) {
                    //Debug.LogError("Found match: "+offsetPercentage +" <= " +m_AccuracyTable[i].percentageTheshold+" "+m_AccuracyTable[i].name);
                    return m_GoodAccuracyTable[i];
                }
                
                //Debug.LogError("TryNext:"+offsetPercentage +" <= " +m_AccuracyTable[i].percentageTheshold+" "+m_AccuracyTable[i].name);

                last = m_GoodAccuracyTable[i];
            }

            return last;
        }
        
        public virtual NoteAccuracy GetBadNoteAccuracy(float offsetPercentage)
        {
            NoteAccuracy last = null;
            
            for (int i = 0; i < m_BadAccuracyTable.Length; i++) {
                
                if (offsetPercentage <= m_BadAccuracyTable[i].percentageTheshold) {
                    //Debug.LogError("Found match: "+offsetPercentage +" <= " +m_AccuracyTable[i].percentageTheshold+" "+m_AccuracyTable[i].name);
                    return m_BadAccuracyTable[i];
                }
                
                //Debug.LogError("TryNext:"+offsetPercentage +" <= " +m_AccuracyTable[i].percentageTheshold+" "+m_AccuracyTable[i].name);

                last = m_BadAccuracyTable[i];
            }

            return last;
        }
    
        public virtual NoteAccuracy GetMissAccuracy()
        {
            return m_MissAccuracy;
        }
    
        public virtual ScoreRank GetRank(float percentage)
        {
            ScoreRank scoreRank = m_RankTable[0];
            for (int i = 0; i < m_RankTable.Length; i++) {

                if (percentage > m_RankTable[i].percentageTheshold) {
                    scoreRank = m_RankTable[i]; 
                    continue;
                }
                break;
            }

            return scoreRank;
        }

        public virtual int GetID(NoteAccuracy noteAccuracy)
        {
            return m_OrderedAllAccuracyList?.IndexOf(noteAccuracy) ?? -1;
        }
    }
}