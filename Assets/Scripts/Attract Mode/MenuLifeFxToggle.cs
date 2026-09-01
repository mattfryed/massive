using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class MenuLifeFxToggle : MonoBehaviour
{
    [SerializeField] private MonoBehaviour fx; // must implement IPlayerLifeFx
    [SerializeField] private PlayerControllerScript player; // optional; can be null

    [Header("Timings")]
    [Min(0f)] [SerializeField] private float showDuration = 0.25f;
    [Min(0f)] [SerializeField] private float hideDuration = 0.20f;

    [Header("Behavior")]
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool animateHide = true;

    private IPlayerLifeFx _fx;
    private Coroutine _routine;
    private int _token;
    private bool _visible;

    private void Awake()
    {
        if (!player) player = GetComponent<PlayerControllerScript>();

        if (!fx) fx = GetComponent<PlayerLifeFx_DissolveGPU>();
        _fx = fx as IPlayerLifeFx;
    }

    private void Start()
    {
        // IMPORTANT: Start runs after all Awake() calls, so PlayerLifeFx can cache defaults safely.
        if (_fx == null) return;

        _visible = !startHidden;
        _fx.SetVisibleInstant(player, _visible);
    }

    public void Show(bool replayEvenIfVisible = false)
    {
        if (_fx == null) return;
        if (_visible && !replayEvenIfVisible) return;

        _visible = true;
        StartFx(_fx.PlayRespawn(player, showDuration));
    }

    public void Hide()
    {
        if (_fx == null) return;
        if (!_visible) return;

        _visible = false;

        if (!animateHide || hideDuration <= 0.001f)
        {
            StopFx();
            _fx.SetVisibleInstant(player, false);
            return;
        }

        StartFx(_fx.PlayDeath(player, hideDuration));
    }

    private void StartFx(IEnumerator routine)
    {
        _token++;
        int myToken = _token;

        StopFx();
        _routine = StartCoroutine(Run(routine, myToken));
    }

    private IEnumerator Run(IEnumerator routine, int myToken)
    {
        if (routine != null)
            yield return routine;

        // If another Show/Hide happened, ignore the completion of this one.
        if (myToken != _token) yield break;

        _routine = null;

        // Ensure final state matches our current intent.
        _fx.SetVisibleInstant(player, _visible);
    }

    private void StopFx()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private void OnDisable()
    {
        StopFx();
    }
}
