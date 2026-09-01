namespace Dypsloom.RhythmTimeline.UI
{
    using Dypsloom.RhythmTimeline.Core.Managers;
    using Dypsloom.Shared;
    using UnityEngine;

    public class SongChooserPanelHandler : MonoBehaviour
    {
        [Tooltip("For local multiplayer you may have multiple Rhythm tracks playing, match the PlayerIds for all relevant components.")]
        [SerializeField] protected uint m_PlayerID = 0;
        [Tooltip("Song Chooser")]
        [SerializeField] protected SongChooserPanel m_SongChooserPanel;
        [Tooltip("The button to play the selected song")]
        [SerializeField] protected string m_PlayButtonName = "Submit";
        [Tooltip("The button to play the selected song")]
        [SerializeField] protected string m_NavigationAxisName = "Vertical";
        [Tooltip("Repeat the input after a certain delay when navigation the buttons.")]
        [SerializeField] protected float m_RepeatNavigationInput = 0.5f;

        public uint PlayerID => m_PlayerID;
        protected RhythmGameManager m_RhythmGameManager;
        protected float m_RepeatInputNavigationTimer = 0;
        
        protected virtual void Start()
        {
            if (m_SongChooserPanel == null) {
                m_SongChooserPanel = GetComponent<SongChooserPanel>();
            }

            m_RhythmGameManager = Toolbox.Get<RhythmGameManager>(m_PlayerID);
        }

        public void Update()
        {
            if(m_SongChooserPanel.IsOpen == false){return;}
            
            NavigationInputs();

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetButtonDown(m_PlayButtonName)) {
                m_SongChooserPanel.PlaySelectedSong();
            }
#else
            //TODO add new input system
#endif
            
            
        }

        protected virtual void NavigationInputs()
        {
            float axisValue = 0f;
            
#if ENABLE_LEGACY_INPUT_MANAGER
            axisValue = Input.GetAxisRaw(m_NavigationAxisName);
#else
            //TODO add new input system
#endif
            

            var next = axisValue < -0.1f;
            var previous = axisValue > 0.1f;
            
            if (!next && !previous){
                m_RepeatInputNavigationTimer = -1;
                return;
            }

            if (m_RepeatInputNavigationTimer > 0) {
                m_RepeatInputNavigationTimer -= Time.deltaTime;
                return;
            }

            m_RepeatInputNavigationTimer = m_RepeatNavigationInput;
            SelectNextPrevious(next);
        }
        
        protected virtual void SelectNextPrevious(bool isNext)
        {
            var currentIndex = m_SongChooserPanel.SelectedIndex;
            var newIndex = isNext ? currentIndex + 1 : currentIndex - 1;

            //Loop if the index is out of range.
            var songCount = m_RhythmGameManager.Songs.Length;
            if (newIndex >= songCount) {
                newIndex = 0;
            }else if (newIndex <= 0) {
                newIndex = songCount - 1;
            }
            
            m_SongChooserPanel.SetSelectedSong(newIndex);
        }
    }
}