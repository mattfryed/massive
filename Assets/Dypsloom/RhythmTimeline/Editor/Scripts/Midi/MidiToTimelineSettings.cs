namespace Dypsloom.RhythmTimeline.Midi
{
    using System;
    using System.Collections.Generic;
    using Dypsloom.RhythmTimeline.Core;
    using Dypsloom.RhythmTimeline.Core.Generators;
    using Dypsloom.RhythmTimeline.Core.Notes;
    using UnityEngine;
    using UnityEngine.Serialization;

    [CreateAssetMenu(fileName = "MidiToTimelineSettings", menuName="Dypsloom/Rhythm Timeline/Midi/MidiToTimelineSettings")]
    public class MidiToTimelineSettings : ScriptableObject
    {
        public MidiFileAsset MidiFileAsset;
        public RhythmTimelineAsset RhythmTimelineAsset;
        public List<MidiToRhythmTrack> NotesToTracks = new List<MidiToRhythmTrack>();
        
        public bool TryGetMidiToRhythmTrack(string trackName, out MidiToRhythmTrack rhythmTrack)
        {
            rhythmTrack = null;
            if (NotesToTracks == null) { return false; }

            foreach (var settingsTrack in NotesToTracks) {
                if (settingsTrack.Name == trackName) {
                    rhythmTrack = settingsTrack;
                    return true;
                }
            }

            return false;
        }
         
        [Serializable]
        public class MidiToRhythmTrack
        {
            public string Name;
            public List<MidiNoteToNoteClipGenerator> MidiToGeneratorList = new List<MidiNoteToNoteClipGenerator>();

            public MidiNoteToNoteClipGenerator FindMidiNoteGenerator(int midiNoteNoteNumber)
            {
                foreach (var midiNoteToNoteClipGenerator in MidiToGeneratorList) {
                    if (midiNoteToNoteClipGenerator.NoteNumber == midiNoteNoteNumber) {
                        return midiNoteToNoteClipGenerator;
                    }
                }

                return null;
            }

            public MidiNoteToNoteClipGenerator GetOrCreateMidiNoteGenerator(int midiNoteNoteNumber)
            {
                var midiNoteToNoteClipGenerator = FindMidiNoteGenerator(midiNoteNoteNumber);
                if (midiNoteToNoteClipGenerator == null) {
                    midiNoteToNoteClipGenerator = new MidiNoteToNoteClipGenerator()
                    {
                        NoteNumber = midiNoteNoteNumber
                    };
                    MidiToGeneratorList.Add(midiNoteToNoteClipGenerator);
                }

                return midiNoteToNoteClipGenerator;
                
            }
        }
        
        [Serializable]
        public class MidiNoteToNoteClipGenerator
        {
            [SerializeField]
            public int NoteNumber;
            
            [SerializeField]
            public int TrackID;

            [SerializeField]
            public NoteDefinition NoteDefinition;
            
            [SerializeField]
            [Tooltip("Optional clip generator to populate clip parameters with custom values when generated.")]
            public NoteClipGenerator NoteClipGenerator;
        }

        public void AddMidiToRhythmTrack(MidiToRhythmTrack midiTrackToRhythmTrack)
        {
            NotesToTracks.Add(midiTrackToRhythmTrack);
            
        }
    }
}