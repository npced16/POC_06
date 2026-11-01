using System.Collections.Generic;
using System.Linq;
using IronRing.Art.Editor;
using IronRing.Hud.Ui;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IronRing.Hud.Editor
{
    /// <summary>
    /// 스토리 024. 시트 조각을 UiTheme.asset에 넣고(ApplySheetTheme), 기존 UI 프리팹의 옛 생성 스프라이트를 시트 조각으로 바꾼다(Reskin).
    /// 둘 다 멱등. BuildMainCanvas.cs는 건드리지 않는다(스토리 025와 병행).
    /// </summary>
    public static class UiSheetTheme
    {
        private const string THEME = "Assets/Prefabs/UI/UiTheme.asset";
        private const string OLD = "Assets/Art/Generated/ui/";
        private static readonly string[] FOLDERS = { "Assets/Prefabs/UI/Screens", "Assets/Prefabs/UI/Parts" };
        /// <summary>시트 픽셀 2개 = 화면 1px(캔버스 refPPU 64, 스프라이트 PPU 64 기준 절반 크기). 캡처 보고 조정.</summary>
        private const float SHEET_MULTIPLIER = 2f;
        /// <summary>이보다 낮은 ui_panel은 title_plate로 바꾼다(화면 단위 px).</summary>
        private const float THIN_PANEL = 120f;

        /// <summary>옛 생성 스프라이트 → 시트 조각 이름(스토리 024 AC 테마 교체).</summary>
        private static readonly Dictionary<string, string> MAP = new()
        {
            { "ui_panel", "frame_plain" }, { "ui_button", "btn_copper" }, { "ui_button_pressed", "btn_gold" },
            { "ui_slot", "slot_dark" }, { "ui_slot_selected", "slot_gold" }, { "ui_toast", "title_plate" },
        };

        private static Dictionary<string, Sprite> SheetSprites() =>
            AssetDatabase.LoadAllAssetsAtPath(UiSheetSlicer.TEXTURE).OfType<Sprite>().ToDictionary(s => s.name);

        /// <summary>테마 스프라이트·여백·배율을 시트 조각으로 채운다. 이미 같으면 아무것도 바꾸지 않는다.</summary>
        public static void ApplySheetTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(THEME);
            var sheet = SheetSprites();
            var pads = UiSheetSlicer.LoadPieces().ToDictionary(p => p.name, p => p.pad);
            var so = new SerializedObject(theme);
            void S(string field, string piece) => so.FindProperty(field).objectReferenceValue = sheet[piece];
            void P(string field, string piece) => so.FindProperty(field).vector4Value = pads[piece];
            S("_panelSprite", "frame_plain"); S("_buttonSprite", "btn_copper"); S("_buttonPressedSprite", "btn_gold");
            S("_slotSprite", "slot_dark"); S("_slotSelectedSprite", "slot_gold"); S("_toastSprite", "title_plate");
            S("_frameTitledSprite", "frame_titled"); S("_progressBarSprite", "progress_bar"); S("_roundCloseSprite", "btn_round_close");
            S("_buttonDarkSprite", "btn_dark"); S("_slotCopperSprite", "slot_copper");
            P("_panelPad", "frame_plain"); P("_frameTitledPad", "frame_titled"); P("_toastPad", "title_plate");
            P("_buttonPad", "btn_copper"); P("_progressBarPad", "progress_bar"); P("_slotPad", "slot_dark");
            so.FindProperty("_sliceMultiplier").floatValue = SHEET_MULTIPLIER;
            // 사용자 지시(21:40) 글자 4단계: Title 28 · Heading 22 · Normal 18 · Small 14, 줄 높이 ×1.3, 버튼 높이 40(상단 바 기준)·좁은 줄 32.
            so.FindProperty("_fontTitle").floatValue = 28f;
            so.FindProperty("_fontHeading").floatValue = 22f;
            so.FindProperty("_fontNormal").floatValue = 18f;
            so.FindProperty("_fontSmall").floatValue = 14f;
            so.FindProperty("_lineHeightFactor").floatValue = 1.3f;
            so.FindProperty("_buttonHeight").floatValue = 40f;
            so.FindProperty("_buttonHeightSmall").floatValue = 32f;
            bool changed = so.ApplyModifiedPropertiesWithoutUndo();
            if (changed) { EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets(); }
            Debug.Log("[UiSheetTheme] ApplySheetTheme " + (changed ? "변경" : "변경 없음"));
        }

        /// <summary>
        /// 화면·부품 프리팹의 Image.sprite와 Button SpriteSwap을 옛 스프라이트에서 시트 조각으로 바꾼다.
        /// 바꾼 패널(ui_panel·ui_toast)에 한해 내용 여백을 프레임 두께만큼 늘린다(LayoutGroup.padding, 없으면 늘이기 앵커 자식의 offset).
        /// 옛 스프라이트가 남아 있을 때만 바꾸므로 두 번째 실행은 변경 0.
        /// </summary>
        public static void Reskin()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(THEME);
            var sheet = SheetSprites();
            int prefabs = 0, refs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", FOLDERS))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int n = 0;
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (!TryMap(image.sprite, sheet, out Sprite next)) continue;
                    string old = image.sprite.name;
                    bool panel = old == "ui_panel" || old == "ui_toast";
                    if (old == "ui_panel")
                    {
                        // 결정 1: 높이 THIN_PANEL 미만의 얇은 띠(상단 바·배너·실내 제목)는 프레임 대신 제목판.
                        float h = image.rectTransform.rect.height;
                        if (h > 0f && h < THIN_PANEL) next = sheet["title_plate"];
                        Debug.Log($"[UiSheetTheme] {path} {image.name}: 높이 {h:0} → {next.name}");
                    }
                    image.sprite = next;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = theme.SliceMultiplier;
                    // 결정 2: 시트 슬롯은 가운데 톱니가 있어 테두리만 쓰고, 일반 슬롯은 안쪽에 어두운 바탕을 깐다.
                    if (old == "ui_slot" || old == "ui_slot_selected") image.fillCenter = false;
                    if (old == "ui_slot") UiKit.AddSlotBack(image, theme);
                    if (panel) GrowPadding(image.rectTransform, theme.ContentPadding(next));
                    n++;
                }
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    SpriteState st = button.spriteState;
                    bool any = false;
                    if (TryMap(st.pressedSprite, sheet, out Sprite p)) { st.pressedSprite = p; any = true; }
                    if (TryMap(st.selectedSprite, sheet, out Sprite s)) { st.selectedSprite = s; any = true; }
                    if (TryMap(st.highlightedSprite, sheet, out Sprite h)) { st.highlightedSprite = h; any = true; }
                    if (TryMap(st.disabledSprite, sheet, out Sprite d)) { st.disabledSprite = d; any = true; }
                    if (any) { button.spriteState = st; n++; }
                }
                if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++; refs += n; Debug.Log($"[UiSheetTheme] {path}: {n}"); }
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[UiSheetTheme] Reskin 프리팹 {prefabs}개, 참조 {refs}개 교체");
        }

        /// <summary>
        /// 스토리 024 타이포 통일. 화면·부품 프리팹의 TMP 글자 크기(고정·자동 축소 최대값)를 옛 단계에서 테마 4단계로 옮긴다.
        /// 옛 값: Small 16 · Normal 20 · Title 28 · 상단 바 버튼 23.8(Title×0.85) · 패배 제목 35(Title×1.25).
        /// 새 값(테마): Small · Normal · Title. 이미 새 값이면 그대로라 멱등.
        /// SavePrefabIfMissing 부품(핫바 슬롯·시설 줄·대화 선택지 등)은 빌더로 다시 만들지 않으므로 이 정리로 맞춘다.
        /// </summary>
        public static void NormalizeFonts()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(THEME);
            var map = new Dictionary<float, float>
            {
                { 16f, theme.Size(UiTextSize.Small) }, { 20f, theme.Size(UiTextSize.Normal) },
                { 23.8f, theme.Size(UiTextSize.Normal) }, { 35f, theme.Size(UiTextSize.Title) },
            };
            float Map(float v)
            {
                foreach (var pair in map) if (Mathf.Abs(v - pair.Key) < 0.05f) return pair.Value;
                return v;
            }
            int prefabs = 0, labels = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", FOLDERS))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int n = 0;
                foreach (TMPro.TextMeshProUGUI label in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                {
                    float size = Map(label.fontSize), max = Map(label.fontSizeMax);
                    if (size == label.fontSize && max == label.fontSizeMax) continue;
                    label.fontSize = size; label.fontSizeMax = max;
                    if (label.fontSizeMin > max) label.fontSizeMin = max;
                    n++;
                }
                if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++; labels += n; Debug.Log($"[UiSheetTheme] 글자 {path}: {n}"); }
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[UiSheetTheme] NormalizeFonts 프리팹 {prefabs}개, 라벨 {labels}개");
        }

        private static bool TryMap(Sprite old, Dictionary<string, Sprite> sheet, out Sprite next)
        {
            next = null;
            if (old == null || !AssetDatabase.GetAssetPath(old).StartsWith(OLD)) return false;
            return MAP.TryGetValue(old.name, out string piece) && sheet.TryGetValue(piece, out next);
        }

        private static void GrowPadding(RectTransform panel, RectOffset pad)
        {
            if (panel.TryGetComponent(out LayoutGroup group))
            {
                group.padding = new RectOffset(group.padding.left + pad.left, group.padding.right + pad.right,
                    group.padding.top + pad.top, group.padding.bottom + pad.bottom);
                return;
            }
            foreach (RectTransform child in panel)
            {
                if (child.anchorMin.x < child.anchorMax.x) { child.offsetMin += new Vector2(pad.left, 0); child.offsetMax -= new Vector2(pad.right, 0); }
                if (child.anchorMin.y < child.anchorMax.y) { child.offsetMin += new Vector2(0, pad.bottom); child.offsetMax -= new Vector2(0, pad.top); }
            }
        }
    }
}
