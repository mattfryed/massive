#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Lattice.EditorTools
{
    [InitializeOnLoad]
    public static class LatticeValidation
    {
        const string Key = "MASSIVE.LatticeValidation", Output = "Library/LatticeValidation";
        static readonly List<string> results = new(), errors = new();
        static readonly Stack<IEnumerator> routines = new();
        static readonly List<Object> owned = new();
        static int frame;
        static double deadline;
        static LatticeDisruptionField field;
        static PlayerControllerScript actor;
        static LatticePlayerMotor motor;
        static Rigidbody body;
        static Vector3 origin;
        static LatticeValidation() { EditorApplication.playModeStateChanged += State; }
        [MenuItem("MASSIVE/LATTICE/Validate prototype %#&l")]
        public static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != LatticeSetup.ScenePath)
                throw new InvalidOperationException("Open LATTICE in Edit Mode.");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.txt", "RUNNING\n");
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(Key + ".Background", Application.runInBackground); Application.runInBackground = true;
                results.Clear(); errors.Clear(); owned.Clear(); routines.Clear(); routines.Push(Checks());
                frame = -1; deadline = EditorApplication.timeSinceStartup + 180;
                Application.logMessageReceived += Log; EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            { Application.runInBackground = SessionState.GetBool(Key + ".Background", true); SessionState.SetBool(Key, false); }
        }
        static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.frameCount == frame || routines.Count == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out");
                while (routines.Count > 0)
                {
                    IEnumerator current = routines.Peek();
                    if (!current.MoveNext()) { routines.Pop(); continue; }
                    if (current.Current is IEnumerator nested) { routines.Push(nested); continue; }
                    frame = Time.frameCount; return;
                }
                Finish(null);
            }
            catch (Exception e) { Finish(e); }
        }
        static void Finish(Exception error)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routines.Clear();
            bool passed = error == null && errors.Count == 0;
            File.WriteAllText(Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + error + "\n" + string.Join("\n", errors));
            foreach (Object value in owned) if (value) Object.Destroy(value);
            Time.timeScale = 1; EditorApplication.isPlaying = false;
            Debug.Log("LATTICE validation " + (passed ? "PASSED" : "FAILED: " + error));
        }
        static void Check(bool condition, string label)
        {
            if (!condition) throw new Exception(label);
            results.Add("PASS " + label); File.WriteAllText(Output + "/report.txt", "RUNNING\n" + string.Join("\n", results));
        }
        static IEnumerator Seconds(float seconds)
        { float end = Time.time + seconds; while (Time.time < end) yield return null; }
        static IEnumerator Until(Func<bool> predicate, string label, float timeout = 6)
        { float end = Time.time + timeout; while (!predicate() && Time.time < end) yield return null; Check(predicate(), label); }
        static void Input(Vector2 direction) => actor.SetScriptedInput(new PlayerInputFrame { moveInput = direction });
        static void ResetActor()
        { Input(Vector2.zero); motor.Release(); body.position = origin; body.linearVelocity = Vector3.zero; Physics.SyncTransforms(); }

        static IEnumerator Checks()
        {
            var scene = SceneManager.GetActiveScene();
            var installed = LatticeSetup.InScene<LatticeDisruptionField>(scene).Single();
            var players = LatticeSetup.InScene<PlayerControllerScript>(scene);
            Check(players.Length >= 4 && players.All(p => p.GetComponent<LatticePlayerMotor>()?.field == installed), "All four roster actors bind to the scene field");
            Check(installed.strandShader && installed.strandShader.isSupported && !ShaderUtil.ShaderHasError(installed.strandShader), "LATTICE shader compiles for the current DX11 renderer");
            yield return Until(() => installed.IsReady && installed.EdgeCount > 0, "Saved scene starts with a live connection field");
            Check(installed.Grid.size == new Vector2(28,12), "Existing 28 by 12 arena footprint retained");
            yield return Seconds(1);
            Capture(Camera.main, "lattice-gameplay.png");

            var root = new GameObject("LATTICE temporary validation fixture"); owned.Add(root); root.SetActive(false);
            var gridObject = new GameObject("Fixture grid"); gridObject.transform.SetParent(root.transform);
            gridObject.transform.SetPositionAndRotation(new Vector3(1000,-.09f,1000), Quaternion.Euler(270,0,0));
            var grid = gridObject.AddComponent<VectorGridGPU>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(installed.Grid), grid); grid.registerAsDefault = false;
            field = gridObject.AddComponent<LatticeDisruptionField>(); field.strandShader = installed.strandShader;
            var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), root.transform);
            origin = new Vector3(1000,0,1000); playerObject.transform.position = origin;
            actor = playerObject.GetComponent<PlayerControllerScript>(); actor.ConfigureDemonstration(root.transform,0,1);
            var scale = playerObject.GetComponent<PlayerScaleAdjuster>(); scale.UseGlobalModifiers = false; scale.Size = 1;
            body = playerObject.GetComponent<Rigidbody>(); body.useGravity = false;
            motor = playerObject.AddComponent<LatticePlayerMotor>(); motor.field = field;
            root.SetActive(true);
            yield return Until(() => field.IsReady, "Independent fixture binds the canonical player and existing grid renderer");
            Check(VectorGridGPU.Instance == installed.Grid, "Secondary fixture does not replace the gameplay grid singleton");
            yield return BoundaryChecks(gridObject.transform);
            field.mode = LatticeDisruptionField.FieldMode.Connected; yield return Seconds(1.25f);
            Check(field.DisconnectedEdgeCount == 0 && field.DisruptionAt(origin) < .001f, "Connected control has all threads and no quantized region");
            Input(Vector2.right); yield return Seconds(.24f);
            Check(!motor.IsQuantized && body.position.x > origin.x + .01f, "Canonical player retains continuous motion on connected grid");
            ResetActor(); field.mode = LatticeDisruptionField.FieldMode.Disconnected;
            yield return Seconds(.3f);
            Check(field.TransitioningEdgeCount > 0 && field.DisconnectedEdgeCount < field.EdgeCount, "Disruption retracts through intermediate strand lengths");
            CaptureFixture(gridObject.transform, "lattice-retracting.png");
            yield return Seconds(1);
            Check(field.DisconnectedEdgeCount == field.EdgeCount && field.DisruptionAt(origin) > .999f, "Fully disrupted field removes every connection while retaining nodes");
            yield return Until(() => motor.IsQuantized, "Player enters quantized movement inside disconnected region");
            var still = body.position; yield return Seconds(.25f);
            Check(Vector3.Distance(body.position, still) < .001f, "Neutral input holds exactly at a node");
            CaptureFixture(gridObject.transform, "lattice-disconnected.png");
            var directions = new[] { Vector2.right, new Vector2(1,1).normalized, Vector2.up, new Vector2(-1,1).normalized,
                Vector2.left, new Vector2(-1,-1).normalized, Vector2.down, new Vector2(1,-1).normalized };
            float cardinalInterval = 0, diagonalInterval = 0;
            for (int i = 0; i < directions.Length; i++)
            {
                ResetActor(); yield return Seconds(.08f); int start = motor.CompletedSteps; Input(directions[i]);
                yield return Until(() => motor.CompletedSteps >= start + 1, "Direction " + i + " produces a discrete step");
                Vector3 actual = motor.LastStepTo - motor.LastStepFrom;
                Vector3 expected = new Vector3(directions[i].x,0,directions[i].y).normalized;
                Check(Vector3.Dot(actual.normalized,expected) > .999f, "Direction " + i + " lands on the correct neighboring point");
                Check(Vector3.Distance(body.position, field.NodeWorld(field.NearestNode(body.position),body.position.y)) < .01f, "Direction " + i + " has no fractional drift between points");
                float first = motor.LastStepTime;
                yield return Until(() => motor.CompletedSteps >= start + 2, "Direction " + i + " repeats while held");
                if (i == 0) cardinalInterval = motor.LastStepTime - first;
                if (i == 1) diagonalInterval = motor.LastStepTime - first;
            }
            Check(diagonalInterval >= cardinalInterval * 1.15f, "Diagonal steps pay their longer travel distance instead of moving faster");
            yield return SpeedChecks(root.transform);
            ResetActor(); Input(Vector2.zero); yield return Seconds(.1f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(wall);
            wall.transform.position = origin + new Vector3(.5f,0,0); wall.transform.localScale = new Vector3(.05f,2,2); Physics.SyncTransforms();
            int beforeWall = motor.CompletedSteps; Input(Vector2.right); yield return Seconds(.45f);
            Check(motor.CompletedSteps == beforeWall && body.position.x < origin.x + .2f, "Swept body query prevents hopping through a thin wall");
            wall.SetActive(false); ResetActor(); yield return Seconds(.1f);
            body.position = field.NodeWorld(new Vector2Int(13,0),0); Input(Vector2.right); yield return Seconds(.4f);
            Check(body.position.x <= origin.x + 13.01f, "Body clearance prevents a step beyond the arena edge");
            ResetActor(); yield return Seconds(.1f); actor.SetMatchInputLocked(true); Input(Vector2.right); still = body.position;
            yield return Seconds(.2f); Check(!motor.IsQuantized && Vector3.Distance(body.position,still) < .01f, "Match input lock releases quantization and blocks movement");
            actor.SetMatchInputLocked(false); Input(Vector2.zero); yield return Seconds(.4f);
            Check(motor.IsQuantized, "Movement ownership returns after the lock ends");
            actor.ExternalStun(.3f); actor.ProtectActionMomentum(.3f); body.linearVelocity = Vector3.right * 2;
            yield return Seconds(.15f); Check(!motor.IsQuantized && body.position.x > origin.x + .05f, "Stun and protected knockback remain continuous");
            yield return Seconds(.4f); ResetActor(); yield return Seconds(.2f);
            actor.SetScriptedInput(new PlayerInputFrame { attackDown=true, attackHeld=true, hasAimDirWS=true, aimDirWS=Vector3.right });
            yield return Until(() => actor.attackController.IsAttacking, "Canonical melee attack still activates");
            actor.SetScriptedInput(PlayerInputFrame.Neutral); yield return Seconds(.05f);
            Check(!motor.IsQuantized, "Attack movement temporarily owns the player body");
            actor.attackController.CancelAttack(); yield return Seconds(.4f); ResetActor(); yield return Seconds(.1f);
            float frozenClock = field.FieldTime; still = body.position; Time.timeScale = 0;
            double resumeAt = EditorApplication.timeSinceStartup + .15; while (EditorApplication.timeSinceStartup < resumeAt) yield return null;
            Check(Mathf.Abs(field.FieldTime-frozenClock)<.001f && Vector3.Distance(body.position,still)<.001f, "Pause freezes both the disruption field and quantized movement");
            Time.timeScale=1; field.mode=LatticeDisruptionField.FieldMode.Connected; yield return Seconds(1.2f);
            Check(!motor.IsQuantized && field.DisconnectedEdgeCount==0, "Reconnection restores continuous movement without residual snapping");
            field.mode=LatticeDisruptionField.FieldMode.DriftingNoise; yield return Seconds(1.2f);
            int changed = 0;
            for(int y=-5;y<=5;y++) for(int x=-13;x<=13;x++)
                if((field.NoiseAt(new Vector2(x,y),0)>.51f)!=(field.NoiseAt(new Vector2(x,y),35)>.51f)) changed++;
            Check(changed>20, "Evolving noise changes the connected topology across the field");
            CaptureFixture(gridObject.transform, "lattice-noise.png");
            yield return CaptureBreeze(gridObject.transform);
            yield return DotJitterChecks(gridObject.transform);
            field.enabled=false; yield return null;
            Check(!grid.GetComponent<MeshRenderer>().forceRenderingOff && !motor.IsQuantized, "Disabling the field restores the original renderer and locomotion");
            field.enabled=true; yield return Until(()=>field.IsReady,"Field rebuilds after disable and enable");
            motor.enabled=false; yield return null;
            Check(body.interpolation != RigidbodyInterpolation.None || !motor.IsQuantized,"Motor releases its interpolation ownership on disable");
            Check(!ShaderUtil.ShaderHasError(installed.strandShader),"Rendered strand and node shader has no compilation errors");
            Check(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Anomalies/LATTICE/Editor/LatticeBoundaryGuide.shader")),
                "Editor-only dashed contour shader has no compilation errors");
        }

        static IEnumerator BoundaryChecks(Transform grid)
        {
            field.mode = LatticeDisruptionField.FieldMode.Connected; yield return Seconds(1.2f);
            field.noiseFrequency = .75f; field.drift = Vector2.zero; field.evolutionSpeed = 0; field.noiseHysteresis = 0;
            field.mode = LatticeDisruptionField.FieldMode.DriftingNoise;
            yield return Seconds(.08f);
            int nodeCuts = 0, middleCuts = 0; bool singleTethers = true, twoTethers = true;
            for (int i = 0; i < field.EdgeCount; i++)
            {
                field.GetConnection(i, out var a, out var b, out var lengths);
                var boundary = field.ConnectionAt(a, b, field.FieldTime);
                if (boundary.disruptedA != boundary.disruptedB)
                {
                    nodeCuts++;
                    singleTethers &= boundary.disruptedA ? lengths.x < .001f && lengths.y > .5f : lengths.y < .001f && lengths.x > .5f;
                }
                else if (boundary.cut && !boundary.disruptedA)
                { middleCuts++; twoTethers &= lengths.x > 0 && lengths.y > 0; }
            }
            Check(nodeCuts > 0 && singleTethers, "A broken endpoint releases one full tether anchored at its intact neighbor");
            Check(middleCuts > 0 && twoTethers, "A disruption between intact endpoints releases two strands");
            CaptureFixture(grid, "lattice-one-sided.png");
            yield return Seconds(1.2f);
            field.keepStrandsAtNoiseBoundary = true; yield return Seconds(.08f);
            CheckBoundaryTips("Held strand tips land on the exact noise boundary, with no tether from a disrupted node");
            CaptureFixture(grid, "lattice-boundary-held.png");
            field.drift = new Vector2(.09f,.035f); field.evolutionSpeed = .075f;
            for (int i = 0; i < 100; i++) field.Advance(.1f);
            CheckBoundaryTips("Held strand tips follow the contour as the noise moves and changes shape");
            field.keepStrandsAtNoiseBoundary = false; yield return Seconds(1.2f);
            int retracted = 0;
            for (int i = 0; i < field.EdgeCount; i++)
            {
                field.GetConnection(i,out var a,out var b,out var lengths);
                if (lengths.sqrMagnitude < .00001f) retracted++;
            }
            Check(retracted > 10, "Turning boundary hold off restores full retraction");
            field.noiseFrequency = .19f; field.noiseHysteresis = .018f;
        }

        static void CheckBoundaryTips(string label)
        {
            int tips = 0; bool correct = true;
            for (int i = 0; i < field.EdgeCount; i++)
            {
                field.GetConnection(i,out var a,out var b,out var lengths);
                var boundary = field.ConnectionAt(a,b,field.FieldTime);
                if (!boundary.cut) continue;
                correct &= !boundary.disruptedA || lengths.x < .00001f;
                correct &= !boundary.disruptedB || lengths.y < .00001f;
                if (lengths.x > .001f)
                { tips++; correct &= Mathf.Abs(field.NoiseAt(Vector2.Lerp(a,b,lengths.x),field.FieldTime)-field.threshold) < .001f; }
                if (lengths.y > .001f)
                { tips++; correct &= Mathf.Abs(field.NoiseAt(Vector2.Lerp(b,a,lengths.y),field.FieldTime)-field.threshold) < .001f; }
            }
            Check(tips > 10 && correct,label);
        }

        static IEnumerator SpeedChecks(Transform scope)
        {
            var referenceRoot = new GameObject("Continuous movement comparison"); owned.Add(referenceRoot); referenceRoot.SetActive(false);
            var referenceObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), referenceRoot.transform);
            var reference = referenceObject.GetComponent<PlayerControllerScript>(); reference.ConfigureDemonstration(scope,0,1);
            var referenceScale = reference.GetComponent<PlayerScaleAdjuster>(); referenceScale.UseGlobalModifiers = false; referenceScale.Size = 1;
            var referenceBody = referenceObject.GetComponent<Rigidbody>(); referenceBody.useGravity = false;
            Vector3 referenceOrigin = origin + Vector3.forward * 30; referenceObject.transform.position = referenceOrigin;
            referenceRoot.SetActive(true); yield return Seconds(.2f);
            float oldMass = body.mass, oldDamping = body.linearDamping;
            var sticks = new[] { Vector2.right, new Vector2(1,1).normalized, Vector2.right * .5f, Vector2.right };
            for (int trial = 0; trial < sticks.Length; trial++)
            {
                ResetActor(); reference.SetScriptedInput(PlayerInputFrame.Neutral);
                referenceBody.position = referenceOrigin; referenceBody.linearVelocity = Vector3.zero;
                body.mass = referenceBody.mass = trial == 3 ? oldMass * 2 : oldMass;
                body.linearDamping = referenceBody.linearDamping = trial == 3 ? 4 : oldDamping;
                Physics.SyncTransforms(); yield return Seconds(.1f);
                float travelled = motor.TravelledDistance;
                Vector3 start = referenceBody.position;
                Input(sticks[trial]); reference.SetScriptedInput(new PlayerInputFrame { moveInput = sticks[trial] });
                yield return Seconds(.9f);
                float continuousDistance = Vector3.Distance(referenceBody.position,start);
                float hops = motor.TravelledDistance - travelled;
                float accounted = hops + motor.PendingDistance;
                Check(continuousDistance > .2f && Mathf.Abs(accounted-continuousDistance) < .08f,
                    $"Speed trial {trial}: hopping preserves ordinary travel distance ({accounted:F3} vs {continuousDistance:F3}, including pending fraction)");
                float oneStep = trial == 1 ? Mathf.Sqrt(2) * field.Spacing : field.Spacing;
                Check(continuousDistance - hops < oneStep + .08f && hops <= continuousDistance + .08f,
                    $"Speed trial {trial}: visible travel differs by at most the next discrete hop");
                Input(Vector2.zero); reference.SetScriptedInput(PlayerInputFrame.Neutral);
            }
            body.mass = oldMass; body.linearDamping = oldDamping; referenceRoot.SetActive(false); ResetActor();
        }
        static IEnumerator CaptureBreeze(Transform grid)
        {
            // Compare the actual GPU-rendered strands against a stationary contour,
            // so the motion review is not confused by changing connection lengths.
            field.keepStrandsAtNoiseBoundary = true; field.drift = Vector2.zero; field.evolutionSpeed = 0;
            field.showNoiseBoundary = false; yield return Seconds(.8f);
            Vector2 focus = Vector2.zero; float nearest = float.MaxValue;
            for (int i = 0; i < field.EdgeCount; i++)
            {
                field.GetConnection(i, out var a, out var b, out var lengths);
                if ((lengths.x > .35f && lengths.x < .9f || lengths.y > .35f && lengths.y < .9f) && a.sqrMagnitude < nearest)
                { focus = (a + b) * .5f; nearest = a.sqrMagnitude; }
            }
            for (int i = 0; i < 3; i++)
            {
                CaptureFixture(grid, "lattice-breeze-" + i + ".png", focus, 2.6f);
                yield return Seconds(1.5f);
            }
            field.strandMotionSeed += 731; yield return Seconds(.08f);
            CaptureFixture(grid, "lattice-breeze-alternate-seed.png", focus, 2.6f);
            CheckBoundaryTips("Randomized breeze leaves held strand endpoints on the disruption contour");
        }
        static IEnumerator DotJitterChecks(Transform grid)
        {
            field.dotGhostTrails = false;
            field.rgbDotJitter = true; field.mode = LatticeDisruptionField.FieldMode.DriftingNoise;
            field.drift = Vector2.zero; field.evolutionSpeed = 0; field.showNoiseBoundary = false;
            yield return Seconds(.1f);
            int outside = 0, near = 0, deep = 0, compared = 0;
            bool validDepth = true, accurate = true;
            Vector2 focus = Vector2.zero;
            for (int i = 0; i < field.NodeCount; i++)
            {
                field.GetNode(i, out var p, out _, out float depth);
                if (field.NoiseAt(p, field.FieldTime) <= field.threshold)
                { outside++; validDepth &= depth == 0; continue; }
                if (depth < .25f) near++;
                if (depth > 1) { deep++; focus = p; }
                if (compared++ < 12)
                {
                    // Independent reference: find the nearest exit by dense radial
                    // sampling, rather than duplicating the marching-squares algorithm.
                    float distance = field.dotJitterDepth;
                    for (int ray = 0; ray < 64; ray++)
                    {
                        float angle = ray * Mathf.PI * 2 / 64;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        for (float d = .025f; d < distance; d += .025f)
                            if (field.NoiseAt(p + direction * d * field.Spacing, field.FieldTime) <= field.threshold)
                            { distance = d; break; }
                    }
                    accurate &= Mathf.Abs(depth - distance) < .12f;
                }
            }
            Check(outside > 0 && validDepth, "Dots outside the disruption have zero RGB jitter depth");
            Check(near > 0 && deep > 0, "Noise regions provide quiet boundary dots and stronger interior jitter");
            Check(compared >= 12 && accurate, "Dot depth agrees with independently sampled distance to the noise contour");
            CaptureFixture(grid, "lattice-rgb-noise.png");
            CaptureFixture(grid, "lattice-rgb-detail.png", focus, 2.6f);
            field.mode = LatticeDisruptionField.FieldMode.Connected; yield return Seconds(1.2f);
            bool zero = true;
            for (int i = 0; i < field.NodeCount; i++) { field.GetNode(i, out _, out _, out float depth); zero &= depth == 0; }
            Check(zero, "Connected mode clears all dot jitter depth");
            field.mode = LatticeDisruptionField.FieldMode.Disconnected; yield return Seconds(1.2f);
            bool full = true;
            for (int i = 0; i < field.NodeCount; i++) { field.GetNode(i, out _, out _, out float depth); full &= Mathf.Abs(depth-field.dotJitterDepth) < .001f; }
            Check(full, "Fully disconnected mode reaches maximum RGB jitter even at arena edges");

            // Larger diagnostic dots make individual channels and overlaps easy to inspect.
            actor.gameObject.SetActive(false); field.dotDiameterPixels = 12; field.dotJitterRadius = 4;
            field.dotOpacity = 1; yield return Seconds(.08f);
            CaptureFixture(grid, "lattice-rgb-channels.png", Vector2.zero, 2.6f);
            var rgb = ReadCapture("lattice-rgb-channels.png");
            Check(CountPixels(rgb, c => c.r > 180 && c.g < 20 && c.b < 20) > 10 &&
                CountPixels(rgb, c => c.g > 180 && c.r < 20 && c.b < 20) > 10 &&
                CountPixels(rgb, c => c.b > 180 && c.r < 20 && c.g > 35 && c.g < c.b - 20) > 10,
                "Rendered dots contain independently separated red, green and cyan-tinted blue layers");
            Check(CountPixels(rgb, c => c.r > 240 && c.g > 240 && c.b > 240) > 20 &&
                CountPixels(rgb, c => c.r > 200 && c.g > 200 && c.b < 20) > 10 &&
                CountPixels(rgb, c => c.g > 200 && c.b > 200 && c.r < 20) > 10 &&
                CountPixels(rgb, c => c.r > 200 && c.b > 200 && c.g > 35 && c.g < c.b - 20) > 10,
                "Three-color overlap renders white, with yellow, cyan and tinted magenta for two-layer overlaps");
            yield return Seconds(.17f); CaptureFixture(grid, "lattice-rgb-motion.png", Vector2.zero, 2.6f);
            Check(!rgb.SequenceEqual(ReadCapture("lattice-rgb-motion.png")), "The color layers jitter over time");
            Time.timeScale = 0; CaptureFixture(grid, "lattice-rgb-paused-a.png", Vector2.zero, 2.6f);
            double resume = EditorApplication.timeSinceStartup + .15;
            while (EditorApplication.timeSinceStartup < resume) yield return null;
            CaptureFixture(grid, "lattice-rgb-paused-b.png", Vector2.zero, 2.6f);
            Check(ReadCapture("lattice-rgb-paused-a.png").SequenceEqual(ReadCapture("lattice-rgb-paused-b.png")), "Pause freezes the rendered RGB dot offsets");
            Time.timeScale = 1;
            field.rgbDotJitter = false; yield return Seconds(.08f);
            CaptureFixture(grid, "lattice-rgb-disabled.png", Vector2.zero, 2.6f);
            var white = ReadCapture("lattice-rgb-disabled.png");
            Check(CountPixels(white, c => Math.Abs(c.r-c.g) > 2 || Math.Abs(c.g-c.b) > 2) == 0 &&
                CountPixels(white, c => c.r > 200) > 20, "Disabling RGB jitter restores white dots");
            Check(field.DisruptionAt(origin) > .999f, "Dot color controls do not change the locomotion field");
            yield return DotTrailChecks(grid);
            actor.gameObject.SetActive(true);
        }
        static IEnumerator DotTrailChecks(Transform grid)
        {
            field.rgbDotJitter = true; field.dotGhostTrails = false;
            field.dotDiameterPixels = 5; field.dotJitterRadius = 9;
            field.dotTrailDuration = .18f; field.dotTrailOpacity = .5f;
            yield return Seconds(.1f);
            Time.timeScale = 0; yield return null;
            CaptureFixture(grid, "lattice-trails-off.png", Vector2.zero, 2.6f);
            var heads = ReadCapture("lattice-trails-off.png");
            field.dotGhostTrails = true; yield return null;
            CaptureFixture(grid, "lattice-trails-on.png", Vector2.zero, 2.6f);
            var trails = ReadCapture("lattice-trails-on.png");
            int ghosts = 0; bool headsRetained = true;
            for (int i = 0; i < heads.Length; i++)
            {
                var a = heads[i]; var b = trails[i];
                if (a.r < 3 && a.g < 3 && a.b < 3 && Mathf.Max(b.r, Mathf.Max(b.g,b.b)) > 15) ghosts++;
                headsRetained &= b.r >= a.r-2 && b.g >= a.g-2 && b.b >= a.b-2;
            }
            Check(ghosts > 100, "Ghost trails render fading afterimages beyond the current RGB dots");
            Check(headsRetained, "Trails preserve the current dot heads without dimming their channels");
            double resume = EditorApplication.timeSinceStartup + .15;
            while (EditorApplication.timeSinceStartup < resume) yield return null;
            CaptureFixture(grid, "lattice-trails-paused.png", Vector2.zero, 2.6f);
            Check(trails.SequenceEqual(ReadCapture("lattice-trails-paused.png")), "Pause freezes the dot ghost trails and their fading history");
            field.dotTrailOpacity = .15f; yield return null;
            CaptureFixture(grid, "lattice-trails-faint.png", Vector2.zero, 2.6f);
            var faint = ReadCapture("lattice-trails-faint.png");
            long strongEnergy = 0, faintEnergy = 0;
            for (int i = 0; i < trails.Length; i++)
            { strongEnergy += trails[i].r + trails[i].g + trails[i].b; faintEnergy += faint[i].r + faint[i].g + faint[i].b; }
            Check(faintEnergy < strongEnergy, "Trail opacity reduces afterimage visibility");
            field.dotTrailOpacity = 0; yield return null;
            CaptureFixture(grid, "lattice-trails-zero.png", Vector2.zero, 2.6f);
            Check(heads.SequenceEqual(ReadCapture("lattice-trails-zero.png")), "Zero trail opacity restores the exact untrailed dots");
            field.dotTrailOpacity = .5f; field.dotTrailDuration = .4f; yield return null;
            CaptureFixture(grid, "lattice-trails-long.png", Vector2.zero, 2.6f);
            Check(!trails.SequenceEqual(ReadCapture("lattice-trails-long.png")), "Trail duration selects an older portion of each jitter path");
            field.dotJitterRadius = 0; yield return null;
            CaptureFixture(grid, "lattice-trails-still-on.png", Vector2.zero, 2.6f);
            field.dotGhostTrails = false; yield return null;
            CaptureFixture(grid, "lattice-trails-still-off.png", Vector2.zero, 2.6f);
            Check(ReadCapture("lattice-trails-still-on.png").SequenceEqual(ReadCapture("lattice-trails-still-off.png")),
                "Zero jitter produces no ghost halo or brightness buildup");
            Time.timeScale = 1;
        }
        static Color32[] ReadCapture(string name)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try { texture.LoadImage(File.ReadAllBytes(Output + "/" + name)); return texture.GetPixels32(); }
            finally { Object.Destroy(texture); }
        }
        static int CountPixels(Color32[] pixels, Func<Color32, bool> predicate) => pixels.Count(predicate);

        static void CaptureFixture(Transform grid, string name, Vector2 focus = default, float size = 7.1f)
        {
            var go = new GameObject("LATTICE capture camera"); var camera = go.AddComponent<Camera>(); camera.enabled=false;
            go.transform.SetPositionAndRotation(grid.TransformPoint(new Vector3(focus.x,focus.y,0))+Vector3.up*25,Quaternion.Euler(90,0,0));
            camera.orthographic=true; camera.orthographicSize=size; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            Capture(camera,name); Object.Destroy(go);
        }
        static void Capture(Camera camera, string name)
        {
            if (!camera) return;
            var target = new RenderTexture(1600,900,24); var previous = camera.targetTexture; var active=RenderTexture.active;
            var texture = new Texture2D(1600,900,TextureFormat.RGB24,false);
            try { camera.targetTexture=target; camera.Render(); RenderTexture.active=target; texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply(); File.WriteAllBytes(Output+"/"+name,texture.EncodeToPNG()); }
            finally { camera.targetTexture=previous; RenderTexture.active=active; target.Release(); Object.Destroy(target); Object.Destroy(texture); }
        }
    }
}
#endif
