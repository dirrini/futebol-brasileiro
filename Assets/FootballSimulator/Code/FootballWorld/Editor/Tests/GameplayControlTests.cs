#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using FStudio.MatchEngine.Input;
using FStudio.MatchEngine.Players.InputBehaviours;
using FStudio.MatchEngine.Players.PlayerController;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.OnScreen;

namespace FStudio.FootballWorld.Editor.Tests {
    public sealed class GameplayControlTests {
        [TestCase(ChargedBallAction.ShortPass)]
        [TestCase(ChargedBallAction.ThroughPass)]
        [TestCase(ChargedBallAction.Cross)]
        [TestCase(ChargedBallAction.Shot)]
        public void HoldFillsInFiveHundredMillisecondsAndReleaseSpendsExactlyOnce(ChargedBallAction action) {
            var owner = new object(); var charge = new BallActionCharge();
            Assert.That(charge.TryBegin(action, owner, 20), Is.True);
            Assert.That(charge.Power(20, .5f), Is.Zero);
            Assert.That(charge.Power(20.25f, .5f), Is.EqualTo(.5f));
            Assert.That(charge.Power(20.5f, .5f), Is.EqualTo(1));
            Assert.That(charge.Power(25, .5f), Is.EqualTo(1));
            Assert.That(charge.TryRelease(action, owner, 20.5f, .5f, out var power), Is.True);
            Assert.That(power, Is.EqualTo(1));
            Assert.That(charge.TryRelease(action, owner, 21, .5f, out _), Is.False);
        }

        [Test]
        public void CancellationControlSwitchAndOtherButtonsCannotSpendAnotherPlayersHold() {
            var owner = new object(); var other = new object(); var charge = new BallActionCharge();
            Assert.That(charge.TryBegin(ChargedBallAction.ShortPass, owner, 0), Is.True);
            Assert.That(charge.TryBegin(ChargedBallAction.Cross, owner, .1f), Is.False);
            Assert.That(charge.TryRelease(ChargedBallAction.Cross, owner, .4f, .5f, out _), Is.False);
            Assert.That(charge.IsActive, Is.True);
            Assert.That(charge.TryRelease(ChargedBallAction.ShortPass, other, .5f, .5f, out _), Is.False);
            Assert.That(charge.IsActive, Is.False);
            foreach (var reason in new[] { "pause", "focus loss", "control switch", "possession lost", "action map disabled" }) {
                charge.TryBegin(ChargedBallAction.Shot, owner, 1);
                charge.Cancel();
                Assert.That(charge.TryRelease(ChargedBallAction.Shot, owner, 5, .5f, out _), Is.False, reason);
                Assert.That(charge.Owner, Is.Null);
            }
        }

        [Test]
        public void AerialAndGroundChargesKeepTheirOriginalPossessionContext() {
            var owner = new object(); var opponent = new object(); var charge = new BallActionCharge();
            charge.TryBegin(ChargedBallAction.Shot, owner, 0, beganWithoutPossession: true);
            Assert.That(charge.MatchesPossession(null), Is.True);
            Assert.That(charge.MatchesPossession(owner), Is.False, "Receiving the ball cannot turn an aerial intention into a ground shot.");
            Assert.That(charge.MatchesPossession(opponent), Is.False);
            charge.Cancel(); charge.TryBegin(ChargedBallAction.Shot, owner, 0);
            Assert.That(charge.MatchesPossession(owner), Is.True);
            Assert.That(charge.MatchesPossession(null), Is.False, "Losing possession cancels the original shot even near an airborne ball.");
        }

        [TestCase("Pass", GamepadButton.South, ChargedBallAction.ShortPass)]
        [TestCase("ThroughtPass", GamepadButton.North, ChargedBallAction.ThroughPass)]
        [TestCase("Cross", GamepadButton.East, ChargedBallAction.Cross)]
        [TestCase("Shoot", GamepadButton.West, ChargedBallAction.Shot)]
        public void AuthoredGamepadButtonProducesPressAndReleaseForChargedActions(string actionName, GamepadButton button, ChargedBallAction kind) {
            using var inputUpdates = new PlayerInputUpdatesInEditMode();
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/FootballSimulator/Input/engine.inputactions");
            var asset = Object.Instantiate(source);
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try {
                asset.devices = new InputDevice[] { gamepad };
                var map = asset.FindActionMap("MatchEngine", true);
                var action = map.FindAction(actionName, true);
                Assert.That(action.type, Is.EqualTo(InputActionType.PassThrough));
                var charge = new BallActionCharge(); var owner = new object(); var clock = 10f;
                var values = new List<float>(); var kicks = 0; var releasedPower = -1f;
                action.performed += ctx => {
                    values.Add(ctx.ReadValue<float>());
                    if (ctx.ReadValue<float>() > .5f) charge.TryBegin(kind, owner, clock);
                    else if (charge.TryRelease(kind, owner, clock, .5f, out var power)) { kicks++; releasedPower = power; }
                };
                map.Enable();
                Assert.That(action.controls, Does.Contain(gamepad[button]), "The authored binding must resolve to the virtual gamepad button.");
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); InputSystem.Update();
                Assert.That(InputState.currentUpdateType, Is.EqualTo(InputUpdateType.Dynamic));
                Assert.That(gamepad[button].isPressed, Is.True, "The queued event must reach the player input buffer.");
                Assert.That(charge.IsActive, Is.True); Assert.That(kicks, Is.Zero);
                InputSystem.Update();
                Assert.That(charge.IsActive, Is.True); Assert.That(values.Count, Is.EqualTo(1), "Holding does not trigger another press or an early release.");
                clock += .5f;
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
                Assert.That(values, Is.EqualTo(new[] { 1f, 0f }));
                Assert.That(kicks, Is.EqualTo(1)); Assert.That(releasedPower, Is.EqualTo(1));
            }
            finally { asset.Disable(); Object.DestroyImmediate(asset); InputSystem.RemoveDevice(gamepad); }
        }

        [Test]
        public void RightShoulderEnablesSprintWithoutTurningOrdinaryFullStickMovementIntoSprint() {
            using var inputUpdates = new PlayerInputUpdatesInEditMode();
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/FootballSimulator/Input/engine.inputactions");
            var asset = Object.Instantiate(source); var gamepad = InputSystem.AddDevice<Gamepad>();
            try {
                asset.devices = new InputDevice[] { gamepad };
                var map = asset.FindActionMap("MatchEngine", true); var sprint = map.FindAction("Sprint", true);
                Assert.That(sprint.bindings.Any(value => value.path == "<Keyboard>/leftShift"), Is.True);
                Assert.That(map.FindAction("Tackle").bindings, Is.Empty, "Circle is routed once through contextual Cross input.");
                map.Enable();
                Assert.That(sprint.controls, Does.Contain(gamepad.rightShoulder));
                Assert.That(TeamInputListener.MovementForInput(1, sprint.IsPressed()), Is.EqualTo(MovementType.Normal));
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder)); InputSystem.Update();
                Assert.That(InputState.currentUpdateType, Is.EqualTo(InputUpdateType.Dynamic));
                Assert.That(TeamInputListener.MovementForInput(1, sprint.IsPressed()), Is.EqualTo(MovementType.BestHeCanDo));
                InputSystem.Update();
                Assert.That(TeamInputListener.MovementForInput(1, sprint.IsPressed()), Is.EqualTo(MovementType.BestHeCanDo), "Sprint remains held across input updates until release.");
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
                Assert.That(TeamInputListener.MovementForInput(1, sprint.IsPressed()), Is.EqualTo(MovementType.Normal));
                Assert.That(TeamInputListener.MovementForInput(.4f, true), Is.EqualTo(MovementType.Relax));
            }
            finally { asset.Disable(); Object.DestroyImmediate(asset); InputSystem.RemoveDevice(gamepad); }
        }

        private sealed class PlayerInputUpdatesInEditMode : System.IDisposable {
            private const string RunPlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
            private readonly InputSettings previous = InputSystem.settings;
            private readonly InputSettings temporary;

            public PlayerInputUpdatesInEditMode() {
                temporary = Object.Instantiate(previous);
                temporary.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                // Input System 1.14 otherwise routes Update() to the Editor buffer outside Play Mode.
                // Keep the real event queue, device layouts and authored binding resolution under test.
                temporary.SetInternalFeatureFlag(RunPlayerUpdates, true);
                InputSystem.settings = temporary;
            }

            public void Dispose() {
                // ApplySettings only updates cached flags when a settings object has a flag collection.
                // Clear ours first so restoring an original object with no flags cannot leave this on.
                temporary.SetInternalFeatureFlag(RunPlayerUpdates, false);
                InputSystem.settings = previous;
                Object.DestroyImmediate(temporary);
            }
        }

        [TestCase("Assets/FootballSimulator/Arts/UI/Buttons/GamepadButton.prefab", 1)]
        [TestCase("Assets/FootballSimulator/Code/Input/InputMobile/Resources/InputMobile.prefab", 9)]
        public void OnScreenControlsIncludingPrefabOverridesUseThePortableGamepadLayout(string path, int expectedControls) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            var controls = prefab.GetComponentsInChildren<OnScreenControl>(true);
            Assert.That(controls.Length, Is.EqualTo(expectedControls));
            var gamepad = InputSystem.AddDevice<Gamepad>();
            try {
                foreach (var control in controls) {
                    Assert.That(InputControlPath.TryGetDeviceLayout(control.controlPath), Is.EqualTo("Gamepad"),
                        control.name + " must create the generic virtual device available in WebGL.");
                    Assert.That(InputControlPath.TryFindControl(gamepad, control.controlPath), Is.Not.Null,
                        control.name + " must reference an existing gamepad control.");
                }
            }
            finally { InputSystem.RemoveDevice(gamepad); }
        }

        [Test]
        public void AuthoredControlsGiveIncreasingPassSpeedCrossRangeAndAGreenToRedMeter() {
            var settings = MatchControlSettings.Current;
            Assert.That(settings, Is.Not.Null); Assert.That(settings.ChargeDuration, Is.EqualTo(.5f));
            Assert.That(AbstractInputPassBehaviour.GroundPassPower(0), Is.GreaterThan(0));
            Assert.That(AbstractInputPassBehaviour.GroundPassPower(1), Is.GreaterThan(AbstractInputPassBehaviour.GroundPassPower(0)));
            var origin = new Vector3(10, 0, 20); var target = new Vector3(50, 0, 40);
            var low = AbstractInputPassBehaviour.ChargedCrossTarget(origin, target, 0);
            var high = AbstractInputPassBehaviour.ChargedCrossTarget(origin, target, 1);
            Assert.That(Vector3.Distance(origin, high), Is.GreaterThan(Vector3.Distance(origin, low)));
            Assert.That(settings.PowerColors.Evaluate(0).g, Is.GreaterThan(settings.PowerColors.Evaluate(0).r));
            Assert.That(settings.PowerColors.Evaluate(1).r, Is.GreaterThan(settings.PowerColors.Evaluate(1).g));
        }
    }
}
#endif
