using UnityEngine;

namespace FStudio.MatchEngine.Input {
    [CreateAssetMenu(menuName = "Football World/Match controls")]
    public sealed class MatchControlSettings : ScriptableObject {
        public const string ResourcePath = "FootballWorld/MatchControlSettings";
        private static MatchControlSettings current;
        public static MatchControlSettings Current => current != null ? current : current = Resources.Load<MatchControlSettings>(ResourcePath);
        [Header("Hold and release / seconds")]
        [Min(.05f)] public float ChargeDuration = .5f;
        [Range(.1f, 1f)] public float PassMinPower = .45f;
        [Range(1f, 2f)] public float PassMaxPower = 1.15f;
        [Range(.1f, 1f)] public float CrossMinRange = .5f;
        [Range(1f, 1.5f)] public float CrossMaxRange = 1.1f;
        [Header("Defending")]
        [Range(.5f, 2f)] public float StandingTackleDistance = 1.6f;
        [Min(.1f)] public float StandingTackleCooldown = .6f;
        [Header("Goalkeeper clearance / metres")]
        [Min(5f)] public float GoalkeeperClearanceMinRange = 18f;
        [Min(10f)] public float GoalkeeperClearanceMaxRange = 70f;
        [Header("Power indicator")]
        public Vector2 PowerBarSize = new Vector2(76, 10);
        public Vector3 PowerBarWorldOffset = new Vector3(0, 2.8f, 0);
        [Range(.3f, 2f)] public float MinimumScreenScale = .8f;
        public Gradient PowerColors = DefaultPowerColors();
        private static Gradient DefaultPowerColors() {
            var colors = new Gradient();
            colors.SetKeys(new[] {
                new GradientColorKey(new Color32(0x31, 0xD6, 0x6B, 0xFF), 0),
                new GradientColorKey(new Color32(0xFF, 0xD4, 0x47, 0xFF), .5f),
                new GradientColorKey(new Color32(0xF4, 0x43, 0x36, 0xFF), 1)
            }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return colors;
        }
    }
}
