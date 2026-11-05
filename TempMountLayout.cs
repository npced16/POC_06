var s=UnityEngine.Object.FindFirstObjectByType<IronRing.Combat.TrainArmamentScreen>(UnityEngine.FindObjectsInactive.Include);
var so=new UnityEditor.SerializedObject(s);
var mounts=so.FindProperty("_slots"); var icons=so.FindProperty("_slotIcons"); var ghosts=so.FindProperty("_ghostIcons");
var result="";
for(int i=0;i<mounts.arraySize;i++)
{
 var b=(UnityEngine.UI.Button)mounts.GetArrayElementAtIndex(i).objectReferenceValue;
 var t=((UnityEngine.UI.Image)icons.GetArrayElementAtIndex(i).objectReferenceValue).rectTransform;
 var g=((UnityEngine.UI.Image)ghosts.GetArrayElementAtIndex(i).objectReferenceValue).rectTransform;
 result += $"{i}: prefab={UnityEditor.PrefabUtility.IsPartOfPrefabInstance(b)} source={UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(b)} turret={t.anchoredPosition} ghost={g.anchoredPosition} anchors={t.anchorMin}/{g.anchorMin} sizes={t.sizeDelta}/{g.sizeDelta}\n";
}
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/UI/Parts/ArmamentMount.prefab");
result += "PREFAB: ";
foreach(UnityEngine.Transform t in prefab.transform) result += t.name+",";
result += " turretPos="+((UnityEngine.RectTransform)prefab.transform.Find("Turret")).anchoredPosition+" ghostPos="+((UnityEngine.RectTransform)prefab.transform.Find("Ghost")).anchoredPosition;
result += "\nSCENE: ";
var first=((UnityEngine.UI.Button)mounts.GetArrayElementAtIndex(0).objectReferenceValue).transform;
foreach(UnityEngine.Transform t in first) result += t.name+",";
return result;
