using System;
using System.Collections.Generic;
using Massive.Multiplier;
using Massive.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    /// <summary>Disposable prefab and PhysX world. Never simulates the authored scene.</summary>
    public sealed class AmplifierCoreInteractionPreview : IDisposable
    {
        public enum Look { Neutral, LightAim, DarkAim, Contest, TimeoutWarning, Spawn, Despawn, PostContestLight, PostContestDark, Ejected }
        public enum Phase { Preview, Charging, Flying, ReentryDelay, Enterable }
        const string PrefabPath = "Assets/Power-ups/Amplifier Core/Amplifier Core.prefab";
        readonly PreviewRenderUtility preview;
        readonly PhysicsScene physics;
        readonly GameObject instance, arena;
        readonly Rigidbody body;
        readonly SphereCollider sphere;
        readonly AmplifierCoreVisual visual;
        readonly Renderer[] renderers;
        readonly Transform[] movingParts;
        readonly Quaternion[] originalRotations;
        readonly Vector3 originalScale;
        readonly PhysicsMaterial contactMaterial;
        readonly Material lineMaterial;
        readonly Mesh grid;
        readonly Mesh aimMesh;
        readonly AmplifierCoreControlUI controlUI;
        readonly List<Vector3> dotVertices = new();
        readonly List<int> dotIndices = new();
        readonly List<Color> dotColors = new();
        readonly GameObject[] walls = new GameObject[4];
        readonly List<Vector3> trace = new();
        readonly MaterialPropertyBlock properties = new();
        readonly RaycastHit[] contacts = new RaycastHit[12];
        AmplifierSharedProfile profile;
        int profileVersion = -1;
        float accumulator, chargeDrag, stillAge, restAge;
        bool disposed;
        bool ejectionPreviewTriggered;

        public Look Appearance { get; private set; } = Look.LightAim;
        public Phase State { get; private set; }
        public float Clock { get; private set; }
        public float ShotTime { get; private set; }
        public float Distance { get; private set; }
        public float StopTime { get; private set; } = -1f;
        public int Bounces { get; private set; }
        public float Speed => body && !body.isKinematic ? new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude : 0f;
        public float Radius => sphere.radius * Mathf.Max(instance.transform.lossyScale.x, instance.transform.lossyScale.z);
        public Vector3 Position => body.position;
        public float Charge { get; set; } = 1f;
        public float AimAngle { get; set; } = 20f;
        public float ContestBalance { get; set; } = .5f;
        public float Width { get; private set; } = 24f;
        public float Height { get; private set; } = 14f;
        public bool WallsEnabled { get; private set; } = true;
        public bool Loop { get; set; }
        public bool IsIsolated => physics.IsValid() && !physics.Equals(Physics.defaultPhysicsScene);
        public bool IsDisposed => disposed;
        public Camera Camera => preview.camera;
        public GameObject CoreObject => instance;
        public AmplifierCoreControlUI ControlUI => controlUI;
        public float DisplayCharge => State == Phase.Charging ? Mathf.Min(Charge, ShotTime / Mathf.Max(.1f, Settings.fullChargeSeconds)) :
            Held && PostContest ? Mathf.Min(Charge, Mathf.Max(0f, Clock - PostContestReadyAt) / Mathf.Max(.1f, Settings.fullChargeSeconds)) : Charge;
        float PostContestReadyAt => .8f + Settings.PostContestLockSeconds + Mathf.Max(0f, Settings.postContestReleaseSeconds);
        public float PostContestProtectionRemaining => Held && PostContest && Clock >= .8f ? Mathf.Max(0f, .8f + Settings.PostContestLockSeconds - Clock) : 0f;
        public bool PostContestWaitingForRelease => Held && PostContest && Clock >= .8f && PostContestProtectionRemaining <= 0f && Clock < PostContestReadyAt;
        public float DisplayControlSecondsRemaining => Mathf.Max(0f, ContestVisible ? Settings.contestSeconds - Clock :
            PostContest ? Settings.winnerAimSeconds - Mathf.Max(0f, Clock - (.8f + Settings.PostContestLockSeconds)) : Settings.possessionSeconds - Clock);
        public float ReentryRemaining => State == Phase.ReentryDelay ? Mathf.Max(0f, Settings.reentryDelay - restAge) : 0f;
        AmplifierPossessionSettings Settings => profile.core.possession;
        Vector3 Direction => new(Mathf.Cos(AimAngle * Mathf.Deg2Rad), 0f, Mathf.Sin(AimAngle * Mathf.Deg2Rad));
        bool Held => State is Phase.Preview or Phase.Charging;
        bool PostContest => Appearance is Look.PostContestLight or Look.PostContestDark;
        bool ContestVisible => Held && (Appearance == Look.Contest || PostContest && Clock < .8f);
        bool SingleVisible => Held && (Appearance is Look.LightAim or Look.DarkAim || PostContest && Clock >= .8f || Appearance == Look.Ejected && Clock < .8f);
        float DisplayBalance => PostContest ? Mathf.Lerp(.5f, Appearance == Look.PostContestLight ? 1f : 0f, Mathf.SmoothStep(0f, 1f, Clock / .8f)) : ContestBalance;
        bool Possession => Settings.mode == AmplifierInteractionMode.PossessionShot;

        public AmplifierCoreInteractionPreview(AmplifierSharedProfile tuning)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Preview is available in Edit Mode.");
            profile = tuning;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) throw new InvalidOperationException("Amplifier Core prefab is missing.");
            preview = new PreviewRenderUtility();
            try
            {
                physics = preview.camera.gameObject.scene.GetPhysicsScene();
                if (!IsIsolated) throw new InvalidOperationException("Unity did not create an isolated preview physics scene.");
                instance = preview.InstantiatePrefabInScene(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.layer = 0; // The test walls must not inherit gameplay layer exclusions.
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                body = instance.GetComponent<Rigidbody>(); sphere = instance.GetComponent<SphereCollider>();
                visual = instance.GetComponent<AmplifierCoreVisual>();
                // The prefab's serialized scale dial may differ from the shared project value.
                using (var data = new SerializedObject(instance.GetComponent<AmplifierCoreGameplay>()))
                    originalScale = instance.transform.localScale / Mathf.Max(.05f, data.FindProperty("appliedCoreScale").floatValue);
                renderers = instance.GetComponentsInChildren<Renderer>(true);
                movingParts = new[] { instance.transform, instance.transform.Find("Visual Root/Energy Core"), instance.transform.Find("Visual Root/Neutral Shell") };
                originalRotations = Array.ConvertAll(movingParts, t => t.localRotation);
                contactMaterial = new PhysicsMaterial("Core preview contacts") { hideFlags = HideFlags.HideAndDontSave,
                    dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
                sphere.sharedMaterial = contactMaterial; sphere.enabled = true; sphere.isTrigger = false;
                body.useGravity = false; body.sleepThreshold = 0f; body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                arena = EditorUtility.CreateGameObjectWithHideFlags("Core preview arena", HideFlags.HideAndDontSave);
                preview.AddSingleGO(arena);
                lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lineMaterial.SetInt("_Cull", 0); lineMaterial.SetInt("_ZWrite", 0);
                grid = new Mesh { name = "Core preview grid", hideFlags = HideFlags.HideAndDontSave };
                arena.AddComponent<MeshFilter>().sharedMesh = grid;
                arena.AddComponent<MeshRenderer>().sharedMaterial = lineMaterial;
                var dots = EditorUtility.CreateGameObjectWithHideFlags("Preview aim dots", HideFlags.HideAndDontSave);
                dots.transform.SetParent(arena.transform, false);
                aimMesh = new Mesh { name = "Core preview aim dots", hideFlags = HideFlags.HideAndDontSave };
                dots.AddComponent<MeshFilter>().sharedMesh = aimMesh;
                dots.AddComponent<MeshRenderer>().sharedMaterial = lineMaterial;
                controlUI = new AmplifierCoreControlUI(instance.transform);
                for (int i = 0; i < walls.Length; i++)
                {
                    walls[i] = EditorUtility.CreateGameObjectWithHideFlags("Preview wall " + i, HideFlags.HideAndDontSave);
                    walls[i].transform.SetParent(arena.transform);
                    walls[i].AddComponent<BoxCollider>().sharedMaterial = contactMaterial;
                }
                preview.camera.orthographic = true; preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 100f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = Color.black;
                preview.camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                preview.ambientColor = Color.white;
                preview.lights[0].intensity = .8f; preview.lights[1].intensity = .4f;
                SetArena(Width, Height, true); SyncSettings(true); Reset();
            }
            catch { Dispose(); throw; }
        }

        public void SyncSettings(bool force = false)
        {
            int version = EditorUtility.GetDirtyCount(profile);
            if (!force && version == profileVersion) return;
            profileVersion = version;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile.coreSurface), visual);
            visual.UseSharedSettings = false;
            instance.transform.localScale = originalScale * Mathf.Max(.05f, profile.core.coreScale);
            body.mass = Mathf.Max(.01f, profile.core.mass);
            body.angularDamping = Mathf.Max(0f, profile.core.angularDrag);
            visual.SetLifecycleReveal(1f, 1f);
            ApplyVisual(0f);
        }

        public void SetArena(float width, float height, bool wallsOn)
        {
            Width = Mathf.Clamp(width, 8f, 100f); Height = Mathf.Clamp(height, 6f, 60f); WallsEnabled = wallsOn;
            var points = new List<Vector3>(); var colors = new List<Color>();
            void Line(Vector3 a, Vector3 b, Color color) { points.Add(a); points.Add(b); colors.Add(color); colors.Add(color); }
            Color faint = new(.14f, .16f, .18f, 1f);
            for (float x = -Width * .5f; x <= Width * .5f; x += 2f) Line(new(x, -1f, -Height * .5f), new(x, -1f, Height * .5f), faint);
            for (float z = -Height * .5f; z <= Height * .5f; z += 2f) Line(new(-Width * .5f, -1f, z), new(Width * .5f, -1f, z), faint);
            for (int i = 0; i < 4; i++)
            {
                bool vertical = i < 2; float sign = i % 2 == 0 ? -1f : 1f;
                var t = walls[i].transform;
                t.localPosition = vertical ? new Vector3(sign * (Width * .5f + .1f), 0, 0) : new Vector3(0, 0, sign * (Height * .5f + .1f));
                t.localScale = vertical ? new Vector3(.2f, 4f, Height + .4f) : new Vector3(Width + .4f, 4f, .2f);
                walls[i].SetActive(wallsOn);
                var a = vertical ? new Vector3(sign * Width * .5f, -1f, -Height * .5f) : new Vector3(-Width * .5f, -1f, sign * Height * .5f);
                var b = vertical ? new Vector3(sign * Width * .5f, -1f, Height * .5f) : new Vector3(Width * .5f, -1f, sign * Height * .5f);
                Line(a, b, wallsOn ? Color.white : faint);
            }
            grid.Clear(); grid.SetVertices(points); grid.SetColors(colors);
            var indices = new int[points.Count]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
            grid.SetIndices(indices, MeshTopology.Lines, 0); grid.RecalculateBounds();
        }

        public void SetAppearance(Look value) { Appearance = value; Reset(); }
        public void Reset()
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            State = Phase.Preview; Clock = ShotTime = Distance = stillAge = restAge = accumulator = 0f;
            StopTime = -1f; Bounces = 0; trace.Clear();
            ejectionPreviewTriggered = false;
            instance.transform.position = new Vector3(-Width * .3f, 0f, 0f);
            body.position = instance.transform.position;
            for (int i = 0; i < movingParts.Length; i++) movingParts[i].localRotation = originalRotations[i];
            body.rotation = originalRotations[0];
            visual.RestorePresentation(); ApplyVisual(0f);
        }

        public void Launch(bool animateCharge = false)
        {
            Reset(); SyncSettings(true);
            if (animateCharge && Possession) { State = Phase.Charging; return; }
            Fire();
        }

        void Fire()
        {
            State = Phase.Flying; ShotTime = 0f; body.isKinematic = false;
            float speed = AmplifierCoreMotion.LimitSpeed(Direction * Mathf.Max(.1f, Settings.shotSpeed), profile.core.maximumPlanarSpeed).magnitude;
            float range = Mathf.Lerp(Mathf.Max(.1f, Settings.minimumRange), Mathf.Max(Settings.minimumRange, Settings.maximumRange), Mathf.Clamp01(Charge));
            chargeDrag = Possession ? AmplifierCoreMotion.ShotDrag(speed, range, Time.fixedDeltaTime, Settings) : 0f;
            body.linearDamping = Possession ? AmplifierCoreMotion.CoastingDrag(chargeDrag, profile.core.linearDrag, Settings) : Mathf.Max(0f, profile.core.linearDrag);
            body.linearVelocity = Possession ? Direction * speed : Vector3.zero;
            if (!Possession) body.AddForce(Direction * Mathf.Max(0f, profile.core.attackImpulse), ForceMode.Impulse);
            body.angularVelocity = Vector3.Cross(Vector3.up, body.linearVelocity) * (Mathf.Clamp01(Settings.rollingSpin) / Mathf.Max(.05f, Radius));
            body.WakeUp(); trace.Add(Position); ApplyVisual(0f);
            if (Possession) visual.PlayEjectionRipple(-Direction, Settings);
        }

        public void Advance(float seconds)
        {
            if (disposed || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SyncSettings(); accumulator += Mathf.Max(0f, seconds);
            float step = Mathf.Max(.001f, Time.fixedDeltaTime);
            int limit = 0;
            while (accumulator >= step && limit++ < 500) { accumulator -= step; Step(step); }
        }

        void Step(float step)
        {
            Clock += step;
            if (State == Phase.Charging)
            {
                ShotTime += step;
                if (ShotTime >= Mathf.Clamp01(Charge) * Mathf.Max(.1f, Settings.fullChargeSeconds)) Fire();
            }
            else if (State == Phase.Flying)
            {
                ShotTime += step;
                body.linearDamping = Possession ? AmplifierCoreMotion.Drag(chargeDrag, profile.core.linearDrag, ShotTime, Settings) : Mathf.Max(0f, profile.core.linearDrag);
                body.angularDamping = Mathf.Max(0f, profile.core.angularDrag);
                body.linearVelocity = AmplifierCoreMotion.LimitSpeed(body.linearVelocity, profile.core.maximumPlanarSpeed);
                if (Possession && AmplifierCoreMotion.Settle(Speed, false, step, ref stillAge, Settings))
                {
                    StopTime = ShotTime; body.linearVelocity = body.angularVelocity = Vector3.zero;
                    body.isKinematic = true; State = Phase.ReentryDelay; restAge = 0f;
                }
                else
                {
                    var previous = Position; var incoming = body.linearVelocity;
                    // Editor simulation does not dispatch MonoBehaviour collision callbacks.
                    // Sweep the isolated walls and apply the same rebound rule as gameplay.
                    int hits = WallsEnabled && incoming.sqrMagnitude > .00001f
                        ? physics.SphereCast(previous, Radius, incoming.normalized, contacts, incoming.magnitude * step + sphere.contactOffset, ~0, QueryTriggerInteraction.Ignore) : 0;
                    physics.Simulate(step);
                    for (int i = 0; i < hits; i++)
                    {
                        var hit = contacts[i];
                        if (!hit.collider || hit.collider == sphere || hit.collider.transform.parent != arena.transform) continue;
                        int wall = Array.FindIndex(walls, w => w == hit.collider.gameObject);
                        // Initial-overlap sphere casts return -castDirection, not a surface normal.
                        // These rectangular test walls have stable inward-facing normals.
                        Vector3 normal = wall < 2 ? (wall == 0 ? Vector3.right : Vector3.left) : (wall == 2 ? Vector3.forward : Vector3.back);
                        if (Vector3.Dot(incoming, normal) >= 0f) continue;
                        incoming = AmplifierCoreMotion.Bounce(incoming, normal, profile.core.wallRestitution, profile.core.maximumPlanarSpeed);
                        body.linearVelocity = incoming;
                        var roll = Vector3.Cross(Vector3.up, incoming) * (Mathf.Clamp01(Settings.rollingSpin) / Mathf.Max(.05f, Radius));
                        body.angularVelocity = Vector3.Lerp(body.angularVelocity, roll, .65f); Bounces++;
                    }
                    Distance += Vector3.Distance(previous, Position);
                    if (trace.Count < 4096 && (trace.Count == 0 || (Position - trace[trace.Count - 1]).sqrMagnitude > .025f)) trace.Add(Position);
                    if (!Possession && Speed < .02f) { restAge += step; if (restAge > .5f) { State = Phase.Enterable; StopTime = ShotTime; } }
                }
            }
            else if (State == Phase.ReentryDelay)
            {
                restAge += step;
                if (restAge >= Mathf.Max(0f, Settings.reentryDelay)) { State = Phase.Enterable; restAge = 0f; }
            }
            else if (State == Phase.Enterable && Loop)
            {
                restAge += step; if (restAge >= 1f) Launch(true);
            }
            else if (State == Phase.Preview && Loop && Appearance is Look.Spawn or Look.Despawn)
            {
                float duration = Appearance == Look.Spawn
                    ? Mathf.Max(profile.core.spawnScaleSeconds, Mathf.Max(profile.core.spawnShellMorphSeconds, profile.core.spawnCoreDelaySeconds + profile.core.spawnCoreRevealSeconds))
                    : Mathf.Max(profile.core.despawnScaleSeconds, Mathf.Max(profile.core.despawnShellMorphSeconds, profile.core.despawnCoreHideSeconds));
                if (Clock > duration + 1f) Reset();
            }
            else if (State == Phase.Preview && Loop && (PostContest || Appearance == Look.Ejected))
            {
                float finish = PostContest ? Mathf.Max(.8f + Settings.ejectionRippleSeconds,
                    PostContestReadyAt + Mathf.Clamp01(Charge) * Mathf.Max(.1f, Settings.fullChargeSeconds)) + .7f :
                    .8f + Mathf.Max(.05f, Settings.ejectionRippleSeconds) + 1f;
                if (Clock > finish) Reset();
            }
            ApplyVisual(step);
        }

        void ApplyVisual(float step)
        {
            if (!visual) return;
            float size = 1f;
            if (Held && Appearance == Look.Spawn) size = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Clock / Mathf.Max(.05f, profile.core.spawnScaleSeconds)));
            if (Held && Appearance == Look.Despawn) size = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Clock / Mathf.Max(.05f, profile.core.despawnScaleSeconds)));
            instance.transform.localScale = originalScale * (Mathf.Max(.05f, profile.core.coreScale) * Mathf.Max(.0001f, size));
            float light = 0f, dark = 0f; bool contest = ContestVisible;
            if (Held && Appearance == Look.LightAim || contest) light = Settings.playerMarkerScale;
            if (Held && Appearance == Look.DarkAim || contest) dark = Settings.playerMarkerScale;
            if (Held && PostContest && !contest)
            {
                if (Appearance == Look.PostContestLight) light = Settings.playerMarkerScale; else dark = Settings.playerMarkerScale;
            }
            if (Held && Appearance == Look.Ejected && Clock < .8f) light = Settings.playerMarkerScale;
            if (Held && (PostContest || Appearance == Look.Ejected) && Clock >= .8f && !ejectionPreviewTriggered)
            {
                ejectionPreviewTriggered = true;
                visual.PlayEjectionRipple(Appearance == Look.PostContestDark ? Vector3.left : Vector3.right, Settings);
            }
            visual.SetLifecycleReveal(1f, 1f);
            if (Held && Appearance == Look.Spawn)
                visual.SetLifecycleReveal(Clock / Mathf.Max(.05f, profile.core.spawnShellMorphSeconds), (Clock - profile.core.spawnCoreDelaySeconds) / Mathf.Max(.05f, profile.core.spawnCoreRevealSeconds));
            if (Held && Appearance == Look.Despawn)
                visual.SetLifecycleReveal(1f - Clock / Mathf.Max(.05f, profile.core.despawnShellMorphSeconds), 1f - Clock / Mathf.Max(.05f, profile.core.despawnCoreHideSeconds));
            visual.SetPossessionMarkers(light, dark, contest, Settings.playerMarkerScale, Settings.playerMarkerOutline,
                Settings.markerFlowDeflection, Settings.markerClearance, step);
            visual.SetTimeoutWarning(Held && Appearance == Look.TimeoutWarning ? 1f : 0f);
            visual.SetChargeFeedback(DisplayCharge, SingleVisible, Settings);
            visual.SetExcitement(contest ? 1f : SingleVisible ? DisplayCharge : 0f);
            visual.AdvancePresentation(step, Clock);
            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(properties); properties.SetFloat("_PreviewTime", Clock); renderer.SetPropertyBlock(properties);
            }
        }

        public Texture Render(Rect rect, bool closeup)
        {
            SyncSettings(); ApplyVisual(0f);
            float aspect = Mathf.Max(.1f, rect.width / rect.height);
            preview.camera.transform.position = (closeup ? Position : Vector3.zero) + Vector3.up * 30f;
            preview.camera.orthographicSize = closeup ? Mathf.Max(1.6f, Radius * 2.5f) : Mathf.Max(Height * .5f + 1f, (Width * .5f + 1f) / aspect);
            UpdateControlUI();
            // Supersample the cutout shell so this preview does not introduce a
            // jagged edge that could be mistaken for a material problem.
            float scale = Mathf.Min(2f, 2048f / Mathf.Max(rect.width, rect.height));
            preview.BeginPreview(new Rect(0, 0, rect.width * scale, rect.height * scale), GUIStyle.none);
            preview.camera.Render(); return preview.EndPreview();
        }

        void UpdateControlUI()
        {
            var state = !Possession ? AmplifierPromptState.Hidden : ContestVisible ? AmplifierPromptState.Contested :
                SingleVisible ? (PostContest ? AmplifierPromptState.Winner : AmplifierPromptState.Aiming) : AmplifierPromptState.Hidden;
            int team = Appearance is Look.DarkAim or Look.PostContestDark ? 2 : 1;
            controlUI.Update(Settings, preview.camera, Position, Radius, state, team, DisplayControlSecondsRemaining, DisplayBalance);
            // Render dots into the same camera image as the Core and prompts, at their world size.
            // Runtime uses Shapes discs; this isolated mesh avoids global camera callbacks in edit mode.
            aimMesh.Clear(); dotVertices.Clear(); dotIndices.Clear(); dotColors.Clear();
            if (!Possession || !SingleVisible || !Settings.showTrajectory) return;
            float first = Radius + .12f, spacing = Mathf.Max(.08f, Settings.dotSpacing);
            float length = Mathf.Min(Mathf.Max(.1f, Mathf.Max(Settings.minimumRange, Settings.maximumRange)), Mathf.Max(.5f, Settings.previewLength));
            int count = Mathf.Clamp(Mathf.FloorToInt((length - first) / spacing) + 1, 0, 2048);
            const int segments = 20;
            for (int i = 0; i < count; i++)
            {
                float radius = AmplifierCoreMotion.AimDotRadius(Settings, i, count, DisplayCharge, Settings.showDotCharge, out _);
                if (radius < .0001f) continue;
                Vector3 center = Position + Direction * (first + i * spacing) + Vector3.up * (Radius + .05f);
                int start = dotVertices.Count; dotVertices.Add(center); dotColors.Add(Color.white);
                for (int j = 0; j <= segments; j++)
                {
                    float a = j * Mathf.PI * 2f / segments;
                    dotVertices.Add(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius); dotColors.Add(Color.white);
                    if (j > 0) { dotIndices.Add(start); dotIndices.Add(start + j); dotIndices.Add(start + j + 1); }
                }
            }
            aimMesh.SetVertices(dotVertices); aimMesh.SetColors(dotColors); aimMesh.SetTriangles(dotIndices, 0); aimMesh.RecalculateBounds();
        }

        Vector3 ScreenPoint(Vector3 point, Rect rect)
        {
            var v = preview.camera.WorldToViewportPoint(point); return new Vector3(v.x * rect.width, (1f - v.y) * rect.height, 0f);
        }

        public void DrawOverlays(Rect rect, bool showTrace)
        {
            GUI.BeginGroup(rect); Handles.BeginGUI();
            Color previousColor = Handles.color;
            try
            {
                if (showTrace && trace.Count > 1)
                {
                    Handles.color = new Color(.3f, .7f, .8f, .45f);
                    var points = new Vector3[trace.Count]; for (int i = 0; i < points.Length; i++) points[i] = ScreenPoint(trace[i], rect);
                    Handles.DrawAAPolyLine(1.5f, points);
                }
            }
            finally { Handles.color = previousColor; Handles.EndGUI(); GUI.EndGroup(); }
        }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            controlUI?.Dispose();
            preview?.Cleanup();
            if (contactMaterial) Object.DestroyImmediate(contactMaterial);
            if (lineMaterial) Object.DestroyImmediate(lineMaterial);
            if (grid) Object.DestroyImmediate(grid);
            if (aimMesh) Object.DestroyImmediate(aimMesh);
        }
    }
}
