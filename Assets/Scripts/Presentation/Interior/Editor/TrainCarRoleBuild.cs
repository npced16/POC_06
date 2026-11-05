using System;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;

using TMPro;

using IronRing.Combat;

namespace IronRing.Presentation.Interior.Editor
{
    /// <summary>정차 열차의 기관차와 짐칸을 서로 다른 스프라이트와 표식으로 만드는 제작 도구.</summary>
    public static class TrainCarRoleBuild
    {
        private const string HUB_PREFAB = "Assets/Prefabs/Interior/HubInterior.prefab";
        private const string CATALOG = "Assets/Prefabs/Interior/InteriorCatalog.asset";
        private const string FONT = "Assets/Fonts/Mulmaru SDF.asset";
        private const string SCENE = "Assets/Scenes/Main.unity";
        private const string MARKERS = "TrainRoleMarkers";
        private const string LOCOMOTIVE_IMAGE = "Assets/Art/Generated/train_locomotive.png";
        private const string CARGO_IMAGE = "Assets/Art/Generated/train_cargo.png";
        private const int SPRITE_WIDTH = 48;
        private const int SPRITE_HEIGHT = 64;

        /// <summary>
        /// 열린 Main 씬과 HUB_PREFAB을 확인해 기관차·짐칸 그림과 표식을 만든다.
        /// 변경한 열차 프리팹만 저장하며 열린 씬의 수동 편집은 보존한다.
        /// </summary>
        [MenuItem("IronRing/Interior/Mark Train Cars")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing train prefabs.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE) throw new InvalidOperationException("Main scene must be active.");
            InteriorCatalog catalog = AssetDatabase.LoadAssetAtPath<InteriorCatalog>(CATALOG);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
            if (catalog == null || font == null) throw new InvalidOperationException("Train catalog or Mulmaru font is missing.");
            GameObject contents = PrefabUtility.LoadPrefabContents(HUB_PREFAB);
            try
            {
                if (ApplyToHub(contents, catalog, font)) PrefabUtility.SaveAsPrefabAsset(contents, HUB_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>거점 프리팹의 열차를 전투 열차와 같은 그림과 장착 포탑으로 구성해 저장한다.</summary>
        public static void ApplyBattleTrain()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            GameObject hub = PrefabUtility.LoadPrefabContents(HUB_PREFAB);
            try
            {
                Transform anchor = hub.transform.Find("TrainAnchor");
                Transform track = hub.transform.Find("Track");
                if (anchor == null || track == null) throw new InvalidOperationException("Train anchor or track missing.");
                Transform[] cars = anchor.Cast<Transform>().Where(child => child.name.StartsWith("TrainCar ")).OrderBy(child => child.localPosition.x).ToArray();
                if (cars.Length == 0) throw new InvalidOperationException("Train cars missing.");
                float width = Mathf.Max(5f, cars[cars.Length - 1].localPosition.x - cars[0].localPosition.x + 2.5f);
                foreach (Transform car in cars) car.gameObject.SetActive(false);
                Transform markers = anchor.Find(MARKERS);
                if (markers != null) markers.gameObject.SetActive(false);
                track.gameObject.SetActive(false);
                Transform old = anchor.Find("BattleStyleTrain");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                Transform oldRail = anchor.Find("Railway");
                if (oldRail != null) UnityEngine.Object.DestroyImmediate(oldRail.gameObject);
                Sprite trainSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/Battle/side_train.png");
                Sprite turretSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/Battle/side_turret.png");
                Sprite railSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/Battle/battle_rail_dense.png");
                if (trainSprite == null || turretSprite == null || railSprite == null) throw new InvalidOperationException("Battle train sprites missing.");
                GameObject visual = new GameObject("BattleStyleTrain", typeof(SpriteRenderer), typeof(TrainArmamentVisual));
                visual.transform.SetParent(anchor, false);
                SpriteRenderer trainRenderer = visual.GetComponent<SpriteRenderer>();
                trainRenderer.sprite = trainSprite;
                trainRenderer.flipX = true;
                trainRenderer.sortingOrder = 20;
                float scale = width / trainSprite.bounds.size.x;
                visual.transform.localScale = Vector3.one * scale;
                GameObject rail = new GameObject("Railway", typeof(SpriteRenderer));
                rail.transform.SetParent(anchor, false);
                rail.transform.localPosition = new Vector3(0f, -0.28f, 0.1f);
                rail.transform.localScale = new Vector3(width * 1.5f / railSprite.bounds.size.x, 0.2f, 1f);
                rail.GetComponent<SpriteRenderer>().sprite = railSprite;
                rail.GetComponent<SpriteRenderer>().sortingOrder = 19;
                SerializedObject serialized = new SerializedObject(visual.GetComponent<TrainArmamentVisual>());
                SerializedProperty turrets = serialized.FindProperty("_turrets");
                turrets.arraySize = TrainArmament.SLOT_COUNT;
                for (int i = 0; i < TrainArmament.SLOT_COUNT; i++)
                {
                    GameObject mount = new GameObject("Turret " + (i + 1), typeof(SpriteRenderer));
                    mount.transform.SetParent(visual.transform, false);
                    mount.transform.localPosition = new Vector3(-trainSprite.bounds.size.x * (0.36f - i * 0.11f), trainSprite.bounds.size.y * 0.22f, -0.1f);
                    mount.transform.localScale = Vector3.one * (trainSprite.bounds.size.x * 0.065f / turretSprite.bounds.size.x);
                    SpriteRenderer renderer = mount.GetComponent<SpriteRenderer>();
                    renderer.sprite = turretSprite;
                    renderer.flipX = true;
                    renderer.sortingOrder = 21;
                    turrets.GetArrayElementAtIndex(i).objectReferenceValue = renderer;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(hub, HUB_PREFAB);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
        }

        /// <summary>
        /// hub의 오른쪽 선두에 기관차 그림을, 나머지 칸에 짐칸 그림을 적용한다.
        /// catalog와 font로 표식을 만들거나 갱신하고 프리팹이 바뀌었으면 true를 반환한다.
        /// </summary>
        public static bool ApplyToHub(GameObject hub, InteriorCatalog catalog, TMP_FontAsset font)
        {
            Transform train = hub.transform.Find("TrainAnchor");
            if (train == null) throw new InvalidOperationException("TrainAnchor is missing from the hub prefab.");
            Transform[] cars = train.Cast<Transform>()
                .Where(child => child.GetComponent<SpriteRenderer>() != null)
                .OrderBy(child => child.localPosition.x)
                .ToArray();
            if (cars.Length == 0) throw new InvalidOperationException("No train cars are present in the hub prefab.");
            Sprite locomotive = EnsureSprite(LOCOMOTIVE_IMAGE, true);
            Sprite cargo = EnsureSprite(CARGO_IMAGE, false);
            Transform markers = train.Find(MARKERS);
            bool createdMarkers = markers == null;
            if (createdMarkers)
            {
                markers = new GameObject(MARKERS).transform;
                markers.SetParent(train, false);
            }
            bool changed = createdMarkers;
            for (int index = 0; index < cars.Length; index++)
            {
                bool front = index == cars.Length - 1;
                SpriteRenderer renderer = cars[index].GetComponent<SpriteRenderer>();
                Sprite wanted = front ? locomotive : cargo;
                if (renderer.sprite != wanted || renderer.color != Color.white)
                {
                    renderer.sprite = wanted;
                    renderer.color = Color.white;
                    changed = true;
                }
                Vector3 oldScale = cars[index].localScale;
                if (wanted.texture.height > 256 && Mathf.Approximately(oldScale.x, oldScale.y))
                {
                    cars[index].localScale = front
                        ? new Vector3(oldScale.x * 2.4f, oldScale.y * 1.3f, oldScale.z)
                        : new Vector3(oldScale.x * 1.6f, oldScale.y * 1.25f, oldScale.z);
                    changed = true;
                }
                else if (front
                    && Mathf.Approximately(oldScale.x, oldScale.z * 2.4f)
                    && Mathf.Approximately(oldScale.y, oldScale.z * 1.7f))
                {
                    cars[index].localScale = new Vector3(oldScale.x, oldScale.z * 1.3f, oldScale.z);
                    changed = true;
                }
                string labelText = front ? "기관차" : "짐칸 " + (cars.Length - index - 1);
                if (createdMarkers)
                {
                    AddLabel(markers, cars[index].localPosition.x, labelText, front, catalog.TrainOrder, font);
                }
                else if (index < markers.childCount && markers.GetChild(index).TryGetComponent(out TextMeshPro label))
                {
                    if (label.text != labelText) { label.text = labelText; changed = true; }
                    Color wantedColor = front ? new Color32(240, 245, 246, 255) : new Color32(210, 222, 229, 255);
                    if (label.color != wantedColor) { label.color = wantedColor; changed = true; }
                    string name = front ? "LocomotiveLabel" : "CargoCarLabel";
                    if (label.gameObject.name != name) { label.gameObject.name = name; changed = true; }
                }
            }
            Transform direction = markers.Find("EngineDirection");
            if (direction != null)
            {
                UnityEngine.Object.DestroyImmediate(direction.gameObject);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// parent 아래 x 위치에 text를 놓고 front 여부에 맞는 글자색을 적용한다.
        /// 생성한 월드 글자와 정렬 순서를 프리팹 자식으로 남긴다.
        /// </summary>
        private static void AddLabel(Transform parent, float x, string text, bool front, int trainOrder, TMP_FontAsset font)
        {
            var labelObject = new GameObject(front ? "LocomotiveLabel" : "CargoCarLabel");
            labelObject.transform.SetParent(parent, false);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.font = font;
            label.text = text;
            label.fontSize = 34f;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = front ? new Color32(240, 245, 246, 255) : new Color32(210, 222, 229, 255);
            label.rectTransform.sizeDelta = new Vector2(100f, 36f);
            label.transform.localScale = Vector3.one * 0.012f;
            label.transform.localPosition = new Vector3(x, -0.68f, -0.05f);
            label.GetComponent<MeshRenderer>().sortingOrder = trainOrder + 2;
        }

        /// <summary>
        /// path의 기관차 또는 짐칸 스프라이트를 가져오고 없으면 픽셀 그림을 생성한다.
        /// locomotive으로 형태를 선택하며, 사용자 편집이 가능한 스프라이트 애셋을 반환한다.
        /// </summary>
        private static Sprite EnsureSprite(string path, bool locomotive)
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing == null)
            {
                var texture = new Texture2D(SPRITE_WIDTH, SPRITE_HEIGHT, TextureFormat.RGBA32, false);
                var pixels = new Color32[SPRITE_WIDTH * SPRITE_HEIGHT];
                if (locomotive) DrawLocomotive(pixels);
                else DrawCargo(pixels);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool illustrated = importedTexture.height > 256;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            float pixelsPerUnit = illustrated ? importedTexture.height : 64f;
            FilterMode filterMode = illustrated ? FilterMode.Bilinear : FilterMode.Point;
            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit)
                || importer.filterMode != filterMode
                || importer.maxTextureSize != (illustrated ? 4096 : 2048)
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || !importer.alphaIsTransparency
                || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.filterMode = filterMode;
                importer.maxTextureSize = illustrated ? 4096 : 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// pixels에 좁아지는 선두와 보일러, 운전실이 있는 기관차의 위쪽 모습을 그린다.
        /// SPRITE_WIDTH와 SPRITE_HEIGHT 크기의 배열을 직접 변경한다.
        /// </summary>
        private static void DrawLocomotive(Color32[] pixels)
        {
            Color32 shadow = new Color32(25, 27, 32, 255);
            Color32 iron = new Color32(48, 54, 60, 255);
            Color32 bodySteel = new Color32(83, 104, 114, 255);
            Color32 lightSteel = new Color32(176, 196, 202, 255);
            Color32 glass = new Color32(91, 181, 193, 255);
            DrawRectangle(pixels, 19, 0, 29, 8, iron);
            DrawRectangle(pixels, 3, 10, 10, 23, shadow);
            DrawRectangle(pixels, 38, 10, 45, 23, shadow);
            DrawRectangle(pixels, 3, 38, 10, 51, shadow);
            DrawRectangle(pixels, 38, 38, 45, 51, shadow);
            DrawRectangle(pixels, 8, 7, 40, 55, shadow);
            DrawRectangle(pixels, 10, 9, 38, 55, bodySteel);
            DrawRectangle(pixels, 12, 11, 36, 28, lightSteel);
            DrawRectangle(pixels, 15, 15, 33, 24, iron);
            DrawRectangle(pixels, 17, 17, 31, 23, glass);
            DrawRectangle(pixels, 10, 29, 14, 51, lightSteel);
            DrawRectangle(pixels, 34, 29, 38, 51, lightSteel);
            DrawCircle(pixels, 24, 40, 12, shadow);
            DrawCircle(pixels, 24, 40, 10, lightSteel);
            DrawCircle(pixels, 24, 40, 7, iron);
            DrawCircle(pixels, 24, 40, 4, bodySteel);
            for (int y = 54; y < SPRITE_HEIGHT; y++)
            {
                int inset = (y - 54) / 2;
                DrawRectangle(pixels, 8 + inset, y, 40 - inset, y + 1, shadow);
                DrawRectangle(pixels, 10 + inset, y, 38 - inset, y + 1, lightSteel);
            }
            DrawCircle(pixels, 24, 54, 6, shadow);
            DrawCircle(pixels, 24, 54, 4, iron);
            DrawRectangle(pixels, 14, 58, 18, 61, new Color32(240, 246, 222, 255));
            DrawRectangle(pixels, 30, 58, 34, 61, new Color32(240, 246, 222, 255));
        }

        /// <summary>
        /// pixels에 네모난 적재함, 측면 난간과 목재 상자가 있는 짐칸의 위쪽 모습을 그린다.
        /// SPRITE_WIDTH와 SPRITE_HEIGHT 크기의 배열을 직접 변경한다.
        /// </summary>
        private static void DrawCargo(Color32[] pixels)
        {
            Color32 shadow = new Color32(24, 29, 35, 255);
            Color32 steel = new Color32(95, 121, 134, 255);
            Color32 rim = new Color32(163, 187, 196, 255);
            DrawRectangle(pixels, 19, 0, 29, 7, shadow);
            DrawRectangle(pixels, 19, 57, 29, 64, shadow);
            DrawRectangle(pixels, 2, 9, 9, 23, shadow);
            DrawRectangle(pixels, 39, 9, 46, 23, shadow);
            DrawRectangle(pixels, 2, 40, 9, 54, shadow);
            DrawRectangle(pixels, 39, 40, 46, 54, shadow);
            DrawRectangle(pixels, 7, 5, 41, 59, shadow);
            DrawRectangle(pixels, 9, 7, 39, 57, steel);
            DrawRectangle(pixels, 12, 11, 36, 53, new Color32(40, 55, 62, 255));
            DrawRectangle(pixels, 8, 8, 12, 56, rim);
            DrawRectangle(pixels, 36, 8, 40, 56, rim);
            DrawRectangle(pixels, 10, 7, 38, 11, rim);
            DrawRectangle(pixels, 10, 53, 38, 57, rim);
            DrawCrate(pixels, 14, 14, 24, 29);
            DrawCrate(pixels, 25, 17, 35, 32);
            DrawCrate(pixels, 17, 34, 32, 49);
        }

        /// <summary>
        /// pixels의 xMin·yMin부터 xMax·yMax 직전까지 color로 채운다.
        /// 스프라이트 경계 안의 픽셀만 변경한다.
        /// </summary>
        private static void DrawRectangle(Color32[] pixels, int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            for (int y = Mathf.Max(0, yMin); y < Mathf.Min(SPRITE_HEIGHT, yMax); y++)
                for (int x = Mathf.Max(0, xMin); x < Mathf.Min(SPRITE_WIDTH, xMax); x++)
                    pixels[y * SPRITE_WIDTH + x] = color;
        }

        /// <summary>
        /// pixels의 centerX·centerY를 중심으로 radius 안을 color로 채운다.
        /// 기관차 보일러와 연돌의 둥근 형태를 배열에 기록한다.
        /// </summary>
        private static void DrawCircle(Color32[] pixels, int centerX, int centerY, int radius, Color32 color)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
                for (int x = centerX - radius; x <= centerX + radius; x++)
                    if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= radius * radius)
                        SetPixel(pixels, x, y, color);
        }

        /// <summary>
        /// pixels의 지정한 직사각형에 목재 상자와 대각 보강대를 그린다.
        /// xMin·yMin·xMax·yMax 영역의 픽셀을 변경한다.
        /// </summary>
        private static void DrawCrate(Color32[] pixels, int xMin, int yMin, int xMax, int yMax)
        {
            DrawRectangle(pixels, xMin, yMin, xMax, yMax, new Color32(73, 48, 31, 255));
            DrawRectangle(pixels, xMin + 2, yMin + 2, xMax - 2, yMax - 2, new Color32(170, 113, 61, 255));
            int width = xMax - xMin - 5;
            int height = yMax - yMin - 5;
            for (int step = 0; step <= Mathf.Min(width, height); step++)
            {
                SetPixel(pixels, xMin + 2 + step * width / Mathf.Min(width, height), yMin + 2 + step * height / Mathf.Min(width, height), new Color32(228, 165, 95, 255));
                SetPixel(pixels, xMax - 3 - step * width / Mathf.Min(width, height), yMin + 2 + step * height / Mathf.Min(width, height), new Color32(228, 165, 95, 255));
            }
        }

        /// <summary>
        /// pixels의 x·y가 스프라이트 안에 있을 때 해당 위치를 color로 바꾼다.
        /// 경계 밖 좌표는 무시한다.
        /// </summary>
        private static void SetPixel(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x < 0 || x >= SPRITE_WIDTH || y < 0 || y >= SPRITE_HEIGHT) return;
            pixels[y * SPRITE_WIDTH + x] = color;
        }
    }
}
