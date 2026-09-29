using System;
using UnityEngine;

namespace FStudio.FootballWorld.Infrastructure.GameModes
{
    [Serializable]
    public sealed class LocalizedGameString
    {
        public string Key;
        [TextArea] public string Portuguese;
        [TextArea] public string English;
    }

    [CreateAssetMenu(fileName = "GameText", menuName = "Football World/Game text")]
    public sealed class GameTextCatalog : ScriptableObject
    {
        public LocalizedGameString[] Entries = Array.Empty<LocalizedGameString>();
    }
}
