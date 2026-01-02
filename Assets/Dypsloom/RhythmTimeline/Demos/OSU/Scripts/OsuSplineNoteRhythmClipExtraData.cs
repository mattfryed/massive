namespace Dypsloom.RhythmTimeline.Demos.OSU.Scripts
{
    using Dypsloom.RhythmTimeline.Core.Notes;
    using Dypsloom.RhythmTimeline.Core.Playables;
    using UnityEngine;
    using UnityEngine.U2D;

    /// <summary>
    /// This scriptable object is created when a note clip is created in the timeline.
    /// It will automatically get hooked to the clip. 
    /// </summary>
    public class OsuSplineNoteRhythmClipExtraData : RhythmClipExtraNoteData
    {
        [SerializeField]
        public bool PingPong;
        [SerializeField]
        public Spline spline;

        public override void Initialize(Note notePrefab)
        {
            if (m_Initialized == false) {
                var osuNoteInstance = notePrefab as OSUSplineNote;
                if(osuNoteInstance == null){ return;}

                var mainSplineShapeController = osuNoteInstance.MainSpline.ShapeController;
                if(mainSplineShapeController == null){ return;}

                PingPong = osuNoteInstance.PingPong;
                var jsonSplineCopy = JsonUtility.ToJson(mainSplineShapeController.spline);
                spline = JsonUtility.FromJson<Spline>(jsonSplineCopy);
            }
            base.Initialize(notePrefab);
        }
    }
}