using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;

using TMPro;

using IronRing.Art.Editor;
using IronRing.Hud.Editor.Screens;
using IronRing.Hud.Ui;

namespace IronRing.Hud.Editor
{
    /// <summary>
    /// 스토리 022 제작 도구. UiTheme 에셋, 화면 프리팹(Assets/Prefabs/UI/Screens), MainCanvas 프리팹(Canvas·CanvasScaler·GraphicRaycaster·UiScreens + 화면 자식 인스턴스),
    /// Main 씬 루트의 MainCanvas 인스턴스와 EventSystem(InputSystemUIInputModule)을 없을 때만 만들고 빠진 것만 보충한다.
    /// 이미 있는 프리팹·값(손으로 바꾼 위치, 색, 순서)은 덮어쓰지 않는다. 모달을 띄우지 않는다.
    /// </summary>
    public static class BuildMainCanvas
    {
        /// <summary>UI 프리팹 폴더.</summary>
        public const string FOLDER = "Assets/Prefabs/UI";
        /// <summary>화면 프리팹 폴더.</summary>
        public const string SCREENS_FOLDER = FOLDER + "/Screens";
        /// <summary>화면 부품 프리팹 폴더(슬롯·카드 등, 2단계).</summary>
        public const string PARTS_FOLDER = FOLDER + "/Parts";
        /// <summary>테마 에셋.</summary>
        public const string THEME = FOLDER + "/UiTheme.asset";
        /// <summary>캔버스 프리팹.</summary>
        public const string PREFAB = FOLDER + "/MainCanvas.prefab";
        private const string ROOT_NAME = "MainCanvas";
        private const string FONT = "Assets/Fonts/Mulmaru SDF.asset";
        private const string SCENE = "Assets/Scenes/Main.unity";
        private const string EVIDENCE = "production/qa/evidence/story-022-ugui-screens";
        private const string CAPTURE_NAME = "022-editmode-gameview";
        private const int CAPTURE_WAIT_UPDATES = 30;
        private const int CAPTURE_TIMEOUT_SECONDS = 120;

        /// <summary>
        /// 화면 id(= 프리팹 이름)와 생성 함수, 그리는 순서(하이어라키 순서)대로. 최종 순서:
        /// TopBar, HubPanel, Defeat, PhaseBanner, InteriorTitle, Hotbar, Toast, NpcDialog, Codex, RestEvent (스토리 039: Raid·CombatFx 제거).
        /// 2단계는 이 파일을 고치지 않고 Screens/&lt;Id&gt;ScreenBuilder.cs(public static GameObject Build(UiTheme))만 만든다.
        /// 빌더 형식은 리플렉션으로 찾고, 아직 없는 화면은 건너뛴다(병렬 작업 충돌 방지).
        /// </summary>
        public static readonly string[] SCREEN_IDS =
        {
            "TopBar", "Passengers", "HubPanel", "Defeat", "RunResult", "PhaseBanner", "InteriorTitle", "Hotbar", "Toast", "NpcDialog", "Codex", "RestEvent", "Onboarding", "DevSituations", // 스펙 001: 온보딩 팝업과 F9 개발자 상황 메뉴 화면 추가
            "GameMenu", // 스펙 003: 게임 메뉴(저장·백그라운드 실행·타이틀로·종료) 화면 추가
        };

        /// <summary>스토리 039: 보관함으로 옮긴 화면 id. MainCanvas에 인스턴스가 남아 있으면 지운다.</summary>
        private static readonly string[] REMOVED_SCREEN_IDS = { "Raid", "CombatFx", "Trade", "Warehouse" };

        /// <summary>SCREEN_IDS 순서대로, 빌더 형식이 존재하는 화면만 (id, 생성 함수) 쌍으로 돌려준다.</summary>
        public static (string Id, Func<UiTheme, GameObject> Build)[] SCREENS => ResolveScreens();

        private static (string Id, Func<UiTheme, GameObject> Build)[] ResolveScreens()
        {
            var list = new List<(string Id, Func<UiTheme, GameObject> Build)>();
            foreach (string id in SCREEN_IDS)
            {
                Type type = Type.GetType("IronRing.Hud.Editor.Screens." + id + "ScreenBuilder, Assembly-CSharp-Editor");
                System.Reflection.MethodInfo method = type?.GetMethod("Build", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new[] { typeof(UiTheme) }, null);
                if (method == null) continue;
                list.Add((id, theme => (GameObject)method.Invoke(null, new object[] { theme })));
            }
            return list.ToArray();
        }

        /// <summary>테마·화면 프리팹·캔버스 프리팹·씬 인스턴스를 보장한다. 여러 번 실행해도 결과가 같다.</summary>
        [MenuItem("IronRing/UI/Generate MainCanvas")]
        public static void Generate()
        {
            EnsureFolder(FOLDER);
            EnsureFolder(SCREENS_FOLDER);
            EnsureFolder(PARTS_FOLDER);
            UiTheme theme = EnsureTheme();
            var prefabs = new GameObject[SCREENS.Length];
            for (int i = 0; i < SCREENS.Length; i++)
            {
                var entry = SCREENS[i];
                // 표식 파일 .bridge/rebuild/<Id>가 있으면 그 화면 프리팹만 같은 경로에 다시 만들고(GUID 유지) 표식을 지운다.
                string marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".bridge", "rebuild", entry.Id);
                bool force = File.Exists(marker);
                prefabs[i] = UiKit.SavePrefab(SCREENS_FOLDER + "/" + entry.Id + ".prefab", () => entry.Build(theme), force);
                if (force) { File.Delete(marker); Debug.Log("MAINCANVAS REBUILT SCREEN: " + entry.Id); }
            }
            bool canvasChanged = EnsureCanvas(theme, prefabs);
            bool sceneChanged = EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("MAINCANVAS GENERATE: canvasChanged=" + canvasChanged + " sceneChanged=" + sceneChanged);
        }

        /// <summary>
        /// Main 씬의 TopBar 출발 버튼을 TopBar 프리팹의 위치와 크기로 맞춘다.
        /// 대상 씬 인스턴스의 RectTransform을 저장하고 다른 화면 설정은 변경하지 않는다.
        /// </summary>
        public static void AlignDepartButton()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing MainCanvas.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE) throw new InvalidOperationException("Main scene must be active.");
            IronRing.Hud.TopBarScreen screen = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<IronRing.Hud.TopBarScreen>(true)).FirstOrDefault();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SCREENS_FOLDER + "/TopBar.prefab");
            RectTransform target = screen != null ? screen.transform.Find("Depart") as RectTransform : null;
            RectTransform authored = source != null ? source.transform.Find("Depart") as RectTransform : null;
            if (target == null || authored == null) throw new InvalidOperationException("TopBar Depart button is missing.");
            target.anchorMin = authored.anchorMin;
            target.anchorMax = authored.anchorMax;
            target.pivot = authored.pivot;
            target.anchoredPosition = authored.anchoredPosition;
            target.sizeDelta = authored.sizeDelta;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Main 씬 상단 잔고의 동전과 숫자를 TopBar 프리팹 배치로 맞춘다.
        /// 기존 씬 인스턴스의 RectTransform과 잔고 글자 크기를 갱신해 저장한다.
        /// </summary>
        public static void AlignCurrencyDisplay()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before editing MainCanvas.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE) throw new InvalidOperationException("Main scene must be active.");
            IronRing.Hud.TopBarScreen screen = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<IronRing.Hud.TopBarScreen>(true)).FirstOrDefault();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SCREENS_FOLDER + "/TopBar.prefab");
            Transform sceneBar = screen != null ? screen.transform.Find("Bar") : null;
            Transform sourceBar = source != null ? source.transform.Find("Bar") : null;
            if (sceneBar == null || sourceBar == null) throw new InvalidOperationException("TopBar balance row is missing.");
            foreach (string name in new[] { "Coin", "Balance" })
            {
                RectTransform target = sceneBar.Find(name) as RectTransform;
                RectTransform authored = sourceBar.Find(name) as RectTransform;
                if (target == null || authored == null) throw new InvalidOperationException("TopBar currency element is missing: " + name);
                target.anchorMin = authored.anchorMin;
                target.anchorMax = authored.anchorMax;
                target.pivot = authored.pivot;
                target.anchoredPosition = authored.anchoredPosition;
                target.sizeDelta = authored.sizeDelta;
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                TMP_Text targetText = target.GetComponent<TMP_Text>();
                TMP_Text sourceText = authored.GetComponent<TMP_Text>();
                if (targetText == null || sourceText == null) continue;
                targetText.fontSize = sourceText.fontSize;
                targetText.enableAutoSizing = sourceText.enableAutoSizing;
                targetText.fontSizeMin = sourceText.fontSizeMin;
                targetText.fontSizeMax = sourceText.fontSizeMax;
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetText);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        /// <summary>테마가 없으면 만들고 색을 팔레트 JSON에서 채운다. 이미 있으면 비어 있는 글꼴·스프라이트 참조만 채운다.</summary>
        private static UiTheme EnsureTheme()
        {
            UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(THEME);
            bool created = theme == null;
            if (created)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, THEME);
            }
            var so = new SerializedObject(theme);
            if (created)
            {
                SteampunkPalette palette = SteampunkPalette.Load();
                so.FindProperty("_panel").colorValue = palette["ui_panel"].B;
                so.FindProperty("_border").colorValue = palette["brass"].B;
                so.FindProperty("_accent").colorValue = palette["glow"].B;
                so.FindProperty("_button").colorValue = palette["copper"].B;
                so.FindProperty("_text").colorValue = palette["smoke"].B;
                so.FindProperty("_muted").colorValue = palette["smoke"].S;
            }
            FillRef(so, "_font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT));
            FillRef(so, "_panelSprite", Sprite("ui_panel"));
            FillRef(so, "_buttonSprite", Sprite("ui_button"));
            FillRef(so, "_buttonPressedSprite", Sprite("ui_button_pressed"));
            FillRef(so, "_slotSprite", Sprite("ui_slot"));
            FillRef(so, "_slotSelectedSprite", Sprite("ui_slot_selected"));
            FillRef(so, "_toastSprite", Sprite("ui_toast"));
            if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(theme);
            return theme;
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteGenUi.OUTPUT + name + ".png");

        private static void FillRef(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property.objectReferenceValue == null && value != null) property.objectReferenceValue = value;
        }

        /// <summary>캔버스 프리팹이 없으면 만들고, 빠진 컴포넌트·화면 인스턴스·UiScreens 등록·테마 참조만 더한다. 바뀐 것이 있을 때만 저장한다.</summary>
        private static bool EnsureCanvas(UiTheme theme, GameObject[] prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB) == null)
            {
                var root = new GameObject(ROOT_NAME, typeof(RectTransform)) { layer = UiKit.UI_LAYER };
                root.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 64;
                root.AddComponent<GraphicRaycaster>();
                root.AddComponent<UiScreens>();
                PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
                UnityEngine.Object.DestroyImmediate(root);
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(PREFAB);
            try
            {
                bool changed = false;
                if (contents.GetComponent<Canvas>() == null) { contents.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; changed = true; }
                if (contents.GetComponent<CanvasScaler>() == null) { contents.AddComponent<CanvasScaler>(); changed = true; }
                if (contents.GetComponent<GraphicRaycaster>() == null) { contents.AddComponent<GraphicRaycaster>(); changed = true; }
                UiScreens screens = contents.GetComponent<UiScreens>();
                if (screens == null) { screens = contents.AddComponent<UiScreens>(); changed = true; }
                // 스토리 039: SCREEN_IDS에서 빠진 화면(Raid·CombatFx)의 인스턴스는 캔버스에서 지운다.
                for (int c = contents.transform.childCount - 1; c >= 0; c--)
                {
                    Transform child = contents.transform.GetChild(c);
                    if (!Array.Exists(REMOVED_SCREEN_IDS, id => child.name == id || child.name.StartsWith(id + " ("))) continue;
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    changed = true;
                }
                for (int i = 0; i < SCREENS.Length; i++)
                {
                    if (FindScreen(contents.transform, prefabs[i], SCREENS[i].Id) != null) continue;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], contents.transform);
                    instance.name = SCREENS[i].Id;
                    int index = 0;
                    for (int j = 0; j < i; j++)
                    {
                        Transform earlier = FindScreen(contents.transform, prefabs[j], SCREENS[j].Id);
                        if (earlier != null) index = Math.Max(index, earlier.GetSiblingIndex() + 1);
                    }
                    instance.transform.SetSiblingIndex(index);
                    changed = true;
                }
                var so = new SerializedObject(screens);
                FillRef(so, "_theme", theme);
                SerializedProperty list = so.FindProperty("_screens");
                // 제거된 화면을 가리키던 빈 참조도 목록에서 정리한다.
                for (int k = list.arraySize - 1; k >= 0; k--)
                    if (list.GetArrayElementAtIndex(k).objectReferenceValue == null) list.DeleteArrayElementAtIndex(k);
                var registered = new HashSet<UnityEngine.Object>();
                for (int k = 0; k < list.arraySize; k++) registered.Add(list.GetArrayElementAtIndex(k).objectReferenceValue);
                for (int i = 0; i < SCREENS.Length; i++)
                {
                    Transform node = FindScreen(contents.transform, prefabs[i], SCREENS[i].Id);
                    UiScreen screen = node != null ? node.GetComponent<UiScreen>() : null;
                    if (screen == null || registered.Contains(screen)) continue;
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = screen;
                    registered.Add(screen);
                }
                if (so.ApplyModifiedPropertiesWithoutUndo()) changed = true;
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, PREFAB);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>캔버스 자식 중 prefab의 인스턴스이거나 이름이 id인 것.</summary>
        private static Transform FindScreen(Transform root, GameObject prefab, string id)
        {
            foreach (Transform child in root)
                if (child.name == id || PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == prefab) return child;
            return null;
        }

        /// <summary>
        /// Main 씬 루트에 MainCanvas 인스턴스가 없으면 놓는다. EventSystem은 씬에 하나라도 있으면 재사용하고(InputSystemUIInputModule이 없으면 추가만),
        /// 없을 때만 루트에 새로 만든다. 바뀌었을 때만 저장한다.
        /// </summary>
        private static bool EnsureScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE) scene = EditorSceneManager.OpenScene(SCENE);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            GameObject[] roots = scene.GetRootGameObjects();
            bool changed = false;
            if (!roots.Any(root => root.name == ROOT_NAME || PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab))
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = ROOT_NAME;
                changed = true;
            }
            EventSystem events = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).FirstOrDefault();
            if (events == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(go, scene);
                changed = true;
            }
            else if (events.GetComponent<InputSystemUIInputModule>() == null)
            {
                events.gameObject.AddComponent<InputSystemUIInputModule>();
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, SCENE);
            }
            return changed;
        }

        /// <summary>
        /// 스토리 022 3단계. Main 씬에서 옛 UI Toolkit 루트 "MainUI"를 지우고 씬을 저장한다. 없으면 아무것도 하지 않는다(여러 번 불러도 같다).
        /// 콘솔에 "MAINCANVAS LEGACY UI: removed|absent"를 남긴다.
        /// </summary>
        public static void RemoveLegacyUi()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE) scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            bool removed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "MainUI") continue;
                UnityEngine.Object.DestroyImmediate(root);
                removed = true;
            }
            if (removed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("MAINCANVAS LEGACY UI: " + (removed ? "removed" : "absent"));
        }

        /// <summary>
        /// 편집 모드 Game 뷰를 앞으로 가져와 창 영역 화면 픽셀(InternalEditorUtility.ReadScreenPixel)을
        /// production/qa/evidence/story-022-ugui-screens/022-editmode-gameview.png로 저장한다(덮어쓰지 않고 -v2, -v3…).
        /// Unity 편집기가 OS 앞 창이 될 때까지 최대 CAPTURE_TIMEOUT_SECONDS 기다린다. 비동기라 콘솔에 "MAINCANVAS CAPTURE: 경로"를 남긴다.
        /// </summary>
        public static void CaptureGameView()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) { Debug.LogError("MAINCANVAS CAPTURE FAILED: GameView 형식을 찾지 못했습니다."); return; }
            EditorWindow window = EditorWindow.GetWindow(gameViewType);
            window.Focus();
            window.Repaint();
            double deadline = EditorApplication.timeSinceStartup + CAPTURE_TIMEOUT_SECONDS;
            int activeUpdates = 0;
            Debug.Log("MAINCANVAS CAPTURE WAITING: Unity 편집기 창이 앞으로 오면 Game 뷰를 찍습니다(최대 " + CAPTURE_TIMEOUT_SECONDS + "초).");
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (window == null) { EditorApplication.update -= tick; Debug.LogError("MAINCANVAS CAPTURE FAILED: Game 뷰가 닫혔습니다."); return; }
                if (!InternalEditorUtility.isApplicationActive)
                {
                    activeUpdates = 0;
                    if (EditorApplication.timeSinceStartup < deadline) return;
                    EditorApplication.update -= tick;
                    Debug.LogError("MAINCANVAS CAPTURE FAILED: Unity 편집기가 앞 창이 아닙니다.");
                    return;
                }
                window.Repaint();
                if (++activeUpdates < CAPTURE_WAIT_UPDATES) return;
                EditorApplication.update -= tick;
                try { ReadWindow(window); }
                catch (Exception error) { Debug.LogError("MAINCANVAS CAPTURE FAILED: " + error.Message); }
            };
            EditorApplication.update += tick;
        }

        private static void ReadWindow(EditorWindow window)
        {
            Rect area = window.position;
            float scale = EditorGUIUtility.pixelsPerPoint;
            int width = Mathf.RoundToInt(area.width * scale);
            int height = Mathf.RoundToInt(area.height * scale);
            Color[] pixels = InternalEditorUtility.ReadScreenPixel(new Vector2(area.x * scale, area.y * scale), width, height);
            if (pixels == null || pixels.Length != width * height) { Debug.LogError("MAINCANVAS CAPTURE FAILED: 픽셀 수가 맞지 않습니다."); return; }
            if (pixels.All(pixel => pixel == pixels[0])) { Debug.LogError("MAINCANVAS CAPTURE FAILED: 한 가지 색만 읽혔습니다."); return; }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, EVIDENCE);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, CAPTURE_NAME + ".png");
            for (int version = 2; File.Exists(path); version++) path = Path.Combine(folder, CAPTURE_NAME + "-v" + version + ".png");
            File.WriteAllBytes(path, png);
            Debug.Log("MAINCANVAS CAPTURE: " + path.Replace('\\', '/') + " (" + width + "x" + height + ")");
        }
    }
}
