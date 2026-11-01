using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using UnityEditor;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022. 항상 켜진 화면 "CombatFx"(떠오르는 문구 풀 + 전체 화면 그늘) 프리팹과 부품 FloatingMessage.prefab을 만든다.</summary>
    public static class CombatFxScreenBuilder
    {
        /// <summary>떠오르는 문구 부품 프리팹 경로.</summary>
        public const string MESSAGE_PART = BuildMainCanvas.PARTS_FOLDER + "/FloatingMessage.prefab";

        /// <summary>미리 놓아 두는 문구 수(동시에 떠 있는 문구 최대).</summary>
        private const int POOL = 6;

        /// <summary>화면 루트(UiScreen "CombatFx", 항상 켜짐) + CombatFxScreen + Messages(문구 풀, 첫 칸만 예시로 보임) + Shade(꺼둠).</summary>
        public static GameObject Build(UiTheme theme)
        {
            GameObject root = UiKit.ScreenRoot("CombatFx", true);
            CombatFxScreen screen = root.AddComponent<CombatFxScreen>();

            RectTransform area = UiKit.Stretch(UiKit.Node(root.transform, "Messages"));
            GameObject part = UiKit.SavePrefabIfMissing(MESSAGE_PART, () => BuildMessage(theme));
            var pool = new List<FloatingMessageView>();
            for (int i = 0; i < POOL; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(part, area);
                instance.name = "FloatingMessage_" + i;
                var rect = (RectTransform)instance.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-160, -40);
                instance.SetActive(i == 0);
                pool.Add(instance.GetComponent<FloatingMessageView>());
            }

            Image shade = UiKit.Image(root.transform, "Shade", null, theme.Panel, false);
            shade.preserveAspect = false;
            UiKit.Stretch(shade.rectTransform);
            TextMeshProUGUI title = UiKit.Label(shade.transform, "Title", Strings.Get("defeat_title"), UiTextSize.Title, theme);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(700, 56));
            TextMeshProUGUI body = UiKit.Label(shade.transform, "Body", Strings.Get("defeat_body"), UiTextSize.Normal, theme);
            UiKit.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(700, 40));
            shade.gameObject.SetActive(false);

            UiKit.Bind(screen, "_area", area);
            ScreenArrayBinding.BindArray(screen, "_pool", pool);
            UiKit.Bind(screen, "_shade", shade);
            UiKit.Bind(screen, "_shadeTitle", title);
            UiKit.Bind(screen, "_shadeBody", body);
            return root;
        }

        /// <summary>FloatingMessage 부품: 토스트 9분할 말풍선(위 가운데 피벗) + 한 줄 라벨.</summary>
        private static GameObject BuildMessage(UiTheme theme)
        {
            Image bubble = UiKit.Sliced(null, "FloatingMessage", theme.ToastSprite, theme, false);
            RectTransform rect = bubble.rectTransform;
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(240, 38);
            FloatingMessageView view = bubble.gameObject.AddComponent<FloatingMessageView>();
            TextMeshProUGUI label = UiKit.Label(bubble.transform, "Label", Strings.Get("combat_victory"), UiTextSize.Small, theme);
            UiKit.Stretch(label.rectTransform, 8, 2, 8, 2);
            UiKit.Bind(view, "_label", label);
            return bubble.gameObject;
        }
    }
}
