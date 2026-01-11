using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-200)] // run before PlayerControllerScript.Update()
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerControllerScript))]
public class PseudoPlayerDirector : MonoBehaviour
{
    [SerializeField] private PlayerControllerScript player;

    [Header("Playback")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop = true;

    [Tooltip("Recommended ON for instruction screens (ignores Time.timeScale).")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Tap Behavior")]
    [Tooltip("Extra delay after taps. Helps ensure the next step can't accidentally buffer into a combo.")]
    [SerializeField] private float defaultPostTapDelay = 0.05f;

    [Tooltip("If true, AttackTap waits until the current attack sequence finishes before advancing.")]
    [SerializeField] private bool waitForAttackToFinish = true;

    [Tooltip("Safety timeout so the director can't hang forever if attackController is miswired.")]
    [SerializeField] private float attackFinishTimeoutSeconds = 3.0f;

    [Header("Scripted Steps (executed in inspector order)")]
    [SerializeField] private Step[] steps;

    [Serializable]
    public struct Step
    {
        public StepType type;

        [Tooltip(
            "Wait/Move: duration.\n" +
            "MoveTo: max duration.\n" +
            "AttackTap/ShieldTap: optional post-tap delay override (<=0 uses Default Post Tap Delay)."
        )]
        public float seconds;

        [Tooltip("Move: (MoveH, MoveV) == (world X, world Z)")]
        public Vector2 move;

        [Tooltip("MoveTo: move toward this target in XZ.")]
        public Transform target;

        [Tooltip("MoveTo: stop when within this distance.")]
        public float stopDistance;

        [Tooltip("Mass delta for MassDeltaNoDeath.")]
        public float massDelta01;

        [Header("External actions (optional, run during this step)")]
        public ExternalAction[] external;
    }

    public enum StepType
    {
        Wait,
        Move,
        MoveTo,
        AttackTap,
        ShieldTap,
        MassDeltaNoDeath
    }

    [Serializable]
    public struct ExternalAction
    {
        [Tooltip("The external GameObject to manipulate.")]
        public GameObject target;

        [Header("Enable for X seconds")]
        [Tooltip("Enable this object at step start.")]
        public bool enableOnStart;

        [Tooltip("If > 0, disable after this many seconds (counted from step start).")]
        public float enableSeconds;

        [Header("Translate with easing")]
        public bool translate;

        [Tooltip("If true, uses localPosition; else uses world position.")]
        public bool localSpace;

        [Tooltip("Offset to translate by (local or world depending on Local Space).")]
        public Vector3 offset;

        [Tooltip("Seconds to move from start -> start+offset.")]
        public float translateSeconds;

        [Tooltip("0..1 -> 0..1 curve. If empty, linear is used.")]
        public AnimationCurve ease;

        [Header("Reverse translation (optional)")]
        public bool reverse;

        [Tooltip("Seconds to wait at the end position before reversing.")]
        public float reverseDelay;

        [Tooltip("Seconds to move back. If <=0, uses Translate Seconds.")]
        public float reverseSeconds;

        [Tooltip("If empty, uses Ease (or linear).")]
        public AnimationCurve reverseEase;
    }

    private Coroutine _routine;

    private float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    void Reset()
    {
        player = GetComponent<PlayerControllerScript>();
    }

    void Awake()
    {
        if (!player) player = GetComponent<PlayerControllerScript>();
        if (player != null)
        {
            player.SetControlMode(PlayerControlMode.Scripted);
        }
    }

    void OnEnable()
    {
        if (!playOnEnable) return;
        Play();
    }

    void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        Stop(); // ensures we don't double-run
        if (player == null || steps == null || steps.Length == 0) return;
        _routine = StartCoroutine(Run());
    }

    public void Stop()
    {
        StopAllCoroutines();
        _routine = null;

        if (player != null)
            player.ClearScriptedInput();
    }

    private IEnumerator Run()
    {
        do
        {
            for (int i = 0; i < steps.Length; i++)
            {
                yield return ExecuteStep(steps[i]);
            }
        }
        while (loop);

        PushFrame(default);
        yield return null;
    }

    private IEnumerator ExecuteStep(Step s)
    {
        // Always begin step neutral for 1 frame (prevents edge-pulse leakage between steps)
        PushFrame(default);
        yield return null;

        // Launch external actions concurrently, but DO NOT advance to next step
        // until they complete (keeps everything sequential).
        int externalRunning = 0;

        if (s.external != null && s.external.Length > 0)
        {
            for (int i = 0; i < s.external.Length; i++)
            {
                if (s.external[i].target == null) continue;

                externalRunning++;
                var actionCopy = s.external[i]; // avoid modified-closure gotchas
                StartCoroutine(ExternalActionRoutine(actionCopy, () => externalRunning--));
            }
        }

        switch (s.type)
        {
            case StepType.Wait:
                yield return HoldMove(Vector2.zero, s.seconds);
                break;

            case StepType.Move:
                yield return HoldMove(s.move, s.seconds);
                break;

            case StepType.MoveTo:
                yield return MoveTo(s.target, s.stopDistance, s.seconds);
                break;

            case StepType.AttackTap:
                yield return TapAttack(s.seconds);
                break;

            case StepType.ShieldTap:
                yield return TapShield(s.seconds);
                break;

            case StepType.MassDeltaNoDeath:
                if (player != null && Mathf.Abs(s.massDelta01) > 0.0001f)
                    player.ApplyExternalMassDelta(s.massDelta01, allowDeath: false);
                yield return null;
                break;
        }

        // Wait external actions (sequential step guarantee)
        while (externalRunning > 0)
            yield return null;

        // End step neutral for 1 frame
        PushFrame(default);
        yield return null;
    }

    private IEnumerator HoldMove(Vector2 move, float seconds)
    {
        seconds = Mathf.Max(0f, seconds);

        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            PushFrame(new PlayerInputFrame { move = move });
            yield return null;
        }

        PushFrame(default);
        yield return null;
    }

    private IEnumerator MoveTo(Transform target, float stopDistance, float maxSeconds)
    {
        stopDistance = Mathf.Max(0.01f, stopDistance);
        maxSeconds = Mathf.Max(0.01f, maxSeconds);

        if (player == null || target == null)
        {
            yield return HoldMove(Vector2.zero, maxSeconds);
            yield break;
        }

        float t = 0f;
        while (t < maxSeconds)
        {
            t += Dt;

            Vector3 here = player.transform.position;
            Vector3 dst = target.position;

            Vector2 to = new Vector2(dst.x - here.x, dst.z - here.z);
            float d = to.magnitude;

            if (d <= stopDistance)
                break;

            Vector2 dir = to / Mathf.Max(0.0001f, d);
            PushFrame(new PlayerInputFrame { move = dir });

            yield return null;
        }

        PushFrame(default);
        yield return null;
    }

    private IEnumerator TapAttack(float postDelayOverride)
    {
        // One-frame press only (fires once)
        PushFrame(new PlayerInputFrame { attackDown = true });
        yield return null;

        PushFrame(default);
        yield return null;

        // Prevent "AttackTap > MoveTo > AttackTap" from buffering combos unintentionally
        if (waitForAttackToFinish && player != null && player.attackController != null)
        {
            float timeout = Mathf.Max(0.05f, attackFinishTimeoutSeconds);
            float t = 0f;

            while (player.attackController.IsAttacking && t < timeout)
            {
                t += Dt;
                yield return null;
            }
        }

        float post = (postDelayOverride > 0f) ? postDelayOverride : defaultPostTapDelay;
        if (post > 0f)
            yield return WaitTime(post);
    }

    private IEnumerator TapShield(float postDelayOverride)
    {
        // One-frame down pulse only (fires once)
PushFrame(new PlayerInputFrame { shieldDown = true, shieldHeld = true });
yield return null;
PushFrame(default);
yield return null;

        float post = (postDelayOverride > 0f) ? postDelayOverride : defaultPostTapDelay;
        if (post > 0f)
            yield return WaitTime(post);
    }

    private IEnumerator ExternalActionRoutine(ExternalAction a, Action onDone)
    {
        try
        {
            if (a.target == null) yield break;

            Transform tr = a.target.transform;

            // Enable
            float enableStartTime = 0f;
            bool shouldDisable = a.enableOnStart && a.enableSeconds > 0f;

            if (a.enableOnStart)
            {
                a.target.SetActive(true);
                enableStartTime = useUnscaledTime ? Time.unscaledTime : Time.time;
            }

            // Translate
            if (a.translate && a.offset != Vector3.zero)
            {
                float dur = Mathf.Max(0.0001f, a.translateSeconds);

                Vector3 start = a.localSpace ? tr.localPosition : tr.position;
                Vector3 end = start + a.offset;

                yield return LerpPosition(tr, start, end, dur, a.ease, a.localSpace);

                if (a.reverse)
                {
                    if (a.reverseDelay > 0f)
                        yield return WaitTime(a.reverseDelay);

                    float backDur = (a.reverseSeconds > 0f) ? a.reverseSeconds : dur;
                    var backEase = (a.reverseEase != null && a.reverseEase.length > 0) ? a.reverseEase : a.ease;

                    yield return LerpPosition(tr, end, start, Mathf.Max(0.0001f, backDur), backEase, a.localSpace);
                }
            }

            // Disable after enableSeconds (counted from step start)
            if (shouldDisable)
            {
                float now = useUnscaledTime ? Time.unscaledTime : Time.time;
                float elapsed = now - enableStartTime;
                float remaining = a.enableSeconds - elapsed;

                if (remaining > 0f)
                    yield return WaitTime(remaining);

                if (a.target != null)
                    a.target.SetActive(false);
            }
        }
        finally
        {
            onDone?.Invoke();
        }
    }

    private IEnumerator WaitTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        float t = 0f;

        while (t < seconds)
        {
            t += Dt;
            yield return null;
        }
    }

    private IEnumerator LerpPosition(Transform tr, Vector3 from, Vector3 to, float seconds, AnimationCurve curve, bool localSpace)
    {
        seconds = Mathf.Max(0.0001f, seconds);

        float t = 0f;
        while (t < seconds)
        {
            t += Dt;
            float u = Mathf.Clamp01(t / seconds);
            float eased = EvaluateCurve(curve, u);

            Vector3 p = Vector3.LerpUnclamped(from, to, eased);
            if (tr != null)
            {
                if (localSpace) tr.localPosition = p;
                else tr.position = p;
            }

            yield return null;
        }

        if (tr != null)
        {
            if (localSpace) tr.localPosition = to;
            else tr.position = to;
        }
    }

    private static float EvaluateCurve(AnimationCurve curve, float u)
    {
        if (curve == null || curve.length == 0)
            return u; // linear fallback

        return curve.Evaluate(u);
    }

    private void PushFrame(in PlayerInputFrame frame)
    {
        if (player == null) return;
        player.SetScriptedInput(frame);
    }
}
