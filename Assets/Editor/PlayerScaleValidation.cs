#if UNITY_EDITOR
using System;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.EditorTools
{
    /// <summary>Isolated geometry and policy checks; never activates gameplay or edits an authored asset.</summary>
    public static class PlayerScaleValidation
    {
        [MenuItem("MASSIVE/Player/Validate Player Scaling")]
        public static void RunMenu()
        {
            Debug.Log(RunChecks());
        }

        public static string RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run player scaling validation in Edit Mode.");

            int passed = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new InvalidOperationException("Player scaling: " + message);
                passed++;
            };
            Action<float, float, string> near = (actual, expected, message) =>
                check(Mathf.Abs(actual - expected) <= 0.0001f,
                    message + " (expected " + expected + ", got " + actual + ")");

            Scene fixture = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject parent = Make("Player scaling validation — inert fixture", fixture);
                parent.transform.position = new Vector3(12f, 3f, -9f);
                parent.transform.localScale = Vector3.one * 2f;
                parent.transform.rotation = Quaternion.Euler(0f, 31f, 0f);

                GameObject body = Make("Player body", fixture);
                body.transform.SetParent(parent.transform, false);
                body.transform.localPosition = new Vector3(1f, 0f, -2f);
                SphereCollider collider = body.AddComponent<SphereCollider>();
                collider.radius = 0.8f;
                collider.center = new Vector3(0.1f, 0f, 0.2f);
                Vector3 authoredCenter = collider.center;

                // This component has no ExecuteAlways/RequireComponent side effects. Keeping
                // the entire hierarchy inactive prevents Awake, gameplay input and registration.
                PlayerControllerScript player = body.AddComponent<PlayerControllerScript>();
                player.enabled = false;

                GameObject visual = Make("Attached visual", fixture);
                visual.transform.SetParent(body.transform, false);
                visual.transform.localPosition = new Vector3(0.7f, 0.1f, 0f);
                visual.transform.localScale = Vector3.one * 0.6f;

                GameObject particleObject = Make("Attached native particles", fixture);
                // Preview scenes are never saved. Ordinary flags model an authored particle
                // system, whereas DontSave marks generated systems excluded by the adjuster.
                particleObject.hideFlags = HideFlags.None;
                particleObject.transform.SetParent(body.transform, false);
                ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Local;
                main.startSize = 0.23f;
                main.startSpeed = 1.7f;
                main.startLifetime = 0.9f;
                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.65f;
                var emission = particles.emission;
                emission.enabled = false;

                PlayerScaleAdjuster scale = body.AddComponent<PlayerScaleAdjuster>();
                check(scale.ScaleActionReach, "action reach should follow size by default");
                check(!scale.ScaleMovement, "movement speed should remain independent by default");
                check(!scale.ScaleProjectileRange, "projectile range should remain independent by default");

                // Exercise reversals and repeated applications: a relative multiply would drift.
                float[] sizes = { 1f, 0.5f, 0.25f, 2f, 0.5f, 1f, 2f, 0.25f, 1f };
                foreach (float size in sizes)
                {
                    scale.Size = size;
                    scale.ApplyScale();
                    scale.ApplyScale();
                    near(body.transform.localScale.x, size, "local scale after repeated changes");
                    near(body.transform.localScale.y, size, "uniform local Y scale");
                    near(body.transform.localScale.z, size, "uniform local Z scale");
                    near(body.transform.lossyScale.x, size * 2f, "uniform parent contributes once");
                    near(collider.radius, 0.8f, "body collider authored radius must not compound");
                    check(collider.center == authoredCenter, "body collider authored center changed");
                    near(Vector3.Distance(body.transform.position, visual.transform.position),
                        new Vector3(0.7f, 0.1f, 0f).magnitude * size * 2f,
                        "child placement follows player size once");
                    near(visual.transform.localScale.x, 0.6f, "child authored scale must stay intact");
                    near(PlayerScaleAdjuster.SizeOf(visual.transform), size * 2f, "child resolves the owning player world scale");
                    near(PlayerScaleAdjuster.ActionReachOf(player), size * 2f, "default action reach follows world size");
                    near(PlayerScaleAdjuster.MovementOf(player), 1f, "default movement is world sized");
                    near(PlayerScaleAdjuster.ProjectileReachOf(player), 1f, "default projectile range is world sized");
                    near(PlayerScaleAdjuster.BodyRadiusOf(player), 0.8f * size * 2f,
                        "body radius honors the uniform parent");
                }

                scale.Size = 0.25f;
                scale.ScaleActionReach = false;
                scale.ScaleMovement = true;
                scale.ScaleProjectileRange = true;
                scale.ApplyScale();
                near(PlayerScaleAdjuster.ActionReachOf(player), 1f, "action reach opt-out");
                near(PlayerScaleAdjuster.MovementOf(player), 0.5f, "movement scaling opt-in");
                near(PlayerScaleAdjuster.ProjectileReachOf(player), 0.5f, "projectile range scaling opt-in");
                scale.ScaleActionReach = true;
                scale.ScaleMovement = false;
                scale.ScaleProjectileRange = false;
                scale.ApplyScale();
                near(PlayerScaleAdjuster.ActionReachOf(player), 0.5f, "action policy can be restored without drift");
                near(PlayerScaleAdjuster.MovementOf(player), 1f, "movement policy can be restored");
                near(PlayerScaleAdjuster.ProjectileReachOf(player), 1f, "projectile policy can be restored");

                collider.enabled = false;
                near(PlayerScaleAdjuster.BodyRadiusOf(player), 0.4f,
                    "disabled body retains a usable radius for respawn clearance");
                check(!body.activeInHierarchy, "fixture must remain inert throughout validation");
                check(body.transform.localPosition == new Vector3(1f, 0f, -2f), "size adjustment moved the player");
                near(parent.transform.localScale.x, 2f, "size adjustment modified the parent");

                main = particles.main;
                shape = particles.shape;
                check(main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                    "attached native particles must use the same hierarchy scale");
                near(main.startSize.constant, 0.23f, "native particle authored size must not compound");
                near(main.startSpeed.constant, 1.7f, "native particle authored speed must not compound");
                near(main.startLifetime.constant, 0.9f, "native particle lifetime must remain unchanged");
                near(shape.radius, 0.65f, "native emitter authored radius must not compound");

                GameObject plain = Make("No adjuster", fixture);
                near(PlayerScaleAdjuster.SizeOf(plain.transform), 1f, "missing helper uses neutral size");
                near(PlayerScaleAdjuster.ActionReachOf(plain.transform), 1f, "missing helper uses neutral action range");
                near(PlayerScaleAdjuster.MovementOf(plain.transform), 1f, "missing helper uses neutral movement");
                near(PlayerScaleAdjuster.ProjectileReachOf(plain.transform), 1f, "missing helper uses neutral projectile range");
                near(PlayerScaleAdjuster.SizeOf(null), 1f, "null owner uses neutral size");

                GameObject authoredLarge = Make("Authored non-unit player scale", fixture);
                authoredLarge.transform.localScale = Vector3.one * 2f;
                PlayerScaleAdjuster relative = authoredLarge.AddComponent<PlayerScaleAdjuster>();
                foreach (float size in new[] { 0.5f, 2f, 0.25f, 1f })
                {
                    relative.Size = size;
                    relative.ApplyScale();
                    relative.ApplyScale();
                    near(authoredLarge.transform.localScale.x, 2f * size,
                        "authored non-unit root scale remains the stable baseline");
                }

                GameObject generatedObject = Make("Generated particles own their world scale", fixture);
                generatedObject.transform.SetParent(body.transform, false);
                ParticleSystem generated = generatedObject.AddComponent<ParticleSystem>();
                var generatedMain = generated.main;
                generatedMain.scalingMode = ParticleSystemScalingMode.Local;
                generatedMain.playOnAwake = false;
                scale.ApplyScale();
                generatedMain = generated.main;
                check(generatedMain.scalingMode == ParticleSystemScalingMode.Local,
                    "DontSave generated particles must retain their explicit scaling policy");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(fixture);
            }

            return "Player scaling: " + passed + " checks passed. Temporary preview-scene fixtures removed; no authored assets changed.";
        }

        private static GameObject Make(string name, Scene scene)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
    }
}
#endif
