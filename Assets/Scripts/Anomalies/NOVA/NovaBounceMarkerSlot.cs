using UnityEngine;

public class NovaBounceMarkerSlot : MonoBehaviour
{
    public enum State { Future, Current, Past }

    [Header("Normal bounce variants")]
    [SerializeField] private GameObject futureBounce;
    [SerializeField] private GameObject currentBounce;
    [SerializeField] private GameObject pastBounce;

    [Header("Special final marker")]
    [SerializeField] private GameObject supernova;

    [Tooltip("If true, this slot always shows the Supernova visual.")]
    [SerializeField] private bool isSupernovaSlot;

    private State _state = State.Future;

    public void SetIsSupernovaSlot(bool value)
    {
        isSupernovaSlot = value;
        Apply();
    }

    public void SetState(State state)
    {
        _state = state;
        Apply();
    }

    private void Apply()
    {
        if (isSupernovaSlot)
        {
            SetActive(supernova, true);
            SetActive(futureBounce, false);
            SetActive(currentBounce, false);
            SetActive(pastBounce, false);
            return;
        }

        SetActive(supernova, false);
        SetActive(futureBounce,  _state == State.Future);
        SetActive(currentBounce, _state == State.Current);
        SetActive(pastBounce,    _state == State.Past);
    }

    private static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on)
            go.SetActive(on);
    }
}
