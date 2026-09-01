
namespace Dypsloom.RhythmTimeline.Editor.TimeLine
{
    using Dypsloom.RhythmTimeline.Core.Notes;
    using Dypsloom.RhythmTimeline.Core.Playables;
    using UnityEditor;
    using UnityEditor.Timeline;
    using UnityEngine;
    using UnityEngine.Timeline;

    [CustomTimelineEditor(typeof(RhythmClip))]
    public class RhythmClipEditor : ClipEditor
    {
        public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
        {
            base.OnCreate(clip, track, clonedFrom);
            
            //By default set the name to an hidden character
            clip.displayName = " ";

            var otherRhythmClipAsset = clonedFrom?.asset as RhythmClip;

            var thisRhythmClip = clip.asset as RhythmClip;
            thisRhythmClip.Copy(otherRhythmClipAsset);


            var noteDefinition = thisRhythmClip.RhythmPlayableBehaviour?.NoteDefinition;
            thisRhythmClip.SetNoteDefinition(noteDefinition);
        }
        
        public override void OnClipChanged(TimelineClip clip)
        {
            base.OnClipChanged(clip);
        }

        public override void DrawBackground(TimelineClip clip, ClipBackgroundRegion region)
        {
            base.DrawBackground(clip, region);
            
            var rhythmClip = clip.asset as RhythmClip;

            var clipEditorSettings = rhythmClip?.RhythmPlayableBehaviour?.NoteDefinition?.RhythmClipEditorSettings;
            if(clipEditorSettings == null) {
                return;
            }

            var regionHalfHeight = region.position.height / 2;
            var yPosition = region.position.position.y + regionHalfHeight / 2;
            var iconSize = new Vector2(regionHalfHeight,regionHalfHeight);

            var startRegion = new Rect(
                region.position.position.x,
                yPosition,
                iconSize.x,
                iconSize.y);
            var centerRegion = new Rect(
                region.position.position.x+region.position.width/2f-iconSize.x/2,
                yPosition,
                iconSize.x,
                iconSize.y);
            var endRegion = new Rect(
                region.position.position.x+region.position.width-iconSize.x,
                yPosition,
                iconSize.x,
                iconSize.y);
            var backgroundRegion = new Rect(
                region.position.position.x,
                yPosition,
                region.position.width,
                iconSize.y);

            EditorGUI.DrawRect(backgroundRegion, clipEditorSettings.Color);
            
            Color previousGuiColor = GUI.color;
            GUI.color = Color.clear;

            if (clipEditorSettings.Left != null) {
                EditorGUI.DrawTextureTransparent(startRegion, clipEditorSettings.Left);
            }
            
            if (clipEditorSettings.Center != null) {
                EditorGUI.DrawTextureTransparent(centerRegion, clipEditorSettings.Center);
            }
            
            if (clipEditorSettings.Right != null) {
                EditorGUI.DrawTextureTransparent(endRegion, clipEditorSettings.Right);
            }

            GUI.color = previousGuiColor;
        }
    }
}
