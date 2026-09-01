namespace Dypsloom.RhythmTimeline.UI
{
    using System;
    using TMPro;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.UI;

    public class SongTimeToDurationSlider : MonoBehaviour
    {
        [Tooltip("The Playable Director")]
        [SerializeField] protected PlayableDirector m_PlayableDirector;
        [Tooltip("The slider (optional).")]
        [SerializeField] protected Slider m_Slider;
        [Tooltip("The image slider fill (optional).")]
        [SerializeField] protected Image m_SliderFill;
        [Tooltip("The Slider Gradient.")]
        [SerializeField] protected Gradient m_SliderGradient;
        [Tooltip("The text to display the time in seconds.")]
        [SerializeField] protected TMP_Text m_TimeText;
        [Tooltip("The time text format.")]
        [SerializeField] protected string m_TimeTextFormat = "0.00";
        [Tooltip("The text to display the text in percentage.")]
        [SerializeField] protected TMP_Text m_PercentageText;
        [Tooltip("The time text format.")]
        [SerializeField] protected string m_PercentageTextFormat = "0.00";

        private void Start()
        {
            if (m_PlayableDirector == null) {
                enabled = false;
            }
        }

        private void Update()
        {
            UpdateSlider(m_PlayableDirector);
        }

        public void UpdateSlider(PlayableDirector director)
        {
            var time = director.time;
            var duration = director.duration;
            var normalizedTime = (float)(time / duration);
            
            if (m_Slider != null) {
                m_Slider.value = normalizedTime;
            }

            if (m_SliderFill != null && m_SliderGradient !=null) {
                m_SliderFill.color = m_SliderGradient.Evaluate(normalizedTime);
            }

            if (m_TimeText != null) {
                m_TimeText.text = time.ToString(m_TimeTextFormat);
            }
            
            if (m_PercentageText != null) {
                m_PercentageText.text = ( 100*normalizedTime ).ToString(m_PercentageTextFormat)+"%";
            }
        }
    }
}