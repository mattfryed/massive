#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Massive.Multiplier;
using Massive.Player;
using Massive.PowerUps;
using Massive.Resonance;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

// Editor-only, opt-in gameplay experiment. Never attached to saved scene objects.
[DefaultExecutionOrder(-10000)]
public sealed class AmplifierDuelTrial : MonoBehaviour
{
    sealed class Driver
    {
        public PlayerControllerScript player;
        public int team;
        public PlayerControlMode originalMode;
        public PlayerPowerUpController power;
        public Vector3 goal, move, lastPosition;
        public float reaction, prediction, aim, nextThink, nextAttack, releaseAt;
        public bool held;
        public bool wallRecovery;
        public int presses, melee, shots;
        public string powerName = "";
        public UnityAction<AttackStage> stageListener;
    }

    readonly List<Driver> drivers = new List<Driver>();
    readonly StringBuilder events = new StringBuilder();
    readonly StringBuilder frames = new StringBuilder("file,time\n");
    readonly HashSet<int> projectiles = new HashSet<int>();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly Collider[] overlaps = new Collider[128];
    readonly bool[] blocked = new bool[39 * 17];
    AmplifierCoreGameplay core;
    Camera captureCamera;
    RenderTexture target;
    Texture2D pixels;
    string folder;
    float began, nextFrame, nextSample, nextMap, successAt = -1, impactStamp;
    double wallBegan;
    int frame, winner, beforeLight, beforeDark;
    bool started, finished, violation;
    bool finishQueued;
    string finishReason;
    int releaseFrame;
    const int NX = 39, NZ = 17;
    const float Step = .68f;

    public static string StartTrial(string outputFolder)
    {
        if (!Application.isPlaying) return "Enter Play Mode first.";
        if (FindAnyObjectByType<AmplifierDuelTrial>() != null) return "A trial is already running.";
        var go = new GameObject("Amplifier duel — temporary player inputs");
        go.hideFlags = HideFlags.HideAndDontSave;
        var trial = go.AddComponent<AmplifierDuelTrial>();
        trial.folder = outputFolder;
        Directory.CreateDirectory(outputFolder);
        SessionState.SetString("AmplifierDuel.Status", "Waiting for players, scoring and core spawn");
        return "Trial armed. Light: 0.10 s reaction, 0.16 s prediction, tighter shot alignment. Dark: 0.20 s reaction, 0.08 s prediction. Same movement and gameplay rules.";
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    static object Field(object owner, string name) { return owner == null ? null : owner.GetType().GetField(name, Private)?.GetValue(owner); }
    static float Number(object owner, string name) { var v = Field(owner, name); return v is float ? (float)v : 0; }
    void Log(string line) { events.AppendLine((Time.time - began).ToString("F3", CultureInfo.InvariantCulture) + " " + line); }

    void Begin()
    {
        core = AmplifierCoreGameplay.ActiveCores.FirstOrDefault(c => !c.IsCaptured && !c.IsPresentationOnly);
        var score = MatchScoreService.Instance;
        var light = PlayerControllerScript.ActivePlayers.FirstOrDefault(p => p.teamID == 1);
        var dark = PlayerControllerScript.ActivePlayers.FirstOrDefault(p => p.teamID == 2);
        if (!core || !light || !dark || score == null || !score.IsScoringOpen) return;
        // Round setup only: the authored core spawn heavily favors the left goal.
        // Both players retain their authored starts; no transforms change after this.
        core.Body.position = new Vector3(0, core.Body.position.y, 0);
        core.transform.position = core.Body.position;
        began = Time.time;
        wallBegan = EditorApplication.timeSinceStartup;
        beforeLight = score.GetTeamAmplifierMultiplier(1);
        beforeDark = score.GetTeamAmplifierMultiplier(2);
        foreach (var player in new[] { light, dark })
        {
            bool better = player.teamID == 1;
            var d = new Driver { player = player, team = player.teamID, originalMode = player.ControlMode,
                power = player.GetComponent<PlayerPowerUpController>(), reaction = better ? .10f : .20f,
                prediction = better ? .16f : .08f, aim = better ? .985f : .95f,
                goal = Flat(FindObjectsByType<AmplifierGoalCapture>(FindObjectsSortMode.None).First(g => g.TeamID == player.teamID).CapturePoint.position) };
            d.stageListener = stage => {
                if (player.attackController.CurrentStageIndex != 0) return;
                d.melee++;
                bool pa = d.power && d.power.HasActive && d.power.ActiveDefinition is ParticleAcceleratorPowerUpDefinition;
                Log("MELEE team=" + player.teamID + " power=" + d.powerName + " inputHeld=" + d.held);
                if (pa) { violation = true; Log("INVALID: new melee while accelerator equipped"); }
            };
            player.attackController.OnStageStarted.AddListener(d.stageListener);
            player.ClearScriptedInput();
            player.SetControlMode(PlayerControlMode.Scripted);
            drivers.Add(d);
        }
        var camGo = new GameObject("Duel recording camera") { hideFlags = HideFlags.HideAndDontSave };
        captureCamera = camGo.AddComponent<Camera>();
        captureCamera.CopyFrom(Camera.main);
        captureCamera.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
        captureCamera.enabled = false;
        target = new RenderTexture(1280, 720, 24) { antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) };
        target.Create(); captureCamera.targetTexture = target;
        pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        impactStamp = Number(core, "_nextAttackImpactTime");
        started = true;
        Log("START Light=more precise; Dark=less precise; input-only control after setup; full button lifecycle; obstacle-aware routing shared by both");
        Log("SETUP core placed at midfield before round; players at authored starts; no gameplay statistics modified");
        Log("SPAWN light=" + light.transform.position + " dark=" + dark.transform.position + " core=" + core.transform.position);
    }

    void Update()
    {
        if (finished || string.IsNullOrEmpty(folder)) return;
        if (finishQueued)
        {
            // Allow a complete subsequent player Update to consume the release,
            // even if Finish was called after this frame's player input update.
            if (Time.frameCount > releaseFrame + 1) CompleteFinish(finishReason, true);
            return;
        }
        if (!started) { Begin(); return; }
        if (violation) { Finish("INVALID INPUT/ABILITY INTERACTION"); return; }
        if (!core || drivers.Any(d => !d.player)) { Finish("Interrupted: object removed"); return; }
        if (Time.time - began > 95 || EditorApplication.timeSinceStartup - wallBegan > 180) { Finish("TIMEOUT"); return; }
        var score = MatchScoreService.Instance;
        if (successAt < 0 && (score.GetTeamAmplifierMultiplier(1) > beforeLight || score.GetTeamAmplifierMultiplier(2) > beforeDark))
        {
            winner = score.GetTeamAmplifierMultiplier(1) > beforeLight ? 1 : 2;
            successAt = Time.time;
            Log("CAPTURE winner=" + winner + " Light=" + score.GetTeamAmplifierMultiplier(1) + " Dark=" + score.GetTeamAmplifierMultiplier(2));
        }
        if (successAt >= 0 && Time.time - successAt > 2.5f) { Finish("SUCCESS team=" + winner); return; }
        if (Time.time >= nextMap) { MapObstacles(); nextMap = Time.time + 1; }
        for (int i = 0; i < drivers.Count; i++) Drive(drivers[i], drivers[1 - i]);
    }

    void Drive(Driver d, Driver opponent)
    {
        bool activePA = d.power && d.power.HasActive && d.power.ActiveDefinition is ParticleAcceleratorPowerUpDefinition;
        string pn = d.power && d.power.HasActive ? d.power.ActiveDefinition.name : "none";
        if (pn != d.powerName) { d.powerName = pn; Log("POWER team=" + d.player.teamID + " " + pn); }
        Vector3 p = Flat(d.player.transform.position), c = Flat(core.transform.position);
        Vector3 toGoal = (d.goal - c).normalized;
        bool wallRecovery = Mathf.Abs(c.z) > 4.8f;
        Vector3 shotDirection = wallRecovery ? new Vector3(Mathf.Sign(toGoal.x), 0, -Mathf.Sign(c.z) * .25f).normalized : toGoal;
        if (wallRecovery != d.wallRecovery) { d.wallRecovery = wallRecovery; Log("WALL_RECOVERY team=" + d.player.teamID + " active=" + wallRecovery); }
        bool wantPress = false;
        if (successAt < 0 && Time.time >= d.nextThink)
        {
            d.nextThink = Time.time + d.reaction;
            Vector3 prediction = Vector3.ClampMagnitude(Flat(core.Body.linearVelocity) * d.prediction, .9f);
            Vector3 behind = c + prediction - shotDirection * (activePA || wallRecovery ? 1.0f : 1.65f);
            behind.x = Mathf.Clamp(behind.x, -12.9f, 12.9f);
            behind.z = Mathf.Clamp(behind.z, -5.3f, 5.3f);
            Vector3 toCore = (c - p).normalized;
            float distance = Vector3.Distance(p, c);
            float alignment = Vector3.Dot(toCore, shotDirection);
            bool linedUp = alignment >= (wallRecovery ? .88f : d.aim);
            // Once aligned, approach through contact; otherwise route around the core.
            Vector3 destination = linedUp && distance < 2.7f ? c + toGoal * .35f : behind;
            Vector3 waypoint = Route(p, destination, c, !linedUp);
            var delta = waypoint - p;
            d.move = delta.normalized * Mathf.Clamp01(delta.magnitude / .35f);
            bool ready = !d.player.attackController.IsAttacking && Time.time >= d.nextAttack && !d.held;
            if (!activePA && linedUp && distance < (d.player.teamID == 1 ? 1.85f : 2.05f) && ready)
            {
                d.move = wallRecovery ? shotDirection : toCore;
                wantPress = true;
                d.releaseAt = Time.time + .12f;
            }
            if (activePA && ready && Vector3.Distance(p, opponent.player.transform.position) < 7 && Number(d.power, "cooldownRemaining") <= 0)
            {
                wantPress = true;
                d.releaseAt = Time.time + .5f;
            }
        }
        bool down = successAt < 0 && (wantPress || (d.held && Time.time < d.releaseAt));
        if (activePA && (down || d.held))
            d.move = Flat(opponent.player.transform.position - d.player.transform.position).normalized;
        var input = PlayerInputFrame.Neutral;
        input.moveInput = successAt < 0 ? new Vector2(d.move.x, d.move.z) : Vector2.zero;
        input.attackDown = down && !d.held;
        input.attackHeld = down;
        input.attackUp = !down && d.held;
        if (input.attackDown) { d.presses++; d.nextAttack = Time.time + (d.player.teamID == 1 ? .85f : 1.05f); Log("PRESS team=" + d.player.teamID + " power=" + pn + " p=" + p + " core=" + c); }
        if (input.attackUp) Log("RELEASE team=" + d.player.teamID);
        d.held = down;
        d.player.SetScriptedInput(input);
    }

    bool IsObstacle(Collider c)
    {
        return c && !c.isTrigger && (c.name.StartsWith("ArenaWall_") || c.GetComponentInParent<ResonanceSegment>() != null);
    }
    static Vector3 Node(int n) { return new Vector3((n % NX - 19) * Step, 0, (n / NX - 8) * Step); }
    static int Index(Vector3 p) { return Mathf.Clamp(Mathf.RoundToInt(p.x / Step) + 19, 0, NX - 1) + NX * Mathf.Clamp(Mathf.RoundToInt(p.z / Step) + 8, 0, NZ - 1); }
    void MapObstacles()
    {
        for (int n = 0; n < blocked.Length; n++)
        {
            int count = Physics.OverlapSphereNonAlloc(Node(n), .53f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            blocked[n] = false;
            for (int i = 0; i < count; i++) if (IsObstacle(overlaps[i])) { blocked[n] = true; break; }
        }
    }
    bool ClearSegment(Vector3 a, Vector3 b, Vector3 c, bool avoidCore)
    {
        Vector3 delta = b - a;
        if (avoidCore && delta.sqrMagnitude > .01f)
        {
            float u = Mathf.Clamp01(Vector3.Dot(c - a, delta) / delta.sqrMagnitude);
            if (Vector3.Distance(a + u * delta, c) < 1.25f) return false;
        }
        foreach (var hit in Physics.SphereCastAll(a, .5f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (IsObstacle(hit.collider)) return false;
        return true;
    }
    Vector3 Route(Vector3 start, Vector3 end, Vector3 c, bool avoidCore)
    {
        if (ClearSegment(start, end, c, avoidCore)) return end;
        int first = Index(start), last = Index(end);
        var cost = new float[blocked.Length]; var prev = new int[blocked.Length]; var closed = new bool[blocked.Length];
        for (int i = 0; i < cost.Length; i++) { cost[i] = float.PositiveInfinity; prev[i] = -1; }
        var open = new List<int> { first }; cost[first] = 0;
        int best = first; float nearest = Vector3.Distance(Node(first), end);
        for (int iterations = 0; open.Count > 0 && iterations < blocked.Length; iterations++)
        {
            int choice = 0; float rating = float.PositiveInfinity;
            for (int j = 0; j < open.Count; j++) { int n = open[j]; float f = cost[n] + Vector3.Distance(Node(n), end); if (f < rating) { rating = f; choice = j; } }
            int current = open[choice]; open.RemoveAt(choice);
            if (closed[current]) continue;
            closed[current] = true;
            float near = Vector3.Distance(Node(current), end);
            if (near < nearest) { best = current; nearest = near; }
            if (current == last) { best = current; break; }
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = current % NX + dx, z = current / NX + dz;
                if (x < 0 || x >= NX || z < 0 || z >= NZ) continue;
                int n = x + z * NX;
                if (closed[n] || blocked[n] || (avoidCore && Vector3.Distance(Node(n), c) < 1.25f)) continue;
                if (dx != 0 && dz != 0 && (blocked[current + dx] || blocked[current + dz * NX])) continue;
                float nc = cost[current] + (dx != 0 && dz != 0 ? Step * 1.4142f : Step);
                if (nc < cost[n]) { cost[n] = nc; prev[n] = current; open.Add(n); }
            }
        }
        if (best == first) return start;
        var route = new List<int>();
        for (int n = best; n != first && n >= 0; n = prev[n]) route.Add(n);
        for (int i = 0; i < route.Count; i++) if (ClearSegment(start, Node(route[i]), c, avoidCore)) return Node(route[i]);
        return Node(route[route.Count - 1]);
    }

    void LateUpdate()
    {
        if (!started || finished) return;
        foreach (var shot in FindObjectsByType<ParticleAcceleratorProjectile>(FindObjectsSortMode.None))
        {
            if (!projectiles.Add(shot.GetInstanceID())) continue;
            var shooter = Field(shot, "_shooter") as PlayerControllerScript;
            var d = drivers.FirstOrDefault(v => v.player == shooter);
            if (d != null) { d.shots++; Log("ACCELERATOR_SHOT team=" + d.player.teamID + " charge=" + Number(shot, "_charge01")); }
        }
        float impact = Number(core, "_nextAttackImpactTime");
        if (impact > impactStamp) { impactStamp = impact; Log("CORE_ATTACK_IMPULSE position=" + core.transform.position); }
        if (Time.time >= nextSample)
        {
            nextSample = Time.time + .25f;
            string state = "core=" + core.transform.position + " velocity=" + core.Body.linearVelocity;
            foreach (var d in drivers) state += " team" + d.player.teamID + "=" + d.player.transform.position + " presses=" + d.presses + " melee=" + d.melee + " shots=" + d.shots;
            Log(state);
            SessionState.SetString("AmplifierDuel.Status", "Running " + (Time.time - began).ToString("F1") + "s " + state);
        }
        if (Time.time < nextFrame) return;
        nextFrame = Time.time + 1f / 20f;
        captureCamera.Render();
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            string name = frame.ToString("D5") + ".png";
            File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG());
            frames.AppendLine(name + "," + (Time.time - began).ToString("F6", CultureInfo.InvariantCulture)); frame++;
        }
        finally { RenderTexture.active = previous; }
    }

    public void Finish(string reason)
    {
        if (finished || finishQueued) return;
        if (Application.isPlaying && started && isActiveAndEnabled)
        {
            finishQueued = true;
            finishReason = reason;
            releaseFrame = Time.frameCount;
            foreach (var d in drivers)
            {
                if (!d.player) continue;
                var input = PlayerInputFrame.Neutral;
                input.attackUp = d.held;
                d.held = false;
                d.player.SetScriptedInput(input);
            }
            return;
        }
        CompleteFinish(reason, true);
    }

    void CompleteFinish(string reason, bool destroyObject)
    {
        if (finished) return;
        finished = true;
        foreach (var d in drivers)
        {
            if (!d.player) continue;
            if (d.player.attackController) d.player.attackController.OnStageStarted.RemoveListener(d.stageListener);
            d.player.ClearScriptedInput(); d.player.SetControlMode(d.originalMode);
        }
        Application.runInBackground = SessionState.GetBool("AmplifierDuel.Background", false);
        if (!string.IsNullOrEmpty(folder))
        {
            string counts = string.Join("; ", drivers.Select(d => "team=" + d.team + " presses=" + d.presses + " melee=" + d.melee + " acceleratorShots=" + d.shots));
            File.WriteAllText(Path.Combine(folder, "run.txt"), reason + "\n" + counts + "\n" + events);
            File.WriteAllText(Path.Combine(folder, "frames.csv"), frames.ToString());
            SessionState.SetString("AmplifierDuel.Status", reason + "; " + counts + "; frames=" + frame + "; folder=" + folder);
        }
        if (captureCamera) DestroyImmediate(captureCamera.gameObject);
        if (target) { target.Release(); DestroyImmediate(target); }
        if (pixels) DestroyImmediate(pixels);
        if (destroyObject) Destroy(this.gameObject);
    }
    void OnDestroy()
    {
        // Play Mode teardown cannot wait for another Update or destroy this object again.
        if (!finished) CompleteFinish("Interrupted", false);
    }
}

#endif
