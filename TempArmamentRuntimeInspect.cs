var s=UnityEngine.Object.FindFirstObjectByType<IronRing.Combat.TrainArmamentScreen>(UnityEngine.FindObjectsInactive.Include);
var so=new UnityEditor.SerializedObject(s);
var panel=(UnityEngine.GameObject)so.FindProperty("_panel").objectReferenceValue;
var slots=so.FindProperty("_slots");
var ghosts=so.FindProperty("_ghostIcons");
var icons=so.FindProperty("_slotIcons");
var output=$"active={s.isActiveAndEnabled},panel={panel.activeInHierarchy},screen={UnityEngine.Screen.width}x{UnityEngine.Screen.height}\n";
for(int i=0;i<slots.arraySize;i++)
{
 var b=(UnityEngine.UI.Button)slots.GetArrayElementAtIndex(i).objectReferenceValue;
 var g=(UnityEngine.UI.Image)ghosts.GetArrayElementAtIndex(i).objectReferenceValue;
 var icon=(UnityEngine.UI.Image)icons.GetArrayElementAtIndex(i).objectReferenceValue;
 var d=b.GetComponent<IronRing.Combat.TrainMountDrag>();
 var target=typeof(IronRing.Combat.TrainMountDrag).GetField("_screen",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(d);
 var p=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,b.transform.position);
 var sp=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,g.transform.position);
 var outline=icon.GetComponent<UnityEngine.UI.Outline>();
 output+=$"{i}:pos={p} bound={target==s} ghost={g.gameObject.activeSelf} ghostAlpha={g.canvasRenderer.GetAlpha()} depth={g.depth} icon={icon.enabled} iconAlpha={icon.canvasRenderer.GetAlpha()} outline={outline.enabled} color={outline.effectColor}\n";
}
return output;
