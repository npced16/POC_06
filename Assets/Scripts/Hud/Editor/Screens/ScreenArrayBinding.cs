using System.Collections.Generic;

using UnityEditor;

using UObject = UnityEngine.Object;

namespace IronRing.Hud.Editor.Screens
{
    /// <summary>스토리 022(2-A). 화면 스크립트의 [SerializeField] 배열 필드에 프리팹 안의 참조 목록을 꽂는다(UiKit.Bind의 배열판).</summary>
    public static class ScreenArrayBinding
    {
        /// <summary>target의 field 배열을 values로 채운다.</summary>
        public static void BindArray<T>(UObject target, string field, IList<T> values) where T : UObject
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field) ?? throw new System.ArgumentException(target.GetType().Name + "에 필드 " + field + "가 없습니다.");
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
