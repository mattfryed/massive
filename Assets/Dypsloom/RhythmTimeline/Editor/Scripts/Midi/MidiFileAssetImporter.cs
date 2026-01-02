namespace Dypsloom.RhythmTimeline.Midi.Editor
{
    using Melanchall.DryWetMidi.Core;
    using UnityEditor.AssetImporters;
    using UnityEngine;

    [ScriptedImporter(1,new []{"midi", "mid"})]
    public class MidiFileAssetImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            //Melanchall.DryWetMidi.Core
            var midiFile = MidiFile.Read(ctx.assetPath);
            var midiAsset = ScriptableObject.CreateInstance<MidiFileAsset>();
            midiAsset.Initialize(midiFile);
            
            ctx.AddObjectToAsset("main obj", midiAsset);
            ctx.SetMainObject(midiAsset);
        }
    }
}