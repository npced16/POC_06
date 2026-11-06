using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using UnityEditor;
using UnityEditor.SceneManagement;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>잔고와 오른쪽 설계·승객 아이콘, 오른쪽 아래 출발 버튼을 가진 TopBar 화면을 만든다.</summary>
    public static class TopBarScreenBuilder
    {
        /// <summary>theme과 아이콘 카탈로그로 최소 HUD를 구성하고 TopBarScreen 참조를 연결한다.</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("TopBar", true);
            TopBarScreen screen = root.AddComponent<TopBarScreen>();

            RectTransform bar = UiKit.Node(root.transform, "Bar");
            UiKit.StretchTop(bar, 54, 12, 16, 16);
            IconCatalog icons = AssetDatabase.LoadAssetAtPath<IconCatalog>("Assets/Prefabs/UI/IconCatalog.asset");
            var coin = UiKit.Image(bar.transform, "Coin", icons.Get("ui_coin"), Color.white, false);
            UiKit.Place(coin.rectTransform, Vector2.one, Vector2.one, new Vector2(-4, -2), new Vector2(24, 24));
            var balance = UiKit.Label(bar.transform, "Balance", "84", UiTextSize.Normal, theme);
            UiKit.Place(balance.rectTransform, Vector2.one, Vector2.one, new Vector2(-32, -2), new Vector2(256, 24));
            balance.alignment = TextAlignmentOptions.MidlineRight;
            balance.fontStyle = FontStyles.Bold;
            balance.textWrappingMode = TextWrappingModes.NoWrap;
            balance.overflowMode = TextOverflowModes.Overflow;
            balance.enableAutoSizing = false;
            balance.fontSize = 18;
            balance.outlineColor = theme.Panel;
            balance.outlineWidth = 0.2f;
            UiKit.Bind(screen, "_balance", balance); UiKit.Bind(screen, "_coin", coin); UiKit.Bind(screen, "_icons", icons);
            Button codex = UiKit.Button(root.transform, "Codex", "", theme);
            UiKit.Place((RectTransform)codex.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -82), new Vector2(58, 58));
            Image codexIcon = UiKit.Image(codex.transform, "Icon", CodexIcon(), Color.white);
            UiKit.Place(codexIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(34, 34));

            Button passengers = UiKit.Button(root.transform, "Passengers", "", theme);
            UiKit.Place((RectTransform)passengers.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -150), new Vector2(58, 58));
            Image passengersIcon = UiKit.Image(passengers.transform, "Icon", icons.Get("ui_people"), Color.white);
            UiKit.Place(passengersIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-3, 2), new Vector2(34, 34));
            TextMeshProUGUI passengerCount = UiKit.Label(passengers.transform, "Count", "0", UiTextSize.Normal, theme);
            UiKit.Place(passengerCount.rectTransform, Vector2.one, Vector2.one, new Vector2(3, 2), new Vector2(24, 22));
            passengerCount.fontSize = 18;

            Button depart = UiKit.Button(root.transform, "Depart", Strings.Get("hud_depart"), theme);
            UiKit.Place((RectTransform)depart.transform, Vector2.zero, Vector2.zero, new Vector2(20, 166), new Vector2(76, 76));
            TMP_Text departLabel = depart.GetComponentInChildren<TMP_Text>(true);
            UiKit.Place((RectTransform)departLabel.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 4), new Vector2(68, 24));
            departLabel.fontSize = 18;
            Image departIcon = UiKit.Image(depart.transform, "Icon", icons.Get("ui_depart"), Color.white);
            UiKit.Place(departIcon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -7), new Vector2(34, 34));

            Button gameMenu = UiKit.Button(root.transform, "GameMenuButton", "", theme); // 스펙 003: 오른쪽 위 잔고 왼쪽에 게임 메뉴 톱니 버튼 추가
            UiKit.Place((RectTransform)gameMenu.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -12), new Vector2(52, 52));
            Image gameMenuIcon = UiKit.Image(gameMenu.transform, "Icon", icons.Get("goods_parts"), Color.white); // 스펙 003: 톱니 그림은 기존 톱니+렌치 아이콘(goods_parts) 재사용
            UiKit.Place(gameMenuIcon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(34, 34)); // 스펙 003: 톱니 아이콘 크기

            UiKit.Bind(screen, "_depart", depart);
            UiKit.Bind(screen, "_gameMenu", gameMenu); // 스펙 003: 톱니 버튼을 TopBarScreen에 연결
            UiKit.Bind(screen, "_codex", codex);
            UiKit.Bind(screen, "_passengers", passengers);
            UiKit.Bind(screen, "_passengerCount", passengerCount);
            BuildResourceRow(root.transform, screen, theme);
            BuildHabitatInfo(root.transform, screen, theme);
            return root;
        }

        /// <summary>parent에 클릭을 막지 않는 서식지 정보창을 만들고 screen에 제목과 내용을 연결한다.</summary>
        private static void BuildHabitatInfo(Transform parent, TopBarScreen screen, UiTheme theme)
        {
            UnityEngine.UI.Image panel = UiKit.Panel(parent, "HabitatInfo", theme, false);
            UiKit.Place(panel.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(280, 110));
            TMP_Text title = UiKit.Label(panel.transform, "Title", "서식지", UiTextSize.Normal, theme, TextAlignmentOptions.MidlineLeft);
            UiKit.Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -8), new Vector2(256, 28));
            title.fontStyle = FontStyles.Bold;
            TMP_Text body = UiKit.Label(panel.transform, "Body", "거주자 없음\n생산 없음", UiTextSize.Small, theme, TextAlignmentOptions.TopLeft);
            UiKit.Place(body.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -40), new Vector2(256, 62));
            panel.gameObject.SetActive(false);
            UiKit.Bind(screen, "_habitatInfo", panel.rectTransform);
            UiKit.Bind(screen, "_habitatInfoTitle", title);
            UiKit.Bind(screen, "_habitatInfoBody", body);
        }

        /// <summary>저장된 Main 씬과 TopBar 프리팹에 서식지 정보창을 추가하고 각 화면의 참조를 저장한다.</summary>
        public static void AddHabitatInfo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play mode before editing the HUD.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity" || scene.isDirty) throw new System.InvalidOperationException("Main scene must be active and saved.");
            TopBarScreen target = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TopBarScreen found = root.GetComponentInChildren<TopBarScreen>(true);
                if (found != null) { if (target != null) throw new System.InvalidOperationException("Multiple TopBar screens found."); target = found; }
            }
            if (target == null) throw new System.InvalidOperationException("Main scene TopBar is missing.");
            UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(BuildMainCanvas.THEME);
            const string path = "Assets/Prefabs/UI/Screens/TopBar.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (prefab.transform.Find("HabitatInfo") == null) BuildHabitatInfo(prefab.transform, prefab.GetComponent<TopBarScreen>(), theme);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            Transform existing = target.transform.Find("HabitatInfo");
            if (existing == null) BuildHabitatInfo(target.transform, target, theme);
            else
            {
                UiKit.Bind(target, "_habitatInfo", (RectTransform)existing);
                UiKit.Bind(target, "_habitatInfoTitle", existing.Find("Title").GetComponent<TMP_Text>());
                UiKit.Bind(target, "_habitatInfoBody", existing.Find("Body").GetComponent<TMP_Text>());
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>parent 아래에 해금 순서별 재료 아이콘과 수량을 세로로 만들고 screen에 표시 참조를 저장한다.</summary>
        private static void BuildResourceRow(Transform parent, TopBarScreen screen, UiTheme theme)
        {
            string[] ids = { "tile_stone", "tile_timber", "tile_water_can", "tile_grain", "tile_coal", "tile_iron_stock", "tile_copper", "tile_steam_pipe", "tile_gear", "tile_wire" };
            UnityEngine.Object[] sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Generated/town_resource_icons.png");
            RectTransform row = UiKit.Node(parent, "Resources");
            UiKit.Place(row, Vector2.one, Vector2.one, new Vector2(-20, -40), new Vector2(180, 320));
            VerticalLayoutGroup layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperRight;
            layout.spacing = 1f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var items = new List<GameObject>();
            var counts = new List<TMP_Text>();
            foreach (string id in ids)
            {
                RectTransform item = UiKit.Node(row, id);
                item.sizeDelta = new Vector2(180, 26);
                Sprite sprite = null;
                foreach (UnityEngine.Object asset in sprites) if (asset is Sprite candidate && candidate.name == id) { sprite = candidate; break; }
                Image icon = UiKit.Image(item, "Icon", sprite, Color.white);
                UiKit.Place(icon.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(24, 24));
                TMP_Text count = UiKit.Label(item, "Count", "0", UiTextSize.Small, theme, TextAlignmentOptions.MidlineRight);
                UiKit.Place((RectTransform)count.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-28, 0), new Vector2(256, 24));
                count.fontSize = 18;
                count.textWrappingMode = TextWrappingModes.NoWrap;
                count.overflowMode = TextOverflowModes.Overflow;
                items.Add(item.gameObject);
                counts.Add(count);
                item.gameObject.SetActive(false);
            }
            UiKit.Bind(screen, "_resourceRow", row.gameObject);
            ScreenArrayBinding.BindArray(screen, "_resourceItems", items);
            ScreenArrayBinding.BindArray(screen, "_resourceCounts", counts);
        }

        /// <summary>Main 씬과 TopBar 프리팹의 재료 행을 숫자 다음 아이콘 순서로 조정해 저장한다.</summary>
        public static void AlignResourceDisplay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play mode before editing the HUD.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity" || scene.isDirty) throw new System.InvalidOperationException("Main scene must be active and saved.");
            TopBarScreen target = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TopBarScreen found = root.GetComponentInChildren<TopBarScreen>(true);
                if (found != null) { if (target != null) throw new System.InvalidOperationException("Multiple TopBar screens found."); target = found; }
            }
            if (target == null || target.transform.Find("Resources") == null) throw new System.InvalidOperationException("Main scene resource row is missing.");
            const string path = "Assets/Prefabs/UI/Screens/TopBar.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureResourceDisplay(prefab.transform.Find("Resources"));
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            ConfigureResourceDisplay(target.transform.Find("Resources"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>row의 위치와 간격을 조정하고 각 재료의 숫자를 아이콘 왼쪽에 배치한다.</summary>
        private static void ConfigureResourceDisplay(Transform row)
        {
            if (row == null) throw new System.InvalidOperationException("Resource row is missing.");
            RectTransform rect = (RectTransform)row;
            UiKit.Place(rect, Vector2.one, Vector2.one, new Vector2(-20, -40), new Vector2(180, 320));
            VerticalLayoutGroup layout = row.GetComponent<VerticalLayoutGroup>();
            if (layout == null) throw new System.InvalidOperationException("Resource layout is missing.");
            layout.spacing = 1f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(layout);
            foreach (Transform child in row)
            {
                RectTransform item = (RectTransform)child;
                RectTransform icon = child.Find("Icon") as RectTransform;
                TMP_Text count = child.Find("Count")?.GetComponent<TMP_Text>();
                if (icon == null || count == null) throw new System.InvalidOperationException("Resource icon or count is missing: " + child.name);
                item.sizeDelta = new Vector2(180, 26);
                UiKit.Place(icon, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(24, 24));
                UiKit.Place((RectTransform)count.transform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-28, 0), new Vector2(256, 24));
                count.alignment = TextAlignmentOptions.MidlineRight;
                count.textWrappingMode = TextWrappingModes.NoWrap;
                count.overflowMode = TextOverflowModes.Overflow;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
                PrefabUtility.RecordPrefabInstancePropertyModifications(count.rectTransform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(count);
            }
        }

        /// <summary>저장된 Main 씬의 기존 TopBar에 프리팹의 버튼 배치와 재료 표시를 적용하고 씬을 저장한다.</summary>
        public static void ApplyToMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play mode before editing the HUD.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity" || scene.isDirty) throw new System.InvalidOperationException("Main scene must be active and saved.");
            TopBarScreen target = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TopBarScreen found = root.GetComponentInChildren<TopBarScreen>(true);
                if (found != null) { if (target != null) throw new System.InvalidOperationException("Multiple TopBar screens found."); target = found; }
            }
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Screens/TopBar.prefab");
            if (target == null || source == null) throw new System.InvalidOperationException("TopBar screen or prefab is missing.");
            Transform sourceRow = source.transform.Find("Resources");
            if (sourceRow == null || target.transform.Find("Resources") != null) throw new System.InvalidOperationException("Resource row is missing from prefab or already exists in scene.");
            foreach (string name in new[] { "Codex", "Passengers", "Depart", "GameMenuButton" })
            {
                RectTransform from = source.transform.Find(name) as RectTransform;
                RectTransform to = target.transform.Find(name) as RectTransform;
                if (from == null || to == null) throw new System.InvalidOperationException("Missing TopBar button: " + name);
                to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax; to.pivot = from.pivot;
                to.anchoredPosition = from.anchoredPosition; to.sizeDelta = from.sizeDelta;
            }
            GameObject row = UnityEngine.Object.Instantiate(sourceRow.gameObject, target.transform, false);
            row.name = "Resources";
            var items = new List<GameObject>();
            var counts = new List<TMP_Text>();
            foreach (Transform item in row.transform)
            {
                items.Add(item.gameObject);
                counts.Add(item.Find("Count").GetComponent<TMP_Text>());
            }
            UiKit.Bind(target, "_resourceRow", row);
            ScreenArrayBinding.BindArray(target, "_resourceItems", items);
            ScreenArrayBinding.BindArray(target, "_resourceCounts", counts);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>도감 버튼용 책 이미지를 스프라이트로 가져와 반환한다.</summary>
        private static Sprite CodexIcon()
        {
            const string path = "Assets/Art/Generated/codex_journal.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
