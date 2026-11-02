using System.Collections.Generic;
using System.IO;

using UnityEditor;
using UnityEngine;

using IronRing.Presentation.Effects;

namespace IronRing.Presentation.HubPhase.Editor
{
    /// <summary>
    /// Story 014 editor generator: builds the interior NPC prefab under Assets/Prefabs/HubPhase/ from the
    /// existing generated art (Story 010 sprite set) and assigns them to the HubPhasePrefabCatalog asset. Re-runnable.
    /// Story 017: the NPC prefab also carries a trigger BoxCollider2D sized to its sprite.
    /// Bridge: execute-method --method IronRing.Presentation.HubPhase.Editor.HubPhasePrefabBuild.Generate
    /// </summary>
    public static class HubPhasePrefabBuild
    {
        private const string PrefabFolder = "Assets/Prefabs/HubPhase";
        private const string CatalogPath = "Assets/Scripts/Presentation/HubPhase/Resources/HubPhase/HubPhasePrefabCatalog.asset";
        private const string ArtPath = "Assets/Scripts/Presentation/Effects/Resources/Story010Effects.asset";
        private const string ViewJsonPath = "Assets/Scripts/Presentation/HubPhase/Resources/HubPhase/hub_phase_view.json";

        /// <summary>Creates or overwrites the prefabs and the catalog; returns a summary string for the bridge.</summary>
        public static string Generate()
        {
            var art = AssetDatabase.LoadAssetAtPath<EffectsSettings>(ArtPath);
            if (art == null) throw new FileNotFoundException("Missing sprite set", ArtPath);
            var settings = JsonUtility.FromJson<HubPhaseViewSettings>(AssetDatabase.LoadAssetAtPath<TextAsset>(ViewJsonPath).text);
            EnsureFolder(PrefabFolder);
            GameObject npc = SavePiece("HubPhaseNpc", art, "npc", settings.NpcSortingOrder, true);

            var catalog = AssetDatabase.LoadAssetAtPath<HubPhasePrefabCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HubPhasePrefabCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("_npc").objectReferenceValue = npc;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return "HubPhase prefabs: npc -> " + CatalogPath;
        }

        /// <summary>
        /// Saves a single-SpriteRenderer prefab named name with spriteName art and the given sorting order. clickable adds a
        /// trigger BoxCollider2D sized to the sprite bounds (Story 017: interior NPC click via Physics2D ray).
        /// </summary>
        private static GameObject SavePiece(string name, EffectsSettings art, string spriteName, int order, bool clickable = false)
        {
            Sprite sprite = art.Sprite(spriteName);
            if (sprite == null) throw new KeyNotFoundException("Missing sprite " + spriteName + " in " + ArtPath);
            var go = new GameObject(name);
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (art.SpriteMaterial != null) renderer.sharedMaterial = art.SpriteMaterial;
                renderer.sortingOrder = order;
                if (clickable)
                {
                    var collider = go.AddComponent<BoxCollider2D>();
                    collider.isTrigger = true;
                    collider.size = sprite.bounds.size;
                    collider.offset = sprite.bounds.center;
                }
                return PrefabUtility.SaveAsPrefabAsset(go, PrefabFolder + "/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>Creates path (and parents) as AssetDatabase folders when missing.</summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
