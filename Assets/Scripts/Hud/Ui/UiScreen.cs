using UnityEngine;

namespace IronRing.Hud.Ui
{
    /// <summary>
    /// 스토리 022. 화면 프리팹(Assets/Prefabs/UI/Screens/*.prefab) 루트의 표식. UiScreens가 Id로 이 루트를 SetActive 한다.
    /// AlwaysOn이면 Play 시작 때 놓인 그대로 두고, 아니면(상황 화면) Play 시작 때 끈다. 편집 모드에서는 모든 화면이 프리팹 그대로 보인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiScreen : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private bool _alwaysOn;

        /// <summary>화면 id(UiScreens.Show/Hide의 키). 프리팹 이름과 같다.</summary>
        public string Id => _id;
        /// <summary>항상 켜진 화면이면 true, 상황에 따라 켜는 화면이면 false.</summary>
        public bool AlwaysOn => _alwaysOn;
    }
}
