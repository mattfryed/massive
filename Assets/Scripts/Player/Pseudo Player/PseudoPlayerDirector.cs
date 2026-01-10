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

    [Header("Scripted Steps")]
    [SerializeField] private Step[] steps;

    [System.Serializable]
    public struct Step
    {
        public StepType type;

        [Tooltip("For Wait/Move/MoveTo: max duration (seconds). For taps: ignored.")]
        public float seconds;

        [Tooltip("For Move: (MoveH, MoveV) == (world X, world Z)")]
        public Vector2 move;

        [Tooltip("For MoveTo: move toward this target in XZ.")]
        public Transform target;

        [Tooltip("For MoveTo: stop when within this distance.")]
        public float stopDistance;

        [Tooltip("Optional mass tweak step.")]
        public float massDelta01;
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

    Coroutine _routine;

    void Reset()
    {
        player = GetComponent<PlayerControllerScript>();
    }

    void Awake()
    {
        if (!player) player = GetComponent<PlayerControllerScript>();
        if (player)
        {
            player.SetControlMode(PlayerControlMode.Scripted);

            // Strongly recommended: do NOT tag pseudo players as "Player"
            // unless you explicitly want them treated as gameplay players.
            // (Set the tag in the prefab/scene, not here.)
        }
    }

    void OnEnable()
    {
        if (!playOnEnable) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Run());
    }

    void OnDisable()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;

        if (player != null)
            player.ClearScriptedInput();
    }

    IEnumerator Run()
    {
        if (steps == null || steps.Length == 0 || player == null)
            yield break;

        do
        {
            for (int i = 0; i < steps.Length; i++)
            {
                yield return Execute(steps[i]);
            }
        }
        while (loop);
    }

    IEnumerator Execute(Step s)
    {
        switch (s.type)
        {
            case StepType.Wait:
                yield return HoldInput(Vector2.zero, s.seconds);
                break;

            case StepType.Move:
                yield return HoldInput(s.move, s.seconds);
                break;

            case StepType.MoveTo:
                yield return MoveTo(s.target, s.stopDistance, s.seconds);
                break;

            case StepType.AttackTap:
                yield return TapAttack();
                break;

            case StepType.ShieldTap:
                yield return TapShield();
                break;

            case StepType.MassDeltaNoDeath:
                // Safe for instruction screens:
                // avoids triggering the full death/respawn sequence.
                if (Mathf.Abs(s.massDelta01) > 0.0001f)
                    player.ApplyExternalMassDelta(s.massDelta01, allowDeath: false);

                // give one frame for visuals/nuggets to respond
                yield return null;
                break;
        }
    }

    IEnumerator HoldInput(Vector2 move, float seconds)
    {
        float t = 0f;
        seconds = Mathf.Max(0f, seconds);

        while (t < seconds)
        {
            t += Time.deltaTime;
            PushFrame(new PlayerInputFrame { move = move });
            yield return null;
        }

        // release
        PushFrame(default);
        yield return null;
    }

    IEnumerator MoveTo(Transform target, float stopDistance, float maxSeconds)
    {
        if (target == null)
        {
            yield return HoldInput(Vector2.zero, maxSeconds);
            yield break;
        }

        stopDistance = Mathf.Max(0.01f, stopDistance);
        maxSeconds = Mathf.Max(0.01f, maxSeconds);

        float t = 0f;
        while (t < maxSeconds)
        {
            t += Time.deltaTime;

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

    IEnumerator TapAttack()
    {
        // Frame 1: Down + Held
        PushFrame(new PlayerInputFrame { attackDown = true, attackHeld = true });
        yield return null;

        // Frame 2: Up pulse
        PushFrame(new PlayerInputFrame { attackUp = true });
        yield return null;

        // Clear
        PushFrame(default);
        yield return null;
    }

    IEnumerator TapShield()
    {
        // ShieldAbility uses "down" to activate (timed), so one frame is enough.
        PushFrame(new PlayerInputFrame { shieldDown = true });
        yield return null;

        PushFrame(default);
        yield return null;
    }

    void PushFrame(in PlayerInputFrame frame)
    {
        if (player == null) return;
        player.SetScriptedInput(frame);
    }
}
