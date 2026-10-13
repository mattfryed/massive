using Massive.Demonstrations;
using Massive.Player;
using Massive.Scoring;
using UnityEngine;

/// <summary>A full-scale HIGGS close-up with an isolated, gameplay-tuned player.</summary>
[DisallowMultipleComponent]
public sealed class HiggsInstructionsDemo : MonoBehaviour
{
    [Header("Gameplay presentation")]
    public VectorGridGPU grid;
    public HiggsFieldGPU field;
    public GameObject playerPrefab;
    public SymmetryKnotVisual knotTemplate;
    public Transform goalAnchor;
    public SymmetryKnotIncomeToast incomeToast;
    public PlayerDemoView viewport;
    public Transform panelFrame;
    public Vector2 viewportSize = new Vector2(7.37f, 3.9f);
    [Header("Read-only gameplay settings")]
    public SymmetryKnotController knotRules;
    public ScoreEconomyProfile economy;
    [Header("Loop")]
    [Min(4)] public float possessionSeconds = 7;
    [Range(.2f, 1)] public float stickStrength = .42f;
    public Vector3 approachOffset = new Vector3(2.6f, 0, -.35f);

    public PlayerControllerScript Player { get; private set; }
    public SymmetryKnotVisual Knot { get; private set; }
    public string Phase { get; private set; } = "Entering";
    public long DisplayedScore { get; private set; }
    public int CompletedLoops { get; private set; }
    public float CaptureProgress => capture != null ? capture.Hold01 : 0;

    private GameObject session;
    private Camera previewCamera;
    private Rigidbody body;
    private SymmetryKnotCaptureState capture;
    private Vector3 entryPosition;
    private float age, possessionAge;
    private long baseTick;
    private bool leaving, retiring;

    private void OnEnable()
    {
        previewCamera = viewport.GetComponent<Camera>();
        previewCamera.enabled = false;
        CompletedLoops = 0;
        if(economy && economy.TryGetReward(ScoreRewardKeys.HiggsControlTick, out var rule) && rule.enabled)
            baseTick = rule.BaseMilliElectronVolts;
        session = new GameObject("Isolated HIGGS player session");
        session.transform.SetParent(transform, false);
        session.SetActive(false);
        var actor = Instantiate(playerPrefab, session.transform);
        actor.name = "Demonstration player";
        entryPosition = knotTemplate.transform.position + approachOffset;
        actor.transform.position = entryPosition;
        Player = actor.GetComponent<PlayerControllerScript>();
        Player.ConfigureDemonstration(session.transform, 0, 1);
        actor.GetComponent<PlayerScaleAdjuster>()?.ApplyScale();
        foreach(var interactor in actor.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = grid;
        foreach(var pulse in actor.GetComponentsInChildren<PlayerRepulsorGridPulse>(true)) pulse.BindGrid(grid);
        body = actor.GetComponent<Rigidbody>();
        session.SetActive(true);
        ResetLoop();
    }

    private void ResetLoop()
    {
        age = possessionAge = 0;
        leaving = retiring = false;
        DisplayedScore = 0;
        Phase = "Entering";
        // Gameplay creates a fresh knot after its collapsing outro. Reusing the
        // collapsed Shapes rings retains their zero thickness on subsequent loops.
        if(Knot) { Knot.gameObject.SetActive(false); Destroy(Knot.gameObject); }
        Knot = Instantiate(knotTemplate, transform);
        Knot.name = "Symmetry knot visual";
        Knot.gameObject.SetActive(true);
        Knot.Initialize(knotTemplate.transform.position, knotRules.CaptureRadius);
        Knot.SetOwner(-1, null, false, 0);
        capture = knotRules.CreateCaptureState(Knot.SpawnFadeSeconds);
        incomeToast.Initialize(Knot.transform, knotRules.CaptureRadius, previewCamera);
        field.ReleaseReservedExcitation();
    }

    private void Update()
    {
        if(capture == null || !Player || !body) return;
        float dt = Time.deltaTime;
        age += dt;
        if(!field.HasReservedExcitation) field.TryReserveExcitation(Knot.transform.position, knotRules.CaptureRadius);

        Vector3 offset = Player.transform.position - Knot.transform.position;
        offset.y = 0;
        bool inside = offset.sqrMagnitude <= knotRules.CaptureRadius * knotRules.CaptureRadius;
        int ticks = capture.Tick(dt, inside ? 1 : 0, true);
        bool active = capture.Phase == SymmetryKnotCaptureState.Stage.Active;
        bool earning = active && capture.OwnerTeam > 0 && capture.Hold01 >= 1;
        if(earning && !leaving) possessionAge += dt;
        if(possessionAge >= possessionSeconds || !active && age > Knot.SpawnFadeSeconds) leaving = true;

        Vector3 target = leaving ? entryPosition : Knot.transform.position;
        Vector3 delta = target - body.position;
        delta.y = 0;
        Vector3 direction = delta.normalized;
        // Submit normal stick input to the same motor and rendering rig used in gameplay.
        Player.SetScriptedInput(new PlayerInputFrame {
            moveInput = age > .6f && delta.magnitude > .12f
                ? new Vector2(direction.x, direction.z) * stickStrength : Vector2.zero,
            hasAimDirWS = true, aimDirWS = direction.sqrMagnitude > 0 ? direction : Vector3.left
        });

        bool ending = capture.Phase == SymmetryKnotCaptureState.Stage.Retiring || capture.Phase == SymmetryKnotCaptureState.Stage.Finished;
        Knot.SetOwner(ending ? -1 : capture.OwnerTeam, capture.OwnerTeam > 0 ? goalAnchor : null, false, ending ? 0 : capture.Hold01);
        Phase = ending ? "Resetting" : leaving ? "Leaving" : !inside ? "Entering" : earning ? "Earning" : "Capturing";
        if(active && capture.OwnerTeam > 0 && capture.Hold01 < 1) incomeToast.ShowCapturing(1);
        else if(!earning) incomeToast.Hide();
        for(int i = 0; i < ticks; i++)
        {
            long amount = EnergyScoreMath.SaturatingScale(baseTick, knotRules.IncomeMultiplierAtTick(capture.FirstAwardTickIndex + i));
            DisplayedScore = EnergyScoreMath.SaturatingAdd(DisplayedScore, amount);
            // Presentation receipts never write to the match score authority.
            incomeToast.Show(new ScoreAwardResult {
                accepted = true, teamID = 1, quantity = 1, finalMilliElectronVolts = amount
            }, (float)capture.TickSeconds);
        }
        if(ending && !retiring)
        {
            retiring = true;
            Knot.BeginDespawn(knotRules.DespawnFadeSeconds);
            incomeToast.Hide();
        }
        field.SetReservedExcitationStrength(ending ? 0 : Mathf.SmoothStep(0, 1, age / Mathf.Max(.01f, Knot.SpawnFadeSeconds)));
        if(capture.Phase == SymmetryKnotCaptureState.Stage.Finished && delta.magnitude < .2f)
        {
            CompletedLoops++;
            ResetLoop();
        }
    }

    private void LateUpdate()
    {
        if(!viewport || !panelFrame || !previewCamera) return;
        if(!viewport.screenCamera) viewport.screenCamera = Camera.main;
        if(!viewport.screenCamera) return;
        Vector3 half = new Vector3(viewportSize.x, viewportSize.y, 0) * .5f;
        viewport.lowerLeft = panelFrame.TransformPoint(-half);
        viewport.upperRight = panelFrame.TransformPoint(half);
        viewport.RefreshRect();
        previewCamera.depth = viewport.screenCamera.depth + 1;
        previewCamera.enabled = true;
    }

    private void OnDisable()
    {
        if(previewCamera) previewCamera.enabled = false;
        if(incomeToast) incomeToast.HideImmediate();
        if(field) field.ReleaseReservedExcitation();
        if(Player) Player.ClearScriptedInput();
        if(session) { session.SetActive(false); Destroy(session); }
        if(Knot) { Knot.gameObject.SetActive(false); Destroy(Knot.gameObject); }
        session = null; Player = null; body = null; Knot = null; capture = null;
    }
}
