namespace Dypsloom.RhythmTimeline.Midi
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Melanchall.DryWetMidi.Core;
    using Melanchall.DryWetMidi.Interaction;
    using UnityEngine;
    using MidiEventType = Melanchall.DryWetMidi.Core.MidiEventType;
    using TrackChunkUtilities = Melanchall.DryWetMidi.Core.TrackChunkUtilities;

    public class MidiFileAsset : ScriptableObject
    {
        public MidiFile MidiFile;
        
        [Serializable]
        public class MidiNote
        {
            public int NoteNumber;
            public int Octave => NoteNumber/12;
            public long Time;
            public long length;
        }
        
        [Serializable]
        public class MidiTrack
        {
            public int TrackID;
            public string TrackName;
            public long Tempo;
            public List<MidiNote> Notes;
        }

        public List<MidiTrack> m_MidiTracks = new List<MidiTrack>();
        
        public void Initialize(MidiFile midiFile)
        {
            MidiFile = midiFile;

            m_MidiTracks = new List<MidiTrack>();
            
            int trackID = 0;
            foreach (var chunk in TrackChunkUtilities.GetTrackChunks(midiFile)) {
                
                var chunkNotes = chunk.GetNotes();

                var trackNotes = new List<MidiNote>();
                foreach (var note in chunkNotes)
                {
                    
                    trackNotes.Add(new MidiNote()
                    {
                        NoteNumber =note.NoteNumber,
                        Time=note.Time,
                        length=note.Length,
                    });
                }

                var allEvents = chunk.GetTimedEvents();
                var trackNameEvent = allEvents.FirstOrDefault(x => x.Event.EventType == MidiEventType.SequenceTrackName)?.Event as SequenceTrackNameEvent;
                var tempoEvent =
                    allEvents.FirstOrDefault(x => x.Event.EventType == MidiEventType.SetTempo)?.Event as SetTempoEvent;

                m_MidiTracks.Add( new MidiTrack()
                {
                    TrackID = trackID,
                    TrackName = trackNameEvent?.Text ?? "No NAME",
                    Tempo = tempoEvent?.MicrosecondsPerQuarterNote ?? SetTempoEvent.DefaultMicrosecondsPerQuarterNote, // 500,000 microseconds = 120 beats per minute
                    Notes = trackNotes,
                });
                
                
                
                trackID += 1;
            }

            
            
            
            
            
        }
    }
}