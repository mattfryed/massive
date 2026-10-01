using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class EnemySpawnTelegraph
    {
        private sealed class JoinedOutline
        {
            public Mesh mesh;
            public int users;
        }

        private static readonly Dictionary<Mesh, JoinedOutline> JoinedOutlines = new Dictionary<Mesh, JoinedOutline>();
        private Mesh _sourceOutline;
        private Mesh _ownedOutline;

        private void ReplaceOutline(Mesh mesh)
        {
            if (_ownedOutline == mesh) return;
            ReleaseOutlineJoins();
            if (_ownedOutline) { OutlineBounds.Remove(_ownedOutline); Destroy(_ownedOutline); }
            _ownedOutline = mesh;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            InitializeOutlineJoins();
            if (_diffuseTransform) _diffuseTransform.localPosition = DiffuseBounds().center;
            _ghostMesh = GetComponent<MeshFilter>().sharedMesh;
            foreach (var ghost in _ghosts)
                if (ghost != null && ghost.transform) ghost.transform.GetComponent<MeshFilter>().sharedMesh = _ghostMesh;
        }

        private void InitializeOutlineJoins()
        {
            var filter = GetComponent<MeshFilter>();
            _sourceOutline = filter.sharedMesh;
            if (!JoinedOutlines.TryGetValue(_sourceOutline, out var outline))
            {
                // The edge mesh contains four vertices per segment. Count each incident edge
                // once, not the duplicated quad vertices, so shared tips keep one edge's energy.
                var vertices = _sourceOutline.vertices;
                var degree = new Dictionary<Vector3, int>();
                for (int i = 0; i < vertices.Length; i += 4)
                {
                    CountJoin(degree, vertices[i]);
                    CountJoin(degree, vertices[i + 2]);
                }
                var joins = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i += 4)
                {
                    var pair = new Vector2(degree[vertices[i]], degree[vertices[i + 2]]);
                    for (int k = 0; k < 4; k++) joins[i + k] = pair;
                }
                var mesh = Instantiate(_sourceOutline);
                mesh.name = _sourceOutline.name + " (shared join weights)";
                mesh.uv2 = joins;
                outline = new JoinedOutline { mesh = mesh };
                JoinedOutlines.Add(_sourceOutline, outline);
            }
            outline.users++;
            filter.sharedMesh = outline.mesh;
        }

        private static void CountJoin(Dictionary<Vector3, int> degree, Vector3 vertex)
        {
            degree.TryGetValue(vertex, out int count);
            degree[vertex] = count + 1;
        }

        private void OnDestroy()
        {
            ReleaseOutlineJoins();
            if (_ownedOutline) { OutlineBounds.Remove(_ownedOutline); Destroy(_ownedOutline); }
        }
        private void ReleaseOutlineJoins()
        {
            if (!_sourceOutline || !JoinedOutlines.TryGetValue(_sourceOutline, out var outline)) return;
            if (--outline.users > 0) return;
            JoinedOutlines.Remove(_sourceOutline);
            Destroy(outline.mesh);
        }
    }
}
