using System;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.SceneManagement;

using IronRing.Art.Editor;
using IronRing.Train.Equipment;

namespace IronRing.Presentation.Interior.Editor
{
    /// <summary>
    /// 스토리 034 제작 도구(멱등). 장비 스프라이트가 없으면 SpriteGenEquipment로 만들고, EquipmentPiece·TrainCarGrid 프리팹과
    /// Resources/Train/TrainEquipmentCatalog.asset을 만들거나 갱신한 뒤, Main 씬의 HubInteriorRoot마다 TrainEquipmentView를 붙이고
    /// 각 TrainCar 아래에 TrainCarGrid 인스턴스(회전·크기 월드 1로 보정, 칸 순번 = 오른쪽(진행 방향)부터 0)를 놓는다. 바뀐 것이 있을 때만 씬을 저장한다.
    /// </summary>
    public static class TrainEquipmentBuild
    {
        private const string MAIN = "Assets/Scenes/Main.unity";
        private const string ROOT = "Assets/Prefabs/Interior/";
        private const string PIECE = ROOT + "EquipmentPiece.prefab";
        private const string GRID = ROOT + "TrainCarGrid.prefab";
        private const string CATALOG = "Assets/Scripts/Train/Equipment/Resources/Train/TrainEquipmentCatalog.asset";
        private const string MATERIAL = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string DATA = "Assets/Scripts/Train/Equipment/Resources/Train/TrainEquipment.json";

        /// <summary>프리팹·카탈로그·씬 배치를 맞춘다.</summary>
        [MenuItem("IronRing/Interior/Generate Train Equipment Grid")]
        public static void Generate()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(SpriteGenEquipment.OUTPUT + SpriteGenEquipment.CELL_FRAME + ".png") == null) SpriteGenEquipment.Generate();
            TrainEquipmentData data = TrainEquipmentData.Parse(System.IO.File.ReadAllText(DATA));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL) ?? throw new InvalidOperationException("Missing " + MATERIAL);
            TrainEquipmentCatalog catalog = EnsureCatalog();
            GameObject piece = EnsurePiece(material, catalog);
            GameObject grid = EnsureGrid(material, catalog, data);
            catalog.Configure(SpriteGenEquipment.IDS.Select(id => new TrainEquipmentCatalog.IconEntry(id, Sp(id))).ToArray(), Sp(SpriteGenEquipment.CELL_FRAME), piece, grid);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Scene scene = OpenMain();
            int changed = 0;
            foreach (HubInteriorRoot root in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HubInteriorRoot>(true)))
            {
                if (root.GetComponent<TrainEquipmentView>() == null) { root.gameObject.AddComponent<TrainEquipmentView>(); changed++; }
                Transform[] cars = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.StartsWith("TrainCar ", StringComparison.Ordinal)).OrderBy(t => t.position.x).ToArray();
                for (int i = 0; i < cars.Length; i++)
                {
                    int index = cars.Length - 1 - i; // 진행 방향(+x)의 맨 앞 칸이 0
                    TrainCarGridView view = cars[i].GetComponentInChildren<TrainCarGridView>(true);
                    if (view == null)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(grid, cars[i]);
                        instance.name = "TrainCarGrid";
                        view = instance.GetComponent<TrainCarGridView>();
                        changed++;
                    }
                    Transform t = view.transform;
                    Vector3 lossy = cars[i].lossyScale;
                    var scale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)), 1f);
                    bool dirty = view.Car != index || t.localPosition != Vector3.zero || Quaternion.Angle(t.rotation, Quaternion.identity) > 0.01f || (t.localScale - scale).sqrMagnitude > 1e-6f;
                    if (!dirty) continue;
                    t.localPosition = Vector3.zero;
                    t.rotation = Quaternion.identity;
                    t.localScale = scale;
                    view.Configure(index, t.Find("Frames").gameObject, t.Find("Pieces"));
                    EditorUtility.SetDirty(view);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    changed++;
                }
            }
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("TrainEquipmentBuild: scene changes " + changed);
        }

        private static Scene OpenMain()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path == MAIN) return scene;
            if (scene.isDirty) throw new InvalidOperationException("Active scene is dirty; save it before running TrainEquipmentBuild.");
            return EditorSceneManager.OpenScene(MAIN, OpenSceneMode.Single);
        }

        private static Sprite Sp(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(SpriteGenEquipment.OUTPUT + name + ".png") ?? throw new InvalidOperationException("Missing equipment sprite " + name);

        private static TrainEquipmentCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TrainEquipmentCatalog>(CATALOG);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<TrainEquipmentCatalog>();
            AssetDatabase.CreateAsset(catalog, CATALOG);
            return catalog;
        }

        /// <summary>EquipmentPiece.prefab: 루트 + 자식 Sprite(SpriteRenderer). 없을 때만 만든다.</summary>
        private static GameObject EnsurePiece(Material material, TrainEquipmentCatalog catalog)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PIECE);
            if (existing != null) return existing;
            var temp = new GameObject("EquipmentPiece");
            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(temp.transform, false);
            var renderer = sprite.AddComponent<SpriteRenderer>();
            renderer.sprite = Sp("turret");
            renderer.sharedMaterial = material;
            renderer.sortingOrder = catalog.PieceOrder;
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, PIECE);
            UnityEngine.Object.DestroyImmediate(temp);
            return asset;
        }

        /// <summary>TrainCarGrid.prefab: TrainCarGridView + Frames(칸 틀 Columns×Rows, 기본 꺼짐) + Pieces. 없을 때만 만든다.</summary>
        private static GameObject EnsureGrid(Material material, TrainEquipmentCatalog catalog, TrainEquipmentData data)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(GRID);
            if (existing != null) return existing;
            var temp = new GameObject("TrainCarGrid");
            var frames = new GameObject("Frames");
            frames.transform.SetParent(temp.transform, false);
            var pieces = new GameObject("Pieces");
            pieces.transform.SetParent(temp.transform, false);
            Sprite frame = Sp(SpriteGenEquipment.CELL_FRAME);
            for (int row = 0; row < data.Rows; row++)
                for (int column = 0; column < data.Columns; column++)
                {
                    var cell = new GameObject($"Cell {column},{row}");
                    cell.transform.SetParent(frames.transform, false);
                    cell.transform.localPosition = TrainCarGridView.LocalCenter(column, row, 1, data.Columns, data.Rows, catalog.CellSize);
                    cell.transform.localScale = Vector3.one * catalog.CellSize * 0.96f / Mathf.Max(0.01f, frame.bounds.size.x);
                    var renderer = cell.AddComponent<SpriteRenderer>();
                    renderer.sprite = frame;
                    renderer.sharedMaterial = material;
                    renderer.color = catalog.FrameColor;
                    renderer.sortingOrder = catalog.FrameOrder;
                }
            frames.SetActive(false);
            temp.AddComponent<TrainCarGridView>().Configure(0, frames, pieces.transform);
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, GRID);
            UnityEngine.Object.DestroyImmediate(temp);
            return asset;
        }
    }
}
