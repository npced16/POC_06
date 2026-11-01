using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace IronRing.Hud.Npc
{
    /// <summary>스토리 022. NPC 대화 선택지 버튼 부품(Parts/DialogOption.prefab). 버튼과 라벨 참조만 갖는다.</summary>
    public sealed class DialogOptionView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;

        /// <summary>선택지 버튼.</summary>
        public Button Button => _button;

        /// <summary>문구와 누를 수 있는지를 쓴다.</summary>
        public void Show(string text, bool interactable)
        {
            if (_label != null) _label.text = text;
            if (_button != null) _button.interactable = interactable;
        }
    }
}
