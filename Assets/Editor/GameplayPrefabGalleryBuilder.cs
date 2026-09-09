#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Massive.EditorTools.Prototyping
{
    public static class GameplayPrefabGalleryBuilder
    {
        private const string CatalogPath =
            "Assets/Editor/Gameplay Prefab Gallery Catalog.asset";
        private const string GalleryPrefabPath =
            "Assets/Editor/Gameplay Prefab Gallery.prefab";
        private const string PrototypeScenePath =
            "Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity";
        private const string ToastPrefabPath =
            "Assets/Power-ups/PU_PickupToast.prefab";

        [MenuItem("MASSIVE/Prototyping/Rebuild Gameplay Prefab Gallery")]
        public static void RebuildActiveSceneGallery()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("No loaded active scene is available.");

            if (!string.Equals(scene.path, PrototypeScenePath, StringComparison.OrdinalIgnoreCase))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Build Gameplay Prefab Gallery",
                    $"The active scene is '{scene.name}', not S-8_DYNAMO-PROTOTYPE. " +
                    "Build the display-only gallery here anyway?",
                    "Build Here",
                    "Cancel");
                if (!proceed)
                    return;
            }

            GameplayPrefabGalleryCatalog catalog = GetOrCreateCatalog();
            GameObject galleryRoot = Build(scene, catalog);
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                galleryRoot,
                GalleryPrefabPath,
                InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[Gameplay Prefab Gallery] Rebuilt '{catalog.GalleryRootName}' " +
                $"with {catalog.Categories.Sum(category => category.Entries.Count)} catalog entries.");
        }

        [MenuItem("MASSIVE/Prototyping/Select Gameplay Prefab Gallery Catalog")]
        public static void SelectCatalog()
        {
            Selection.activeObject = GetOrCreateCatalog();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("MASSIVE/Prototyping/Reset Gameplay Prefab Gallery Catalog")]
        public static void ResetCatalogToDefaults()
        {
            GameplayPrefabGalleryCatalog catalog = GetOrCreateCatalog();
            Undo.RecordObject(catalog, "Reset Gameplay Prefab Gallery Catalog");
            PopulateDefaultCatalog(catalog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            Debug.Log("[Gameplay Prefab Gallery] Catalog reset to the curated default entries.");
        }

        private static GameplayPrefabGalleryCatalog GetOrCreateCatalog()
        {
            GameplayPrefabGalleryCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameplayPrefabGalleryCatalog>(CatalogPath);
            if (catalog != null)
                return catalog;

            catalog = ScriptableObject.CreateInstance<GameplayPrefabGalleryCatalog>();
            PopulateDefaultCatalog(catalog);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void PopulateDefaultCatalog(GameplayPrefabGalleryCatalog catalog)
        {
            catalog.ResetLayoutToDefaults();
            catalog.Categories.Clear();

            GameplayPrefabGalleryCategory players = new(
                "01 PLAYERS", "PLAYERS", 5.8f, -4.2f, 8.4f);
            players.Entries.Add(Entry(
                "LIGHT PLAYER", "Assets/Prefabs/Players.prefab", 1f, "Player 1"));
            players.Entries.Add(Entry(
                "DARK PLAYER", "Assets/Prefabs/Players.prefab", 1f, "Player 3"));
            catalog.Categories.Add(players);

            GameplayPrefabGalleryCategory enemies = new(
                "02 ENEMIES", "ENEMIES", 2.9f, -3.2f, 6.4f);
            enemies.Entries.Add(Entry(
                "BASE ENEMY",
                "Assets/Enemy System/Enemy Types/BaseEnemy.prefab"));
            enemies.Entries.Add(Entry(
                "DYSON SPHERE",
                "Assets/Enemy System/Enemy Types/Melee/DysonSphere/Enemy_DysonSphere.prefab"));
            catalog.Categories.Add(enemies);

            GameplayPrefabGalleryCategory powerUps = new(
                "03 POWER-UPS", "POWER-UPS", 0f, -4.8f, 3.2f);
            powerUps.Entries.Add(Entry(
                "TIME DILATION", "Assets/Power-ups/PU_TimeDilation.prefab"));
            powerUps.Entries.Add(Entry(
                "PARTICLE ACCELERATOR", "Assets/Power-ups/PU_ParticleAccelerator.prefab"));
            powerUps.Entries.Add(Entry(
                "MASS NODE", "Assets/Power-ups/PU_MassNode.prefab"));
            powerUps.Entries.Add(Entry(
                "DECOHERENCE", "Assets/Power-ups/PU_Decoherence.prefab"));
            catalog.Categories.Add(powerUps);

            GameplayPrefabGalleryCategory scoring = new(
                "04 SCORING OBJECTS", "SCORING OBJECTS", -3f, -3.2f, 6.4f);
            scoring.Entries.Add(Entry(
                "MATTER NUGGET", "Assets/Prefabs/Matter nugget.prefab"));
            scoring.Entries.Add(Entry(
                "AMPLIFIER CORE", "Assets/Power-ups/Amplifier Core/Amplifier Core.prefab"));
            catalog.Categories.Add(scoring);

            catalog.Categories.Add(new GameplayPrefabGalleryCategory(
                "99 FUTURE ADDITIONS",
                "FUTURE ADDITIONS",
                -5.8f,
                0f,
                4f,
                "ADD PREFABS TO THE GALLERY CATALOG, THEN REBUILD"));
        }

        private static GameplayPrefabGalleryEntry Entry(
            string displayName,
            string prefabPath,
            float relativeScale = 1f,
            string focusChildPath = null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                Debug.LogWarning($"[Gameplay Prefab Gallery] Missing prefab: {prefabPath}");
            return new GameplayPrefabGalleryEntry(
                displayName,
                prefab,
                relativeScale,
                focusChildPath);
        }

        private static GameObject Build(Scene scene, GameplayPrefabGalleryCatalog catalog)
        {
            Transform parent = FindRoot(scene, catalog.GalleryParentName);
            if (parent == null)
            {
                GameObject parentObject = new(catalog.GalleryParentName);
                Undo.RegisterCreatedObjectUndo(parentObject, "Create GameplayObjects root");
                SceneManager.MoveGameObjectToScene(parentObject, scene);
                parent = parentObject.transform;
            }

            Transform existing = parent.Find(catalog.GalleryRootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            GameObject galleryRootObject = new(catalog.GalleryRootName);
            Undo.RegisterCreatedObjectUndo(galleryRootObject, "Create Gameplay Prefab Gallery");
            SceneManager.MoveGameObjectToScene(galleryRootObject, scene);
            galleryRootObject.transform.SetParent(parent, false);
            galleryRootObject.tag = "EditorOnly";

            TMP_FontAsset font = ResolveGalleryFont();
            foreach (GameplayPrefabGalleryCategory category in catalog.Categories)
                BuildCategory(galleryRootObject.transform, category, catalog, scene, font);

            Selection.activeGameObject = galleryRootObject;
            SceneVisibilityManager.instance.Show(galleryRootObject, true);
            return galleryRootObject;
        }

        private static void BuildCategory(
            Transform galleryRoot,
            GameplayPrefabGalleryCategory category,
            GameplayPrefabGalleryCatalog catalog,
            Scene scene,
            TMP_FontAsset font)
        {
            GameObject categoryObject = new(category.HierarchyName);
            Undo.RegisterCreatedObjectUndo(categoryObject, "Create Gallery Category");
            categoryObject.transform.SetParent(galleryRoot, false);
            categoryObject.transform.localPosition = new Vector3(0f, 0f, category.RowZ);

            CreateLabel(
                categoryObject.transform,
                "[CATEGORY LABEL] " + category.DisplayName,
                category.DisplayName,
                new Vector3(catalog.HeaderX, 0.82f, 0f),
                new Vector2(350f, 58f),
                28f,
                catalog.LabelWorldScale,
                font,
                true);

            for (int i = 0; i < category.Entries.Count; i++)
            {
                GameplayPrefabGalleryEntry entry = category.Entries[i];
                float x = category.FirstItemX + i * category.ColumnSpacing;
                BuildEntry(
                    categoryObject.transform,
                    entry,
                    i,
                    x,
                    catalog,
                    scene,
                    font);
            }

            if (category.Entries.Count == 0 && !string.IsNullOrWhiteSpace(category.EmptyHint))
            {
                CreateLabel(
                    categoryObject.transform,
                    "[EMPTY SLOT] " + category.EmptyHint,
                    category.EmptyHint,
                    new Vector3(category.FirstItemX, 0.82f, 0f),
                    new Vector2(560f, 54f),
                    20f,
                    catalog.LabelWorldScale,
                    font,
                    false);
            }
        }

        private static void BuildEntry(
            Transform categoryRoot,
            GameplayPrefabGalleryEntry entry,
            int index,
            float x,
            GameplayPrefabGalleryCatalog catalog,
            Scene scene,
            TMP_FontAsset font)
        {
            string displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.Prefab != null ? entry.Prefab.name : "MISSING PREFAB"
                : entry.DisplayName;

            GameObject cell = new($"[{index + 1:00}] {displayName}");
            Undo.RegisterCreatedObjectUndo(cell, "Create Gallery Cell");
            cell.transform.SetParent(categoryRoot, false);
            cell.transform.localPosition = new Vector3(x, 0f, 0f);

            CreateLabel(
                cell.transform,
                "[LABEL] " + displayName,
                displayName,
                new Vector3(0f, 0.82f, -catalog.ItemLabelOffset),
                new Vector2(330f, 50f),
                21f,
                catalog.LabelWorldScale,
                font,
                false);

            if (entry.Prefab == null)
            {
                Debug.LogWarning($"[Gameplay Prefab Gallery] '{displayName}' has no prefab assigned.");
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(entry.Prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Could not instantiate '{entry.Prefab.name}'.");

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Gallery Prefab");
            instance.name = "[PREFAB] " + entry.Prefab.name;
            instance.transform.SetParent(cell.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.SetActive(true);

            IsolateFocusChild(instance, entry.FocusChildPath);
            MakeDisplayOnly(instance);
            FitAndPlace(instance, cell.transform.position, catalog, entry.RelativeScale);
        }

        private static void IsolateFocusChild(GameObject instance, string focusChildPath)
        {
            if (string.IsNullOrWhiteSpace(focusChildPath))
                return;

            Transform focus = instance.transform.Find(focusChildPath);
            if (focus == null)
            {
                Debug.LogWarning(
                    $"[Gameplay Prefab Gallery] Could not find focus child " +
                    $"'{focusChildPath}' in '{instance.name}'.");
                return;
            }

            foreach (Transform child in instance.transform)
                child.gameObject.SetActive(child == focus);
            focus.gameObject.SetActive(true);
            focus.localPosition = Vector3.zero;
            instance.name += " :: " + focus.name;
        }

        private static void FitAndPlace(
            GameObject instance,
            Vector3 cellWorldPosition,
            GameplayPrefabGalleryCatalog catalog,
            float relativeScale)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (!TryGetBounds(renderers, out Bounds bounds))
            {
                instance.transform.localScale *= Mathf.Max(0.05f, relativeScale);
                instance.transform.position =
                    cellWorldPosition + Vector3.up * catalog.ItemBaseHeight;
                return;
            }

            float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largestDimension <= 0.0001f)
            {
                instance.transform.localScale *= Mathf.Max(0.05f, relativeScale);
                instance.transform.position =
                    cellWorldPosition + Vector3.up * catalog.ItemBaseHeight;
                return;
            }

            float scaleFactor = catalog.DisplayDiameter / largestDimension;
            scaleFactor *= Mathf.Max(0.05f, relativeScale);
            instance.transform.localScale *= scaleFactor;

            renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (!TryGetBounds(renderers, out bounds))
                return;

            Vector3 desired = new(
                cellWorldPosition.x,
                catalog.ItemBaseHeight,
                cellWorldPosition.z);
            Vector3 current = new(bounds.center.x, bounds.min.y, bounds.center.z);
            instance.transform.position += desired - current;
        }

        private static bool TryGetBounds(Renderer[] renderers, out Bounds bounds)
        {
            Renderer first = renderers.FirstOrDefault(renderer =>
                renderer != null && renderer.gameObject.activeInHierarchy);
            if (first == null)
            {
                bounds = default;
                return false;
            }

            bounds = first.bounds;
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.gameObject.activeInHierarchy)
                    bounds.Encapsulate(renderer.bounds);
            }
            return true;
        }

        private static void MakeDisplayOnly(GameObject instance)
        {
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Collider2D collider in instance.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;

            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                body.detectCollisions = false;
                body.isKinematic = true;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }
            foreach (Rigidbody2D body in instance.GetComponentsInChildren<Rigidbody2D>(true))
                body.simulated = false;

            foreach (AudioSource audioSource in instance.GetComponentsInChildren<AudioSource>(true))
                audioSource.enabled = false;

            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || IsPresentationBehaviour(behaviour.GetType()))
                    continue;
                behaviour.enabled = false;
            }
        }

        private static bool IsPresentationBehaviour(Type type)
        {
            string name = type.Name;
            return name.IndexOf("Visual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Appearance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Rotator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Rotate", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CreateLabel(
            Transform parent,
            string hierarchyName,
            string text,
            Vector3 localPosition,
            Vector2 pixelSize,
            float fontSize,
            float worldScale,
            TMP_FontAsset font,
            bool categoryHeader)
        {
            GameObject canvasObject = new(
                hierarchyName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Gallery Label");
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = localPosition;
            canvasObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            canvasObject.transform.localScale = Vector3.one * Mathf.Max(0.0001f, worldScale);

            RectTransform canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = pixelSize;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = categoryHeader ? 6100 : 6000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 10f;

            GameObject panelObject = new(
                "Toast Panel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panel = panelObject.GetComponent<Image>();
            panel.color = Color.black;
            panel.raycastTarget = false;
            Outline outline = panelObject.GetComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject textObject = new(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font;
            label.fontSize = fontSize;
            label.fontStyle = categoryHeader ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
        }

        private static TMP_FontAsset ResolveGalleryFont()
        {
            GameObject toast = AssetDatabase.LoadAssetAtPath<GameObject>(ToastPrefabPath);
            TMP_Text toastLabel = toast != null
                ? toast.GetComponentInChildren<TMP_Text>(true)
                : null;
            return toastLabel != null ? toastLabel.font : TMP_Settings.defaultFontAsset;
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .Select(root => root.transform)
                .FirstOrDefault(root => root.name == name);
        }
    }

    [CustomEditor(typeof(GameplayPrefabGalleryCatalog))]
    public sealed class GameplayPrefabGalleryCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Gallery In Active Scene"))
                GameplayPrefabGalleryBuilder.RebuildActiveSceneGallery();
            if (GUILayout.Button("Reset Catalog To Curated Defaults"))
                GameplayPrefabGalleryBuilder.ResetCatalogToDefaults();
        }
    }
}
#endif
