#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.EditorTools.Prototyping
{
    [CreateAssetMenu(
        fileName = "Gameplay Prefab Gallery Catalog",
        menuName = "MASSIVE/Prototyping/Gameplay Prefab Gallery Catalog")]
    public sealed class GameplayPrefabGalleryCatalog : ScriptableObject
    {
        [SerializeField] private string galleryRootName = "PREFAB GALLERY - DISPLAY ONLY";
        [SerializeField] private string galleryParentName = "GameplayObjects";
        [SerializeField] private float displayDiameter = 2.15f;
        [SerializeField] private float itemBaseHeight = 0.2f;
        [SerializeField] private float itemLabelOffset = 1.22f;
        [SerializeField] private float labelWorldScale = 0.0085f;
        [SerializeField] private float headerX = -11.2f;
        [SerializeField] private List<GameplayPrefabGalleryCategory> categories = new();

        public string GalleryRootName => galleryRootName;
        public string GalleryParentName => galleryParentName;
        public float DisplayDiameter => displayDiameter;
        public float ItemBaseHeight => itemBaseHeight;
        public float ItemLabelOffset => itemLabelOffset;
        public float LabelWorldScale => labelWorldScale;
        public float HeaderX => headerX;
        public List<GameplayPrefabGalleryCategory> Categories => categories;

        public void ResetLayoutToDefaults()
        {
            galleryRootName = "PREFAB GALLERY - DISPLAY ONLY";
            galleryParentName = "GameplayObjects";
            displayDiameter = 2.15f;
            itemBaseHeight = 0.2f;
            itemLabelOffset = 1.22f;
            labelWorldScale = 0.0085f;
            headerX = -11.2f;
        }
    }

    [Serializable]
    public sealed class GameplayPrefabGalleryCategory
    {
        [SerializeField] private string hierarchyName;
        [SerializeField] private string displayName;
        [SerializeField] private float rowZ;
        [SerializeField] private float firstItemX;
        [SerializeField] private float columnSpacing = 4f;
        [SerializeField] private string emptyHint;
        [SerializeField] private List<GameplayPrefabGalleryEntry> entries = new();

        public string HierarchyName => hierarchyName;
        public string DisplayName => displayName;
        public float RowZ => rowZ;
        public float FirstItemX => firstItemX;
        public float ColumnSpacing => columnSpacing;
        public string EmptyHint => emptyHint;
        public List<GameplayPrefabGalleryEntry> Entries => entries;

        public GameplayPrefabGalleryCategory(
            string hierarchyName,
            string displayName,
            float rowZ,
            float firstItemX,
            float columnSpacing,
            string emptyHint = null)
        {
            this.hierarchyName = hierarchyName;
            this.displayName = displayName;
            this.rowZ = rowZ;
            this.firstItemX = firstItemX;
            this.columnSpacing = columnSpacing;
            this.emptyHint = emptyHint;
        }
    }

    [Serializable]
    public sealed class GameplayPrefabGalleryEntry
    {
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [Tooltip("Optional child to isolate when the prefab contains a roster or collection.")]
        [SerializeField] private string focusChildPath;
        [SerializeField, Min(0.05f)] private float relativeScale = 1f;

        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public string FocusChildPath => focusChildPath;
        public float RelativeScale => relativeScale;

        public GameplayPrefabGalleryEntry(
            string displayName,
            GameObject prefab,
            float relativeScale = 1f,
            string focusChildPath = null)
        {
            this.displayName = displayName;
            this.prefab = prefab;
            this.relativeScale = relativeScale;
            this.focusChildPath = focusChildPath;
        }
    }
}
#endif
