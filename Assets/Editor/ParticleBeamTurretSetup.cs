#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using Massive.PowerUps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ParticleBeamTurretSetup
{
    public const string Folder = "Assets/Enemy System/Enemy Types/Ranged/Particle Beam Turret";
    public const string PrefabPath = Folder + "/Enemy_ParticleBeamTurret.prefab";
    public const string DefinitionPath = Folder + "/ED_ParticleBeamTurret.asset";
    public const string ProfilePath = Folder + "/ESP_ParticleBeamTurretTest.asset";
    public const string WarningPath = Folder + "/Turret Spawn Telegraph.prefab";
    [MenuItem("MASSIVE/Enemies/Particle Beam Turret/Create Prefab")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        EnsureFolder(Folder);
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        if (!definition)
        {
            definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DronePrototypeSetup.DefinitionPath));
            definition.name = "ED_ParticleBeamTurret"; definition.id = "PARTICLE_BEAM_TURRET";
            definition.category = EnemyCategory.Ranged; definition.healthMassEq = 3f;
            definition.defeatRewardKey = "ENEMY_DEFEAT"; definition.moveSpeed = 0f;
            definition.damageToPlayerMass01 = 0f; definition.spawnRadiusWorld = 1f; definition.maxAliveOverride = 8;
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab)
        {
            var root = new GameObject("Enemy_ParticleBeamTurret"); root.SetActive(false); root.layer = LayerMask.NameToLayer("Enemy");
            try
            {
                var body = root.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
                body.constraints = RigidbodyConstraints.FreezeAll;
                var enemy = root.AddComponent<EnemyBase>(); enemy.despawnDelaySeconds = .8f;
                var normal = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath);
                root.AddComponent<EnemyScoreReward>().scoreToastPrefab = normal.GetComponent<EnemyScoreReward>().scoreToastPrefab;
                var turret = root.AddComponent<ParticleBeamTurretController>(); turret.definition = definition;
                var visual = root.AddComponent<ParticleBeamTurretVisuals>();
                var panel = Child(root, "Octagon-mounted base and floating triangles");
                visual.panels = panel.AddComponent<MeshFilter>();
                panel.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DronePrototypeSetup.Folder + "/Drone Black.mat");
                var shell = Child(root, "Solid base hull").AddComponent<MeshCollider>();
                shell.sharedMesh = CollisionMesh(); shell.convex = true; turret.shellCollider = shell;
                var pivot = Child(root, "Firing apparatus pivot"); pivot.transform.localPosition = Vector3.forward * .43f;
                turret.firingPivot = visual.firingPivot = pivot.transform;
                var hurt = Child(root, "Damage trigger");
                var trigger = hurt.AddComponent<CapsuleCollider>(); trigger.direction = 2; trigger.radius = .59f;
                trigger.height = 1.65f; trigger.center = Vector3.forward * .3f; trigger.isTrigger = true;
                hurt.AddComponent<EnemyHurtbox>(); turret.damageTrigger = trigger;
                var core = GameObject.CreatePrimitive(PrimitiveType.Cube); core.name = "Energy core";
                Object.DestroyImmediate(core.GetComponent<Collider>()); core.transform.SetParent(pivot.transform, false); core.layer = root.layer;
                visual.core = core.GetComponent<Renderer>(); visual.core.sharedMaterial = normal.GetComponent<DroneVisuals>().engineRenderer.sharedMaterial;
                var accelerator = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Particle Accelerator/ParticleAcceleratorProjectile.prefab");
                var beam = Object.Instantiate(accelerator.GetComponentInChildren<ParticleAcceleratorBeamVisual>(true).gameObject, root.transform);
                beam.name = "Particle Accelerator beam"; beam.layer = root.layer;
                turret.beamVisual = beam.GetComponent<ParticleAcceleratorBeamVisual>();
                var serialized = new SerializedObject(turret.beamVisual);
                serialized.FindProperty("useFixedVolumeSize").boolValue = false; serialized.ApplyModifiedPropertiesWithoutUndo();
                beam.SetActive(false);
                ConfigurePlasma(root);
                turret.spawnTelegraphPrefab = WarningPrefab();
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
                root.SetActive(true); visual.Rebuild();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab.GetComponent<ParticleBeamTurretController>().contactPlasma || !prefab.GetComponent<ParticleBeamTurretController>().corePlasma)
        {
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { ConfigurePlasma(contents); PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        if (definition.prefab != prefab) { definition.prefab = prefab; EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition); }
        if (!AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath))
        {
            var profile = ScriptableObject.CreateInstance<EnemySpawnProfile>(); profile.maxAliveTotal = profile.maxAliveRanged = 8;
            profile.rules.Clear(); profile.batches.Clear(); AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        WarningPrefab();
        Selection.activeObject = prefab;
        Debug.Log("[Particle Beam Turret] Prefab ready; existing tuning preserved.");
    }
    private static GameObject Child(GameObject parent, string name)
    { var go = new GameObject(name); go.layer = parent.layer; go.transform.SetParent(parent.transform, false); return go; }
    private static void ConfigurePlasma(GameObject root)
    {
        var turret = root.GetComponent<ParticleBeamTurretController>();
        if (!turret.contactPlasma)
        {
            turret.beamVisual.plasmaLayers = true;
            turret.beamFadeSeconds = .36f;
            var plasma = GameObject.CreatePrimitive(PrimitiveType.Cube); plasma.name = "Sustained contact plasma";
            Object.DestroyImmediate(plasma.GetComponent<Collider>()); plasma.transform.SetParent(root.transform, false); plasma.layer = root.layer;
            var renderer = plasma.GetComponent<Renderer>(); renderer.sharedMaterial = turret.beamVisual.GetComponent<Renderer>().sharedMaterial;
            renderer.enabled = false; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            plasma.AddComponent<MetaballSDFInstance>().TargetRenderer = renderer;
            turret.contactPlasma = plasma.AddComponent<ParticleBeamPlasmaContact>();
        }
        if (!turret.corePlasma)
        {
            var plasma = Object.Instantiate(turret.contactPlasma.gameObject, root.transform);
            plasma.name = "Core firing plasma";
            turret.corePlasma = plasma.GetComponent<ParticleBeamPlasmaContact>();
            turret.corePlasma.dropsPerSecond = 28f;
            turret.corePlasma.dropLifetime = .32f;
            turret.corePlasma.dropRadius = .028f;
            turret.corePlasma.splashSpeed = .9f;
            turret.corePlasma.contactRadius = .11f;
            turret.corePlasma.dripAcceleration = Vector3.zero;
        }
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
    private static Mesh CollisionMesh()
    {
        string path = Folder + "/Turret Base Hull.asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh) return mesh;
        var points = new Vector3[16]; var indices = new List<int>(); float r = .68f, h = ParticleBeamTurretGeometry.Height(r);
        for (int i = 0; i < 8; i++)
        {
            points[i] = ParticleBeamTurretGeometry.Octagon(i * Mathf.PI / 4f, r, 0f);
            points[i + 8] = ParticleBeamTurretGeometry.Octagon((i + .5f) * Mathf.PI / 4f, r, h);
            int n = (i + 1) % 8;
            indices.AddRange(new[] { i, n, i + 8, n, n + 8, i + 8 });
        }
        for (int i = 1; i < 7; i++) indices.AddRange(new[] { 0, i + 1, i, 8, i + 8, i + 9 });
        mesh = new Mesh { name = "Octagonal antiprism base collision" }; mesh.vertices = points; mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    private static EnemySpawnTelegraph WarningPrefab()
    {
        var mesh = WarningMesh();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarningPath); if (prefab) return prefab.GetComponent<EnemySpawnTelegraph>();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(CarrierPresentationSetup.TelegraphPath);
        var go = Object.Instantiate(source); go.name = "Turret Spawn Telegraph";
        try
        {
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var telegraph = go.GetComponent<EnemySpawnTelegraph>(); telegraph.rollDegreesPerSecond = 0f; telegraph.rotationAxis = Vector3.forward;
            return PrefabUtility.SaveAsPrefabAsset(go, WarningPath).GetComponent<EnemySpawnTelegraph>();
        }
        finally { Object.DestroyImmediate(go); }
    }
    private static Mesh WarningMesh()
    {
        string path = Folder + "/Turret Spawn Outline.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        int count = ParticleBeamTurretGeometry.BaseFaces * 3;
        if (mesh && mesh.vertexCount == count * 4) return mesh;
        bool created = !mesh;
        if (created) mesh = new Mesh { name = "Turret spawn outline" }; else mesh.Clear();
        var points = new Vector3[count]; var vertices = new List<Vector3>(); var tangents = new List<Vector4>(); var uvs = new List<Vector2>(); var indices = new List<int>();
        for (int i = 0; i < ParticleBeamTurretGeometry.BaseFaces; i++) ParticleBeamTurretGeometry.BaseTriangle(i, .68f, points, i * 3);
        for (int i = 0; i < count; i++)
        {
            Vector3 a = points[i], b = points[i / 3 * 3 + (i + 1) % 3], d = b - a; int n = vertices.Count;
            vertices.AddRange(new[] { a, a, b, b }); for (int j = 0; j < 4; j++) tangents.Add(new Vector4(d.x, d.y, d.z, 1f));
            uvs.AddRange(new[] { new Vector2(0,-1), new Vector2(0,1), new Vector2(1,-1), new Vector2(1,1) });
            indices.AddRange(new[] { n, n+1, n+2, n+2, n+1, n+3 });
        }
        mesh.SetVertices(vertices); mesh.SetTangents(tangents); mesh.SetUVs(0, uvs); mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(.5f); mesh.bounds = bounds;
        if (created) AssetDatabase.CreateAsset(mesh, path); else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
        return mesh;
    }
    [MenuItem("MASSIVE/Enemies/Particle Beam Turret/Place In Player Actions")]
    public static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != CarrierPrototypeSetup.ScenePath)
            throw new InvalidOperationException("Open Player Actions in Edit Mode.");
        Install(); var scene = SceneManager.GetActiveScene();
        var existing = Object.FindObjectsByType<ParticleBeamTurretController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing)
        {
            foreach (var turret in Object.FindObjectsByType<ParticleBeamTurretController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.gameObject.scene == scene))
            { Undo.RecordObject(turret.transform, "Apply turret wall mount"); turret.ApplyWallMount(); PrefabUtility.RecordPrefabInstancePropertyModifications(turret.transform); }
            EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = existing.gameObject; return;
        }
        var root = new GameObject("Particle Beam Turret Test"); Undo.RegisterCreatedObjectUndo(root, "Place Particle Beam Turrets");
        var director = root.AddComponent<EnemyDirector>(); director.enemyRoot = root.transform; director.waitForScoring = true;
        director.spawnProfile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
        director.arenaBounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).First(x => x.gameObject.scene == scene);
        for (int i = 0; i < 2; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), root.transform);
            go.name = i == 0 ? "Particle Beam Turret — Top" : "Particle Beam Turret — Bottom";
            var turret = go.GetComponent<ParticleBeamTurretController>(); turret.sceneDirector = director; turret.arenaBounds = director.arenaBounds;
            turret.mount = i == 0 ? ParticleBeamTurretController.WallMount.Top : ParticleBeamTurretController.WallMount.Bottom;
            turret.wallPosition = i == 0 ? -.32f : .32f; turret.ApplyWallMount();
            PrefabUtility.RecordPrefabInstancePropertyModifications(turret); PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        foreach (var ranged in Object.FindObjectsByType<RangedDroneController>(FindObjectsSortMode.None).Where(x => x.gameObject.scene == scene))
        { var encounter = ranged.sceneDirector ? ranged.sceneDirector.gameObject : ranged.gameObject; Undo.RecordObject(encounter, "Focus Particle Beam Turrets"); encounter.SetActive(false); }
        var gallery = Object.FindObjectsByType<PlayerDemoGallery>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (gallery)
        {
            Undo.RecordObject(gallery, "Register turret encounter with gallery pause"); var so = new SerializedObject(gallery); var encounters = so.FindProperty("encounters");
            encounters.GetArrayElementAtIndex(encounters.arraySize++).objectReferenceValue = root; so.ApplyModifiedProperties();
        }
        EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = root;
    }
}
#endif
