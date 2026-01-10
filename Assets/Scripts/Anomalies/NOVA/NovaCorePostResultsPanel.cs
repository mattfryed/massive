using UnityEngine;

public class NovaCorePostResultsPanel : MonoBehaviour
{
    [Header("Root / Container")]
    [SerializeField] private GameObject root;          // enable/disable whole panel
    [SerializeField] private Transform contentParent;  // where prefabs are instantiated

    [Header("Prefabs")]
    [SerializeField] private NovaCoreTeamResultView lightTeamPrefab;
    [SerializeField] private NovaCoreTeamResultView darkTeamPrefab;

    [Header("Team Labels")]
    [SerializeField] private string lightTeamName = "LIGHT";
    [SerializeField] private string darkTeamName = "DARK";

    [Header("Timing")]
    [Min(0f)]
    [SerializeField] private float showSeconds = 2.5f;
    public float ShowSeconds => showSeconds;

    private NovaCoreTeamResultView _lightInstance;
    private NovaCoreTeamResultView _darkInstance;

    public void Show(
        int lightParticles, float lightScoreAwarded01,
        int darkParticles,  float darkScoreAwarded01)
    {
        if (root != null) root.SetActive(true);

        ClearInstances();

        Transform parent = contentParent != null ? contentParent : transform;

        if (lightTeamPrefab != null)
        {
            _lightInstance = Instantiate(lightTeamPrefab, parent);
            _lightInstance.Set(lightTeamName, lightParticles, lightScoreAwarded01);
        }

        if (darkTeamPrefab != null)
        {
            _darkInstance = Instantiate(darkTeamPrefab, parent);
            _darkInstance.Set(darkTeamName, darkParticles, darkScoreAwarded01);
        }
    }

    public void Hide()
    {
        ClearInstances();
        if (root != null) root.SetActive(false);
    }

    private void ClearInstances()
    {
        if (_lightInstance != null) Destroy(_lightInstance.gameObject);
        if (_darkInstance != null) Destroy(_darkInstance.gameObject);
        _lightInstance = null;
        _darkInstance = null;
    }
}
