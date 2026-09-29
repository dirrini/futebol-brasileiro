using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    // Geometry is authored in the prefab. A profile selects a palette, never a model or physics asset.
    [ExecuteAlways]
    public sealed class CoachAvatarView : MonoBehaviour
    {
        [SerializeField] private GameHubTheme theme;
        [SerializeField] private string avatarId = "coach-1";
        [SerializeField] private Image face;
        [SerializeField] private Image neck;
        [SerializeField] private Image hair;
        [SerializeField] private Image shirt;

        public void Bind(string id)
        {
            avatarId = id;
            Refresh();
        }

        private void OnEnable() { Refresh(); }
        private void OnValidate() { Refresh(); }

        private void Refresh()
        {
            var portrait = theme == null || theme.Portraits == null ? null : theme.Portraits.FirstOrDefault(item => item != null && item.Id == avatarId);
            if (portrait == null) return;
            if (face != null) face.color = portrait.Skin;
            if (neck != null) neck.color = portrait.Skin;
            if (hair != null) { hair.color = portrait.Hair; hair.gameObject.SetActive(portrait.HasHair); }
            if (shirt != null) shirt.color = portrait.Shirt;
        }
    }
}
