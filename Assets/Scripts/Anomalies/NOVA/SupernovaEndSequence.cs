using System.Collections;
using UnityEngine;

public class SupernovaEndSequence : MonoBehaviour
{
    public NovaStarController star;
    public GameObject sequenceRoot;
    public float endDelaySeconds = 2.5f;

    // optional: hook a particle system you want to explicitly Play()
    public ParticleSystem[] playOnStart;

    void OnEnable()
    {
        if (star != null)
            star.OnFinalSupernova += HandleFinalSupernova;
    }

    void OnDisable()
    {
        if (star != null)
            star.OnFinalSupernova -= HandleFinalSupernova;
    }

    void HandleFinalSupernova()
    {
        if (sequenceRoot != null)
            sequenceRoot.SetActive(true);

        if (playOnStart != null)
            foreach (var ps in playOnStart)
                if (ps != null) ps.Play(true);

        StartCoroutine(EndRoutine());
    }

    IEnumerator EndRoutine()
    {
        yield return new WaitForSeconds(endDelaySeconds);

        // TODO: call your real end condition:
        // Example:
        var gm = FindAnyObjectByType<GameManagerScript>();

        if (gm != null)
        {
            // replace with whatever your project uses
            // gm.EndMatch();
        }
    }
}
