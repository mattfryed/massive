#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Massive.Enemies;
using UnityEngine;

namespace Massive.Orbital
{
    /// <summary>Opt-in checks using the real arena walls and pickup prefabs; never saved in a scene.</summary>
    public sealed class OrbitalPolishValidation : MonoBehaviour
    {
        public static string LastReport = "Not run";
        private readonly List<string> results = new List<string>();
        private readonly List<GameObject> fixtures = new List<GameObject>();
        private OrbitalProbabilityCloud hazard;
        private PlayerControllerScript player;
        private Rigidbody playerBody;
        private Vector3 oldPosition, oldVelocity;
        private float oldMass, oldRate;
        private bool restored;
        private const string Folder = "Assets/Scripts/Anomalies/ORBITAL/";
        private void Check(bool pass, string message) { results.Add((pass ? "PASS " : "FAIL ") + message); }
        private MatterNuggetScript Make(string path)
        {
            var go = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path));
            fixtures.Add(go); return go.GetComponent<MatterNuggetScript>();
        }

        private IEnumerator Start()
        {
            LastReport = "Running";
            yield return null; yield return null;
            hazard = FindFirstObjectByType<OrbitalProbabilityCloud>();
            while (!hazard.IsHazardActive) yield return null;
            oldRate = hazard.peakStrikesPerSecond; hazard.peakStrikesPerSecond = 0f;
            player = FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).First(p => !p.IsPseudoPlayer);
            playerBody = player.GetComponent<Rigidbody>(); oldPosition = playerBody.position;
            oldVelocity = playerBody.linearVelocity; oldMass = player.massScore;
            foreach (var n in hazard.GetComponentsInChildren<OrbitalMassNugget>()) n.gameObject.SetActive(false);
            try
            {
                foreach (string path in new[] { Folder + "Mass Nugglet.prefab", Folder + "Orbital Mass Nugget.prefab", "Assets/Prefabs/Matter nugget.prefab" })
                {
                    var pickup = Make(path); string label = pickup.name;
                    pickup.Eject(new Vector3(9f, 0f, 4f), Vector3.zero, 1f);
                    Check(pickup.VisualScale == 0f && pickup.GetComponentsInChildren<ParticleSystem>().All(p => p.sizeOverLifetime.x.constant == 0f), label + " begins with zero particle size");
                    yield return new WaitForSeconds(.08f);
                    Check(pickup.VisualScale > 0f && pickup.VisualScale < 1f, label + " eases in");
                    yield return new WaitForSeconds(.18f);
                    Check(pickup.VisualScale == 1f, label + " reaches full size");
                    yield return new WaitForSeconds(.65f);
                    Check(pickup && pickup.VisualScale > 0f && pickup.VisualScale < 1f, label + " eases out before expiry");
                    yield return new WaitForSeconds(.15f);
                    Check(!pickup || !pickup.gameObject.activeSelf, label + " completes expiry");
                    if (!pickup) pickup = Make(path);
                    foreach (Vector3 incoming in new[] { new Vector3(.12f, 0f, .07f), new Vector3(3f, 0f, 1.2f) })
                    {
                        pickup.Eject(new Vector3(12f, 0f, -3.5f), Vector3.zero, 3f);
                        var body = pickup.GetComponent<Rigidbody>(); body.linearDamping = 0f;
                        Physics.SyncTransforms();
                        var solid = pickup.GetComponentsInChildren<Collider>().First(c => !c.isTrigger);
                        float radius = solid.bounds.extents.x;
                        pickup.Eject(new Vector3(14f - radius - .001f, 0f, -3.5f), incoming, 3f);
                        Physics.SyncTransforms();
                        float deadline = Time.time + 1f;
                        while (body.linearVelocity.x >= 0f && Time.time < deadline) yield return new WaitForFixedUpdate();
                        Vector3 outgoing = body.linearVelocity;
                        Check(outgoing.x < 0f && Mathf.Abs(outgoing.x + incoming.x * pickup.wallRestitution) < .015f && Mathf.Abs(outgoing.z - incoming.z) < .015f,
                            label + " proportional wall bounce " + incoming + " -> " + outgoing);
                    }
                    Destroy(pickup.gameObject);
                }

                var nugglet = Make(Folder + "Mass Nugglet.prefab");
                playerBody.position = new Vector3(-8f, 0f, 0f); playerBody.linearVelocity = Vector3.zero;
                player.ApplyExternalMassDelta(player.massScoreMax - .005f - player.massScore, false);
                player.ExternalStun(2f); float before = player.massScore;
                nugglet.Eject(playerBody.position, Vector3.zero, 3f); Physics.SyncTransforms();
                float collectDeadline = Time.time + 1f;
                while (!nugglet.IsDespawning && Time.time < collectDeadline) yield return null;
                float gained = player.massScore - before;
                Check(nugglet.IsDespawning && gained > 0f && Mathf.Abs(gained - .005f) < .00001f, "Collection honors the player's remaining mass capacity");
                var toast = FindObjectsByType<EnemyScoreToast>(FindObjectsSortMode.None).FirstOrDefault(t => t.MassRestored > 0f);
                Check(toast && Mathf.Abs(toast.MassRestored - gained) < .000001f && toast.label.text.EndsWith("% MASS"), "Toast reports actual capped mass restored");
                Check(nugglet.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Collected pickup cannot reward twice during its exit");
                yield return new WaitForSeconds(.06f);
                Check(nugglet.VisualScale > 0f && nugglet.VisualScale < 1f, "Collection eases particle size out");
                yield return new WaitForSeconds(.15f);
                Check(!nugglet.gameObject.activeSelf && Mathf.Abs(player.massScore - before - gained) < .000001f, "Collection completes with one reward");
                nugglet.Eject(new Vector3(9f, 0f, 4f), Vector3.zero, 2f);
                Check(nugglet.VisualScale == 0f && nugglet.GetComponentsInChildren<Collider>().All(c => c.enabled), "Pooled reuse resets size and colliders");
                Check(EnemyScoreToast.ShowMass(nugglet.rewardToastPrefab, 0f, 1f, Vector3.zero, gameObject.scene) == null, "No positive toast for zero mass gained");
                yield return new WaitForSeconds(.25f);
                Vector3 retiredPosition = nugglet.transform.position;
                Vector3 nextPosition = new Vector3(7f, 0f, 4f);
                nugglet.EjectSmoothly(nextPosition, Vector3.zero, 2f);
                yield return new WaitForSeconds(.06f);
                Check(nugglet.IsDespawning && nugglet.VisualScale > 0f && nugglet.VisualScale < 1f && nugglet.transform.position == retiredPosition,
                    "Full-pool recycling shrinks the old pickup before relocation");
                yield return new WaitForSeconds(.13f);
                Check(!nugglet.IsDespawning && nugglet.transform.position == nextPosition && nugglet.VisualScale < 1f,
                    "Recycled pickup begins a new size entrance at the next contact");

                var cloud = hazard.cloud;
                playerBody.position = new Vector3(0f, 0f, 2.1f); playerBody.linearVelocity = Vector3.zero;
                player.ExternalStun(4f); Physics.SyncTransforms();
                cloud.Rebuild();
                var ps = cloud.transform.GetChild(cloud.transform.childCount - 1).GetComponent<ParticleSystem>();
                var samples = new ParticleSystem.Particle[cloud.particleCount];
                int count = ps.GetParticles(samples); Color32 purple = OrbitalCloudVisual.DensityColor(0f);
                Check(count == cloud.particleCount && samples.All(p => p.startSize == 0f && p.startColor.r == purple.r && p.startColor.g == purple.g && p.startColor.b == purple.b), "Cloud starts entirely at zero size and darkest purple");
                yield return new WaitForSeconds(.3f);
                ps.GetParticles(samples);
                Check(samples.All(p => p.startSize > 0f && p.startSize < cloud.pointSize.x), "Cloud entrance grows particles gradually");
                ScreenCapture.CaptureScreenshot("Assets/Screenshots/ORBITAL/ORBITAL-cloud-entrance.png");
                yield return new WaitForSeconds(1.2f);
                ps.GetParticles(samples);
                Check(samples.All(p => p.startSize >= cloud.pointSize.x) && samples.Any(p => p.startColor.g > 160), "Cloud reaches normal particle sizes and density palette");
                ScreenCapture.CaptureScreenshot("Assets/Screenshots/ORBITAL/ORBITAL-opaque-occlusion.png");

                hazard.impacts.Show(Vector3.zero, new Vector3(1.2f, .18f, 2.4f), Vector3.right, .8f);
                var lines = hazard.impacts.GetComponentsInChildren<LineRenderer>();
                Check(lines.Length == 3 && lines.All(l => l.widthMultiplier == 0f && l.startColor.a == 1f && l.endColor.a == 1f), "Impact starts at zero thickness with fully opaque colors");
                yield return new WaitForSeconds(hazard.impacts.flashSeconds * .28f);
                float peak = lines[0].widthMultiplier;
                Check(lines.All(l => l.widthMultiplier > 0f && l.startColor.a == 1f && l.endColor.a == 1f), "Electron, arrow and orbit grow without fading");
                ScreenCapture.CaptureScreenshot("Assets/Screenshots/ORBITAL/ORBITAL-opaque-impact.png");
                yield return new WaitForSeconds(hazard.impacts.flashSeconds * .5f);
                Check(lines[0].widthMultiplier < peak && lines.All(l => l.startColor.a == 1f), "Impact shrinks while remaining fully opaque");
                yield return new WaitForSeconds(hazard.impacts.flashSeconds * .28f);
                Check(lines.All(l => l.widthMultiplier == 0f && !l.gameObject.activeInHierarchy), "Impact finishes at zero thickness");
            }
            finally { Restore(); }
            LastReport = string.Join("\n", results); Debug.Log("[ORBITAL polish checks]\n" + LastReport); Destroy(gameObject);
        }
        private void Restore()
        {
            if (restored) return; restored = true;
            foreach (var go in fixtures) if (go) Destroy(go);
            if (hazard) hazard.peakStrikesPerSecond = oldRate;
            if (player) { player.ApplyExternalMassDelta(oldMass - player.massScore, false); player.lastActivityTime = Time.time; }
            if (playerBody) { playerBody.position = oldPosition; playerBody.linearVelocity = oldVelocity; }
        }
        private void OnDestroy() { Restore(); }
    }
}
#endif
