using TMPro;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;
        [SerializeField] private string key;
        public string Key { get => key; set { key = value; Refresh(); } }
        private void OnEnable() { GameText.EnsureLoaded(); GameText.Changed += Refresh; Refresh(); }
        private void OnDisable() { GameText.Changed -= Refresh; }
        private void OnValidate() { if (target == null) target = GetComponent<TMP_Text>(); }
        public void Refresh()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            if (target != null && !string.IsNullOrEmpty(key)) target.text = GameText.Get(key);
        }
    }
}
