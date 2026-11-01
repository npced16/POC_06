using System;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;

using IronRing.Hud.HubPhase;
using IronRing.Hud.Hotbar;
using IronRing.Hud.Items;
using IronRing.Hud.Npc;

namespace IronRing.Hud.Editor
{
    /// <summary>
    /// 스토리 018 제작 도구. Assets/Prefabs/UI/MainUI.prefab(루트 MainUI + HUD 자식 6개, 자식마다 UIDocument + HUD 스크립트)을
    /// 없을 때만 만들고 빠진 자식·컴포넌트만 보충한 뒤, Main 씬 루트에 MainUI 인스턴스가 없으면 놓고 저장한다.
    /// 이미 있는 값(Sort Order, 위치, UXML 교체)은 건드리지 않는다. 모달 대화상자를 띄우지 않는다.
    /// </summary>
    public static class BuildMainUi
    {
        private const string PARENT_FOLDER = "Assets/Prefabs";
        private const string FOLDER = PARENT_FOLDER + "/UI";
        private const string PREFAB = FOLDER + "/MainUI.prefab";
        private const string ROOT_NAME = "MainUI";
        private const string PANEL = "Assets/Fonts/Resources/KoreanPanelSettings.asset";
        private const string UXML = "Assets/UI/Hud/Resources/Hud/";
        private const string SCENE = "Assets/Scenes/Main.unity";
        private const string EVIDENCE = "production/qa/evidence/story-018-main-ui-prefab";
        private const string CAPTURE_NAME = "018-editmode-gameview";
        private const int CAPTURE_WAIT_UPDATES = 30;
        private const int CAPTURE_TIMEOUT_SECONDS = 120;

        /// <summary>자식 이름, HUD 스크립트, UXML 파일, 새로 만들 때의 Sort Order(스토리 018 이전 코드 값과 같음).</summary>
        private static readonly (string Name, Type Hud, string Uxml, int Order)[] CHILDREN =
        {
            ("TopHud", typeof(KoreanHud), "KoreanHud.uxml", 100),
            ("InteriorHud", typeof(IronRing.Hud.Interior.InteriorHud), "InteriorHud.uxml", 110),
            ("InventoryHud", typeof(InventoryHud), "InventoryHud.uxml", 120),
            ("PhaseHud", typeof(PhaseHud), "PhaseHud.uxml", 110),
            ("Hotbar", typeof(HotbarHud), "Hotbar.uxml", 120),
            ("NpcDialog", typeof(NpcDialogHud), "NpcDialog.uxml", 125),
        };

        /// <summary>MainUI 프리팹을 보장하고 Main 씬 루트에 인스턴스가 없을 때만 놓은 뒤 저장한다. 여러 번 실행해도 결과가 같다.</summary>
        [MenuItem("IronRing/UI/Generate MainUI And Place In Main")]
        public static void Generate()
        {
            EnsurePrefab();
            Scene scene = EditorSceneManager.OpenScene(SCENE);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            bool placed = scene.GetRootGameObjects().Any(root => root.name == ROOT_NAME
                || PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab);
            if (!placed)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = ROOT_NAME;
                EditorSceneManager.MarkSceneDirty(scene);
            }
            if (scene.isDirty) EditorSceneManager.SaveScene(scene, SCENE);
            AssetDatabase.SaveAssets();
            Debug.Log("MAINUI GENERATE: prefab=" + PREFAB + " sceneInstanceAdded=" + !placed);
        }

        /// <summary>프리팹이 없으면 빈 루트로 만들고, 빠진 자식·UIDocument·HUD 컴포넌트만 더한다. 바뀐 것이 있을 때만 저장한다.</summary>
        private static void EnsurePrefab()
        {
            if (!AssetDatabase.IsValidFolder(FOLDER)) AssetDatabase.CreateFolder(PARENT_FOLDER, "UI");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB) == null)
            {
                var empty = new GameObject(ROOT_NAME);
                PrefabUtility.SaveAsPrefabAsset(empty, PREFAB);
                UnityEngine.Object.DestroyImmediate(empty);
            }
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL);
            GameObject contents = PrefabUtility.LoadPrefabContents(PREFAB);
            try
            {
                bool changed = false;
                foreach (var child in CHILDREN)
                {
                    Transform node = contents.transform.Find(child.Name);
                    if (node == null)
                    {
                        node = new GameObject(child.Name).transform;
                        node.SetParent(contents.transform, false);
                        changed = true;
                    }
                    if (!node.TryGetComponent(out UIDocument document))
                    {
                        document = node.gameObject.AddComponent<UIDocument>();
                        document.panelSettings = panel;
                        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML + child.Uxml);
                        document.sortingOrder = child.Order;
                        changed = true;
                    }
                    if (node.GetComponent(child.Hud) == null)
                    {
                        node.gameObject.AddComponent(child.Hud);
                        changed = true;
                    }
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 편집 모드 Game 뷰를 앞으로 가져와 창 영역 화면 픽셀(InternalEditorUtility.ReadScreenPixel)을 PNG로 저장한다(기존 파일은 덮어쓰지 않고 -v2, -v3…).
        /// 화면 픽셀은 OS에서 맨 앞에 있는 창을 읽으므로 Unity 편집기가 앞 창(isApplicationActive)이 될 때까지 최대 CAPTURE_TIMEOUT_SECONDS 기다린다.
        /// (Game 뷰의 렌더 텍스처에는 UI Toolkit 오버레이가 들어가지 않아 HUD 증거로 쓸 수 없다.)
        /// 저장은 비동기라 완료 시 콘솔에 "MAINUI CAPTURE: 경로"를, 실패 시 "MAINUI CAPTURE FAILED: 사유"를 남긴다.
        /// </summary>
        public static void CaptureGameView()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) { Debug.LogError("MAINUI CAPTURE FAILED: GameView 형식을 찾지 못했습니다."); return; }
            EditorWindow window = EditorWindow.GetWindow(gameViewType);
            window.Focus();
            window.Repaint();
            double deadline = EditorApplication.timeSinceStartup + CAPTURE_TIMEOUT_SECONDS;
            int activeUpdates = 0;
            Debug.Log("MAINUI CAPTURE WAITING: Unity 편집기 창을 앞으로 가져오면 Game 뷰를 찍습니다(최대 " + CAPTURE_TIMEOUT_SECONDS + "초).");
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (window == null) { EditorApplication.update -= tick; Debug.LogError("MAINUI CAPTURE FAILED: Game 뷰가 닫혔습니다."); return; }
                if (!InternalEditorUtility.isApplicationActive)
                {
                    activeUpdates = 0;
                    if (EditorApplication.timeSinceStartup < deadline) return;
                    EditorApplication.update -= tick;
                    Debug.LogError("MAINUI CAPTURE FAILED: Unity 편집기가 앞 창이 아니어서 화면 픽셀이 다른 창을 가리킵니다.");
                    return;
                }
                window.Repaint();
                if (++activeUpdates < CAPTURE_WAIT_UPDATES) return;
                EditorApplication.update -= tick;
                try { ReadWindow(window); }
                catch (Exception error) { Debug.LogError("MAINUI CAPTURE FAILED: " + error.Message); }
            };
            EditorApplication.update += tick;
        }

        /// <summary>창의 화면 영역(포인트 × pixelsPerPoint)을 읽어 PNG로 쓴다. 한 가지 색뿐이면 증거로 쓰지 않고 실패로 알린다.</summary>
        private static void ReadWindow(EditorWindow window)
        {
            Rect area = window.position;
            float scale = EditorGUIUtility.pixelsPerPoint;
            int width = Mathf.RoundToInt(area.width * scale);
            int height = Mathf.RoundToInt(area.height * scale);
            Color[] pixels = InternalEditorUtility.ReadScreenPixel(new Vector2(area.x * scale, area.y * scale), width, height);
            if (pixels == null || pixels.Length != width * height) { Debug.LogError("MAINUI CAPTURE FAILED: 픽셀 수가 맞지 않습니다."); return; }
            if (pixels.All(pixel => pixel == pixels[0])) { Debug.LogError("MAINUI CAPTURE FAILED: 한 가지 색만 읽혔습니다(화면 읽기 실패)."); return; }
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
            Debug.Log("MAINUI CAPTURE: " + path.Replace('\\', '/') + " (" + width + "x" + height + ", pixelsPerPoint=" + scale + ")");
        }
    }
}
