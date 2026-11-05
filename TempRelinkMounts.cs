if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("편집 모드가 필요합니다.");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!="Assets/Scenes/Main.unity" || scene.isDirty) throw new System.InvalidOperationException("저장된 Main 씬이 필요합니다.");
var screen=UnityEngine.Object.FindFirstObjectByType<IronRing.Combat.TrainArmamentScreen>(UnityEngine.FindObjectsInactive.Include);
var so=new UnityEditor.SerializedObject(screen);
var slots=so.FindProperty("_slots"); var texts=so.FindProperty("_slotTexts"); var icons=so.FindProperty("_slotIcons"); var ghosts=so.FindProperty("_ghostIcons");
const string path="Assets/Prefabs/UI/Parts/ArmamentMount.prefab";
var contents=UnityEditor.PrefabUtility.LoadPrefabContents(path);
try
{
    var turret=(UnityEngine.RectTransform)contents.transform.Find("Turret");
    var ghost=(UnityEngine.RectTransform)contents.transform.Find("Ghost");
    ghost.anchorMin=turret.anchorMin; ghost.anchorMax=turret.anchorMax; ghost.pivot=turret.pivot;
    ghost.anchoredPosition3D=turret.anchoredPosition3D; ghost.sizeDelta=turret.sizeDelta;
    ghost.localScale=turret.localScale; ghost.localRotation=turret.localRotation;
    var button=contents.GetComponent<UnityEngine.UI.Button>();
    var color=new UnityEngine.Color(1f,1f,1f,.004f);
    var colors=button.colors; colors.normalColor=color; colors.highlightedColor=color; colors.pressedColor=color; colors.selectedColor=color; colors.disabledColor=color;
    button.colors=colors; button.image.color=color; button.image.raycastTarget=true; button.image.canvasRenderer.cullTransparentMesh=false;
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(contents,path);
}
finally { UnityEditor.PrefabUtility.UnloadPrefabContents(contents); }
var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
for(int i=0;i<slots.arraySize;i++)
{
    var oldButton=(UnityEngine.UI.Button)slots.GetArrayElementAtIndex(i).objectReferenceValue;
    var old=oldButton.gameObject;
    var replacement=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(asset,old.transform.parent);
    replacement.name=old.name;
    replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
    var oldRoot=(UnityEngine.RectTransform)old.transform;
    var newRoot=(UnityEngine.RectTransform)replacement.transform;
    newRoot.anchorMin=oldRoot.anchorMin; newRoot.anchorMax=oldRoot.anchorMax; newRoot.pivot=oldRoot.pivot;
    newRoot.anchoredPosition3D=oldRoot.anchoredPosition3D; newRoot.sizeDelta=oldRoot.sizeDelta;
    newRoot.localScale=oldRoot.localScale; newRoot.localRotation=oldRoot.localRotation;
    foreach(UnityEngine.Transform child in old.transform)
    {
        var destination=replacement.transform.Find(child.name);
        if(destination==null) throw new System.InvalidOperationException("거치대 자식 누락: "+child.name);
        destination.SetSiblingIndex(child.GetSiblingIndex());
        if(child.name=="Ghost") continue;
        var sourceRect=child as UnityEngine.RectTransform; var targetRect=destination as UnityEngine.RectTransform;
        if(sourceRect!=null && targetRect!=null)
        {
            targetRect.anchorMin=sourceRect.anchorMin; targetRect.anchorMax=sourceRect.anchorMax; targetRect.pivot=sourceRect.pivot;
            targetRect.anchoredPosition3D=sourceRect.anchoredPosition3D; targetRect.sizeDelta=sourceRect.sizeDelta;
            targetRect.localScale=sourceRect.localScale; targetRect.localRotation=sourceRect.localRotation;
        }
        var sourceImage=child.GetComponent<UnityEngine.UI.Image>(); var targetImage=destination.GetComponent<UnityEngine.UI.Image>();
        if(sourceImage!=null && targetImage!=null)
        {
            targetImage.sprite=sourceImage.sprite; targetImage.color=sourceImage.color; targetImage.material=sourceImage.material;
            targetImage.raycastTarget=sourceImage.raycastTarget; targetImage.preserveAspect=sourceImage.preserveAspect;
        }
        destination.gameObject.SetActive(child.gameObject.activeSelf);
    }
    var newButton=replacement.GetComponent<UnityEngine.UI.Button>();
    var newTurret=replacement.transform.Find("Turret").GetComponent<UnityEngine.UI.Image>();
    var newGhost=replacement.transform.Find("Ghost").GetComponent<UnityEngine.UI.Image>();
    var newText=replacement.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
    var iconRect=newTurret.rectTransform; var ghostRect=newGhost.rectTransform;
    ghostRect.anchorMin=iconRect.anchorMin; ghostRect.anchorMax=iconRect.anchorMax; ghostRect.pivot=iconRect.pivot;
    ghostRect.anchoredPosition3D=iconRect.anchoredPosition3D; ghostRect.sizeDelta=iconRect.sizeDelta;
    ghostRect.localScale=iconRect.localScale; ghostRect.localRotation=iconRect.localRotation;
    slots.GetArrayElementAtIndex(i).objectReferenceValue=newButton;
    texts.GetArrayElementAtIndex(i).objectReferenceValue=newText;
    icons.GetArrayElementAtIndex(i).objectReferenceValue=newTurret;
    ghosts.GetArrayElementAtIndex(i).objectReferenceValue=newGhost;
    UnityEngine.Object.DestroyImmediate(old);
}
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
return "거치대 프리팹과 씬 인스턴스 네 개를 연결하고 고스트를 포탑 위치에 맞췄습니다.";
