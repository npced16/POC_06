using UnityEngine;

using TMPro;

namespace IronRing.Combat
{
    /// <summary>전투 피해 숫자를 위로 띄우고 짧게 페이드아웃한다.</summary>
    public sealed class BattleDamagePopup : MonoBehaviour
    {
        [Header("Damage popup")]
        [SerializeField] private float _lifetime = 0.55f;
        [SerializeField] private float _riseSpeed = 1.1f;
        private TMP_Text _label;
        private float _age;

        void Awake() { _label = GetComponent<TMP_Text>(); }

        void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (_riseSpeed * Time.deltaTime);
            Color color = _label.color;
            color.a = 1f - Mathf.Clamp01(_age / _lifetime);
            _label.color = color;
            if (_age >= _lifetime) Destroy(gameObject);
        }
    }
}
