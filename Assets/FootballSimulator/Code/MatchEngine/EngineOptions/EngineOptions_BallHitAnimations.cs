
using FStudio.MatchEngine.Players.PlayerController;
using FStudio.Scriptables;
using FStudio.Utilities;
using System;
using UnityEngine;
using FStudio.MatchEngine.Players;

namespace FStudio.MatchEngine.EngineOptions {
    [Serializable]
    public class EngineOptions_BallHitAnimations : SerializedSingletonScriptable<EngineOptions_BallHitAnimations> {
        public SerializableEnumDictionary<PlayerAnimatorVariable, bool>  AnimSettings;

        public string[] BlockBehavioursWhenThisClipsAreaPlaying;

        public string[] BallHitActionBlockers;

        public float BlockForVolley = 1.5f;
        public float BlockForHeader = 0.75f;
        public float BlockForGroundHeader = 0.25f;

        public float Header_VelMulti = 0.5f;

        public float BALL_HEIGHT_VOLLEY_END = 1.75f;
        public float BALL_HEIGHT_VOLLEY_START = 1.5f;
        public float BALL_HEIGHT_GROUND_HEADER_UNTIL = 1.1f;
        public float SPECIAL_BALL_HIT_TIME_OFFSET = 0.2f;

        [Header("Controlled aerial finishes")]
        [Tooltip("First eligible binding wins. Contact bands and recovery match the separately authored aerial clips.")]
        public AerialShotBinding[] AerialShots = AerialShotRules.Defaults();
        [Range(0.05f, 0.6f)] public float AerialInputBufferSeconds = 0.35f;
        [Min(1)] public float AerialInputReach = 6;
        [Min(0.05f)] public float AerialContactGraceSeconds = 0.2f;
    }
}
