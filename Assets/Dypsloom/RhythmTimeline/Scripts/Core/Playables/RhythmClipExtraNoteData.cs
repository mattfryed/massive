namespace Dypsloom.RhythmTimeline.Core.Playables
{
    using Dypsloom.RhythmTimeline.Core.Notes;
    using UnityEngine;

    /// <summary>
    /// This is a scriptable object, it hold additional data that needs to be set on a RhythmCLip.
    /// Override it and use the CreateRhythmClipCustomData function on the Note component to create it.
    /// </summary>
    public class RhythmClipExtraNoteData : ScriptableObject
    {
        [HideInInspector]
        [SerializeField] protected bool m_Initialized;

        /// <summary>
        /// Set the default values for the note.
        /// </summary>
        /// <param name="notePrefab">The note prefab.</param>
        public virtual void Initialize(Note notePrefab)
        {
            m_Initialized = true;
        }
    }
}