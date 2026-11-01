using System;

using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Ui;

using UImage = UnityEngine.UI.Image;
using UButton = UnityEngine.UI.Button;
using UObject = UnityEngine.Object;

namespace IronRing.Hud.Editor
{
    /// <summary>
    /// 스토리 022. 화면 프리팹 생성기가 함께 쓰는 UGUI 조립 도구. 모든 결과는 편집 모드에서 바로 제대로 보인다(런타임 설정 없음).
    /// 색·글꼴·크기·스프라이트는 UiTheme에서 읽는다. 참조 바인딩은 Bind/Set(SerializedObject)으로 한다.
    /// </summary>
    public static class UiKit
    {
        /// <summary>내장 UI 레이어.</summary>
        public const int UI_LAYER = 5;

        /// <summary>RectTransform을 가진 빈 오브젝트를 parent 아래에 만든다(parent가 null이면 루트).</summary>
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UI_LAYER };
            var rect = (RectTransform)go.transform;
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>한 점 앵커로 배치한다(anchor·pivot은 0~1, position은 앵커 기준 오프셋, size는 크기).</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        /// <summary>부모를 꽉 채우고 네 변 여백을 준다.</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>위쪽 가로로 늘어나는 띠(높이 height, 위에서 top 아래).</summary>
        public static RectTransform StretchTop(RectTransform rect, float height, float top = 0, float left = 0, float right = 0)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>화면 프리팹 루트: 캔버스를 꽉 채우는 RectTransform + UiScreen(id, alwaysOn). 그래픽이 없어 클릭을 막지 않는다.</summary>
        public static GameObject ScreenRoot(string id, bool alwaysOn)
        {
            RectTransform rect = Stretch(Node(null, id));
            UiScreen screen = rect.gameObject.AddComponent<UiScreen>();
            Set(screen, "_id", id);
            Set(screen, "_alwaysOn", alwaysOn);
            return rect.gameObject;
        }

        /// <summary>9분할 Image(테마 pixelsPerUnitMultiplier). blocksPointer면 클릭을 막는다(raycastTarget).</summary>
        public static UImage Sliced(Transform parent, string name, Sprite sprite, UiTheme theme, bool blocksPointer)
        {
            UImage image = Node(parent, name).gameObject.AddComponent<UImage>();
            image.sprite = sprite;
            image.type = UImage.Type.Sliced;
            image.pixelsPerUnitMultiplier = theme.SliceMultiplier;
            image.raycastTarget = blocksPointer;
            return image;
        }

        /// <summary>테마 패널(자두색 바탕·황동 테두리). 기본은 부모를 꽉 채운다 — Place/Stretch로 다시 잡는다.</summary>
        public static UImage Panel(Transform parent, string name, UiTheme theme, bool blocksPointer = true)
        {
            UImage image = Sliced(parent, name, theme.PanelSprite, theme, blocksPointer);
            Stretch(image.rectTransform);
            return image;
        }

        /// <summary>단순 Image(아이콘 등, 비율 유지).</summary>
        public static UImage Image(Transform parent, string name, Sprite sprite, Color color, bool blocksPointer = false)
        {
            UImage image = Node(parent, name).gameObject.AddComponent<UImage>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = blocksPointer;
            return image;
        }

        /// <summary>테마 글꼴·크기·글자색의 TMP 라벨(클릭 통과, 줄바꿈). 부모를 꽉 채운다.</summary>
        public static TextMeshProUGUI Label(Transform parent, string name, string text, UiTextSize size, UiTheme theme,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI label = Stretch(Node(parent, name)).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = theme.Font;
            label.fontSize = theme.Size(size);
            label.color = theme.Text;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        /// <summary>구리 버튼 + 라벨 자식("Label"). 눌림·선택(게임패드)은 눌린 스프라이트로 바꾼다. 크기는 Place로 잡는다.</summary>
        public static UButton Button(Transform parent, string name, string text, UiTheme theme)
        {
            UImage image = Sliced(parent, name, theme.ButtonSprite, theme, true);
            UButton button = image.gameObject.AddComponent<UButton>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = theme.ButtonPressedSprite, selectedSprite = theme.ButtonPressedSprite };
            TextMeshProUGUI label = Label(image.transform, "Label", text, UiTextSize.Normal, theme);
            Stretch(label.rectTransform, 6, 4, 6, 4);
            return button;
        }

        /// <summary>
        /// 슬롯 틀(9분할, 클릭 받음). 선택 표시는 theme.SlotSelectedSprite로 바꿔 끼운다.
        /// 스토리 024: 시트 슬롯은 가운데에 톱니가 그려져 있어 fillCenter를 끄고, 테두리 안쪽에 테마 Panel 색 바탕(SlotBack)을 깐다.
        /// </summary>
        public static UImage Slot(Transform parent, string name, UiTheme theme)
        {
            UImage image = Sliced(parent, name, theme.SlotSprite, theme, true);
            image.fillCenter = false;
            AddSlotBack(image, theme);
            return image;
        }

        /// <summary>스토리 024. 프레임 패널(frame_plain)의 내용 여백(화면 단위). 글자·목록은 이 안쪽에 둔다.</summary>
        public static RectOffset FramePad(UiTheme theme) => theme.ContentPadding(theme.PanelSprite);

        /// <summary>제목판 높이(화면 px). 제목판은 프레임 위 가장자리에 세로 가운데로 걸친다.</summary>
        public const float TITLE_PLATE_HEIGHT = 54f;
        /// <summary>둥근 닫기 버튼 지름, 패널 오른쪽 위 모서리에서 중심까지 거리(화면 px).</summary>
        public const float CLOSE_SIZE = 48f, CLOSE_INSET = 24f;

        /// <summary>제목판·배너 상자 글자의 좌우 여백(title_plate 끝 장식 + 6, 화면 px).</summary>
        public static int PlateSide(UiTheme theme) => theme.ContentPadding(theme.ToastSprite).left + 6;

        /// <summary>제목판이 걸친 프레임에서 내용(힌트 첫 줄)이 시작하는 위쪽 거리 = pad.top + 제목판 높이/2 + 8.</summary>
        public static float ContentTop(UiTheme theme) => FramePad(theme).top + TITLE_PLATE_HEIGHT / 2f + 8f;

        /// <summary>
        /// 스토리 024. 프레임 위 가장자리 가운데에 걸치는 제목판(title_plate) + 제목 라벨(이름 name, Title, 강조색, 한 줄).
        /// 폭은 글자 폭 + 좌우 PlateSide로 런타임에도 따라 늘어난다(HorizontalLayoutGroup + ContentSizeFitter) — 말줄임 없음.
        /// 반환은 라벨(화면 스크립트 바인딩용).
        /// </summary>
        public static TextMeshProUGUI TitlePlate(Transform panel, string name, string text, UiTheme theme)
        {
            UImage plate = Sliced(panel, name + "Plate", theme.ToastSprite, theme, false);
            Place(plate.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, TITLE_PLATE_HEIGHT));
            HorizontalLayoutGroup layout = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
            int side = PlateSide(theme);
            layout.padding = new RectOffset(side, side, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            plate.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI label = Label(plate.transform, name, text, UiTextSize.Title, theme, TextAlignmentOptions.Center);
            label.color = theme.Accent;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        /// <summary>스토리 024. 패널 오른쪽 위 모서리에 얹는 둥근 X 닫기 버튼(btn_round_close, Simple, 색 변화로 눌림 표시). 라벨 없음.</summary>
        public static UButton CloseButton(Transform panel, string name, UiTheme theme)
        {
            RectTransform rect = Node(panel, name);
            Place(rect, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-CLOSE_INSET, -CLOSE_INSET), new Vector2(CLOSE_SIZE, CLOSE_SIZE));
            UImage image = rect.gameObject.AddComponent<UImage>();
            image.sprite = theme.RoundCloseSprite;
            image.type = UImage.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = true;
            UButton button = rect.gameObject.AddComponent<UButton>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            return button;
        }

        /// <summary>스토리 024. 얇은 띠(상단 바·배너·실내 제목) 바탕: 제목판 조각을 부모에 꽉 채운다.</summary>
        public static UImage Bar(Transform parent, string name, UiTheme theme, bool blocksPointer = true)
        {
            UImage image = Sliced(parent, name, theme.ToastSprite, theme, blocksPointer);
            Stretch(image.rectTransform);
            return image;
        }

        /// <summary>슬롯 테두리 안쪽을 채우는 어두운 바탕(첫 자식, 클릭 통과). 이미 있으면 그대로 둔다.</summary>
        public static UImage AddSlotBack(UImage slot, UiTheme theme)
        {
            Transform existing = slot.transform.Find(SLOT_BACK);
            if (existing != null) return existing.GetComponent<UImage>();
            RectTransform back = Node(slot.transform, SLOT_BACK);
            back.SetAsFirstSibling();
            RectOffset pad = theme.ContentPadding(slot.sprite);
            Stretch(back, pad.left, pad.top, pad.right, pad.bottom);
            UImage image = back.gameObject.AddComponent<UImage>();
            image.color = theme.Panel;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>슬롯 바탕 자식 이름.</summary>
        public const string SLOT_BACK = "SlotBack";

        /// <summary>세로 스크롤 격자: ScrollRect + Viewport(RectMask2D) + Content(GridLayoutGroup 고정 열 + ContentSizeFitter). 부모를 꽉 채운다.</summary>
        public static ScrollRect ScrollGrid(Transform parent, string name, Vector2 cellSize, Vector2 spacing, int columns, out RectTransform content)
        {
            RectTransform root = Stretch(Node(parent, name));
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Stretch(Node(root, "Viewport"));
            UImage hit = viewport.gameObject.AddComponent<UImage>();
            hit.color = Color.clear; // 스크롤 휠·드래그를 받는 투명 면
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Node(viewport, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }

        /// <summary>가로/세로 줄 배치(HorizontalLayoutGroup 또는 VerticalLayoutGroup). 자식 크기는 자식이 정한다.</summary>
        public static HorizontalOrVerticalLayoutGroup Row(Transform parent, string name, bool vertical, float spacing)
        {
            RectTransform rect = Stretch(Node(parent, name));
            HorizontalOrVerticalLayoutGroup group = vertical
                ? rect.gameObject.AddComponent<VerticalLayoutGroup>()
                : (HorizontalOrVerticalLayoutGroup)rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childControlWidth = false; group.childControlHeight = false;
            group.childForceExpandWidth = false; group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.MiddleCenter;
            return group;
        }

        /// <summary>target의 [SerializeField] 참조 필드에 value를 꽂는다(이름 조회 대신 인스펙터 참조 바인딩).</summary>
        public static void Bind(UObject target, string field, UObject value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field) ?? throw new ArgumentException(target.GetType().Name + "에 필드 " + field + "가 없습니다.");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>target의 문자열 필드를 바꾼다.</summary>
        public static void Set(UObject target, string field, string value)
        {
            var so = new SerializedObject(target);
            (so.FindProperty(field) ?? throw new ArgumentException(field)).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>target의 bool 필드를 바꾼다.</summary>
        public static void Set(UObject target, string field, bool value)
        {
            var so = new SerializedObject(target);
            (so.FindProperty(field) ?? throw new ArgumentException(field)).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>target의 float 필드를 바꾼다.</summary>
        public static void Set(UObject target, string field, float value)
        {
            var so = new SerializedObject(target);
            (so.FindProperty(field) ?? throw new ArgumentException(field)).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>path에 프리팹이 없을 때만 build()로 만들어 저장한다. 이미 있으면 손대지 않고 그대로 돌려준다.</summary>
        public static GameObject SavePrefabIfMissing(string path, Func<GameObject> build)
        {
            return SavePrefab(path, build, false);
        }

        /// <summary>
        /// 프리팹이 없으면 만들고, force이면 같은 경로에 덮어써 다시 만든다(GUID가 유지되어 MainCanvas의 자식 인스턴스가 그대로 따라온다).
        /// 빌더를 고친 뒤 다시 생성할 때 쓴다.
        /// </summary>
        public static GameObject SavePrefab(string path, Func<GameObject> build, bool force)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !force) return existing;
            GameObject temp = build();
            try { return PrefabUtility.SaveAsPrefabAsset(temp, path); }
            finally { UObject.DestroyImmediate(temp); }
        }
    }
}
