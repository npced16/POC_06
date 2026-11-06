using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using TMPro;

using IronRing.Interior;
using IronRing.Track;

namespace IronRing.Presentation.Interior.Editor
{
    /// <summary>
    /// 스토리 012 제작 도구. Assets/Prefabs/Interior/에 카탈로그와 프리팹(FloorTile, TrackTile, TrainCar, InteriorCell, HubInterior, ItemPiece)을
    /// 없을 때만 만들고(사용자 편집 보존), Main 씬의 HubInteriors 아래에 거점마다 HubInterior 인스턴스를 없을 때만 놓는다(거점 id로 찾아 중복 없음).
    /// 격자 크기는 Interior/HubInterior.json, 배치 기본값은 카탈로그에서 읽는다. 생성 뒤의 편집은 씬과 프리팹에서 한다.
    /// 스토리 020: HubInterior.prefab의 칸 수가 데이터(행×열×2)와 다르면 프리팹을 새 격자로 다시 만들고, 칸 수가 다른 씬 인스턴스는
    /// 지우고 같은 이름·위치·부모 순서로 새로 놓는다. 칸 수가 같으면 아무것도 바꾸지 않는다(멱등).
    /// </summary>
    public static class Story012Build
    {
        public const string ROOT = "Assets/Prefabs/Interior/";
        public const string INTERIORS_ROOT = "HubInteriors";
        private const string CATALOG = ROOT + "InteriorCatalog.asset";
        private const string HUB_PREFAB = ROOT + "HubInterior.prefab";
        private const string CELL_PREFAB = ROOT + "InteriorCell.prefab";
        private const string FLOOR_PREFAB = ROOT + "FloorTile.prefab";
        private const string TRACK_PREFAB = ROOT + "TrackTile.prefab";
        private const string CAR_PREFAB = ROOT + "TrainCar.prefab";
        private const string ITEM_PREFAB = ROOT + "ItemPiece.prefab";
        private const string GHOST_PREFAB = ROOT + "GhostPiece.prefab";
        private const string GHOST_MATERIAL = ROOT + "GhostPiece.mat";
        private const string FRAME_PREFAB = ROOT + "ClusterFrame.prefab";
        private const string FRAME_SPRITE = ROOT + "ClusterFrame.png";
        private const int GHOST_ORDER = 25;
        private const string BUILDING_PIECE_PREFAB = ROOT + "BuildingPiece.prefab";
        private const string BUILDING_PROP_PREFAB = ROOT + "BuildingProp.prefab";
        private const string ICON_CATALOG = "Assets/Prefabs/UI/IconCatalog.asset";
        private const string SMOKE_PREFAB = "Assets/Prefabs/Fx/Smoke.prefab";
        private const int FRAME_ORDER = 5;
        private const string ART = "Assets/Art/Generated/";
        private const string MATERIAL = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string SCENE = "Assets/Scenes/Main.unity";
        private const string DATA = "Assets/Scripts/Interior/Resources/Interior/HubInterior.json";
        private const string TRACK = "Assets/Scripts/Track/Resources/Track/ring-track.json";

        /// <summary>
        /// 프리팹·카탈로그를 보장하고 Main 씬에서 격자 크기가 다른 거점 인스턴스를 교체하고 빠진 것만 놓은 뒤, 바뀐 것이 있을 때만 씬을 저장한다.
        /// 다시 실행해도 중복되지 않는다.
        /// </summary>
        [MenuItem("IronRing/Interior/Generate Prefabs And Place In Main")]
        public static void Generate()
        {
            GameObject before = AssetDatabase.LoadAssetAtPath<GameObject>(HUB_PREFAB);
            bool gridChanged = before != null && before.GetComponentsInChildren<InteriorCellView>(true).Length
                != InteriorData.Parse(File.ReadAllText(DATA)).CellCount;
            InteriorCatalog catalog = EnsurePrefabs();
            Scene scene = EditorSceneManager.OpenScene(SCENE);
            int removed = RemoveOffTrackInteriors(scene);
            int replaced = ReplaceMismatchedInteriors(scene, catalog, gridChanged);
            int added = PlaceInteriors(scene, catalog);
            if (removed > 0 || replaced > 0 || added > 0 || scene.isDirty) EditorSceneManager.SaveScene(scene, SCENE);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Story012Build: 제거 {removed}, 교체 {replaced}, 추가 {added}");
        }

        /// <summary>
        /// Story 032. scene의 HubInterior 인스턴스 중 거점 id가 선로 데이터의 역이 아닌 것(예: 4거점 고리에서 남은 hub-2~4)을 지우고
        /// 지운 수를 돌려준다. 거점 프로필은 데이터로 남아 있어도 선로에 없으면 내부를 두지 않는다.
        /// </summary>
        public static int RemoveOffTrackInteriors(Scene scene)
        {
            TrackData track = TrackData.Parse(File.ReadAllText(TRACK));
            var stations = new System.Collections.Generic.HashSet<string>(track.Nodes.Where(node => node.IsStation).Select(node => node.Id));
            HubInteriorRoot[] existing = UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(root => root.gameObject.scene == scene && !stations.Contains(root.HubId)).ToArray();
            foreach (HubInteriorRoot root in existing) UnityEngine.Object.DestroyImmediate(root.gameObject);
            if (existing.Length > 0) EditorSceneManager.MarkSceneDirty(scene);
            return existing.Length;
        }

        /// <summary>
        /// 스토리 020. scene의 HubInterior 인스턴스 중 칸(InteriorCellView) 수가 데이터의 행×열×2와 다른 것을 지우고, 같은 부모·이름·위치·
        /// 형제 순서·거점 id로 현재 HubInterior.prefab을 새로 놓는다. replaceAll이면(이번 실행에서 프리팹을 다시 만든 경우, 옛 칸 재정의가
        /// 새 프리팹에 남지 않도록) 칸 수와 관계없이 모두 교체한다. 교체한 수를 돌려준다.
        /// </summary>
        public static int ReplaceMismatchedInteriors(Scene scene, InteriorCatalog catalog, bool replaceAll = false)
        {
            InteriorData data = InteriorData.Parse(File.ReadAllText(DATA));
            GameObject prefab = Load<GameObject>(HUB_PREFAB);
            HubInteriorRoot[] existing = UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(root => root.gameObject.scene == scene).ToArray();
            int replaced = 0;
            foreach (HubInteriorRoot old in existing)
            {
                if (!replaceAll && old.Cells().Length == data.CellCount) continue;
                Transform parent = old.transform.parent;
                string name = old.gameObject.name, hubId = old.HubId;
                Vector3 position = old.transform.position;
                int sibling = old.transform.GetSiblingIndex();
                bool active = old.gameObject.activeSelf;
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (parent != null) instance.transform.SetParent(parent, false);
                instance.transform.SetSiblingIndex(sibling);
                instance.name = name;
                instance.transform.position = position;
                instance.SetActive(active);
                HubInteriorRoot root = instance.GetComponent<HubInteriorRoot>();
                root.ApplyHubId(hubId);
                EditorUtility.SetDirty(root);
                foreach (InteriorCellView cellView in root.Cells()) EditorUtility.SetDirty(cellView);
                replaced++;
            }
            if (replaced > 0) EditorSceneManager.MarkSceneDirty(scene);
            return replaced;
        }

        /// <summary>카탈로그의 스프라이트·재질 참조를 채우고 없는 프리팹만 만든다. 기존 프리팹은 손대지 않는다.</summary>
        public static InteriorCatalog EnsurePrefabs()
        {
            // Story 039: no longer deletes LEGACY_RESOURCES; story 036 keeps RuinDressing.json there (RuinCatalog.Resource).
            Directory.CreateDirectory(ROOT);
            InteriorData data = InteriorData.Parse(File.ReadAllText(DATA));
            Material material = Load<Material>(MATERIAL);
            InteriorCatalog catalog = AssetDatabase.LoadAssetAtPath<InteriorCatalog>(CATALOG);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<InteriorCatalog>();
                AssetDatabase.CreateAsset(catalog, CATALOG);
            }
            var serialized = new SerializedObject(catalog);
            SetSprite(serialized, "_rubbleSprite", "interior_rubble");
            SetSprite(serialized, "_emptySprite", "interior_empty");
            SetSprite(serialized, "_plotSprite", "interior_plot");
            if (serialized.FindProperty("_builtSprite").objectReferenceValue == null) SetSprite(serialized, "_builtSprite", "interior_plot");
            SetSprite(serialized, "_trainSprite", "train");
            SetSprite(serialized, "_trackSprite", "rail_horizontal");
            SetSprite(serialized, "_floorSprite", "interior_floor");
            serialized.FindProperty("_spriteMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject floor = EnsureSpritePrefab(FLOOR_PREFAB, "FloorTile", catalog.FloorSprite, material, catalog.FloorOrder, Vector3.one * catalog.TileSize, Quaternion.identity);
            GameObject track = EnsureSpritePrefab(TRACK_PREFAB, "TrackTile", catalog.TrackSprite, material, catalog.TrackOrder, Vector3.one * catalog.TileSize, Quaternion.identity);
            GameObject car = EnsureSpritePrefab(CAR_PREFAB, "TrainCar", catalog.TrainSprite, material, catalog.TrainOrder, Vector3.one * catalog.TrainScale, Quaternion.Euler(0, 0, -catalog.TrainForwardAngle));
            GameObject item = EnsureSpritePrefab(ITEM_PREFAB, "ItemPiece", null, material, catalog.PieceOrder, Vector3.one, Quaternion.identity);
            EnsureItemLabel(item, catalog.PieceOrder);
            GameObject cell = EnsureCellPrefab(catalog, material);
            EnsureHubPrefab(catalog, data, floor, track, car, cell);
            serialized.Update();
            SerializedProperty itemPrefab = serialized.FindProperty("_itemPiecePrefab");
            if (itemPrefab.objectReferenceValue == null) itemPrefab.objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EnsurePlacementPrefabs(catalog, material);
            EnsureBuildingPrefabs(catalog, material);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>
        /// 스토리 015. 반투명 고스트(GhostPiece.prefab + GhostPiece.mat)와 서식지 묶음 테두리(ClusterFrame.png + ClusterFrame.prefab)를
        /// 없을 때만 만들고, 비어 있는 카탈로그 참조와 시설 아이콘 표(Art/Generated의 wall·turret·workshop)를 채운다.
        /// </summary>
        public static void EnsurePlacementPrefabs(InteriorCatalog catalog, Material baseMaterial)
        {
            Material ghostMaterial = AssetDatabase.LoadAssetAtPath<Material>(GHOST_MATERIAL);
            if (ghostMaterial == null)
            {
                ghostMaterial = new Material(baseMaterial) { name = "GhostPiece" };
                if (ghostMaterial.HasProperty("_Color")) ghostMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.75f));
                AssetDatabase.CreateAsset(ghostMaterial, GHOST_MATERIAL);
            }
            GameObject ghost = EnsureSpritePrefab(GHOST_PREFAB, "GhostPiece", null, ghostMaterial, GHOST_ORDER, Vector3.one, Quaternion.identity);
            Sprite frameSprite = EnsureFrameSprite();
            GameObject frame = EnsureSpritePrefab(FRAME_PREFAB, "ClusterFrame", frameSprite, baseMaterial, FRAME_ORDER, Vector3.one * catalog.TileSize, Quaternion.identity);
            var serialized = new SerializedObject(catalog);
            SerializedProperty ghostProperty = serialized.FindProperty("_ghostPiecePrefab");
            if (ghostProperty.objectReferenceValue == null) ghostProperty.objectReferenceValue = ghost;
            SerializedProperty frameProperty = serialized.FindProperty("_clusterFramePrefab");
            if (frameProperty.objectReferenceValue == null) frameProperty.objectReferenceValue = frame;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>ClusterFrame.png를 투명한 중심과 3픽셀 흰 테두리로 갱신하고 32 PPU 점 필터 스프라이트로 가져온다.</summary>
        private static Sprite EnsureFrameSprite()
        {
            const int size = 32, border = 3;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = edge ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(FRAME_SPRITE, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FRAME_SPRITE, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FRAME_SPRITE);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(FRAME_SPRITE);
        }

        /// <summary>
        /// 완성 영역 프레임 스프라이트의 내부를 투명하게 다시 생성한다.
        /// FRAME_SPRITE 경로를 사용하며, 갱신된 스프라이트 애셋을 저장한다.
        /// </summary>
        public static void RefreshClusterFrameSprite()
        {
            EnsureFrameSprite();
            AssetDatabase.SaveAssets();
        }

        /// <summary>scene의 HubInteriors 아래에 선로 데이터의 거점마다 HubInterior 인스턴스를 거점 id로 찾아 없는 것만 놓고 추가 수를 돌려준다.</summary>
        public static int PlaceInteriors(Scene scene, InteriorCatalog catalog)
        {
            GameObject prefab = Load<GameObject>(HUB_PREFAB);
            TrackData track = TrackData.Parse(File.ReadAllText(TRACK));
            string[] hubIds = track.Nodes.Where(node => node.IsStation).Select(node => node.Id).ToArray();
            GameObject container = scene.GetRootGameObjects().FirstOrDefault(root => root.name == INTERIORS_ROOT);
            if (container == null)
            {
                container = new GameObject(INTERIORS_ROOT);
                SceneManager.MoveGameObjectToScene(container, scene);
            }
            HubInteriorRoot[] existing = UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(root => root.gameObject.scene == scene).ToArray();
            int added = 0;
            for (int index = 0; index < hubIds.Length; index++)
            {
                if (existing.Any(root => root.HubId == hubIds[index])) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                instance.name = "HubInterior " + hubIds[index];
                instance.transform.position = catalog.DefaultOrigin + catalog.DefaultSpacing * index;
                HubInteriorRoot root = instance.GetComponent<HubInteriorRoot>();
                root.ApplyHubId(hubIds[index]);
                EditorUtility.SetDirty(root);
                foreach (InteriorCellView cellView in root.Cells()) EditorUtility.SetDirty(cellView);
                added++;
            }
            if (added > 0) EditorSceneManager.MarkSceneDirty(scene);
            return added;
        }

        /// <summary>스토리 027. 씬을 건드리지 않고 건물 프리팹과 카탈로그의 건물 그림 표만 보장한다(SpriteGenBuildings.Generate 끝에서 호출).</summary>
        public static void EnsureBuildingAssets()
        {
            InteriorCatalog catalog = AssetDatabase.LoadAssetAtPath<InteriorCatalog>(CATALOG);
            if (catalog == null) { UnityEngine.Debug.LogWarning("Story012Build: InteriorCatalog가 없습니다. Generate를 먼저 실행하세요."); return; }
            EnsureBuildingPrefabs(catalog, AssetDatabase.LoadAssetAtPath<Material>(MATERIAL));
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 스토리 027. BuildingPiece.prefab(SpriteRenderer + 트리거 BoxCollider2D + BuildingPiece)과 BuildingProp.prefab을 없을 때만 만들고,
        /// Art/Generated/buildings의 지붕·모서리·문·굴뚝·상징물 스프라이트로 카탈로그 표를 다시 채운다. 비어 있는 프리팹·연기 참조만 채운다.
        /// </summary>
        public static void EnsureBuildingPrefabs(InteriorCatalog catalog, Material material)
        {
            GameObject piece = AssetDatabase.LoadAssetAtPath<GameObject>(BUILDING_PIECE_PREFAB);
            if (piece == null)
            {
                var temp = new GameObject("BuildingPiece");
                var renderer = temp.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = catalog.BuildingOrder;
                var box = temp.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = Vector2.one;
                temp.AddComponent<BuildingPiece>();
                temp.transform.localScale = Vector3.one * catalog.TileSize;
                piece = PrefabUtility.SaveAsPrefabAsset(temp, BUILDING_PIECE_PREFAB);
                UnityEngine.Object.DestroyImmediate(temp);
            }
            GameObject prop = EnsureSpritePrefab(BUILDING_PROP_PREFAB, "BuildingProp", null, material, catalog.BuildingSignOrder, Vector3.one * catalog.TileSize, Quaternion.identity);
            const string dir = ART + "buildings/";
            Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(dir + name + ".png");
            var styles = new System.Collections.Generic.List<InteriorCatalog.BuildingStyleSet>();
            foreach (string style in global::IronRing.Art.Editor.SpriteGenBuildings.STYLES)
            {
                var pieces = new Sprite[16];
                for (int mask = 0; mask < 16; mask++) pieces[mask] = Sprite(global::IronRing.Art.Editor.SpriteGenBuildings.RoofName(style, mask));
                string[] corners = global::IronRing.Art.Editor.SpriteGenBuildings.CORNERS;
                var inner = new Sprite[corners.Length];
                for (int i = 0; i < corners.Length; i++) inner[i] = Sprite(global::IronRing.Art.Editor.SpriteGenBuildings.CornerName(style, corners[i]));
                styles.Add(new InteriorCatalog.BuildingStyleSet(style, pieces, inner));
                // QA 후속(건물 장식): 색조 계열("style@tint")
                foreach (string tint in global::IronRing.Art.Editor.SpriteGenBuildings.TINTS)
                {
                    var tPieces = new Sprite[16];
                    for (int mask = 0; mask < 16; mask++) tPieces[mask] = Sprite(global::IronRing.Art.Editor.SpriteGenBuildings.RoofName(style, tint, mask));
                    var tInner = new Sprite[corners.Length];
                    for (int i = 0; i < corners.Length; i++) tInner[i] = Sprite(global::IronRing.Art.Editor.SpriteGenBuildings.CornerName(style, tint, corners[i]));
                    if (tPieces[15] != null) styles.Add(new InteriorCatalog.BuildingStyleSet(InteriorCatalog.TintedStyleId(style, tint), tPieces, tInner));
                }
            }
            var signs = new System.Collections.Generic.List<InteriorCatalog.FacilitySprite>();
            foreach (string id in global::IronRing.Art.Editor.SpriteGenBuildings.SIGNS)
                signs.Add(new InteriorCatalog.FacilitySprite(id, Sprite(global::IronRing.Art.Editor.SpriteGenBuildings.SignName(id))));
            if (styles[0].Piece(15) == null) UnityEngine.Debug.LogWarning("Story012Build: 건물 스프라이트가 없습니다. IronRing/Art/Generate Building Sprites를 실행하세요.");
            catalog.SetBuildingArt(styles.ToArray(), new[] { Sprite("bld_door_0"), Sprite("bld_door_1") }, Sprite("bld_chimney"), signs.ToArray());
            var windows = new System.Collections.Generic.List<Sprite>();
            for (int p = 1; p <= global::IronRing.Art.Editor.SpriteGenBuildings.WINDOW_PATTERNS; p++)
            {
                Sprite window = Sprite(global::IronRing.Art.Editor.SpriteGenBuildings.WindowName(p));
                if (window != null) windows.Add(window);
            }
            var decor = new System.Collections.Generic.List<InteriorCatalog.FacilitySprite>();
            foreach (string id in global::IronRing.Art.Editor.SpriteGenBuildings.DECOR)
            {
                Sprite sprite = Sprite(global::IronRing.Art.Editor.SpriteGenBuildings.DecorName(id));
                if (sprite != null) decor.Add(new InteriorCatalog.FacilitySprite(id, sprite));
            }
            catalog.SetBuildingDecor(windows.ToArray(), decor.ToArray());
            EditorUtility.SetDirty(catalog);
            var serialized = new SerializedObject(catalog);
            SerializedProperty pieceProperty = serialized.FindProperty("_buildingPiecePrefab");
            if (pieceProperty.objectReferenceValue == null) pieceProperty.objectReferenceValue = piece;
            SerializedProperty propProperty = serialized.FindProperty("_buildingPropPrefab");
            if (propProperty.objectReferenceValue == null) propProperty.objectReferenceValue = prop;
            SerializedProperty smokeProperty = serialized.FindProperty("_buildingSmokePrefab");
            if (smokeProperty.objectReferenceValue == null) smokeProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(SMOKE_PREFAB);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>path의 단일 SpriteRenderer 프리팹이 없으면 name, sprite, material, order, scale, rotation으로 만들어 돌려준다.</summary>
        private static GameObject EnsureSpritePrefab(string path, string name, Sprite sprite, Material material, int order, Vector3 scale, Quaternion rotation)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var temp = new GameObject(name);
            var renderer = temp.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            temp.transform.localScale = scale;
            temp.transform.localRotation = rotation;
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
            UnityEngine.Object.DestroyImmediate(temp);
            return asset;
        }

        /// <summary>
        /// item 프리팹과 렌더 순서 order를 받아 투명 배경의 월드 글자 Label을 처음 한 번 만든다.
        /// 반환값은 없으며 기존 Label의 수동 편집값은 보존한다.
        /// </summary>
        private static void EnsureItemLabel(GameObject item, int order)
        {
            if (item == null || item.transform.Find("Label") != null) return;
            GameObject contents = PrefabUtility.LoadPrefabContents(ITEM_PREFAB);
            try
            {
                GameObject label = new GameObject("Label");
                label.transform.SetParent(contents.transform, false);
                label.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                TextMeshPro text = label.AddComponent<TextMeshPro>();
                text.text = "땅";
                ConfigureItemLabel(label.transform, order);
                PrefabUtility.SaveAsPrefabAsset(contents, ITEM_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// <summary>
        /// 기존 ItemPiece 프리팹의 Label을 PrefabUtility로 열어 큰 물마루 글자로 갱신한다.
        /// ITEM_PREFAB의 Label과 InteriorCatalog.PieceOrder를 사용하며 프리팹 애셋을 저장한다.
        /// </summary>
        public static void UpdateExistingItemLabel()
        {
            Load<GameObject>(ITEM_PREFAB);
            InteriorCatalog catalog = Load<InteriorCatalog>(CATALOG);
            GameObject contents = PrefabUtility.LoadPrefabContents(ITEM_PREFAB);
            try
            {
                Transform label = contents.transform.Find("Label");
                if (label == null) throw new InvalidOperationException("ItemPiece.prefab에 Label이 없습니다.");
                ConfigureItemLabel(label, catalog.PieceOrder);
                PrefabUtility.SaveAsPrefabAsset(contents, ITEM_PREFAB);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// <summary>
        /// label과 order를 사용해 월드 글자 크기, 글꼴, 정렬과 렌더 순서를 설정하며 Label 컴포넌트 상태를 변경한다.
        /// </summary>
        private static void ConfigureItemLabel(Transform label, int order)
        {
            label.localScale = Vector3.one * 0.05f;
            TextMeshPro text = label.GetComponent<TextMeshPro>();
            text.font = Load<TMP_FontAsset>("Assets/Fonts/Mulmaru SDF.asset");
            text.fontSize = 34f;
            text.enableAutoSizing = false;
            text.fontSizeMin = 28f;
            text.fontSizeMax = 34f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.sizeDelta = new Vector2(90f, 44f);
            text.color = new Color32(255, 232, 182, 255);
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            renderer.sortingOrder = order + 1;
        }

        /// <summary>InteriorCell.prefab이 없으면 타일 렌더러, 1×1 Collider2D, InteriorCellView, ItemSlot·FacilitySlot 자식으로 만든다.</summary>
        private static GameObject EnsureCellPrefab(InteriorCatalog catalog, Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(CELL_PREFAB);
            if (existing != null) return existing;
            var temp = new GameObject("InteriorCell");
            var renderer = temp.AddComponent<SpriteRenderer>();
            renderer.sprite = null;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = catalog.CellOrder;
            var collider = temp.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * catalog.TileSize;
            Transform itemSlot = Group(temp.transform, "ItemSlot");
            Transform facilitySlot = Group(temp.transform, "FacilitySlot");
            var view = temp.AddComponent<InteriorCellView>();
            view.Configure(0, 0, 0, true);
            view.BindParts(renderer, itemSlot, facilitySlot);
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, CELL_PREFAB);
            UnityEngine.Object.DestroyImmediate(temp);
            return asset;
        }

        /// <summary>
        /// HubInterior.prefab이 없거나(스토리 020) 칸 수가 데이터의 행×열×2와 다르면 바닥·가로 선로·열차·위/아래 격자(중첩 프리팹 인스턴스)로
        /// 조립해 같은 경로에 저장한다(GUID 유지). 칸 수가 같으면 손대지 않는다.
        /// </summary>
        private static void EnsureHubPrefab(InteriorCatalog catalog, InteriorData data, GameObject floor, GameObject track, GameObject car, GameObject cell)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(HUB_PREFAB);
            if (existing != null && existing.GetComponentsInChildren<InteriorCellView>(true).Length == data.CellCount) return;
            var temp = new GameObject("HubInterior");
            var root = temp.AddComponent<HubInteriorRoot>();
            Transform floorGroup = Group(temp.transform, "Floor");
            Transform trackGroup = Group(temp.transform, "Track");
            Transform trainAnchor = Group(temp.transform, "TrainAnchor");
            Transform cameraAnchor = Group(temp.transform, "CameraAnchor");
            Transform grid = Group(temp.transform, "Grid");
            int rows = data.Rows, columns = data.Columns;
            float trackY = -rows * catalog.TileSize;
            trackGroup.localPosition = new Vector3(0, trackY, 0);
            trainAnchor.localPosition = new Vector3(0, trackY, 0);
            cameraAnchor.localPosition = new Vector3(0, -(rows - 1) * 0.5f * catalog.TileSize, 0);
            for (int column = -catalog.FloorMarginColumns; column < columns + catalog.FloorMarginColumns; column++)
            {
                float x = X(column, columns, catalog.TileSize);
                for (int row = -catalog.FloorMarginRows; row < rows; row++) Instance(floor, floorGroup, new Vector3(x, Y(row, true, rows, catalog.TileSize), 0));
                for (int row = 0; row < rows + catalog.FloorMarginRows; row++) Instance(floor, floorGroup, new Vector3(x, Y(row, false, rows, catalog.TileSize), 0));
                Instance(floor, floorGroup, new Vector3(x, 0, 0));
                Instance(track, trackGroup, new Vector3(x, 0, 0));
            }
            for (int index = 0; index < catalog.TrainCars; index++)
            {
                float x = (index - (catalog.TrainCars - 1) * 0.5f) * catalog.TrainCarSpacing * catalog.TileSize;
                Instance(car, trainAnchor, new Vector3(x, 0, 0)).name = "TrainCar " + index;
            }
            for (int index = 0; index < data.CellCount; index++)
            {
                bool isUpper = index < data.AreaCellCount;
                int local = isUpper ? index : index - data.AreaCellCount;
                int row = local / columns, column = local % columns;
                GameObject instance = Instance(cell, grid, new Vector3(X(column, columns, catalog.TileSize), Y(row, isUpper, rows, catalog.TileSize), 0));
                instance.name = "Cell " + index;
                instance.GetComponent<InteriorCellView>().Configure(index, row, column, isUpper);
            }
            ConfigureUnityGrid(root, catalog.TileSize);
            root.Configure(catalog, catalog.DefaultCameraSize, trainAnchor, cameraAnchor);
            TrainCarRoleBuild.ApplyToHub(temp, catalog, Load<TMP_FontAsset>("Assets/Fonts/Mulmaru SDF.asset"));
            PrefabUtility.SaveAsPrefabAsset(temp, HUB_PREFAB);
            UnityEngine.Object.DestroyImmediate(temp);
        }

        /// <summary>
        /// 기존 마을 프리팹의 위·아래 그리드를 하나로 합친다.
        /// 칸의 위치와 인덱스를 보존하고 프리팹을 저장하며 Play Mode에서는 실행하지 않는다.
        /// </summary>
        public static void UnifyExistingGrid()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing town prefabs.");
            GameObject contents = PrefabUtility.LoadPrefabContents(HUB_PREFAB);
            try
            {
                Transform root = contents.transform;
                Transform upper = root.Find("UpperGrid");
                Transform lower = root.Find("LowerGrid");
                if (upper == null && lower == null) return;
                Transform grid = root.Find("Grid");
                if (grid == null) grid = Group(root, "Grid");
                foreach (Transform old in new[] { upper, lower })
                {
                    if (old == null) continue;
                    while (old.childCount > 0) old.GetChild(0).SetParent(grid, true);
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, HUB_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>열린 Main 씬과 마을 프리팹의 칸 위치를 유지하며 Unity Grid를 설정하고 저장한다.</summary>
        public static void ConfigureExistingUnityGrid()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before configuring the town grid.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE || scene.isDirty) throw new InvalidOperationException("Main scene must be active and saved.");
            InteriorCatalog catalog = Load<InteriorCatalog>(CATALOG);
            GameObject contents = PrefabUtility.LoadPrefabContents(HUB_PREFAB);
            try
            {
                ValidateUnityGrid(contents.GetComponent<HubInteriorRoot>(), catalog.TileSize);
                foreach (HubInteriorRoot root in UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (root.gameObject.scene == scene) ValidateUnityGrid(root, root.Catalog.TileSize);
                ConfigureUnityGrid(contents.GetComponent<HubInteriorRoot>(), catalog.TileSize);
                PrefabUtility.SaveAsPrefabAsset(contents, HUB_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            foreach (HubInteriorRoot root in UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (root.gameObject.scene != scene) continue;
                ConfigureUnityGrid(root, root.Catalog.TileSize);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>root의 칸 위치를 보존하면서 tile 간격의 Grid 중심에 칸을 대응시킨다. 맞지 않는 배치는 오류로 중단한다.</summary>
        private static void ConfigureUnityGrid(HubInteriorRoot root, float tile)
        {
            ValidateUnityGrid(root, tile);
            Transform gridTransform = root.transform.Find("Grid");
            InteriorCellView[] cells = root.Cells();
            Vector3 first = gridTransform.InverseTransformPoint(cells[0].transform.position);
            Grid grid = gridTransform.GetComponent<Grid>();
            if (grid != null) return;
            var positions = new Vector3[gridTransform.childCount];
            for (int i = 0; i < positions.Length; i++) positions[i] = gridTransform.GetChild(i).position;
            gridTransform.position = gridTransform.TransformPoint(first - new Vector3(tile * 0.5f, tile * 0.5f, 0f));
            for (int i = 0; i < positions.Length; i++) gridTransform.GetChild(i).position = positions[i];
            grid = gridTransform.gameObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(tile, tile, 1f);
        }

        /// <summary>root의 칸이 tile 간격의 서로 다른 위치에 있는지 검사하고 잘못되면 예외를 낸다.</summary>
        private static void ValidateUnityGrid(HubInteriorRoot root, float tile)
        {
            Transform gridTransform = root.transform.Find("Grid");
            InteriorCellView[] cells = root.Cells();
            if (gridTransform == null || cells.Length == 0 || tile <= 0f) throw new InvalidOperationException("Town grid or cells are missing.");
            Vector3 first = gridTransform.InverseTransformPoint(cells[0].transform.position);
            var coordinates = new HashSet<Vector2Int>();
            foreach (InteriorCellView cell in cells)
            {
                Vector3 offset = (gridTransform.InverseTransformPoint(cell.transform.position) - first) / tile;
                var coordinate = new Vector2Int(Mathf.RoundToInt(offset.x), Mathf.RoundToInt(offset.y));
                if (Mathf.Abs(offset.x - coordinate.x) > 0.001f || Mathf.Abs(offset.y - coordinate.y) > 0.001f || Mathf.Abs(offset.z) > 0.001f || !coordinates.Add(coordinate))
                    throw new InvalidOperationException($"Town cell {cell.Index} is not aligned with the grid.");
            }
            Grid grid = gridTransform.GetComponent<Grid>();
            if (grid != null)
                foreach (InteriorCellView cell in cells)
                    if (Vector2.Distance(grid.GetCellCenterWorld(grid.WorldToCell(cell.transform.position)), cell.transform.position) > 0.01f)
                        throw new InvalidOperationException($"Town cell {cell.Index} is not at its Grid center.");
        }

        /// <summary>column(음수·초과 허용)의 구역 중심 기준 로컬 x.</summary>
        private static float X(int column, int columns, float tile) => (column - (columns - 1) * 0.5f) * tile;

        /// <summary>row와 구역을 사용해 위쪽은 rows-row, 아래쪽은 -row의 로컬 y를 반환한다.</summary>
        private static float Y(int row, bool upper, int rows, float tile) => (upper ? rows - row : -row) * tile;

        /// <summary>
        /// 현재 허브 프리팹과 열린 Main 씬의 아래쪽 격자를 위쪽 격자에 붙인다.
        /// 카탈로그의 타일 크기와 행 수를 사용하며, 변경된 프리팹과 씬을 저장한다.
        /// </summary>
        public static void AlignExistingGrid()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing town prefabs.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE) throw new InvalidOperationException("Main scene must be active.");
            InteriorCatalog catalog = Load<InteriorCatalog>(CATALOG);
            InteriorData data = InteriorData.Parse(File.ReadAllText(DATA));
            GameObject contents = PrefabUtility.LoadPrefabContents(HUB_PREFAB);
            try
            {
                AlignGrid(contents.GetComponent<HubInteriorRoot>(), data.Rows, catalog.TileSize);
                PrefabUtility.SaveAsPrefabAsset(contents, HUB_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            foreach (HubInteriorRoot root in UnityEngine.Object.FindObjectsByType<HubInteriorRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (root.gameObject.scene != scene) continue;
                AlignGrid(root, data.Rows, catalog.TileSize);
            }
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// root의 아래쪽 셀과 선로를 rows와 tile에 맞춰 한 칸 위로 배치한다.
        /// 이동한 Transform의 프리팹 인스턴스 설정도 기록한다.
        /// </summary>
        private static void AlignGrid(HubInteriorRoot root, int rows, float tile)
        {
            foreach (InteriorCellView cell in root.Cells())
            {
                if (cell.IsUpper) continue;
                Vector3 position = cell.transform.localPosition;
                position.y = -cell.Row * tile;
                cell.transform.localPosition = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(cell.transform);
            }
            foreach (string name in new[] { "Track", "TrainAnchor" })
            {
                Transform anchor = root.transform.Find(name);
                Vector3 position = anchor.localPosition;
                position.y = -rows * tile;
                anchor.localPosition = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
            }
        }

        /// <summary>parent 아래 name 빈 자식을 만들어 돌려준다.</summary>
        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>prefab 인스턴스를 parent 아래 localPosition에 놓고 돌려준다(중첩 프리팹 연결 유지).</summary>
        private static GameObject Instance(GameObject prefab, Transform parent, Vector3 localPosition)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            return instance;
        }

        /// <summary>serialized의 property에 Art/Generated의 spriteName 스프라이트를 넣는다. 없으면 예외.</summary>
        private static void SetSprite(SerializedObject serialized, string property, string spriteName)
        {
            serialized.FindProperty(property).objectReferenceValue = Load<Sprite>(ART + spriteName + ".png");
        }

        /// <summary>path의 필수 에셋을 읽어 T로 돌려주며 누락은 예외로 알린다.</summary>
        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path + " (run IronRing.Art.Editor.SpriteGen.Generate first)");
            return asset;
        }
    }
}
