#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    public static class SingularityAdapterValidation
    {
        [MenuItem("MASSIVE/SINGULARITY/Validate Real Player Adapter")]
        public static string Run()
        {
            int checks = 0;
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                // Shader failures may only become visible when Unity first draws
                // a pass, so C# compilation/Console checks alone are insufficient.
                CheckShader("MASSIVE/PlayerBlobVector", ref checks);
                CheckShader("MASSIVE/NuggetInstanced", ref checks);
                CheckShader("MASSIVE/AttackTrailInstanced", ref checks);
                var surfaceObject = new GameObject("Adapter validation surface");
                SceneManager.MoveGameObjectToScene(surfaceObject, scene);
                var surface = surfaceObject.AddComponent<SingularitySurface>();
                var actor = new GameObject("Adapter validation real controller");
                SceneManager.MoveGameObjectToScene(actor, scene);
                actor.transform.position = new Vector3(3f, .2f, -2f);
                var body = actor.AddComponent<Rigidbody>(); body.useGravity = false;
                var player = actor.AddComponent<PlayerControllerScript>();
                var adapter = actor.AddComponent<SingularityPlayerAdapter>();
                adapter.Configure(surface);
                Vector2 start = adapter.SurfacePosition;
                Check(Mathf.Abs(start.x - 3f) < .0001f, "Front chart preserves X", ref checks);
                Check(Mathf.Abs(start.y - (surface.FrontHeight * .5f - 2f)) < .0001f, "Front chart preserves original Z", ref checks);
                Check(adapter.Surface == surface && adapter.Player == player, "References retain actual player", ref checks);
                Check(Vector3.Distance(actor.transform.position, new Vector3(3f, .2f, -2f)) < .0001f, "Configuration does not move physics", ref checks);
                Vector3 frontRender = adapter.RenderWorldPosition;
                Check(Mathf.Abs(frontRender.x - 3f) < .0001f && Mathf.Abs(frontRender.z + 2f) < .0001f, "Front projected position", ref checks);
                player.massScore = .63f;
                int deaths = 0;
                player.DeathResolved += _ => deaths++;
                body.linearVelocity = new Vector3(1f, 0f, 2f);
                Check(adapter.TeleportToOppositeFace(), "Front portal succeeds", ref checks);
                Vector2 rear = adapter.SurfacePosition;
                Check(Mathf.Abs(rear.x - start.x) < .0001f, "Teleport retains normalized across coordinate", ref checks);
                Check(rear.y >= surface.RearStart && rear.y <= surface.BottomStart, "Teleport reaches rear face", ref checks);
                Check(Mathf.Abs(adapter.RearWeight - 1f) < .0001f, "Rear styling selection", ref checks);
                Check(Mathf.Abs(body.linearVelocity.x - 1f) < .0001f && Mathf.Abs(body.linearVelocity.z + 2f) < .0001f, "Teleport preserves screen direction", ref checks);
                Check(Mathf.Abs(adapter.RenderWorldPosition.x - frontRender.x * surface.RearScale) < .001f, "Rear projection scales X", ref checks);
                Check(Mathf.Abs(adapter.RenderWorldPosition.z - frontRender.z * surface.RearScale) < .001f, "Rear projection scales Z", ref checks);
                Check(Mathf.Abs(adapter.RenderWorldPosition.y - (frontRender.y - surface.Depth)) < .001f, "Rear render has real depth", ref checks);
                Check(Mathf.Abs(actor.transform.position.z + 2f) > surface.FrontHeight, "Overlapping screen positions have separate physics", ref checks);
                Check(Mathf.Abs(player.massScore - .63f) < .0001f && deaths == 0 && !player.temporarilyEliminated, "Portal does not damage, kill, or reset integrity", ref checks);
                Check(Vector2.Distance(adapter.SmoothedAttractionPosition, rear) < .0001f, "Portal snaps attraction, no trail through field", ref checks);
                Check(adapter.TeleportToOppositeFace(), "Rear portal succeeds", ref checks);
                Check(Vector2.Distance(adapter.SurfacePosition, start) < .001f, "Portal round trip returns exact chart position", ref checks);
                Check(Mathf.Abs(body.linearVelocity.z - 2f) < .0001f, "Portal round trip restores velocity", ref checks);
                player.temporarilyEliminated = true;
                Check(!adapter.TeleportToOppositeFace() && !adapter.IsAttractionActive, "Eliminated players are not transferred or attracting", ref checks);
                player.temporarilyEliminated = false;
                adapter.SetSurfacePosition(new Vector2(0f, (surface.TopStart + surface.RearStart) * .5f));
                Check(!adapter.TeleportToOppositeFace(), "Turns do not ambiguously portal", ref checks);
                adapter.SetSurfacePosition(new Vector2(1f, surface.LoopLength + 2f));
                Check(Mathf.Abs(adapter.SurfacePosition.y - 2f) < .0001f, "Coordinates wrap at periodic seam", ref checks);
                Check(body.linearVelocity == Vector3.zero, "Explicit placement clears velocity", ref checks);
                Vector2 beforeInvalid = adapter.SurfacePosition;
                adapter.SetSurfacePosition(new Vector2(float.NaN, 0f));
                Check(adapter.SurfacePosition == beforeInvalid, "Invalid coordinates rejected", ref checks);
                var properties = new MaterialPropertyBlock();
                adapter.ApplyRenderProperties(properties, Matrix4x4.identity, Matrix4x4.identity);
                Check(properties.GetFloat("_SingularityEnabled") == 0f, "Missing grid lookup uses safe original rendering", ref checks);
                adapter.enabled = false;
                Check(!adapter.IsAttractionActive && !adapter.IsRenderingOnSurface, "Disabled adapter cannot render or attract", ref checks);
                string report = "SINGULARITY real-player adapter: PASS " + checks + " checks (logical chart, exact portal mapping, velocity, no damage, depth and lifecycle gates).";
                Debug.Log(report); return report;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static void Check(bool condition, string message, ref int checks)
        { if (!condition) throw new InvalidOperationException("SINGULARITY adapter: " + message); checks++; }
        private static void CheckShader(string name, ref int checks)
        {
            Shader shader = Shader.Find(name);
            Check(shader != null, "Required shader exists: " + name, ref checks);
            Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Required shader compiles: " + name, ref checks);
        }
    }
}
#endif
