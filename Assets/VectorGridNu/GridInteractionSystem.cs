using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class GridInteractionSystem : MonoBehaviour
{
    static readonly HashSet<GridInteractor> _interactors = new HashSet<GridInteractor>();
    static readonly List<GridInteractor> _scratch = new List<GridInteractor>(64);
    static readonly List<VectorGridGPU.Force> _forces = new List<VectorGridGPU.Force>(128);

    public static void Register(GridInteractor gi)  { if (gi != null) _interactors.Add(gi); }
    public static void Unregister(GridInteractor gi){ if (gi != null) _interactors.Remove(gi); }

    void LateUpdate()
    {
        if (_interactors.Count == 0) return;

        _forces.Clear();
        _scratch.Clear();
        foreach (var gi in _interactors) _scratch.Add(gi);

        VectorGridGPU grid = null;

        // Gather forces from all interactors
        for (int i = 0; i < _scratch.Count; i++)
        {
            var gi = _scratch[i];
            if (gi == null || !gi.enabled || !gi.gameObject.activeInHierarchy) continue;
            if (grid == null && gi.grid != null) grid = gi.grid;
            gi.EmitForces(_forces);
        }

        if (grid == null || _forces.Count == 0) return;

        // Upload by adding each force (VectorGridGPU batches internally)
        for (int i = 0; i < _forces.Count; i++)
            grid.AddForce(_forces[i]);
    }
}
