using System;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem.UI;

using IronRing.Combat;
using IronRing.Hud.Ui;

namespace IronRing.Combat.Editor
{
    /// <summary>기존 Main 씬의 저장되지 않은 변경을 건드리지 않고 독립 전투 POC 씬을 생성한다.</summary>
    public static class BattlePocBuild
    {
        private const string SCENE_PATH = "Assets/Scenes/BattlePoc.unity";
        private const string NEW_SCENE_PATH = "Assets/Scenes/BattleReference.unity";

        /// <summary>빈 추가 씬에 열차·전장·카드 UI를 배치하고 BattlePocDriver 참조를 연결해 씬으로 저장한다.</summary>
        public static void Generate()
        {
            GenerateScene(SCENE_PATH);
        }

        /// <summary>새 전투 씬을 처음부터 만들고 시안의 열차와 전장, HUD를 배치해 저장한다.</summary>
        public static void GenerateReferenceScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            GenerateScene(NEW_SCENE_PATH);
            Scene scene = EditorSceneManager.OpenScene(NEW_SCENE_PATH, OpenSceneMode.Single);
            RefreshVisualsForScene(scene, true);
            ApplyBattleLayoutForScene(scene);
            SceneManager.SetActiveScene(scene);
        }

        /// <summary>path에 지정된 새 씬에 전투 오브젝트와 기본 UI를 생성하고 저장한다.</summary>
        private static void GenerateScene(string path)
        {
            if (System.IO.File.Exists(path)) throw new InvalidOperationException("Battle scene already exists: " + path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                Camera camera = CreateCamera(scene);
                Transform train = CreateWorldRoot(scene, "Train");
                CreateQuad("기관차", train, new Vector3(-4.7f, -3.55f, 0), new Vector3(3.0f, 0.85f, 1), new Color(0.2f, 0.18f, 0.17f));
                CreateQuad("객차", train, new Vector3(-0.8f, -3.55f, 0), new Vector3(3.5f, 0.85f, 1), new Color(0.27f, 0.22f, 0.2f));
                CreateQuad("화물차", train, new Vector3(3.1f, -3.55f, 0), new Vector3(3.5f, 0.85f, 1), new Color(0.3f, 0.23f, 0.19f));
                CreateQuad("주포", train, new Vector3(-5.5f, -3f, -0.1f), new Vector3(0.45f, 0.7f, 1), new Color(0.75f, 0.5f, 0.18f));
                Transform[] slots = new Transform[4];
                float[] xs = { -3.5f, -1f, 1.5f, 4f };
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = CreateQuad($"포탑 칸 {i + 1}", train, new Vector3(xs[i], -3f, -0.1f), Vector3.one * 0.34f, new Color(0.26f, 0.22f, 0.2f)).transform;
                Transform enemies = CreateWorldRoot(scene, "Enemies");
                CreateQuad("철로", train, new Vector3(0, -4.25f, 0.2f), new Vector3(16, 0.13f, 1), new Color(0.45f, 0.33f, 0.25f));

                Canvas canvas = CreateCanvas(scene);
                TMP_Text status = CreateText("상태", canvas.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -65), new Vector2(0, 0), 26, TextAlignmentOptions.Center);
                status.text = "전투 준비";
                GameObject cardPanel = CreatePanel("레벨업 카드", canvas.transform, new Vector2(0.19f, 0.34f), new Vector2(0.81f, 0.66f), new Color(0.08f, 0.07f, 0.07f, 0.95f));
                Button[] buttons = new Button[3];
                TMP_Text[] labels = new TMP_Text[3];
                for (int i = 0; i < 3; i++)
                {
                    float left = 0.04f + i * 0.32f;
                    GameObject card = CreatePanel($"카드 {i + 1}", cardPanel.transform, new Vector2(left, 0.15f), new Vector2(left + 0.28f, 0.85f), new Color(0.5f, 0.32f, 0.15f));
                    buttons[i] = card.AddComponent<Button>();
                    labels[i] = CreateText("내용", card.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24, TextAlignmentOptions.Center);
                }
                GameObject resultPanel = CreatePanel("결과", canvas.transform, new Vector2(0.25f, 0.3f), new Vector2(0.75f, 0.7f), new Color(0.08f, 0.07f, 0.07f, 0.96f));
                TMP_Text result = CreateText("결과 문구", resultPanel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 30, TextAlignmentOptions.Center);

                GameObject controller = new GameObject("BattlePocDriver");
                SceneManager.MoveGameObjectToScene(controller, scene);
                BattlePocDriver driver = controller.AddComponent<BattlePocDriver>();
                SerializedObject serialized = new SerializedObject(driver);
                serialized.FindProperty("_battleCamera").objectReferenceValue = camera;
                serialized.FindProperty("_enemyRoot").objectReferenceValue = enemies;
                SerializedProperty slotProperty = serialized.FindProperty("_turretSlots");
                slotProperty.arraySize = slots.Length;
                for (int i = 0; i < slots.Length; i++) slotProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                serialized.FindProperty("_statusText").objectReferenceValue = status;
                serialized.FindProperty("_resultText").objectReferenceValue = result;
                serialized.FindProperty("_resultPanel").objectReferenceValue = resultPanel;
                serialized.FindProperty("_cardPanel").objectReferenceValue = cardPanel;
                SerializedProperty buttonProperty = serialized.FindProperty("_cardButtons");
                SerializedProperty textProperty = serialized.FindProperty("_cardTexts");
                buttonProperty.arraySize = textProperty.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    buttonProperty.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                    textProperty.GetArrayElementAtIndex(i).objectReferenceValue = labels[i];
                }
                SerializedProperty demo = serialized.FindProperty("_demoBuiltHabitats");
                demo.arraySize = 2;
                demo.GetArrayElementAtIndex(0).stringValue = "cleared_lot";
                demo.GetArrayElementAtIndex(1).stringValue = "logging_site";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>기존 Main 씬을 유지하며 저장된 BattlePoc 씬을 추가로 열어 Game View 확인을 준비한다.</summary>
        public static void OpenPreview()
        {
            EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
        }

        /// <summary>열린 전투 씬의 기존 열차와 카드 UI를 유지하며 축소 구도와 체력·경험치·배속 UI를 설정해 저장한다.</summary>
        public static void ApplyBattleLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(SCENE_PATH);
            if (!scene.isLoaded) throw new InvalidOperationException("Open BattlePoc first.");
            ApplyBattleLayoutForScene(scene);
        }

        /// <summary>열린 새 전투 씬의 화면 구도와 HUD를 다시 적용해 저장한다.</summary>
        public static void ApplyReferenceLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(NEW_SCENE_PATH);
            if (!scene.isLoaded) throw new InvalidOperationException("Open BattleReference first.");
            ApplyBattleLayoutForScene(scene);
        }

        /// <summary>새 전투 씬에 지면 배경, 추가 객차와 거치대 테두리, 공용 HUD 테마를 설정해 저장한다.</summary>
        public static void ApplyReferenceUpgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(NEW_SCENE_PATH);
            if (!scene.isLoaded) throw new InvalidOperationException("Open BattleReference first.");
            Transform train = null;
            Canvas canvas = null;
            BattlePocDriver driver = null;
            SpriteRenderer background = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Train") train = root.transform;
                if (root.name == "Battle Canvas") canvas = root.GetComponent<Canvas>();
                if (root.name == "Battle Scenery") background = root.transform.Find("Ruined Battlefield").GetComponent<SpriteRenderer>();
                if (root.TryGetComponent(out BattlePocDriver found)) driver = found;
            }
            if (train == null || canvas == null || driver == null || background == null) throw new InvalidOperationException("Battle scene objects missing.");
            background.sprite = LoadBattleSprite("Assets/Art/Generated/Battle/battlefield_ground.png");
            SerializedObject serialized = new SerializedObject(driver);
            serialized.FindProperty("_spawnY").floatValue = 3.8f;
            SerializedProperty slots = serialized.FindProperty("_turretSlots");
            slots.arraySize = 8;
            SpriteRenderer source = train.Find("Side View Train").GetComponent<SpriteRenderer>();
            const string carPath = "Assets/Art/Generated/Battle/battle_car.asset";
            Sprite car = AssetDatabase.LoadAssetAtPath<Sprite>(carPath);
            if (car == null)
            {
                Texture2D texture = source.sprite.texture;
                car = Sprite.Create(texture, new Rect(texture.width * 0.335f, 0f, texture.width * 0.33f, texture.height), new Vector2(0.5f, 0.5f), source.sprite.pixelsPerUnit);
                AssetDatabase.CreateAsset(car, carPath);
            }
            SerializedProperty cars = serialized.FindProperty("_extraCars");
            cars.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                string name = $"추가 객차 {i + 1}";
                Transform existing = train.Find(name);
                GameObject carriage = existing != null ? existing.gameObject : new GameObject(name, typeof(SpriteRenderer));
                carriage.transform.SetParent(train, false);
                carriage.transform.localPosition = new Vector3(10.3f + 5.15f * i, -2.15f, 0f);
                carriage.transform.localScale = source.transform.localScale;
                carriage.GetComponent<SpriteRenderer>().sprite = car;
                carriage.SetActive(false);
                cars.GetArrayElementAtIndex(i).objectReferenceValue = carriage;
                for (int j = 0; j < 2; j++)
                {
                    int index = 4 + i * 2 + j;
                    string slotName = $"포탑 칸 {index + 1}";
                    Transform slot = train.Find(slotName);
                    if (slot == null)
                    {
                        slot = new GameObject(slotName).transform;
                        slot.SetParent(train, false);
                        slot.localPosition = new Vector3(9.05f + 5.15f * i + 2.45f * j, -1.33f, -0.25f);
                        CreateQuad("Mount Base", slot, slot.position + new Vector3(0f, 0.02f, -0.05f), new Vector3(0.26f, 0.06f, 1f), new Color(0.72f, 0.48f, 0.25f));
                    }
                    slots.GetArrayElementAtIndex(index).objectReferenceValue = slot;
                    slot.gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < slots.arraySize; i++)
            {
                Transform slot = (Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                Transform old = slot.Find("Selection Outline");
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                GameObject outline = new GameObject("Selection Outline", typeof(LineRenderer));
                outline.transform.SetParent(slot, false);
                outline.transform.localPosition = new Vector3(0f, 0.2f, -0.2f);
                LineRenderer line = outline.GetComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 4;
                line.SetPositions(new[] { new Vector3(-0.7f, -0.3f, 0f), new Vector3(-0.7f, 0.5f, 0f), new Vector3(0.7f, 0.5f, 0f), new Vector3(0.7f, -0.3f, 0f) });
                line.startWidth = line.endWidth = 0.07f;
                line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                line.sortingOrder = 20;
                line.enabled = false;
            }
            Transform placement = canvas.transform.Find("전투 HUD/거치대 선택");
            if (placement != null) UnityEngine.Object.DestroyImmediate(placement.gameObject);
            serialized.FindProperty("_placementPanel").objectReferenceValue = null;
            serialized.FindProperty("_placementButtons").arraySize = 0;
            serialized.FindProperty("_placementTexts").arraySize = 0;
            UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Prefabs/UI/UiTheme.asset");
            if (theme == null) throw new InvalidOperationException("UiTheme missing.");
            foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = theme.Font;
                label.color = theme.Text;
            }
            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "채움" || image.name == "포탑 그림" || image.name == "전투 HUD") continue;
                bool button = image.GetComponent<Button>() != null;
                image.sprite = button ? theme.ButtonSprite : theme.PanelSprite;
                if (image.sprite != null) image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>열린 전투 씬의 열차 좌우 방향을 반전하고 씬에 저장한다.</summary>
        public static void FaceTrainRight()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(NEW_SCENE_PATH);
            if (!scene.isLoaded) throw new InvalidOperationException("Open BattleReference first.");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Train") continue;
                Vector3 scale = root.transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                root.transform.localScale = scale;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }
            throw new InvalidOperationException("Train missing.");
        }

        /// <summary>열린 전투 씬의 단순 선로 대신 침목과 레일 스프라이트를 배치하고 씬에 저장한다.</summary>
        public static void ApplyBattleRailway()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(NEW_SCENE_PATH);
            if (!scene.isLoaded) throw new InvalidOperationException("Open BattleReference first.");
            Transform train = null;
            Transform railway = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Train") train = root.transform;
                if (root.name == "Battle Railway") railway = root.transform;
            }
            if (train == null || train.Find("Rail") == null || train.Find("Rail Bed") == null) throw new InvalidOperationException("Existing train rail missing.");
            Transform oldRail = train.Find("Rail");
            Vector3 position = oldRail.position;
            oldRail.gameObject.SetActive(false);
            train.Find("Rail Bed").gameObject.SetActive(false);
            if (railway == null) railway = CreateWorldRoot(scene, "Battle Railway");
            SpriteRenderer renderer = railway.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = railway.gameObject.AddComponent<SpriteRenderer>();
            Sprite sprite = LoadBattleSprite("Assets/Art/Generated/Battle/battle_rail.png");
            renderer.sprite = sprite;
            railway.position = new Vector3(0f, position.y - 0.16f, 0.4f);
            railway.localScale = new Vector3(23f / sprite.bounds.size.x, 0.55f, 1f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>열린 전투 씬의 선로에 얕은 사선 스프라이트를 적용하고 높이를 낮춰 씬 변경 상태로 남긴다.</summary>
        public static void ApplyShallowRailway()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath(NEW_SCENE_PATH);
            if (!scene.isLoaded) throw new InvalidOperationException("Open BattleReference first.");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Battle Railway") continue;
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                Undo.RecordObjects(new UnityEngine.Object[] { renderer, root.transform }, "Adjust battle railway");
                renderer.sprite = LoadBattleSprite("Assets/Art/Generated/Battle/battle_rail_dense.png");
                Vector3 scale = root.transform.localScale;
                float heightChange = scale.y - 0.38f;
                scale.y = 0.38f;
                root.transform.localScale = scale;
                Vector3 position = root.transform.position;
                position.y += heightChange * 0.47f;
                root.transform.position = position;
                EditorSceneManager.MarkSceneDirty(scene);
                return;
            }
            throw new InvalidOperationException("Battle Railway missing.");
        }

        /// <summary>보관된 기존 전투 씬을 프로젝트에서 제거하고 빌드 목록의 경로를 새 전투 씬으로 교체한다.</summary>
        public static void ReplaceOldBattleScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (!System.IO.File.Exists("archive/BattlePoc.unity") || !System.IO.File.Exists(NEW_SCENE_PATH))
                throw new InvalidOperationException("Archived old scene or new scene missing.");
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool found = false;
            for (int i = 0; i < scenes.Length; i++)
                if (scenes[i].path == SCENE_PATH)
                {
                    scenes[i] = new EditorBuildSettingsScene(NEW_SCENE_PATH, scenes[i].enabled);
                    found = true;
                }
            if (!found) throw new InvalidOperationException("Old battle scene not in build settings.");
            EditorBuildSettings.scenes = scenes;
            if (!AssetDatabase.DeleteAsset(SCENE_PATH)) throw new InvalidOperationException("Could not remove old scene.");
        }

        /// <summary>지정한 scene의 열차와 카드 UI를 시안 구도에 맞추고 HUD를 연결해 저장한다.</summary>
        private static void ApplyBattleLayoutForScene(Scene scene)
        {
            Transform train = null;
            Canvas canvas = null;
            BattlePocDriver driver = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Train") train = root.transform;
                if (root.name == "Battle Canvas") canvas = root.GetComponent<Canvas>();
                if (root.TryGetComponent(out BattlePocDriver found)) driver = found;
            }
            if (train == null || canvas == null || driver == null) throw new InvalidOperationException("Battle scene objects missing.");
            train.localScale = Vector3.one * 0.5f;
            train.position = new Vector3(0f, -3.4f, 0f);
            SerializedObject serialized = new SerializedObject(driver);
            serialized.FindProperty("_trainY").floatValue = -4.875f;
            serialized.FindProperty("_spawnY").floatValue = 5.5f;
            serialized.FindProperty("_spawnHalfWidth").floatValue = 9f;
            if (scene.path == NEW_SCENE_PATH)
            {
                serialized.FindProperty("_distantEnemyHeight").floatValue = 0.65f;
                serialized.FindProperty("_nearEnemyHeight").floatValue = 1.15f;
            }
            RectTransform status = ((TMP_Text)serialized.FindProperty("_statusText").objectReferenceValue).rectTransform;
            status.anchorMin = new Vector2(0.25f, 0.13f);
            status.anchorMax = new Vector2(0.75f, 0.18f);
            status.offsetMin = status.offsetMax = Vector2.zero;
            RectTransform cards = ((GameObject)serialized.FindProperty("_cardPanel").objectReferenceValue).GetComponent<RectTransform>();
            cards.anchorMin = new Vector2(0.27f, 0.52f);
            cards.anchorMax = new Vector2(0.73f, 0.82f);
            cards.offsetMin = cards.offsetMax = Vector2.zero;
            cards.GetComponent<Image>().color = new Color(0.12f, 0.07f, 0.09f, 0.94f);
            Transform oldTitle = cards.Find("레벨업 제목");
            if (oldTitle != null) UnityEngine.Object.DestroyImmediate(oldTitle.gameObject);
            TMP_Text[] cardLabels = ((GameObject)serialized.FindProperty("_cardPanel").objectReferenceValue).GetComponentsInChildren<TMP_Text>(true);
            Sprite turretArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/Battle/side_turret.png");
            foreach (TMP_Text label in cardLabels)
            {
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0.04f, 0.04f);
                labelRect.anchorMax = new Vector2(0.96f, 0.48f);
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                label.fontSize = 19f;
                label.enableAutoSizing = true;
                label.fontSizeMin = 15f;
                label.fontSizeMax = 19f;
                label.transform.parent.GetComponent<Image>().color = new Color(0.29f, 0.18f, 0.16f, 1f);
                Transform existing = label.transform.parent.Find("포탑 그림");
                if (existing == null)
                {
                    GameObject icon = CreatePanel("포탑 그림", label.transform.parent, new Vector2(0.18f, 0.5f), new Vector2(0.82f, 0.91f), Color.white);
                    Image illustration = icon.GetComponent<Image>();
                    illustration.sprite = turretArt;
                    illustration.preserveAspect = true;
                    illustration.raycastTarget = false;
                }
            }
            TMP_Text heading = CreateText("레벨업 제목", cards, new Vector2(0f, 0.84f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 26, TextAlignmentOptions.Center);
            heading.text = "레벨 업 · 포탑 선택";
            heading.color = new Color(1f, 0.78f, 0.43f);
            Transform old = canvas.transform.Find("전투 HUD");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject hud = CreatePanel("전투 HUD", canvas.transform, Vector2.zero, Vector2.one, Color.clear);
            hud.GetComponent<Image>().raycastTarget = false;
            GameObject bars = CreatePanel("체력 경험치", hud.transform, new Vector2(0.015f, 0.025f), new Vector2(0.22f, 0.115f), new Color(0.12f, 0.08f, 0.1f, 0.94f));
            Image hp = CreateBar("체력", bars.transform, 0.55f, new Color(0.55f, 0.75f, 0.26f));
            Image xp = CreateBar("경험치", bars.transform, 0.08f, new Color(0.94f, 0.57f, 0.16f));
            TMP_Text hpText = CreateText("체력 글자", bars.transform, new Vector2(0.04f, 0.55f), new Vector2(0.96f, 0.95f), Vector2.zero, Vector2.zero, 20, TextAlignmentOptions.Center);
            TMP_Text xpText = CreateText("경험치 글자", bars.transform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.48f), Vector2.zero, Vector2.zero, 20, TextAlignmentOptions.Center);
            GameObject speed = CreatePanel("배속", hud.transform, new Vector2(0.91f, 0.93f), new Vector2(0.955f, 0.985f), new Color(0.27f, 0.16f, 0.13f));
            Button speedButton = speed.AddComponent<Button>();
            TMP_Text speedText = CreateText("배속 글자", speed.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 23, TextAlignmentOptions.Center);
            speedText.text = "1x";
            GameObject pause = CreatePanel("일시정지", hud.transform, new Vector2(0.96f, 0.93f), new Vector2(0.995f, 0.985f), new Color(0.27f, 0.16f, 0.13f));
            Button pauseButton = pause.AddComponent<Button>();
            TMP_Text pauseText = CreateText("일시정지 글자", pause.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 23, TextAlignmentOptions.Center);
            pauseText.text = "Ⅱ";
            GameObject currency = CreatePanel("재화", hud.transform, new Vector2(0.795f, 0.93f), new Vector2(0.905f, 0.985f), new Color(0.19f, 0.12f, 0.13f, 0.96f));
            TMP_Text currencyText = CreateText("재화 글자", currency.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20, TextAlignmentOptions.Center);
            currencyText.color = new Color(1f, 0.79f, 0.45f);
            GameObject placement = CreatePanel("거치대 선택", hud.transform, new Vector2(0.22f, 0.14f), new Vector2(0.78f, 0.27f), new Color(0.1f, 0.07f, 0.09f, 0.96f));
            Button[] placementButtons = new Button[4];
            TMP_Text[] placementTexts = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float left = 0.015f + i * 0.247f;
                GameObject option = CreatePanel($"거치대 선택 {i + 1}", placement.transform, new Vector2(left, 0.1f), new Vector2(left + 0.23f, 0.9f), new Color(0.2f, 0.3f, 0.25f));
                placementButtons[i] = option.AddComponent<Button>();
                placementTexts[i] = CreateText("표시", option.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 19, TextAlignmentOptions.Center);
                placementTexts[i].enableAutoSizing = true;
                placementTexts[i].fontSizeMin = 14;
                placementTexts[i].fontSizeMax = 19;
            }
            placement.SetActive(false);
            serialized.FindProperty("_healthFill").objectReferenceValue = hp;
            serialized.FindProperty("_experienceFill").objectReferenceValue = xp;
            serialized.FindProperty("_healthText").objectReferenceValue = hpText;
            serialized.FindProperty("_experienceText").objectReferenceValue = xpText;
            serialized.FindProperty("_speedButton").objectReferenceValue = speedButton;
            serialized.FindProperty("_speedText").objectReferenceValue = speedText;
            serialized.FindProperty("_pauseButton").objectReferenceValue = pauseButton;
            serialized.FindProperty("_pauseText").objectReferenceValue = pauseText;
            serialized.FindProperty("_currencyText").objectReferenceValue = currencyText;
            serialized.FindProperty("_placementPanel").objectReferenceValue = placement;
            SerializedProperty placementButtonProperty = serialized.FindProperty("_placementButtons");
            SerializedProperty placementTextProperty = serialized.FindProperty("_placementTexts");
            placementButtonProperty.arraySize = placementTextProperty.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                placementButtonProperty.GetArrayElementAtIndex(i).objectReferenceValue = placementButtons[i];
                placementTextProperty.GetArrayElementAtIndex(i).objectReferenceValue = placementTexts[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>부모 패널의 y 위치에 색상이 채워지는 막대를 만들고 Image를 반환한다.</summary>
        private static Image CreateBar(string name, Transform parent, float y, Color color)
        {
            GameObject back = CreatePanel(name, parent, new Vector2(0.04f, y), new Vector2(0.96f, y + 0.4f), new Color(0.15f, 0.11f, 0.12f));
            GameObject fill = CreatePanel("채움", back.transform, Vector2.zero, Vector2.one, color);
            Image image = fill.GetComponent<Image>();
            return image;
        }

        /// <summary>현재 빌드 씬 목록을 보존하며 BattlePoc 씬을 목록 끝에 추가한다.</summary>
        public static void IncludeInBuild()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in scenes)
                if (scene.path == SCENE_PATH) return;
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            Array.Copy(scenes, updated, scenes.Length);
            updated[scenes.Length] = new EditorBuildSettingsScene(SCENE_PATH, true);
            EditorBuildSettings.scenes = updated;
        }

        /// <summary>저장된 전투 씬을 추가로 열어 모든 TMP 문구에 물마루 글꼴을 연결하고 저장한 뒤 닫는다.</summary>
        public static void ApplyKoreanFont()
        {
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Mulmaru SDF.asset");
                if (font == null) throw new InvalidOperationException("Missing Mulmaru TMP font asset.");
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                        text.font = font;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>기존 전투 씬의 열차 도형을 가로로 긴 열차 아트로 교체하고 포탑 위치를 열차 지붕에 배치해 저장한다.</summary>
        public static void RefreshVisuals()
        {
            Scene scene = SceneManager.GetSceneByPath(SCENE_PATH);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
            try { RefreshVisualsForScene(scene, false); }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }

        /// <summary>scene의 열차와 전장을 배치하고 reference이면 새 전장과 적 스프라이트를 사용한다.</summary>
        private static void RefreshVisualsForScene(Scene scene, bool reference)
        {
            {
                Transform train = null;
                BattlePocDriver driver = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Train") train = root.transform;
                    if (root.TryGetComponent(out BattlePocDriver found)) driver = found;
                }
                if (train == null || driver == null) throw new InvalidOperationException("Battle train or driver missing.");
                SerializedObject serialized = new SerializedObject(driver);
                Camera battleCamera = (Camera)serialized.FindProperty("_battleCamera").objectReferenceValue;
                battleCamera.orthographicSize = 6.3f;
                battleCamera.transform.position = new Vector3(0, 0.85f, -10f);
                SerializedProperty slots = serialized.FindProperty("_turretSlots");
                var kept = new System.Collections.Generic.HashSet<Transform>();
                for (int i = 0; i < slots.arraySize; i++) kept.Add(((Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue));
                for (int i = train.childCount - 1; i >= 0; i--)
                    if (!kept.Contains(train.GetChild(i))) UnityEngine.Object.DestroyImmediate(train.GetChild(i).gameObject);

                Sprite trainSprite = LoadBattleSprite("Assets/Art/Generated/Battle/side_train.png");
                Sprite turret = LoadBattleSprite("Assets/Art/Generated/Battle/side_turret.png");
                Sprite raider = reference ? LoadBattleSprite("Assets/Art/Generated/Battle/enemy_v2.png") : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/raider.png");
                if (turret == null || raider == null) throw new InvalidOperationException("Battle art sprites missing.");
                GameObject trainArt = new GameObject("Side View Train", typeof(SpriteRenderer));
                trainArt.transform.SetParent(train, false);
                trainArt.transform.position = new Vector3(0, -2.15f, 0);
                trainArt.transform.localScale = new Vector3(15.5f / trainSprite.bounds.size.x, 11.55f / trainSprite.bounds.size.x, 1f);
                trainArt.GetComponent<SpriteRenderer>().sprite = trainSprite;
                CreateQuad("Rail Bed", train, new Vector3(0, -3.12f, 0.3f), new Vector3(22f, 0.16f, 1), new Color(0.25f, 0.18f, 0.17f));
                CreateQuad("Rail", train, new Vector3(0, -3.02f, 0.2f), new Vector3(22f, 0.08f, 1), new Color(0.64f, 0.43f, 0.26f));
                GameObject mainGun = new GameObject("Main Gun", typeof(SpriteRenderer));
                mainGun.transform.SetParent(train, false);
                mainGun.transform.position = new Vector3(-3.25f, -1.22f, -0.24f);
                mainGun.transform.localScale = Vector3.one * (0.9f / turret.bounds.size.x);
                mainGun.GetComponent<SpriteRenderer>().sprite = turret;
                mainGun.GetComponent<SpriteRenderer>().color = new Color(1f, 0.78f, 0.48f);
                float[] positions = { -1.6f, 0.85f, 3.55f, 5.9f };
                for (int i = 0; i < slots.arraySize; i++)
                {
                    Transform slot = (Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                    slot.position = new Vector3(positions[i], -1.33f, -0.25f);
                    if (slot.TryGetComponent(out MeshRenderer meshRenderer)) UnityEngine.Object.DestroyImmediate(meshRenderer);
                    if (slot.TryGetComponent(out MeshFilter meshFilter)) UnityEngine.Object.DestroyImmediate(meshFilter);
                    SpriteRenderer renderer = slot.GetComponent<SpriteRenderer>();
                    if (renderer == null) renderer = slot.gameObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = null;
                    slot.localScale = Vector3.one;
                    Transform oldBase = slot.Find("Mount Base");
                    if (oldBase != null) UnityEngine.Object.DestroyImmediate(oldBase.gameObject);
                    CreateQuad("Mount Base", slot, slot.position + new Vector3(0f, 0.04f, -0.05f), new Vector3(0.52f, 0.12f, 1f), new Color(0.72f, 0.48f, 0.25f));
                }
                serialized.FindProperty("_trainY").floatValue = -2.95f;
                serialized.FindProperty("_spawnY").floatValue = 5.5f;
                serialized.FindProperty("_spawnHalfWidth").floatValue = 7.5f;
                serialized.FindProperty("_enemySprite").objectReferenceValue = raider;
                serialized.FindProperty("_perspectiveEnemies").boolValue = reference;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AddBattleScenery(scene, reference);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>전투 씬의 빈 전장에 폐허 소품과 먼지 바탕을 배치해 야외 방어전 구도를 만든다.</summary>
        private static void AddBattleScenery(Scene scene, bool reference)
        {
            GameObject old = null;
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == "Battle Scenery") old = root;
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            Transform scenery = CreateWorldRoot(scene, "Battle Scenery");
            Sprite background = LoadBattleSprite(reference ? "Assets/Art/Generated/Battle/battlefield_v2.png" : "Assets/Art/Generated/Battle/battlefield.png");
            GameObject backdrop = new GameObject("Ruined Battlefield", typeof(SpriteRenderer));
            backdrop.transform.SetParent(scenery, false);
            backdrop.transform.position = new Vector3(0, 0.85f, 2f);
            backdrop.transform.localScale = new Vector3(23f / background.bounds.size.x, 13f / background.bounds.size.y, 1f);
            backdrop.GetComponent<SpriteRenderer>().sprite = background;
        }

        /// <summary>전투 이미지 경로를 스프라이트로 가져오고 임포트된 에셋을 반환한다.</summary>
        private static Sprite LoadBattleSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>sprite를 목표 폭과 높이에 맞춰 회전 배치하고 열차 칸 오브젝트를 반환한다.</summary>
        private static GameObject CreateTrainSprite(Transform parent, string name, Sprite sprite, float x, float width, float height)
        {
            GameObject body = new GameObject(name, typeof(SpriteRenderer));
            body.transform.SetParent(parent, false);
            body.transform.position = new Vector3(x, -3.25f, 0);
            body.transform.rotation = Quaternion.Euler(0, 0, 90f);
            body.transform.localScale = new Vector3(height / sprite.bounds.size.x, width / sprite.bounds.size.y, 1);
            body.GetComponent<SpriteRenderer>().sprite = sprite;
            return body;
        }

        /// <summary>추가 씬에 직교 카메라를 만들고 전장이 들어오는 화면 크기를 설정해 반환한다.</summary>
        private static Camera CreateCamera(Scene scene)
        {
            GameObject go = new GameObject("Battle Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.3f;
            camera.backgroundColor = new Color(0.35f, 0.24f, 0.22f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0, 0.85f, -10);
            go.tag = "MainCamera";
            return camera;
        }

        /// <summary>이름을 붙인 빈 전장 루트를 추가 씬에 만들고 Transform을 반환한다.</summary>
        private static Transform CreateWorldRoot(Scene scene, string name)
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        /// <summary>지정한 위치·크기·색의 전장 도형을 부모 아래에 만들고 객체를 반환한다.</summary>
        private static GameObject CreateQuad(string name, Transform parent, Vector3 position, Vector3 size, Color color)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.position = position;
            quad.transform.localScale = size;
            quad.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = color };
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            return quad;
        }

        /// <summary>전투 UI용 Canvas와 입력 EventSystem을 추가 씬에 만들고 Canvas를 반환한다.</summary>
        private static Canvas CreateCanvas(Scene scene)
        {
            GameObject go = new GameObject("Battle Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            return canvas;
        }

        /// <summary>부모 UI 안에서 앵커 범위와 배경색을 가진 패널을 만들고 반환한다.</summary>
        private static GameObject CreatePanel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return go;
        }

        /// <summary>부모 UI의 앵커·여백·글자 크기에 맞는 TextMeshPro 문구를 만들고 반환한다.</summary>
        private static TMP_Text CreateText(string name, Transform parent, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, float size, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TMP_Text label = go.GetComponent<TMP_Text>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Mulmaru SDF.asset");
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            return label;
        }
    }
}
