using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class GridInteractionSystem : MonoBehaviour
{
    static readonly HashSet<GridInteractor> _interactors = new HashSet<GridInteractor>();
    static readonly List<GridInteractor> _scratch = new List<GridInteractor>(64);

    // Pool per-grid force lists to avoid constant allocations when many interactors are active.
    static readonly Dictionary<VectorGridGPU, List<VectorGridGPU.Force>> _perGridForces = new();
    static readonly Stack<List<VectorGridGPU.Force>> _forceListPool = new();

    public static void Register(GridInteractor gi)
    {
        if (gi != null) _interactors.Add(gi);
    }

    public static void Unregister(GridInteractor gi)
    {
        if (gi != null) _interactors.Remove(gi);
    }

    void LateUpdate()
    {
        if (_interactors.Count == 0) return;

        // Return pooled lists from the previous frame.
        foreach (var kvp in _perGridForces)
        {
            kvp.Value.Clear();
            _forceListPool.Push(kvp.Value);
        }
        _perGridForces.Clear();

        _scratch.Clear();
        foreach (var gi in _interactors) _scratch.Add(gi);

        for (int i = 0; i < _scratch.Count; i++)
        {
            var gi = _scratch[i];
            if (gi == null || !gi.enabled || !gi.gameObject.activeInHierarchy) continue;

            var grid = gi.grid;
            if (grid == null) continue;

            if (!_perGridForces.TryGetValue(grid, out var forces))
            {
                forces = (_forceListPool.Count > 0) ? _forceListPool.Pop() : new List<VectorGridGPU.Force>(32);
                forces.Clear();
                _perGridForces[grid] = forces;
            }

            gi.EmitForces(forces);
        }

        if (_perGridForces.Count == 0) return;

        foreach (var pair in _perGridForces)
        {
            var grid = pair.Key;
            var forces = pair.Value;
            if (grid == null || forces.Count == 0) continue;

            for (int i = 0; i < forces.Count; i++)
                grid.AddForce(forces[i]);
        }
    }
}
