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

        /// <summary>슬롯 틀(어두운 9분할, 클릭 받음). 선택 표시는 theme.SlotSelectedSprite로 바꿔 끼운다.</summary>
        public static UImage Slot(Transform parent, string name, UiTheme theme)
        {
            return Sliced(parent, name, theme.SlotSprite, theme, true);
        }

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
