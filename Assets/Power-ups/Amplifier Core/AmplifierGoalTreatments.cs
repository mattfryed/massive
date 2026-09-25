using Massive.Scoring;
using UnityEngine;

namespace Massive.Multiplier
{
    // One scene-owned workbench. Preview state never writes to MatchScoreService.
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class AmplifierGoalTreatments : MonoBehaviour, Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField] private VectorGridGPU grid;
        // Optional presentation-only frame for non-planar grids. The scoring owner
        // and wave clock remain this same component; ordinary levels keep using grid.
        [SerializeField, HideInInspector] private Transform presentationGridFrame;
        [SerializeField, HideInInspector] private Vector2 presentationGridSize;
        [SerializeField, HideInInspector] private bool presentationGridUsesXZ;
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
        [SerializeField] private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.AmplifierSharedProfile>();
        public string SharedSettingsGroup => "treatment";
        private Massive.Settings.AmplifierSharedProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.AmplifierSharedProfile>(this, useSharedSettings);
        public AmplifierTreatmentSettings Settings => SharedProfile != null ? SharedProfile.treatment : treatment;
        private void ReplaceSettings(AmplifierTreatmentSettings value)
        {
            if (SharedProfile != null) SharedProfile.treatment = value;
            else treatment = value;
        }
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
        public void ConfigurePresentationGrid(Transform frame, Vector2 size, bool usesXZ)
        {
            presentationGridFrame = frame;
            presentationGridSize = size;
            presentationGridUsesXZ = usesXZ;
        }

        private Transform GridFrame => grid != null ? grid.transform : presentationGridFrame;
        private Vector2 GridSize => grid != null ? grid.size : presentationGridSize;
        private Matrix4x4 GridLocalToWorld => GridFrame == null ? Matrix4x4.identity :
            GridFrame.localToWorldMatrix * (grid == null && presentationGridUsesXZ
                ? Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)) : Matrix4x4.identity);
        public void ApplyPreset(int index) { preset = (index + 4) % 4; ReplaceSettings(AmplifierTreatmentSettings.Preset(preset)); }
        public void RestoreSettings(string json) { ReplaceSettings(JsonUtility.FromJson<AmplifierTreatmentSettings>(json)); }
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
            float age = clock - captures[team - 1] - Settings.absorptionSeconds;
            return Settings.captureBurst && age >= 0 ? Settings.burstIntensity * Mathf.Sin(Mathf.PI * Mathf.Clamp01(age / Mathf.Max(0.1f, Settings.burstDuration))) : 0;
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
                float energy=(Settings.heldCharge?Settings.heldIntensity*level:0)+burst;
                var r=surface.TargetRenderer; var block=surfaceBlocks[team-1] ?? (surfaceBlocks[team-1]=new MaterialPropertyBlock());
                r.GetPropertyBlock(block);
                Vector3 origin=goal.CapturePoint.position;
                WriteGridProperties(block);
                if(GridFrame!=null)
                {
                    Matrix4x4 gridToWorld = GridLocalToWorld;
                    block.SetVector("_GridSize",GridSize);
                    block.SetMatrix("_AmpGridWorldToLocal",gridToWorld.inverse);
                    block.SetMatrix("_AmpGridLocalToWorld",gridToWorld);
                }
                block.SetFloat("_AmpGoalEnabled",1);
                block.SetFloat("_AmpGoalTime",clock);
                float gridEdge=GridFrame!=null?GridSize.x*0.5f*(team==1?-1:1):0;
                block.SetVector("_AmpGridEffectGate",new Vector4(Settings.allowEffectsOverGrid?1:0,gridEdge,team==1?1:-1,0));
                float seamAge=clock-captures[team-1]-Settings.absorptionSeconds;
                float seamBurst=Settings.seamCapture && seamAge>=0 ? Settings.seamCaptureIntensity*Mathf.Sin(Mathf.PI*Mathf.Clamp01(seamAge/Mathf.Max(0.1f,Settings.burstDuration))) : 0;
                float seamEnergy=Settings.seamMode==AmplifierSeamMode.Off?0:seamBurst+(Settings.seamHeld?Settings.seamHeldIntensity*level:0);
                block.SetVector("_AmpSeam",new Vector4((int)Settings.seamMode,seamEnergy,Settings.seamWidth,Settings.seamReach));
                block.SetVector("_AmpSeamModes",new Vector4(Settings.seamModeCount,Settings.seamModeMix,Settings.seamBeatFrequency,Settings.seamDrift));
                block.SetFloat("_AmpSeamSplit",Settings.seamSpectralSplit);
                block.SetVector("_AmpInterferenceShimmer",Settings.interferenceShimmer.ShaderValues);
                block.SetVector("_AmpGoalOriginWS",new Vector4(origin.x,origin.y,origin.z,Settings.heldCharge?Settings.goalHeldMotion*level:0));
                block.SetVector("_AmpGoalPulse",new Vector4(clock-captures[team-1]-Settings.absorptionSeconds,Settings.wholeGoalPulse?Settings.goalPulseAmplitude:0,Settings.envelopeWidth,Settings.propagationSpeed));
                block.SetVector("_AmpGoalShape",new Vector4(Settings.wavelength,Settings.carrierFrequency,Settings.dispersion,Settings.alongAcrossRatio));
                block.SetVector("_AmpGoalTrain",new Vector4(Mathf.Clamp(Settings.emissions,1,5),Settings.packetSpacing,Settings.quadraturePhase*Mathf.Deg2Rad,0));
                block.SetVector("_AmpGoalCorona",new Vector4(Settings.radialExtent,(Settings.corona||Settings.branching)?energy*Settings.intensity:0,Settings.density,Settings.nodalClustering));
                block.SetVector("_AmpGoalStyle",new Vector4(Settings.grainSize,Settings.falloff,Settings.chromaticSeparation,Settings.tangleDensity));
                block.SetVector("_AmpCloud",new Vector4(Settings.corona&&Settings.particleCloud?Settings.cloudIntensity:0,Settings.cloudExtent,Settings.cloudDensity,0));
                block.SetVector("_AmpFlare",new Vector4(Settings.corona&&Settings.flareCorona?Settings.flareIntensity:0,Settings.flareExtent,Settings.corona&&Settings.atmosphereWisps?Settings.atmosphereIntensity:0,Settings.atmosphereExtent));
                block.SetVector("_AmpCloudShimmer",Settings.cloudShimmer.ShaderValues);
                block.SetVector("_AmpFlareShimmer",Settings.flareShimmer.ShaderValues);
                block.SetVector("_AmpWispShimmer",Settings.wispShimmer.ShaderValues);
                block.SetColor("_AmpPaletteA",Settings.coronaColorA);block.SetColor("_AmpPaletteB",Settings.coronaColorB);block.SetColor("_AmpPaletteC",Settings.coronaColorC);
                block.SetVector("_AmpGoalWaveA",Settings.waveA); block.SetVector("_AmpGoalWaveB",Settings.waveB); block.SetVector("_AmpGoalWaveC",Settings.waveC);
                int poles=Mathf.Clamp(Settings.poleCount+(Settings.addPolesWithMultiplier?level*2:0),2,14);
                block.SetVector("_AmpGoalPoles",new Vector4(Settings.polar?poles:0,Settings.poleSeparation*2/poles*Mathf.Deg2Rad,Settings.polePhase*Mathf.Deg2Rad,Settings.poleWidth));
                float reach=Settings.solarWispReach+(Settings.branching?Settings.branchReach*(1+level*Settings.multiplierReach):0);
                block.SetVector("_AmpGoalBranch",new Vector4(reach,Settings.branching?Settings.branchDensity*(1+level*Settings.multiplierDensity):0,Settings.branchLifetime*(1+level*Settings.multiplierPersistence),Settings.reconnect));
                block.SetVector("_AmpGoalMode",new Vector4(Settings.particleCloud?1:0,Settings.alternatePoles?1:0,Settings.corona?1:0,Settings.branching?1:0));
                r.SetPropertyBlock(block);
            }
            UpdatePreviewCore();
        }
        public Vector2 BoundaryOffset(float y, int team, int level, out float strength)
        {
            Vector2 offset = Vector2.zero; strength = 0;
            float age = clock - captures[team - 1] - Settings.absorptionSeconds;
            if (Settings.capturePackets)
                for (int i = 0; i < Mathf.Clamp(Settings.emissions, 1, 5); i++)
                    offset += PacketPair(y, age - i * Settings.packetSpacing, Settings.carrierAmplitude, team, ref strength);
            if (Settings.heldCharge && level > 0)
            {
                if (Settings.travellingWave)
                    offset.x += Settings.heldAmplitude * level * Mathf.Sin(y * 2 * Mathf.PI / Mathf.Max(0.25f, Settings.wavelength) - clock * Settings.carrierFrequency * 2 * Mathf.PI);
                if (Settings.occasionalPackets)
                {
                    float period = Mathf.Max(0.5f, Settings.heldPacketInterval);
                    float recent = Mathf.Repeat(clock, period);
                    for (int i = 0; i < 3; i++) offset += PacketPair(y, recent + i * period, Settings.heldAmplitude * level * 2, team, ref strength);
                }
            }
            float edge = grid != null ? Mathf.SmoothStep(0, 1, (grid.size.y * 0.5f - Mathf.Abs(y)) / 0.5f) : 1;
            strength *= edge; return offset * edge * Settings.gridCoupling;
        }
        private Vector2 PacketPair(float y, float age, float amplitude, int team, ref float strength)
        {
            if (age < 0 || age > 8) return Vector2.zero;
            float w = Mathf.Max(0.1f, Settings.envelopeWidth);
            float width = Mathf.Sqrt(w * w + Mathf.Pow(Settings.dispersion * age, 2));
            float gain = Mathf.Sqrt(w / width) * Mathf.SmoothStep(0, 1, age / 0.12f) * Mathf.Exp(-age * 0.65f);
            Vector2 result = Vector2.zero;
            for (int d = -1; d <= 1; d += 2)
            {
                float s = y * d - Settings.propagationSpeed * age;
                float envelope = Mathf.Exp(-s * s / (2 * width * width)) * gain;
                float phase = s * 2 * Mathf.PI / Mathf.Max(0.25f, Settings.wavelength) - age * Settings.carrierFrequency * 2 * Mathf.PI + Settings.dispersion * age * s * s / (width * width);
                result.x += Mathf.Cos(phase) * amplitude * envelope * (team == 1 ? 1 : -1);
                result.y += Mathf.Cos(phase - Settings.quadraturePhase * Mathf.Deg2Rad) * amplitude * envelope * Settings.alongAcrossRatio * d;
                strength += envelope * amplitude;
            }
            return result;
        }
        public void WriteGridProperties(MaterialPropertyBlock block)
        {
            Matrix4x4 worldToGrid = GridLocalToWorld.inverse;
            Vector2 left = Vector2.zero, right = Vector2.zero;
            for (int team = 1; team <= 2; team++)
            {
                var goal = Goal(team);
                Vector3 p = goal != null && GridFrame != null ? worldToGrid.MultiplyPoint3x4(goal.CapturePoint.position) : Vector3.zero;
                if (GridFrame != null) p.x = (team == 1 ? -1 : 1) * GridSize.x * 0.5f;
                if (team == 1) left = new Vector2(p.x, p.y); else right = new Vector2(p.x, p.y);
            }
            WriteGridProperties(block, left, right);
        }

        /// <summary>Same capture ages, held levels and treatment parameters, in a
        /// renderer-owned coordinate chart. No duplicate capture listeners or timers.</summary>
        public void WriteGridProperties(MaterialPropertyBlock block, Vector2 leftOrigin, Vector2 rightOrigin)
        {
            for (int team = 1; team <= 2; team++)
            {
                origins[team - 1] = GetGridOrigin(team, team == 1 ? leftOrigin : rightOrigin);
                states[team - 1] = new Vector4(Settings.capturePackets ? Settings.carrierAmplitude : 0, Settings.heldCharge && Settings.travellingWave ? Settings.heldAmplitude : 0, Settings.heldCharge && Settings.occasionalPackets ? Settings.heldAmplitude * 2 : 0, Settings.gridDischarge && Settings.allowEffectsOverGrid ? Settings.gridAmplitude : 0);
            }
            for (int i=0;i<states.Length;i++) states[i] *= Settings.gridCoupling;
            block.SetVectorArray("_AmpOrigins", origins); block.SetVectorArray("_AmpStates", states);
            block.SetVector("_AmpPacket", new Vector4(Settings.wavelength, Settings.envelopeWidth, Settings.propagationSpeed, Settings.dispersion));
            block.SetVector("_AmpPhase", new Vector4(Settings.carrierFrequency, Settings.alongAcrossRatio, Settings.quadraturePhase * Mathf.Deg2Rad, clock));
            block.SetVector("_AmpTiming", new Vector4(Settings.emissions, Settings.packetSpacing, Settings.heldPacketInterval, isActiveAndEnabled ? 1 : 0));
            block.SetVector("_AmpField", new Vector4(Settings.gridSpeed, Settings.gridWidth, Settings.gridWavelength, Settings.gridCurl));
            block.SetFloat("_AmpHeldTint", Settings.heldCharge && Settings.coloredBoundary ? Settings.heldIntensity : 0);
        }

        public Vector4 GetGridOrigin(int team, Vector2 position)
        {
            team = Mathf.Clamp(team, 1, 2);
            return new Vector4(position.x, position.y, clock - captures[team - 1] - Settings.absorptionSeconds, Level(team));
        }
        private void UpdatePreviewCore()
        {
            if(!preview) { ReleasePreviewCore(); return; }
            float age = clock - captures[previewTeam - 1];
            bool visible = preview && age >= 0 && age < Settings.absorptionSeconds;
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
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, Settings.absorptionSeconds));
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
            GUILayout.Label("COLOR INTERFERENCE / " + AmplifierTreatmentSettings.SeamNames[(int)Settings.seamMode]);
            Settings.seamMode=(AmplifierSeamMode)GUILayout.Toolbar((int)Settings.seamMode,AmplifierTreatmentSettings.SeamNames);
            Settings.seamCapture=GUILayout.Toggle(Settings.seamCapture,"Interference on capture");
            Settings.seamHeld=GUILayout.Toggle(Settings.seamHeld,"Interference while held");
            Settings.wholeGoalPulse = GUILayout.Toggle(Settings.wholeGoalPulse, "Whole goal pulse");
            Settings.allowEffectsOverGrid=GUILayout.Toggle(Settings.allowEffectsOverGrid,"Allow Effects Over Grid");
            Settings.corona = GUILayout.Toggle(Settings.corona, "Corona"); Settings.capturePackets = GUILayout.Toggle(Settings.capturePackets, "Boundary packets");
            Settings.branching = GUILayout.Toggle(Settings.branching, "Branching"); Settings.gridDischarge = GUILayout.Toggle(Settings.gridDischarge, "Grid discharge");
            GUILayout.Label("Full controls: MASSIVE > Amplifier Treatments"); GUILayout.EndArea();
        }
    }
}
