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

        _scratch.Clear();
        foreach (var gi in _interactors) _scratch.Add(gi);

        // Each interactor owns its grid. Never mix local coordinates from different grids.
        for (int i = 0; i < _scratch.Count; i++)
        {
            var gi = _scratch[i];
            if (gi == null || !gi.enabled || !gi.gameObject.activeInHierarchy) continue;
            _forces.Clear();
            gi.EmitForces(_forces);
            var grid = gi.grid; // EmitForces may resolve an unbound legacy interactor.
            if (!grid || !grid.isActiveAndEnabled) continue;
            for (int f = 0; f < _forces.Count; f++) grid.AddForce(_forces[f]);
        }
    }
}
