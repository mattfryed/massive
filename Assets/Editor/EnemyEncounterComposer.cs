#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class EnemyEncounterComposer : EditorWindow
{
    private const float LibraryWidth = 200, DetailWidth = 310, LabelWidth = 120, RulerHeight = 28;
    [SerializeField] private EnemyEncounterTimeline timeline;
    [SerializeField] private EnemyEncounterLab lab;
    [SerializeField] private EnemyDirector sceneDirector;
    [SerializeField] private EnemyArenaLayout layout;
    [SerializeField] private int selected = -1, seed = 1, typeFilter;
    [SerializeField] private float pixelsPerSecond = 22, cursor, snap = .25f;
    [SerializeField] private Vector2 scroll, libraryScroll, detailScroll;
    [SerializeField] private bool follow = true;
    [SerializeField] private bool previewTwoVTwo;
    private bool TwoVTwo => Live ? Director.TimelineIsTwoVTwo : previewTwoVTwo;
    private EnemyEncounterScaling.Limits EffectiveLimits => Live && Director.TimelineLimits != null ? Director.TimelineLimits : EnemyEncounterScaling.Limits.Capture(timeline, TwoVTwo);
    private readonly Dictionary<(EnemyFormation, int), List<EnemyFormation.Slot>> extraPlans = new();
    private List<EnemyFormation.Slot> Extras(EnemyFormation form, int index)
    {
        var key = (form, index);
        if (!extraPlans.TryGetValue(key, out var slots))
        {
            var cue = index >= 0 && index < timeline.cues.Count ? timeline.cues[index] : new EnemyEncounterTimeline.Cue { formation = form };
            slots = EnemyEncounterScaling.Reinforcements(timeline, cue, form, TwoVTwo); extraPlans[key] = slots;
        }
        return slots;
    }
    private float LastDelay(EnemyFormation form, int index) => Mathf.Max(EnemyEncounterAuthoring.LastDelay(form), Extras(form, index).Select(s => s.releaseDelay).DefaultIfEmpty(0).Max());
    [SerializeField] private string search = "";
    [SerializeField] private EnemyFormation librarySelection;
    private readonly List<EnemyFormation> library = new();
    private readonly List<EnemyDefinition> types = new();
    private List<EnemyEncounterAuthoring.Issue> issues = new();
    private bool validationDirty = true, pendingFit;
    private int dragging = -1, dragControl;
    private EnemyFormation pendingLibraryDrag;
    private Vector2 libraryDragStart;
    private float dragStartX, dragStartTime, dragTime;
    private double nextRepaint;
    private string notice;
    private GUIStyle small, trackLabel;
    private float displayTimeScale = 1f;
    private float DisplayDuration => Mathf.Max(1f, timeline.duration) * displayTimeScale;
    internal EnemyDirector Director => lab ? lab.director : sceneDirector;
    private bool Live => Application.isPlaying && Director && Director.encounterTimeline == timeline;
    private float Playhead
    {
        get
        {
            if (!Live) return cursor;
            var state = Director.previewCue >= 0 ? Director.CueStates.FirstOrDefault(c => c.index == Director.previewCue) : null;
            return Director.GameplayAge + (state != null && state.index < timeline.cues.Count
                ? timeline.cues[state.index].arrivalSeconds * displayTimeScale - state.arrival : 0);
        }
    }
    private bool CanEdit => timeline && !EditorApplication.isPlayingOrWillChangePlaymode;
    private static Color Panel => EditorGUIUtility.isProSkin ? new Color(.19f, .19f, .19f) : new Color(.86f, .86f, .86f);
    private static Color Grid => EditorGUIUtility.isProSkin ? new Color(.27f, .27f, .27f) : new Color(.73f, .73f, .73f);
    private static readonly Color ArrivalColor = new(.37f, .73f, 1f);

    [MenuItem("MASSIVE/Enemies/Encounter Composer")]
    public static void Open() => Show(Selection.activeObject as EnemyEncounterTimeline);
    public static EnemyEncounterComposer Show(EnemyEncounterTimeline asset, EnemyEncounterLab preview = null)
    {
        bool existing = Resources.FindObjectsOfTypeAll<EnemyEncounterComposer>().Length > 0;
        var window = GetWindow<EnemyEncounterComposer>("Encounter Composer");
        window.minSize = new Vector2(1000, 580);
        if (!existing)
        {
            var host = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(host.center.x - 730, host.center.y - 420, 1460, 840);
        }
        window.FindContext();
        if (preview) window.BindDirector(preview.director);
        if (asset) window.SetTimeline(asset);
        else if (!window.timeline) window.FindContext();
        window.Show(); window.Focus(); return window;
    }
    public static EnemyEncounterComposer ShowForDirector(EnemyDirector director)
    {
        var window = Show(director ? director.encounterTimeline : null);
        window.BindDirector(director); return window;
    }
    private void BindDirector(EnemyDirector director)
    {
        sceneDirector = director;
        lab = director ? director.GetComponent<EnemyEncounterLab>() : null;
        layout = director ? director.arenaLayout : null;
        if (director) seed = director.encounterSeed;
        validationDirty = true;
    }
    [OnOpenAsset]
    private static bool OnOpenAsset(int instanceId, int line)
    {
        if (EditorUtility.InstanceIDToObject(instanceId) is not EnemyEncounterTimeline value) return false;
        Show(value); return true;
    }
    private void OnEnable()
    {
        wantsMouseMove = true;
        titleContent = new GUIContent("Encounter Composer"); minSize = new Vector2(1000, 580);
        Undo.undoRedoPerformed += UndoChanged; EditorApplication.projectChanged += ProjectChanged;
        EditorApplication.hierarchyChanged += HierarchyChanged; EditorApplication.update += EditorTick;
        EditorApplication.playModeStateChanged += PlayState;
        RefreshLibrary(); FindContext();
    }
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= UndoChanged; EditorApplication.projectChanged -= ProjectChanged;
        EditorApplication.hierarchyChanged -= HierarchyChanged; EditorApplication.update -= EditorTick;
        EditorApplication.playModeStateChanged -= PlayState; CancelDrag();
        if (timeline && !Application.isPlaying) AssetDatabase.SaveAssetIfDirty(timeline);
    }
    private void OnLostFocus() { CancelDrag(); Repaint(); }
    private void CancelDrag() { CancelArenaDrag(); if (dragging >= 0 && GUIUtility.hotControl == dragControl) GUIUtility.hotControl = 0; dragging = -1; pendingLibraryDrag = null; }
    private void UndoChanged() { CancelDrag(); validationDirty = true; Repaint(); }
    private void ProjectChanged() { RefreshLibrary(); validationDirty = true; Repaint(); }
    private void HierarchyChanged() { if (!Director) FindContext(); validationDirty = true; }
    private void PlayState(PlayModeStateChange state) { CancelDrag(); FindContext(); validationDirty = true; Repaint(); }
    private void EditorTick()
    {
        if (EditorApplication.timeSinceStartup < nextRepaint) return;
        nextRepaint = EditorApplication.timeSinceStartup + .1;
        if (Application.isPlaying) Repaint();
    }
    private void FindContext()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!Director || Director.gameObject.scene != scene)
        {
            var preview = Object.FindObjectsByType<EnemyEncounterLab>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(p => p.gameObject.scene == scene);
            BindDirector(preview ? preview.director : Object.FindObjectsByType<EnemyDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(d => d.gameObject.scene == scene && d.encounterTimeline));
        }
        if (!layout && Director) layout = Director.arenaLayout;
        if (!timeline)
        {
            timeline = Selection.activeObject as EnemyEncounterTimeline;
            if (!timeline && Director) timeline = Director.encounterTimeline;
            if (!timeline) timeline = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString("MASSIVE.EncounterComposer.Timeline", "")));
        }
        if (Director) seed = Director.encounterSeed;
        validationDirty = true;
    }
    private void SetTimeline(EnemyEncounterTimeline value)
    {
        if (timeline == value) return;
        if (timeline && !Application.isPlaying) AssetDatabase.SaveAssetIfDirty(timeline);
        CancelDrag(); timeline = value; selected = -1; scroll = Vector2.zero; validationDirty = true;
        if (timeline) EditorPrefs.SetString("MASSIVE.EncounterComposer.Timeline", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(timeline)));
    }
    private void RefreshLibrary()
    {
        library.Clear(); library.AddRange(AssetDatabase.FindAssets("t:EnemyFormation").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<EnemyFormation>).Where(f => f).OrderBy(f => f.name));
        types.Clear(); types.AddRange(library.Select(EnemyEncounterAuthoring.Type).Where(t => t).Distinct()
            .OrderBy(t => EnemyEncounterAuthoring.Name(t)));
        typeFilter = Mathf.Clamp(typeFilter, 0, types.Count);
    }
    private void Changed(bool save = true)
    {
        validationDirty = true; if (save && timeline) AssetDatabase.SaveAssetIfDirty(timeline); Repaint(); SceneView.RepaintAll();
    }
    private void OnGUI()
    {
        small ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        trackLabel ??= new GUIStyle(EditorStyles.label) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
        DrawHeader();
        if (!timeline) { EditorGUILayout.HelpBox("Choose an Encounter Timeline asset, or create one to start planning.", MessageType.Info); return; }
        float timing = Live && Director.CueStates.Count > 0 ? Director.TimelineTimeScale : Director ? Director.PreviewTimelineTimeScale(timeline) : 1f;
        if (!Mathf.Approximately(timing, displayTimeScale)) { CancelDrag(); displayTimeScale = timing; validationDirty = true; pendingFit = true; }
        selected = Mathf.Clamp(selected, -1, timeline.cues.Count - 1);
        HandleLibraryDrag();
        HandleKeys();
        var libraryRect = new Rect(0, 58, LibraryWidth, position.height - 82);
        var detailRect = new Rect(position.width - DetailWidth, 58, DetailWidth, position.height - 82);
        var centerRect = new Rect(LibraryWidth + 1, 58, position.width - LibraryWidth - DetailWidth - 2, position.height - 82);
        DrawLibrary(libraryRect); DrawTimeline(centerRect); DrawDetails(detailRect);
        if (validationDirty && Event.current.type == EventType.Layout)
        {
            if (layout && layout.arena) layout.arena.RefreshNow(false);
            issues = EnemyEncounterAuthoring.Validate(timeline, layout, Director, seed, TwoVTwo); validationDirty = false;
        }
        string status = notice ?? (Application.isPlaying ? "Play Mode: authoring locked. Live status uses the Director's gameplay clock." : "Drag formations onto their tracks. Drag blocks to move; double-click a track to add the selected formation. Ctrl+D duplicate · Delete remove · Ctrl+Z Undo.");
        GUI.Label(new Rect(8, position.height - 22, position.width - 16, 20), status, EditorStyles.miniLabel);
    }
    private void DrawHeader()
    {
        extraPlans.Clear();
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        var asset = (EnemyEncounterTimeline)EditorGUILayout.ObjectField(timeline, typeof(EnemyEncounterTimeline), false, GUILayout.MinWidth(200));
        if (asset != timeline) SetTimeline(asset);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(42)))
            {
                string path = EditorUtility.SaveFilePanelInProject("New encounter timeline", "Encounter Timeline", "asset", "Choose a timeline asset location.");
                if (!string.IsNullOrEmpty(path)) { var created = CreateInstance<EnemyEncounterTimeline>(); AssetDatabase.CreateAsset(created, path); SetTimeline(created); }
            }
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(42)) && timeline) AssetDatabase.SaveAssetIfDirty(timeline);
        }
        GUILayout.Space(12); GUILayout.Label("Director", GUILayout.Width(48));
        var target = (EnemyDirector)EditorGUILayout.ObjectField(Director, typeof(EnemyDirector), true, GUILayout.Width(190));
        if (target != Director) BindDirector(target);
        using (new EditorGUI.DisabledScope(!Director || !timeline))
        {
            if (GUILayout.Button("Play all", EditorStyles.toolbarButton, GUILayout.Width(60))) StartPreview(-1);
            using (new EditorGUI.DisabledScope(selected < 0)) if (GUILayout.Button("Play cue", EditorStyles.toolbarButton, GUILayout.Width(64))) StartPreview(selected);
        }
        using (new EditorGUI.DisabledScope(!Live))
        {
            if (GUILayout.Button(Live && Director.timelinePaused ? "Resume" : "Pause", EditorStyles.toolbarButton, GUILayout.Width(58))) Director.timelinePaused = !Director.timelinePaused;
            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(58))) RestartPlayback();
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            int mode = EditorGUILayout.Popup(TwoVTwo ? 1 : 0, new[] { "1v1 preview", "2v2 preview" }, EditorStyles.toolbarPopup, GUILayout.Width(105));
            if ((mode == 1) != previewTwoVTwo && !Application.isPlaying) { previewTwoVTwo = mode == 1; validationDirty = true; CancelDrag(); }
        }
        GUILayout.Label("Snap", GUILayout.Width(34)); int snapIndex = snap == 0 ? 0 : Mathf.Approximately(snap, .25f) ? 1 : 2;
        snapIndex = EditorGUILayout.Popup(snapIndex, new[] { "Off", "0.25s", "1s" }, EditorStyles.toolbarPopup, GUILayout.Width(60)); snap = new[] { 0f, .25f, 1f }[snapIndex];
        GUILayout.Space(12); GUILayout.Label("Zoom", GUILayout.Width(36)); pixelsPerSecond = GUILayout.HorizontalSlider(pixelsPerSecond, .05f, 100, GUILayout.Width(110));
        if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(35))) pendingFit = true;
        GUILayout.Label("Insert at", GUILayout.Width(52)); cursor = Mathf.Max(0, EditorGUILayout.FloatField(cursor, GUILayout.Width(54))); GUILayout.Label("s", GUILayout.Width(12));
        GUILayout.FlexibleSpace();
        GUILayout.Label("Seed", GUILayout.Width(32));
        int value;
        using (new EditorGUI.DisabledScope(Application.isPlaying)) value = EditorGUILayout.IntField(seed, GUILayout.Width(58));
        if (value != seed) { seed = value; validationDirty = true; }
        follow = GUILayout.Toggle(follow, "Follow playhead", EditorStyles.toolbarButton, GUILayout.Width(108));
        if (Live) GUILayout.Label($"{Director.GameplayAge:0.0}s · Alive {Director.AliveCount} · Reserved {Director.TimelinePendingCount} · Pressure {Director.CurrentPressure:0}/{EffectiveLimits.pressure:0}", GUILayout.Width(350));
        GUILayout.EndHorizontal();
    }
    internal void StartPreview(int cue)
    {
        if (!Director || !timeline) return;
        var owner = lab ? lab.GetComponentInParent<EnemyLab>() : null;
        if (!Application.isPlaying)
        {
            Undo.RecordObject(Director, "Preview encounter timeline");
            if (owner) { Undo.RecordObject(owner, "Preview encounter timeline"); owner.mode = EnemyLab.LabMode.EncounterTimeline; EditorUtility.SetDirty(owner); }
            Director.encounterTimeline = timeline; Director.previewCue = cue; Director.encounterSeed = seed;
            EditorUtility.SetDirty(Director); PrefabUtility.RecordPrefabInstancePropertyModifications(Director);
            AssetDatabase.SaveAssetIfDirty(timeline);
            EnemyEncounterPreviewMode.Request(previewTwoVTwo);
            EditorApplication.isPlaying = true;
        }
        else
        {
            if (owner) owner.mode = EnemyLab.LabMode.EncounterTimeline;
            Director.encounterTimeline = timeline; Director.previewCue = cue; Director.encounterSeed = seed;
            Director.timelinePaused = false;
            if (!lab || lab.isActiveAndEnabled) RestartPlayback();
        }
        notice = null;
    }
    internal void RestartPlayback()
    {
        if (lab) lab.RestartPreview();
        else if (Director) Director.RestartTimeline(clearEnemies: true);
    }
    private void DrawLibrary(Rect rect)
    {
        EditorGUI.DrawRect(rect, Panel); GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16));
        GUILayout.Label("FORMATIONS", EditorStyles.boldLabel);
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
        var names = new[] { "All enemy types" }.Concat(types.Select(EnemyEncounterAuthoring.Name)).ToArray();
        typeFilter = EditorGUILayout.Popup(typeFilter, names);
        libraryScroll = GUILayout.BeginScrollView(libraryScroll);
        foreach (var formation in library)
        {
            if (typeFilter > 0 && EnemyEncounterAuthoring.Type(formation) != types[typeFilter - 1]) continue;
            if (!string.IsNullOrEmpty(search) && formation.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var r = GUILayoutUtility.GetRect(10, 54, GUILayout.ExpandWidth(true));
            if (librarySelection == formation) EditorGUI.DrawRect(r, new Color(.25f, .45f, .62f, .3f));
            string label = System.Text.RegularExpressions.Regex.Replace(formation.name, @"^\d+[a-z]?\s+", "");
            foreach (string prefix in new[] { "Ranged Drone ", "Drone ", "Carrier ", "Turret ", "Seeker ", "Dyson " })
                if (label.StartsWith(prefix)) { label = label.Substring(prefix.Length); break; }
            GUI.Label(new Rect(r.x + 5, r.y + 4, r.width - 10, 20), new GUIContent(label, formation.name + "\n" + AssetDatabase.GetAssetPath(formation)), EditorStyles.boldLabel);
            GUI.Label(new Rect(r.x + 5, r.y + 25, r.width - 10, 24), $"{formation.slots.Count} enemies · {EnemyEncounterAuthoring.Name(EnemyEncounterAuthoring.Type(formation))}", small);
            if (r.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                { librarySelection = formation; pendingLibraryDrag = formation; libraryDragStart = GUIUtility.GUIToScreenPoint(Event.current.mousePosition); selected = -1; Event.current.Use(); Repaint(); }
            }
        }
        GUILayout.EndScrollView();
        using (new EditorGUI.DisabledScope(!CanEdit || !librarySelection))
            if (GUILayout.Button("Add at cursor")) Add(librarySelection, cursor);
        GUILayout.Label("Drag a formation onto its enemy track. Mixed formations have their own track.", small);
        GUILayout.EndArea();
    }
    private void HandleLibraryDrag()
    {
        var e = Event.current;
        if (!pendingLibraryDrag) return;
        if (CanEdit && e.type == EventType.MouseDrag && (GUIUtility.GUIToScreenPoint(e.mousePosition) - libraryDragStart).sqrMagnitude > 16)
        {
            var formation = pendingLibraryDrag; pendingLibraryDrag = null;
            DragAndDrop.PrepareStartDrag(); DragAndDrop.objectReferences = new Object[] { formation };
            DragAndDrop.StartDrag(formation.name); e.Use();
        }
        if (e.type == EventType.MouseUp) pendingLibraryDrag = null;
    }
    private void Add(EnemyFormation formation, float at)
    { selected = EnemyEncounterAuthoring.Add(timeline, formation, EnemyEncounterAuthoring.Snap(at, snap) / displayTimeScale, 0); librarySelection = formation; notice = null; Changed(); }
    private void Duplicate()
    { if (!CanEdit || selected < 0) return; selected = EnemyEncounterAuthoring.Duplicate(timeline, selected); Changed(); }
    private void Remove()
    { if (!CanEdit || selected < 0) return; EnemyEncounterAuthoring.Remove(timeline, selected); selected = Mathf.Min(selected, timeline.cues.Count - 1); notice = "Removed cue. Later cue indices changed, so seeded variant choices may change. Undo restores them."; Changed(); }
    private void HandleKeys()
    {
        var e = Event.current; if (!CanEdit || EditorGUIUtility.editingTextField || e.type != EventType.KeyDown) return;
        if (arenaDrag != null) { if (e.keyCode == KeyCode.Escape) { CancelDrag(); Repaint(); e.Use(); } return; }
        if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) { Remove(); e.Use(); }
        if ((e.control || e.command) && e.keyCode == KeyCode.D) { Duplicate(); e.Use(); }
        if ((e.control || e.command) && e.keyCode == KeyCode.S) { AssetDatabase.SaveAssetIfDirty(timeline); e.Use(); }
        if (e.keyCode == KeyCode.Escape) { CancelDrag(); e.Use(); }
    }

    private sealed class Track
    {
        public EnemyDefinition type;
        public readonly List<(int index, int lane)> clips = new();
        public float y, height;
    }
    private List<Track> BuildTracks()
    {
        var rows = types.Select(t => new Track { type = t }).ToList(); var mixed = new Track(); rows.Add(mixed);
        for (int i = 0; i < timeline.cues.Count; i++)
        {
            var type = EnemyEncounterAuthoring.Type(EnemyEncounterAuthoring.Choose(timeline, i, seed, out _));
            var row = rows.Find(t => t.type == type);
            if (row == null) { row = new Track { type = type }; rows.Insert(rows.Count - 1, row); }
            row.clips.Add((i, 0));
        }
        float y = 0;
        foreach (var row in rows)
        {
            var ends = new List<float>(); var ordered = row.clips.OrderBy(c => TimeOf(c.index)).ToArray(); row.clips.Clear();
            foreach (var item in ordered)
            {
                var formation = EnemyEncounterAuthoring.Choose(timeline, item.index, seed, out _);
                float time = TimeOf(item.index), warning = formation ? formation.warningSeconds : 0;
                int lane = ends.FindIndex(end => end + 10 / pixelsPerSecond < time - warning);
                if (lane < 0) { lane = ends.Count; ends.Add(0); }
                // Reserve room for the label too, so adjacent short cues remain readable at any zoom.
                ends[lane] = Mathf.Max(time + LastDelay(formation, item.index), time - warning + 100 / pixelsPerSecond);
                row.clips.Add((item.index, lane));
            }
            row.y = y; row.height = Mathf.Max(64, ends.Count * 48 + 16); y += row.height;
        }
        return rows;
    }
    private float TimeOf(int index) => dragging == index ? dragTime : timeline.cues[index].arrivalSeconds * displayTimeScale;
    private float EndTime => Mathf.Max(DisplayDuration, timeline.cues.Count == 0 ? 0 : timeline.cues.Select((c, i) => TimeOf(i) + LastDelay(EnemyEncounterAuthoring.Choose(timeline, i, seed, out _), i)).Max()) + 5;
    private void DrawTimeline(Rect rect)
    {
        float viewportWidth = rect.width - LabelWidth - 16;
        if (pendingFit) { pixelsPerSecond = Mathf.Clamp((viewportWidth - 24) / EndTime, .05f, 100); scroll.x = 0; pendingFit = false; }
        var rows = BuildTracks(); float height = rows.Sum(t => t.height);
        float contentWidth = Mathf.Max(viewportWidth, EndTime * pixelsPerSecond + 24);
        if (Live && follow && dragging < 0)
        {
            float x = Playhead * pixelsPerSecond;
            if (x < scroll.x || x > scroll.x + viewportWidth - 40) scroll.x = Mathf.Max(0, x - viewportWidth * .25f);
        }
        var ruler = new Rect(rect.x + LabelWidth, rect.y, rect.width - LabelWidth - 16, RulerHeight);
        GUI.BeginGroup(ruler); EditorGUI.DrawRect(new Rect(0, 0, ruler.width, ruler.height), Panel);
        float tick = new[] { 1f, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1200 }.FirstOrDefault(t => t * pixelsPerSecond >= 50);
        if (tick <= 0) tick = 1200;
        for (float t = Mathf.Floor(scroll.x / pixelsPerSecond / tick) * tick; t <= (scroll.x + ruler.width) / pixelsPerSecond; t += tick)
            GUI.Label(new Rect(12 + t * pixelsPerSecond - scroll.x, 4, 55, 20), t + "s", EditorStyles.miniLabel);
        if (Event.current.type == EventType.MouseDown && new Rect(0, 0, ruler.width, ruler.height).Contains(Event.current.mousePosition))
        { cursor = EnemyEncounterAuthoring.Snap((Event.current.mousePosition.x + scroll.x - 12) / pixelsPerSecond, snap); Event.current.Use(); Repaint(); }
        GUI.EndGroup();
        GUI.Label(new Rect(rect.x + 6, rect.y + 5, LabelWidth - 10, 20), "ENEMY TRACKS", EditorStyles.miniBoldLabel);
        var labels = new Rect(rect.x, rect.y + RulerHeight, LabelWidth, rect.height - RulerHeight - 35);
        GUI.BeginGroup(labels);
        foreach (var row in rows)
        {
            var r = new Rect(0, row.y - scroll.y, LabelWidth, row.height); EditorGUI.DrawRect(r, Panel);
            GUI.Label(new Rect(8, r.y + 8, LabelWidth - 16, Mathf.Min(50, row.height - 16)), EnemyEncounterAuthoring.Name(row.type), trackLabel);
            EditorGUI.DrawRect(new Rect(0, r.yMax - 1, LabelWidth, 1), Grid);
        }
        GUI.EndGroup();
        var view = new Rect(rect.x + LabelWidth, rect.y + RulerHeight, rect.width - LabelWidth, rect.height - RulerHeight - 20);
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, contentWidth, Mathf.Max(height, view.height - 16)), true, true);
        for (float t = 0; t < EndTime; t += tick) EditorGUI.DrawRect(new Rect(12 + t * pixelsPerSecond, 0, 1, height), Grid);
        EditorGUI.DrawRect(new Rect(12 + DisplayDuration * pixelsPerSecond, 0, 2, height), new Color(.75f, .55f, .3f));
        foreach (var row in rows)
        {
            var rowRect = new Rect(0, row.y, contentWidth, row.height);
            EditorGUI.DrawRect(new Rect(0, row.y + row.height - 1, contentWidth, 1), Grid);
            foreach (var item in row.clips) DrawClip(item.index, row.y + 10 + item.lane * 48);
            HandleDrop(rowRect, row.type);
            if (CanEdit && Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && rowRect.Contains(Event.current.mousePosition) && librarySelection && EnemyEncounterAuthoring.Type(librarySelection) == row.type)
            { Add(librarySelection, (Event.current.mousePosition.x - 12) / pixelsPerSecond); Event.current.Use(); }
        }
        float playhead = Playhead;
        EditorGUI.DrawRect(new Rect(12 + playhead * pixelsPerSecond, 0, 2, height), ArrivalColor);
        HandleClipDrag(); GUI.EndScrollView();
        GUI.Label(new Rect(rect.x + 8, rect.yMax - 19, rect.width - 16, 18), "Light: warning   •   Blue: spawn sequence   •   Purple: 2v2 extras   •   Tick: first arrival   •   Orange: timeline end", EditorStyles.miniLabel);
    }
    private void DrawClip(int index, float y)
    {
        var cue = timeline.cues[index]; var form = EnemyEncounterAuthoring.Choose(timeline, index, seed, out _);
        float time = TimeOf(index), warning = form ? Mathf.Max(.1f, form.warningSeconds) : 0, last = LastDelay(form, index);
        var r = new Rect(12 + Mathf.Max(0, time - warning) * pixelsPerSecond, y, Mathf.Max(8, (Mathf.Min(time, warning) + last) * pixelsPerSecond), 26);
        bool problem = issues.Any(i => i.cue == index);
        var state = Live ? Director.CueStates.FirstOrDefault(c => c.index == index) : null;
        Color fill = problem ? new Color(.5f, .3f, .16f) : new Color(.29f, .38f, .45f);
        if (state != null && (state.state == "Skipped" || state.state == "Expired" || state.state == "Holding" || state.state == "Blocked")) fill = new Color(.58f, .25f, .22f);
        if (state != null && state.state == "Complete") fill = new Color(.22f, .44f, .32f);
        if (state != null && state.state == "Partial") fill = new Color(.65f, .43f, .15f);
        EditorGUI.DrawRect(r, fill);
        float arrivalX = 12 + time * pixelsPerSecond;
        EditorGUI.DrawRect(new Rect(arrivalX, y, Mathf.Max(3, last * pixelsPerSecond), 26), new Color(.24f, .52f, .73f));
        foreach (var extra in Extras(form, index)) EditorGUI.DrawRect(new Rect(arrivalX + extra.releaseDelay * pixelsPerSecond, y + 18, 3, 8), new Color(.8f, .55f, 1));
        EditorGUI.DrawRect(new Rect(arrivalX, y - 3, 2, 32), ArrivalColor);
        if (selected == index) { Handles.color = ArrivalColor; Handles.DrawWireCube(new Vector3(r.center.x, r.center.y, 0), new Vector3(r.width + 4, r.height + 4, 0)); }
        string text = string.IsNullOrEmpty(cue.label) ? form ? form.name : "Missing formation" : cue.label;
        if (state != null) text += " · " + state.state;
        if (Extras(form, index).Count > 0) text += $" (+{Extras(form, index).Count})";
        var labelRect = new Rect(r.x, y + 27, Mathf.Max(100, r.width), 18);
        GUI.Label(labelRect, new GUIContent(text, $"{text}\nFirst arrival: {time:0.00}s · Last: {time + last:0.00}s\n{state?.Progress}\n{state?.reason}"), EditorStyles.miniLabel);
        var hit = new Rect(r.x, r.y - 3, Mathf.Max(24, r.width), 46);
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && (hit.Contains(Event.current.mousePosition) || labelRect.Contains(Event.current.mousePosition)))
        {
            selected = index; detailScroll = Vector2.zero; notice = null;
            if (CanEdit)
            {
                dragControl = GUIUtility.GetControlID(FocusType.Passive); GUIUtility.hotControl = dragControl;
                dragging = index; dragStartX = Event.current.mousePosition.x; dragStartTime = dragTime = cue.arrivalSeconds * displayTimeScale;
            }
            Event.current.Use(); Repaint();
        }
    }
    private void HandleClipDrag()
    {
        if (dragging < 0) return;
        var e = Event.current;
        if (e.type == EventType.MouseDrag)
        { dragTime = EnemyEncounterAuthoring.Snap(dragStartTime + (e.mousePosition.x - dragStartX) / pixelsPerSecond, e.alt ? 0 : snap); e.Use(); Repaint(); }
        if (e.type == EventType.MouseUp && e.button == 0)
        {
            int index = dragging; float time = dragTime; CancelDrag();
            EnemyEncounterAuthoring.Move(timeline, index, time / displayTimeScale, 0); Changed(); e.Use();
        }
    }
    private void HandleDrop(Rect row, EnemyDefinition type)
    {
        var e = Event.current;
        if (!CanEdit || !row.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;
        var forms = DragAndDrop.objectReferences.OfType<EnemyFormation>().ToArray();
        bool valid = forms.Length > 0 && forms.All(f => EnemyEncounterAuthoring.Type(f) == type);
        DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
        if (valid && e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag(); Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            foreach (var form in forms) Add(form, (e.mousePosition.x - 12) / pixelsPerSecond);
            Undo.CollapseUndoOperations(group);
        }
        e.Use();
    }
}
#endif
