using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>Isolated instructions encounter using the canonical player and real portal.
    /// Only approach/departure input is scripted; transit, deformation and ripples are gameplay code.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class SingularityInstructionsDemo : MonoBehaviour
    {
        public SingularitySurface surface;
        public SingularityGridRenderer grid;
        public SingularityBlackHolePortal portal;
        public SingularityBlackHoleFeedback feedback;
        public GameObject playerPrefab;
        public Camera previewCamera;
        public Shader previewShader;
        public Transform panelFrame;
        public Vector2 viewportSize = new Vector2(7.37f, 3.9f);
        [Range(256, 2048)] public int previewResolution = 1024;
        [Min(5f)] public float approachDistance = 7f;
        [Min(0f)] public float pauseBetweenPasses = .8f;
        public float playerPlaneHeight = .2f;

        public PlayerControllerScript Player { get; private set; }
        public SingularityPlayerAdapter Actor { get; private set; }
        public int CompletedPasses { get; private set; }
        public int FaceTransfers { get; private set; }
        public string Phase { get; private set; } = "Inactive";

        private GameObject session;
        private Camera screenCamera;
        private RenderTexture previewTexture;
        private GameObject display;
        private Material displayMaterial;
        private Mesh displayMesh;
        private Vector2 center;
        private float wait;
        private bool departing, inTransit;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!surface || !grid || !portal || !playerPrefab || !previewCamera || !previewShader || !panelFrame)
            {
                Debug.LogError("SINGULARITY instructions need a surface, grid, portal, player and panel camera.", this);
                return;
            }
            previewCamera.enabled = false;
            BuildDisplay();
            CompletedPasses = FaceTransfers = 0;
            grid.enabled = true;
            grid.RefreshPresentation();
            var local = surface.transform.InverseTransformPoint(portal.transform.position);
            center = new Vector2(local.x, local.z);

            // Inactive parent prevents the canonical player's Awake/registration until it is scoped.
            session = new GameObject("SINGULARITY instructions actor (non-scoring)");
            session.SetActive(false);
            session.transform.SetParent(transform, false);
            var instance = Instantiate(playerPrefab, session.transform);
            instance.name = "Portal demonstration player";
            instance.transform.position = surface.transform.TransformPoint(new Vector3(-approachDistance, playerPlaneHeight, 0f));
            Player = instance.GetComponent<PlayerControllerScript>();
            Player.ConfigureDemonstration(session.transform, 0, 1);
            foreach (var interactor in instance.GetComponentsInChildren<GridInteractor>(true)) interactor.enabled = false;
            foreach (var pulse in instance.GetComponentsInChildren<Massive.Player.PlayerRepulsorGridPulse>(true)) pulse.enabled = false;
            foreach (var storm in instance.GetComponentsInChildren<PlayerStormPush>(true)) storm.enabled = false;
            foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            // Presentation actor has no physical contacts with the instructions scene.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Actor = instance.GetComponent<SingularityPlayerAdapter>();
            if (!Actor) Actor = instance.AddComponent<SingularityPlayerAdapter>();
            Actor.Configure(surface, grid);
            portal.Configure(surface, new[] { Actor });
            portal.TransitStarted += OnStarted;
            portal.FaceTransferred += OnTransferred;
            portal.TransitCompleted += OnCompleted;
            portal.enabled = true;
            if (feedback)
            {
                feedback.renderCamera = previewCamera;
                feedback.enableHaptics = false;
                feedback.Configure(portal, grid);
                feedback.enabled = true;
            }
            session.SetActive(true);
            BeginPass();
        }

        private void BeginPass()
        {
            bool rear = CompletedPasses % 2 != 0;
            float side = rear ? 1f : -1f;
            portal.CancelAll();
            Actor.ClearPortalVisual();
            Actor.SetSurfacePosition(portal.FaceToSurface(center + new Vector2(side * approachDistance, .15f), rear));
            Player.SetScriptedInput(PlayerInputFrame.Neutral);
            departing = inTransit = false;
            wait = pauseBetweenPasses;
            Phase = rear ? "Rear approach" : "Front approach";
        }

        private void Update()
        {
            if (!Player || !Actor) return;
            if (wait > 0f)
            {
                wait -= Time.deltaTime;
                Player.SetScriptedInput(PlayerInputFrame.Neutral);
                return;
            }
            if (inTransit)
            {
                Player.SetScriptedInput(PlayerInputFrame.Neutral);
                return;
            }
            if (!portal.TryGetFaceCoordinates(Actor.SurfacePosition, out var point, out bool rear)) return;
            Vector2 direction = departing ? (point - center).normalized : (center - point).normalized;
            // The existing controller consumes world-XZ input, just as it does during gameplay.
            Vector2 chart = portal.FaceVectorToChart(direction, rear);
            Vector3 world = surface.transform.TransformDirection(new Vector3(chart.x, 0f, chart.y));
            Player.SetScriptedInput(new PlayerInputFrame
            {
                moveInput = new Vector2(world.x, world.z).normalized,
                hasAimDirWS = true, aimDirWS = world.normalized
            });
            if (departing && (point - center).magnitude >= approachDistance && OutsideCrop())
            {
                CompletedPasses++;
                BeginPass(); // Both endpoints are offscreen; never reset a visible player.
            }
        }

        private bool OutsideCrop()
        {
            Vector3 p = previewCamera.WorldToViewportPoint(Actor.RenderWorldPosition);
            return p.x < -.1f || p.x > 1.1f || p.y < -.15f || p.y > 1.15f;
        }

        private void OnStarted(SingularityPlayerAdapter actor)
        { if (actor == Actor) { inTransit = true; Phase = "Entering"; } }
        private void OnTransferred(SingularityPlayerAdapter actor, bool rear)
        { if (actor == Actor) { FaceTransfers++; Phase = rear ? "Exiting to rear" : "Exiting to front"; } }
        private void OnCompleted(SingularityPlayerAdapter actor)
        { if (actor == Actor) { inTransit = false; departing = true; Phase = "Departing"; } }

        private void LateUpdate()
        {
            if (!session || !previewCamera) return;
            if (!screenCamera) screenCamera = Camera.main;
            if (!screenCamera) return;
            previewCamera.depth = screenCamera.depth - 1f;
            previewCamera.enabled = true;
        }

        private void BuildDisplay()
        {
            float aspect = Mathf.Max(.1f, viewportSize.x) / Mathf.Max(.1f, viewportSize.y);
            int width = Mathf.Clamp(previewResolution, 256, 2048);
            previewTexture = new RenderTexture(width, Mathf.Max(1, Mathf.RoundToInt(width / aspect)), 24,
                previewCamera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default)
            { name = "SINGULARITY instructions preview (owned)", hideFlags = HideFlags.DontSave, antiAliasing = 1 };
            previewTexture.Create();
            previewCamera.targetTexture = previewTexture;
            previewCamera.rect = new Rect(0f, 0f, 1f, 1f);
            previewCamera.aspect = aspect;
            display = new GameObject("Black-hole gameplay panel (owned)");
            display.transform.SetParent(panelFrame, false);
            // The panel is rotated onto XZ; negative local Z places the image above its fill.
            display.transform.localPosition = new Vector3(0f, 0f, -.01f);
            display.transform.localScale = new Vector3(viewportSize.x, viewportSize.y, 1f);
            displayMesh = new Mesh { name = "Instructions preview quad (owned)", hideFlags = HideFlags.DontSave };
            displayMesh.vertices = new[] { new Vector3(-.5f,-.5f,0f), new Vector3(.5f,-.5f,0f),
                new Vector3(.5f,.5f,0f), new Vector3(-.5f,.5f,0f) };
            displayMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            displayMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            displayMesh.RecalculateBounds();
            display.AddComponent<MeshFilter>().sharedMesh = displayMesh;
            displayMaterial = new Material(previewShader)
            { name = "Instructions preview material (owned)", hideFlags = HideFlags.DontSave, mainTexture = previewTexture };
            var renderer = display.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = displayMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDisable()
        {
            if (previewCamera) previewCamera.enabled = false;
            if (previewCamera) previewCamera.targetTexture = null;
            if (display) { display.SetActive(false); Destroy(display); }
            if (displayMaterial) Destroy(displayMaterial);
            if (displayMesh) Destroy(displayMesh);
            if (previewTexture) { previewTexture.Release(); Destroy(previewTexture); }
            display = null; displayMaterial = null; displayMesh = null; previewTexture = null;
            if (portal)
            {
                portal.TransitStarted -= OnStarted;
                portal.FaceTransferred -= OnTransferred;
                portal.TransitCompleted -= OnCompleted;
                portal.CancelAll();
                portal.players = new SingularityPlayerAdapter[0];
                portal.enabled = false;
            }
            if (feedback) feedback.enabled = false;
            if (Player) Player.ClearScriptedInput();
            if (session) { session.SetActive(false); Destroy(session); }
            session = null; Player = null; Actor = null;
            if (grid) { grid.ClearRipples(); grid.enabled = false; }
            Phase = "Inactive";
        }
    }
}
