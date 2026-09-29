using FStudio.Events;
using FStudio.Loaders;
using FStudio.UI;
using FStudio.UI.Events;
using UnityEngine;
using FStudio.FootballWorld.Infrastructure.GameModes;

namespace FStudio {
    public class Boot : MonoBehaviour {
        private async void Start() {
            _ = GameHubSession.Current;
            await UILoader.Current.GeneralUILoader.Load();
            await SceneLoader.LoadDefaultScene();

            // show main menu.
            EventManager.Trigger(new MainMenuEvent());
        }
    }
}
