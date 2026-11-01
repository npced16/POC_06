using UnityEngine;

using UnityEditor;

using TMPro;

using IronRing.Hud.Ui;
using IronRing.Localization;

namespace IronRing.Presentation.Map.Editor
{
    /// <summary>
    /// 스토리 022. 선로 지도 프리팹에 월드 공간 TMP 라벨을 붙인다: HubBuilding.prefab에 NameLabel·PhaseLabel, WaypointMarker.prefab에 NameLabel,
    /// 각 루트에 MapLabel. 옛 TextMesh(Name)는 지우지 않고 렌더러만 끈다(지도 생성기가 이름을 넣고, MapLabel이 그 이름을 옮겨 쓴다).
    /// 이미 있는 자식·값은 건드리지 않으므로 여러 번 실행해도 결과가 같다(멱등). 위치·크기는 디자이너가 프리팹에서 조정한다.
    /// </summary>
    public static class MapLabelsBuild
    {
        /// <summary>거점 건물 프리팹.</summary>
        public const string HUB_PREFAB = "Assets/Prefabs/Map/HubBuilding.prefab";
        /// <summary>경유지 표식 프리팹.</summary>
        public const string WAYPOINT_PREFAB = "Assets/Prefabs/Map/WaypointMarker.prefab";
        /// <summary>UI 테마(글꼴·색).</summary>
        public const string THEME = "Assets/Prefabs/UI/UiTheme.asset";
        /// <summary>지도 라벨용 외곽선 TMP 머티리얼(테마 글꼴 머티리얼 복제).</summary>
        public const string OUTLINE_MATERIAL = "Assets/Prefabs/Map/MapLabelOutline.mat";

        private const int SORTING_ORDER = 30;
        private const float NAME_SIZE = 5f;
        private const float PHASE_SIZE = 4f;

        /// <summary>두 프리팹에 빠진 라벨만 보충한다. 바뀐 프리팹만 저장한다.</summary>
        public static void Generate()
        {
            UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(THEME);
            if (theme == null || theme.Font == null) { Debug.LogError("MapLabelsBuild: " + THEME + " 또는 테마 글꼴이 없습니다(BuildMainCanvas.Generate 먼저)."); return; }
            Material outline = EnsureOutline(theme);
            bool hub = Apply(HUB_PREFAB, theme, outline, true, -1.75f, Strings.Get("hub_1"));
            bool waypoint = Apply(WAYPOINT_PREFAB, theme, outline, false, -0.95f, Strings.Get("waypoint_north"));
            Debug.Log("MAP LABELS: hubChanged=" + hub + " waypointChanged=" + waypoint);
        }

        private static Material EnsureOutline(UiTheme theme)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(OUTLINE_MATERIAL);
            if (existing != null) return existing;
            var material = new Material(theme.Font.material) { name = "MapLabelOutline" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, theme.Panel);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            AssetDatabase.CreateAsset(material, OUTLINE_MATERIAL);
            return material;
        }

        /// <summary>path 프리팹에 MapLabel과 라벨 자식을 없을 때만 더한다. 바뀌었으면 저장하고 true.</summary>
        private static bool Apply(string path, UiTheme theme, Material outline, bool withPhase, float nameY, string sampleName)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                MapLabel label = root.GetComponent<MapLabel>();
                if (label == null) { label = root.AddComponent<MapLabel>(); changed = true; }
                var so = new SerializedObject(label);
                changed |= EnsureLabel(root.transform, so.FindProperty("_name"), "NameLabel", sampleName, NAME_SIZE, theme.Text, nameY, theme, outline);
                if (withPhase)
                    changed |= EnsureLabel(root.transform, so.FindProperty("_phase"), "PhaseLabel", Strings.Get("phase_recovering"), PHASE_SIZE, theme.Accent, nameY - 0.5f, theme, outline);
                so.ApplyModifiedPropertiesWithoutUndo();
                // 옛 TextMesh 이름은 남겨 두되(지도 생성기가 GetComponentInChildren<TextMesh>로 이름을 넣는다) 그리지 않는다.
                foreach (TextMesh legacy in root.GetComponentsInChildren<TextMesh>(true))
                {
                    var renderer = legacy.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.enabled) { renderer.enabled = false; changed = true; }
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return changed;
        }

        /// <summary>property가 비어 있으면 name 자식(TextMeshPro 3D)을 찾거나 만들어 꽂는다. 만들었거나 꽂았으면 true.</summary>
        private static bool EnsureLabel(Transform root, SerializedProperty property, string name, string sample, float size, Color color, float y, UiTheme theme, Material outline)
        {
            if (property.objectReferenceValue != null) return false;
            Transform child = root.Find(name);
            TextMeshPro text = child != null ? child.GetComponent<TextMeshPro>() : null;
            if (text == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(root, false);
                var rect = (RectTransform)go.transform;
                rect.localPosition = new Vector3(0, y, 0);
                rect.sizeDelta = new Vector2(6f, 0.8f);
                text = go.AddComponent<TextMeshPro>();
                text.font = theme.Font;
                if (outline != null) text.fontSharedMaterial = outline;
                text.fontSize = size;
                text.color = color;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.text = sample;
                text.sortingOrder = SORTING_ORDER;
            }
            property.objectReferenceValue = text;
            return true;
        }
    }
}
