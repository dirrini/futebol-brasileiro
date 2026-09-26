using UnityEngine;
using UnityEngine.UI;

namespace FStudio.MatchEngine.UI {
    /// <summary>
    /// A screen-space charge indicator owned by the existing input-pointer canvas.
    /// </summary>
    public sealed class ShotPowerBar {
        private const float WIDTH = 76f;
        private const float HEIGHT = 10f;
        private const float MIN_SCREEN_SCALE = 0.8f;

        private static readonly Color LOW_POWER_COLOR = new Color32(0x31, 0xD6, 0x6B, 0xFF);
        private static readonly Color MID_POWER_COLOR = new Color32(0xFF, 0xD4, 0x47, 0xFF);
        private static readonly Color FULL_POWER_COLOR = new Color32(0xF4, 0x43, 0x36, 0xFF);

        private readonly RectTransform parentRect;
        private readonly Canvas canvas;
        private readonly RectTransform root;
        private readonly Image fill;

        public ShotPowerBar(Transform parent) {
            parentRect = parent as RectTransform;
            canvas = parent.GetComponentInParent<Canvas>();

            var border = CreateImage("ShotPowerBar", parent, new Color(1f, 1f, 1f, 0.75f));
            root = border.rectTransform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(WIDTH, HEIGHT);

            var background = CreateImage("Background", root, new Color32(0x12, 0x1B, 0x22, 0xF2));
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.one;
            backgroundRect.offsetMax = -Vector2.one;

            fill = CreateImage("Fill", backgroundRect, LOW_POWER_COLOR);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            Hide();
        }

        public void Show(Vector3 worldPosition, float normalizedPower) {
            var camera = Camera.main;
            if (root == null || parentRect == null || camera == null) {
                Hide();
                return;
            }

            var screenPosition = camera.WorldToScreenPoint(worldPosition);
            var canvasScale = canvas != null ? Mathf.Max(canvas.scaleFactor, 0.001f) : 1f;
            // Keep the meter readable when the existing 1920px canvas scales down.
            var sizeScale = Mathf.Max(1f, MIN_SCREEN_SCALE / canvasScale);
            var halfWidth = WIDTH * sizeScale * canvasScale * 0.5f;
            var halfHeight = HEIGHT * sizeScale * canvasScale * 0.5f;

            if (screenPosition.z <= 0f ||
                screenPosition.x < halfWidth || screenPosition.x > Screen.width - halfWidth ||
                screenPosition.y < halfHeight || screenPosition.y > Screen.height - halfHeight ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPosition, null, out var localPosition)) {
                Hide();
                return;
            }

            root.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            root.localScale = Vector3.one * sizeScale;

            var power = Mathf.Clamp01(normalizedPower);
            fill.rectTransform.anchorMax = new Vector2(power, 1f);
            fill.color = power <= 0.5f
                ? Color.Lerp(LOW_POWER_COLOR, MID_POWER_COLOR, power * 2f)
                : Color.Lerp(MID_POWER_COLOR, FULL_POWER_COLOR, (power - 0.5f) * 2f);

            root.gameObject.SetActive(true);
        }

        public void Hide() {
            if (root != null) {
                root.gameObject.SetActive(false);
            }
        }

        private static Image CreateImage(string name, Transform parent, Color color) {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            gameObject.transform.SetParent(parent, false);

            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
