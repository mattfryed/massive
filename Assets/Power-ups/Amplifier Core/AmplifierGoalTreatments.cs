using Massive.Scoring;
using UnityEngine;

namespace Massive.Multiplier
{
    // One scene-owned workbench. Preview state never writes to MatchScoreService.
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class AmplifierGoalTreatments : MonoBehaviour
    {
        [SerializeField] private VectorGridGPU grid;
        [SerializeField] private AmplifierGoalCapture[] goals;
        [SerializeField] private Shader treatmentShader;
        [SerializeField] private AmplifierCoreGameplay previewCorePrefab;
        [SerializeField] private AmplifierTreatmentSettings treatment = new AmplifierTreatmentSettings();
        [SerializeField] private bool preview;
        private bool previewPaused;
        [SerializeField, Range(1, 2)] private int previewTeam = 1;
        [SerializeField, Range(0, 3)] private int heldLevel = 1;
        [SerializeField, Range(0, 4)] private float playbackSpeed = 1;
        [SerializeField] private bool showControls = true;
        [SerializeField] private bool legacyCaptureFeedback;
        [SerializeField] private bool loopCapture;
        [SerializeField,Range(1,12)] private float loopInterval = 4;
        private float nextLoopCapture = -1;
        public bool LoopCapture { get => loopCapture; set { if(loopCapture!=value) nextLoopCapture=-1; loopCapture=value; } }
        public float LoopInterval { get => loopInterval; set => loopInterval=Mathf.Clamp(value,1,12); }
        private readonly float[] captures = { -1000, -1000 };
        private readonly Vector4[] origins = new Vector4[2];
        private readonly Vector4[] states = new Vector4[2];
        [SerializeField] private MetaballSDFInstance[] massSurfaces = new MetaballSDFInstance[2];
        private readonly MaterialPropertyBlock[] surfaceBlocks = new MaterialPropertyBlock[2];
        private bool surfacesResolved;
        private AmplifierCoreGameplay previewCore;
        private float clock;
        [SerializeField, HideInInspector] private int preset;
        public int PresetIndex => preset;
        public AmplifierTreatmentSettings Settings => treatment;
        public bool Preview { get => preview; set { if(value) preview=true; else StopPreview(); } }
        public bool PreviewPaused { get => previewPaused; set => previewPaused = value; }
        public int PreviewTeam { get => previewTeam; set => previewTeam = Mathf.Clamp(value, 1, 2); }
        public int HeldLevel { get => heldLevel; set => heldLevel = Mathf.Clamp(value, 0, 3); }
        public float PlaybackSpeed { get => playbackSpeed; set => playbackSpeed = Mathf.Clamp(value, 0, 4); }
        public float Clock => clock;
        public bool LegacyCaptureFeedback => legacyCaptureFeedback;
        public bool ShowControls { get => showControls; set => showControls = value; }

        public void Configure(VectorGridGPU targetGrid, AmplifierGoalCapture[] targetGoals, Shader shader, AmplifierCoreGameplay core)
        {
            grid = targetGrid; goals = targetGoals;
            treatmentShader = shader; previewCorePrefab = core;
            foreach (var g in goals) if (g != null) g.SetTreatments(this);
            surfacesResolved = false; ResolveSurfaces();
        }
        public void ApplyPreset(int index) { preset = (index + 4) % 4; treatment = AmplifierTreatmentSettings.Preset(preset); }
        public void RestoreSettings(string json) { treatment = JsonUtility.FromJson<AmplifierTreatmentSettings>(json); }
        public void Capture(int team) { captures[Mathf.Clamp(team - 1, 0, 1)] = clock; }
        public void StartPreview() { preview=true; previewPaused=false; RenderTreatment(); }
        public void StopPreview()
        {
            preview=false; previewPaused=false; loopCapture=false;
            ResetPreview();
            ReleasePreviewCore();
            // Clear synthetic bursts and return held levels to the score service.
            // Keep the treatment component/settings active for real captures.
            RenderTreatment();
            if(grid!=null) grid.SendMessage("ApplyMaterialBindings",SendMessageOptions.DontRequireReceiver);
        }
        public void TriggerPreview() { preview = true; previewPaused=false; Capture(previewTeam);nextLoopCapture=clock+loopInterval; RenderTreatment(); }
        public void ResetPreview() { clock = 0; captures[0] = captures[1] = -1000;nextLoopCapture=-1; }
        public void SetPreviewTime(float time) { clock = Mathf.Max(0, time); RenderTreatment(); }
        public void AdvancePreview(float delta)
        {
            if(preview && previewPaused) { RenderTreatment(); return; }
            // Playback is a preview dial; it must never pause or retime real capture feedback.
            clock+=Mathf.Max(0,delta)*(preview?playbackSpeed:1);
            if(loopCapture && preview && playbackSpeed>0 && (nextLoopCapture<0 || clock>=nextLoopCapture))
            { Capture(previewTeam);nextLoopCapture=clock+loopInterval; }
            RenderTreatment();
        }
        private void Update()
        {
            if (!Application.isPlaying) return;
            AdvancePreview(Time.unscaledDeltaTime);
        }
        private void LateUpdate() { if (Application.isPlaying) RenderTreatment(); }
        private void OnEnable() { surfacesResolved = false; if(Application.isPlaying) StopPreview(); }
        private void OnDisable() { Cleanup(); }
        private void OnDestroy() { Cleanup(); }
        private void Cleanup()
        {
            if (massSurfaces != null) for (int i=0; i<massSurfaces.Length; i++)
            {
                if (massSurfaces[i] == null || massSurfaces[i].TargetRenderer == null) continue;
                var r=massSurfaces[i].TargetRenderer; var block=surfaceBlocks[i] ?? (surfaceBlocks[i]=new MaterialPropertyBlock());
                r.GetPropertyBlock(block); block.SetFloat("_AmpGoalEnabled",0); r.SetPropertyBlock(block);
            }
            ReleasePreviewCore();
        }
        private void ReleasePreviewCore()
        {
            if(previewCore!=null) { previewCore.gameObject.SetActive(false); Release(previewCore.gameObject); }
            previewCore=null;
        }
        private static void Release(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
        private int Level(int team)
        {
            if (preview) return team == previewTeam ? heldLevel : 0;
            var score = MatchScoreService.Instance;
            return score != null ? Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(1, score.GetTeamAmplifierMultiplier(team)), 2)), 0, 3) : 0;
        }
        private AmplifierGoalCapture Goal(int team)
        {
            if (goals != null) foreach (var g in goals) if (g != null && g.TeamID == team) return g;
            return null;
        }
        private float Burst(int team)
        {
            float age = clock - captures[team - 1] - treatment.absorptionSeconds;
            return treatment.captureBurst && age >= 0 ? treatment.burstIntensity * Mathf.Sin(Mathf.PI * Mathf.Clamp01(age / Mathf.Max(0.1f, treatment.burstDuration))) : 0;
        }
        private void ResolveSurfaces()
        {
            if (surfacesResolved) return;
            surfacesResolved = true;
            if (massSurfaces == null || massSurfaces.Length != 2) massSurfaces = new MetaballSDFInstance[2];
            for (int team=1;team<=2;team++)
            {
                if (massSurfaces[team-1] != null) continue;
                var g=Goal(team); if(g==null || g.transform.parent==null) continue;
                var visual=g.transform.parent.GetComponentInChildren<ScoreVoidMetaballsVisual>(true);
                if(visual!=null) massSurfaces[team-1]=visual.sdf;
            }
        }
        public void RenderTreatment()
        {
            if (!isActiveAndEnabled) return;
            ResolveSurfaces();
            for (int team=1;team<=2;team++)
            {
                var goal=Goal(team); var surface=massSurfaces[team-1];
                if(goal==null || surface==null || surface.TargetRenderer==null) continue;
                int level=Level(team); float burst=Burst(team);
                float energy=(treatment.heldCharge?treatment.heldIntensity*level:0)+burst;
                var r=surface.TargetRenderer; var block=surfaceBlocks[team-1] ?? (surfaceBlocks[team-1]=new MaterialPropertyBlock());
                r.GetPropertyBlock(block);
                Vector3 origin=goal.CapturePoint.position;
                WriteGridProperties(block);
                if(grid!=null)
                {
                    block.SetVector("_GridSize",grid.size);
                    block.SetMatrix("_AmpGridWorldToLocal",grid.transform.worldToLocalMatrix);
                    block.SetMatrix("_AmpGridLocalToWorld",grid.transform.localToWorldMatrix);
                }
                block.SetFloat("_AmpGoalEnabled",1);
                block.SetFloat("_AmpGoalTime",clock);
                float gridEdge=grid!=null?grid.size.x*0.5f*(team==1?-1:1):0;
                block.SetVector("_AmpGridEffectGate",new Vector4(treatment.allowEffectsOverGrid?1:0,gridEdge,team==1?1:-1,0));
                float seamAge=clock-captures[team-1]-treatment.absorptionSeconds;
                float seamBurst=treatment.seamCapture && seamAge>=0 ? treatment.seamCaptureIntensity*Mathf.Sin(Mathf.PI*Mathf.Clamp01(seamAge/Mathf.Max(0.1f,treatment.burstDuration))) : 0;
                float seamEnergy=treatment.seamMode==AmplifierSeamMode.Off?0:seamBurst+(treatment.seamHeld?treatment.seamHeldIntensity*level:0);
                block.SetVector("_AmpSeam",new Vector4((int)treatment.seamMode,seamEnergy,treatment.seamWidth,treatment.seamReach));
                block.SetVector("_AmpSeamModes",new Vector4(treatment.seamModeCount,treatment.seamModeMix,treatment.seamBeatFrequency,treatment.seamDrift));
                block.SetFloat("_AmpSeamSplit",treatment.seamSpectralSplit);
                block.SetVector("_AmpInterferenceShimmer",treatment.interferenceShimmer.ShaderValues);
                block.SetVector("_AmpGoalOriginWS",new Vector4(origin.x,origin.y,origin.z,treatment.heldCharge?treatment.goalHeldMotion*level:0));
                block.SetVector("_AmpGoalPulse",new Vector4(clock-captures[team-1]-treatment.absorptionSeconds,treatment.wholeGoalPulse?treatment.goalPulseAmplitude:0,treatment.envelopeWidth,treatment.propagationSpeed));
                block.SetVector("_AmpGoalShape",new Vector4(treatment.wavelength,treatment.carrierFrequency,treatment.dispersion,treatment.alongAcrossRatio));
                block.SetVector("_AmpGoalTrain",new Vector4(Mathf.Clamp(treatment.emissions,1,5),treatment.packetSpacing,treatment.quadraturePhase*Mathf.Deg2Rad,0));
                block.SetVector("_AmpGoalCorona",new Vector4(treatment.radialExtent,(treatment.corona||treatment.branching)?energy*treatment.intensity:0,treatment.density,treatment.nodalClustering));
                block.SetVector("_AmpGoalStyle",new Vector4(treatment.grainSize,treatment.falloff,treatment.chromaticSeparation,treatment.tangleDensity));
                block.SetVector("_AmpCloud",new Vector4(treatment.corona&&treatment.particleCloud?treatment.cloudIntensity:0,treatment.cloudExtent,treatment.cloudDensity,0));
                block.SetVector("_AmpFlare",new Vector4(treatment.corona&&treatment.flareCorona?treatment.flareIntensity:0,treatment.flareExtent,treatment.corona&&treatment.atmosphereWisps?treatment.atmosphereIntensity:0,treatment.atmosphereExtent));
                block.SetVector("_AmpCloudShimmer",treatment.cloudShimmer.ShaderValues);
                block.SetVector("_AmpFlareShimmer",treatment.flareShimmer.ShaderValues);
                block.SetVector("_AmpWispShimmer",treatment.wispShimmer.ShaderValues);
                block.SetColor("_AmpPaletteA",treatment.coronaColorA);block.SetColor("_AmpPaletteB",treatment.coronaColorB);block.SetColor("_AmpPaletteC",treatment.coronaColorC);
                block.SetVector("_AmpGoalWaveA",treatment.waveA); block.SetVector("_AmpGoalWaveB",treatment.waveB); block.SetVector("_AmpGoalWaveC",treatment.waveC);
                int poles=Mathf.Clamp(treatment.poleCount+(treatment.addPolesWithMultiplier?level*2:0),2,14);
                block.SetVector("_AmpGoalPoles",new Vector4(treatment.polar?poles:0,treatment.poleSeparation*2/poles*Mathf.Deg2Rad,treatment.polePhase*Mathf.Deg2Rad,treatment.poleWidth));
                float reach=treatment.solarWispReach+(treatment.branching?treatment.branchReach*(1+level*treatment.multiplierReach):0);
                block.SetVector("_AmpGoalBranch",new Vector4(reach,treatment.branching?treatment.branchDensity*(1+level*treatment.multiplierDensity):0,treatment.branchLifetime*(1+level*treatment.multiplierPersistence),treatment.reconnect));
                block.SetVector("_AmpGoalMode",new Vector4(treatment.particleCloud?1:0,treatment.alternatePoles?1:0,treatment.corona?1:0,treatment.branching?1:0));
                r.SetPropertyBlock(block);
            }
            UpdatePreviewCore();
        }
        public Vector2 BoundaryOffset(float y, int team, int level, out float strength)
        {
            Vector2 offset = Vector2.zero; strength = 0;
            float age = clock - captures[team - 1] - treatment.absorptionSeconds;
            if (treatment.capturePackets)
                for (int i = 0; i < Mathf.Clamp(treatment.emissions, 1, 5); i++)
                    offset += PacketPair(y, age - i * treatment.packetSpacing, treatment.carrierAmplitude, team, ref strength);
            if (treatment.heldCharge && level > 0)
            {
                if (treatment.travellingWave)
                    offset.x += treatment.heldAmplitude * level * Mathf.Sin(y * 2 * Mathf.PI / Mathf.Max(0.25f, treatment.wavelength) - clock * treatment.carrierFrequency * 2 * Mathf.PI);
                if (treatment.occasionalPackets)
                {
                    float period = Mathf.Max(0.5f, treatment.heldPacketInterval);
                    float recent = Mathf.Repeat(clock, period);
                    for (int i = 0; i < 3; i++) offset += PacketPair(y, recent + i * period, treatment.heldAmplitude * level * 2, team, ref strength);
                }
            }
            float edge = grid != null ? Mathf.SmoothStep(0, 1, (grid.size.y * 0.5f - Mathf.Abs(y)) / 0.5f) : 1;
            strength *= edge; return offset * edge * treatment.gridCoupling;
        }
        private Vector2 PacketPair(float y, float age, float amplitude, int team, ref float strength)
        {
            if (age < 0 || age > 8) return Vector2.zero;
            float w = Mathf.Max(0.1f, treatment.envelopeWidth);
            float width = Mathf.Sqrt(w * w + Mathf.Pow(treatment.dispersion * age, 2));
            float gain = Mathf.Sqrt(w / width) * Mathf.SmoothStep(0, 1, age / 0.12f) * Mathf.Exp(-age * 0.65f);
            Vector2 result = Vector2.zero;
            for (int d = -1; d <= 1; d += 2)
            {
                float s = y * d - treatment.propagationSpeed * age;
                float envelope = Mathf.Exp(-s * s / (2 * width * width)) * gain;
                float phase = s * 2 * Mathf.PI / Mathf.Max(0.25f, treatment.wavelength) - age * treatment.carrierFrequency * 2 * Mathf.PI + treatment.dispersion * age * s * s / (width * width);
                result.x += Mathf.Cos(phase) * amplitude * envelope * (team == 1 ? 1 : -1);
                result.y += Mathf.Cos(phase - treatment.quadraturePhase * Mathf.Deg2Rad) * amplitude * envelope * treatment.alongAcrossRatio * d;
                strength += envelope * amplitude;
            }
            return result;
        }
        public void WriteGridProperties(MaterialPropertyBlock block)
        {
            for (int team = 1; team <= 2; team++)
            {
                var goal = Goal(team);
                Vector3 p = goal != null && grid != null ? grid.transform.InverseTransformPoint(goal.CapturePoint.position) : Vector3.zero;
                if (grid != null) p.x = (team == 1 ? -1 : 1) * grid.size.x * 0.5f;
                origins[team - 1] = new Vector4(p.x, p.y, clock - captures[team - 1] - treatment.absorptionSeconds, Level(team));
                states[team - 1] = new Vector4(treatment.capturePackets ? treatment.carrierAmplitude : 0, treatment.heldCharge && treatment.travellingWave ? treatment.heldAmplitude : 0, treatment.heldCharge && treatment.occasionalPackets ? treatment.heldAmplitude * 2 : 0, treatment.gridDischarge && treatment.allowEffectsOverGrid ? treatment.gridAmplitude : 0);
            }
            for (int i=0;i<states.Length;i++) states[i] *= treatment.gridCoupling;
            block.SetVectorArray("_AmpOrigins", origins); block.SetVectorArray("_AmpStates", states);
            block.SetVector("_AmpPacket", new Vector4(treatment.wavelength, treatment.envelopeWidth, treatment.propagationSpeed, treatment.dispersion));
            block.SetVector("_AmpPhase", new Vector4(treatment.carrierFrequency, treatment.alongAcrossRatio, treatment.quadraturePhase * Mathf.Deg2Rad, clock));
            block.SetVector("_AmpTiming", new Vector4(treatment.emissions, treatment.packetSpacing, treatment.heldPacketInterval, isActiveAndEnabled ? 1 : 0));
            block.SetVector("_AmpField", new Vector4(treatment.gridSpeed, treatment.gridWidth, treatment.gridWavelength, treatment.gridCurl));
            block.SetFloat("_AmpHeldTint", treatment.heldCharge && treatment.coloredBoundary ? treatment.heldIntensity : 0);
        }
        private void UpdatePreviewCore()
        {
            if(!preview) { ReleasePreviewCore(); return; }
            float age = clock - captures[previewTeam - 1];
            bool visible = preview && age >= 0 && age < treatment.absorptionSeconds;
            if (visible && previewCore == null && previewCorePrefab != null)
            {
                // Instantiate beneath an inactive root so the clone can be made presentation-only before OnEnable.
                var staging = new GameObject("Preview core staging") { hideFlags = HideFlags.HideAndDontSave }; staging.SetActive(false);
                previewCore = Instantiate(previewCorePrefab, staging.transform);
                previewCore.SetTreatmentPreview();
                previewCore.transform.SetParent(null); previewCore.gameObject.hideFlags = HideFlags.HideAndDontSave;
                Release(staging);
            }
            if (previewCore == null) return;
            previewCore.gameObject.SetActive(visible);
            var goal = Goal(previewTeam); if (!visible || goal == null) return;
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, treatment.absorptionSeconds));
            Vector3 p = goal.CapturePoint.position;
            previewCore.transform.position = p + Vector3.right * (previewTeam == 1 ? 1 : -1) * 2.6f * (1 - Mathf.SmoothStep(0, 1, t));
            previewCore.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 0.02f, t * t);
        }
        private void OnGUI()
        {
            if (!Application.isPlaying || !showControls) return;
            if(!preview)
            {
                if(GUI.Button(new Rect(12,100,240,28),"Start Amplifier Visual Preview"))StartPreview();
                return;
            }
            GUILayout.BeginArea(new Rect(12, 100, 320, 430), GUI.skin.box);
            GUILayout.Label("AMPLIFIER / " + AmplifierTreatmentSettings.Names[preset]);
            if (GUILayout.Button("Next treatment")) ApplyPreset(preset + 1);
            if(preview)
            {
                GUILayout.Label("PREVIEW ACTIVE — synthetic multiplier");
                if(GUILayout.Button("Stop Preview — return to gameplay"))StopPreview();
            }
            GUILayout.BeginHorizontal(); if (GUILayout.Button("Light")) previewTeam = 1; if (GUILayout.Button("Dark")) previewTeam = 2; GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); for (int i = 0; i < 4; i++) if (GUILayout.Button("×" + (1 << i))) heldLevel = i; GUILayout.EndHorizontal();
            if (GUILayout.Button("Trigger absorption + capture")) TriggerPreview();
            bool loop=GUILayout.Toggle(LoopCapture,"Loop Capture");
            if(loop!=LoopCapture){LoopCapture=loop;if(loop)StartPreview();}
            GUILayout.Label("Playback " + playbackSpeed.ToString("0.00") + "×"); playbackSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0, 4);
            GUILayout.Label("COLOR INTERFERENCE / " + AmplifierTreatmentSettings.SeamNames[(int)treatment.seamMode]);
            treatment.seamMode=(AmplifierSeamMode)GUILayout.Toolbar((int)treatment.seamMode,AmplifierTreatmentSettings.SeamNames);
            treatment.seamCapture=GUILayout.Toggle(treatment.seamCapture,"Interference on capture");
            treatment.seamHeld=GUILayout.Toggle(treatment.seamHeld,"Interference while held");
            treatment.wholeGoalPulse = GUILayout.Toggle(treatment.wholeGoalPulse, "Whole goal pulse");
            treatment.allowEffectsOverGrid=GUILayout.Toggle(treatment.allowEffectsOverGrid,"Allow Effects Over Grid");
            treatment.corona = GUILayout.Toggle(treatment.corona, "Corona"); treatment.capturePackets = GUILayout.Toggle(treatment.capturePackets, "Boundary packets");
            treatment.branching = GUILayout.Toggle(treatment.branching, "Branching"); treatment.gridDischarge = GUILayout.Toggle(treatment.gridDischarge, "Grid discharge");
            GUILayout.Label("Full controls: MASSIVE > Amplifier Treatments"); GUILayout.EndArea();
        }
    }
}
