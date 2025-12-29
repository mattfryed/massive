using UnityEngine;

public class SymmetryKnotSpawnTest : MonoBehaviour
{
    [SerializeField] private SymmetryKnotController knotPrefab;
    [SerializeField] private Vector3 spawnPos = new Vector3(0f, 0f, 0f);

    private void Start()
    {
        var knot = Instantiate(knotPrefab);
        knot.Initialize(spawnPos);
    }
}
