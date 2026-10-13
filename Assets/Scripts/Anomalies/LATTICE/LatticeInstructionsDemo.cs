using System.Collections;
using Massive.Demonstrations;
using Massive.Player;
using UnityEngine;

namespace Massive.Lattice
{
    /// <summary>A cropped, full-scale gameplay simulation, isolated from match participants.</summary>
    [DisallowMultipleComponent]
    public sealed class LatticeInstructionsDemo : MonoBehaviour
    {
        public LatticeDisruptionField field;
        public GameObject playerPrefab;
        public PlayerDemoView viewport;
        public Transform panelFrame;
        public Vector2 viewportSize = new Vector2(7.37f, 3.9f);
        [Range(.2f, 1f)] public float stickStrength = .42f;
        [Min(0f)] public float turnaroundPause = .7f;

        public PlayerControllerScript Player { get; private set; }
        public LatticePlayerMotor Motor { get; private set; }
        public int CompletedLoops { get; private set; }
        public int DisruptionEntries { get; private set; }
        public int DisruptionExits { get; private set; }
        public Vector3 NormalEnd { get; private set; }
        public Vector3 DisruptedEnd { get; private set; }
        public string Phase { get; private set; } = "Preparing";

        GameObject session;
        Camera previewCamera;
        Rigidbody body;
        Coroutine routine;
        bool wasQuantized, hasLane;
        float laneY;
        Vector3 cameraVelocity;

        void OnEnable()
        {
            // Keep a readable boundary in this close-up indefinitely. The noise turns
            // around smoothly; strands, RGB jitter and trails keep their continuous clock.
            if (field) field.NoiseLoopSeconds = 24f;
            if (viewport) previewCamera = viewport.GetComponent<Camera>();
            if (previewCamera) previewCamera.enabled = false;
            routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (!field || !playerPrefab || !previewCamera) yield break;
            // Build the initial node state synchronously so player creation and camera
            // framing finish before the instructions panel's first visible frame.
            if (!field.PrepareForPresentation()) yield break;
            while (!FindLane()) yield return null;

            session = new GameObject("Isolated LATTICE player session");
            session.transform.SetParent(transform, false);
            session.SetActive(false);
            var actor = Instantiate(playerPrefab, session.transform);
            actor.name = "Demonstration player";
            actor.transform.position = NormalEnd;
            Player = actor.GetComponent<PlayerControllerScript>();
            Player.ConfigureDemonstration(session.transform, 0, 1);
            actor.GetComponent<PlayerScaleAdjuster>()?.ApplyScale();
            foreach (var interactor in actor.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = field.Grid;
            foreach (var pulse in actor.GetComponentsInChildren<PlayerRepulsorGridPulse>(true)) pulse.BindGrid(field.Grid);
            Motor = actor.GetComponent<LatticePlayerMotor>();
            if (!Motor) Motor = actor.AddComponent<LatticePlayerMotor>();
            Motor.field = field;
            body = actor.GetComponent<Rigidbody>();
            session.SetActive(true);
            previewCamera.transform.position = CameraTarget();
            yield return null;
            while (true)
            {
                Phase = "Into disruption";
                yield return MoveTo(DisruptedEnd);
                Phase = "At disrupted dots";
                yield return new WaitForSeconds(turnaroundPause);
                Phase = "Back to normal grid";
                yield return MoveTo(NormalEnd);
                Phase = "At connected grid";
                CompletedLoops++;
                yield return new WaitForSeconds(turnaroundPause);
                // Follow the slowly drifting contour between loops, keeping the same grid row.
                // Only inputs move the actor; there is no teleport/reset at the loop seam.
                FindLane();
            }
        }

        bool FindLane()
        {
            float spacing = field.Spacing;
            float best = float.PositiveInfinity;
            Vector3 normal = default, disrupted = default;
            float chosenY = laneY;
            for (int i = 0; i < field.NodeCount; i++)
            {
                field.GetNode(i, out var a, out float disruption, out _);
                if (disruption > .08f || Mathf.Abs(a.y) > field.Grid.size.y * .5f - spacing * 2.5f) continue;
                if (hasLane && Mathf.Abs(a.y - laneY) > spacing * .1f) continue;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector2 b = a + Vector2.right * (sign * spacing * 6f);
                    if (Mathf.Abs(b.x) > field.Grid.size.x * .5f - spacing * 2f) continue;
                    Vector3 aw = field.ToWorld(a, transform.position.y);
                    Vector3 bw = field.ToWorld(b, transform.position.y);
                    if (field.DisruptionAt(bw) < .95f) continue;
                    // Prefer a lane whose two ends remain distinct through the next round trip.
                    float future = field.FieldTime + 8f;
                    float score = Mathf.Max(0, field.NoiseAt(a, future) - field.threshold + .03f) * 100f
                        + Mathf.Max(0, field.threshold - field.NoiseAt(b, future) + .03f) * 100f;
                    score += hasLane ? Vector3.Distance(aw, NormalEnd) * .3f : (a + b).magnitude * .02f;
                    if (score >= best) continue;
                    best = score; normal = aw; disrupted = bw; chosenY = a.y;
                }
            }
            if (float.IsPositiveInfinity(best)) return false;
            NormalEnd = normal; DisruptedEnd = disrupted; laneY = chosenY; hasLane = true;
            return true;
        }

        IEnumerator MoveTo(Vector3 target)
        {
            while (Player && body)
            {
                Vector3 delta = target - body.position; delta.y = 0f;
                if (delta.magnitude < field.Spacing * .16f) break;
                Vector3 direction = delta.normalized;
                Player.SetScriptedInput(new PlayerInputFrame {
                    moveInput = new Vector2(direction.x, direction.z) * stickStrength,
                    hasAimDirWS = true, aimDirWS = direction
                });
                yield return null;
            }
            if (Player) Player.SetScriptedInput(new PlayerInputFrame());
        }

        Vector3 CameraTarget() => (NormalEnd + DisruptedEnd) * .5f + Vector3.up * 25f;

        void LateUpdate()
        {
            if (Motor)
            {
                if (Motor.IsQuantized && !wasQuantized) DisruptionEntries++;
                if (!Motor.IsQuantized && wasQuantized) DisruptionExits++;
                wasQuantized = Motor.IsQuantized;
            }
            if (!viewport || !panelFrame || !previewCamera || !Player) return;
            if (!viewport.screenCamera) viewport.screenCamera = Camera.main;
            if (!viewport.screenCamera) return;
            Vector3 half = new Vector3(viewportSize.x, viewportSize.y, 0f) * .5f;
            viewport.lowerLeft = panelFrame.TransformPoint(-half);
            viewport.upperRight = panelFrame.TransformPoint(half);
            viewport.RefreshRect();
            previewCamera.orthographicSize = field.Spacing * 5f / Mathf.Max(.1f, previewCamera.aspect);
            previewCamera.transform.position = Vector3.SmoothDamp(previewCamera.transform.position, CameraTarget(), ref cameraVelocity, .8f);
            previewCamera.depth = viewport.screenCamera.depth + 1f;
            previewCamera.enabled = true;
        }

        void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (Player) Player.ClearScriptedInput();
            if (Motor) Motor.Release();
            if (field) field.NoiseLoopSeconds = 0f;
            if (session) { session.SetActive(false); Destroy(session); }
            if (previewCamera) previewCamera.enabled = false;
            session = null; Player = null; Motor = null; body = null;
            hasLane = wasQuantized = false; cameraVelocity = Vector3.zero;
            Phase = "Stopped";
        }
    }
}
