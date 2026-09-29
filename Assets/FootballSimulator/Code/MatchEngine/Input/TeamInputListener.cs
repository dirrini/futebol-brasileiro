using UnityEngine;
using UnityEngine.InputSystem;

using FStudio.MatchEngine.Players;
using FStudio.Input;
using FStudio.MatchEngine.Balls;
using FStudio.MatchEngine.Enums;

using System.Linq;

using FStudio.MatchEngine.Players.Behaviours;
using FStudio.MatchEngine.Players.InputBehaviours;
using FStudio.Events;
using FStudio.MatchEngine.Events;
using FStudio.MatchEngine.Tactics;
using FStudio.MatchEngine.Cameras;
using FStudio.MatchEngine.Players.PlayerController;
using FStudio.MatchEngine.UI;
using static UnityEngine.Rendering.DebugUI;

namespace FStudio.MatchEngine.Input {
    public partial class TeamInputListener : InputListener {
        private static Vector3 inputWorldOffset = Vector3.up * 2;

        public enum ActionType {
            None,
            Pass,
            Shoot
        }

        private const float MOVE_DEADZONE = 0.3f;
        private readonly ShotPowerBar shotPowerBar;
        private readonly InputAction shootAction;
        private readonly InputAction passAction, throughAction, crossAction, sprintAction, moveAction;
        private readonly BallActionCharge actionCharge = new BallActionCharge();
        private Ball chargingBall;

        private PlayerBase m_ActivePlayer;
        public PlayerBase ActivePlayer { get => m_ActivePlayer; private set {
                if (m_ActivePlayer != value) {
                    CancelShotCharge();
                }
                Debug.Log("Assigned active player: " + value);
                if (m_ActivePlayer != value && direction.magnitude < MOVE_DEADZONE)
                    m_lastValidDirection = value != null ? value.Rotation * Vector3.forward : Vector3.zero;
                m_ActivePlayer = value;
            } }

        private readonly GameTeam gameTeam;


        private Vector3 m_lastValidDirection;

        /// <summary>
        /// Active direction
        /// </summary>
        private Vector3 direction;

        private readonly Transform inputPointer;
        private readonly Transform inputPointerFollower;

        public TeamInputListener(
            int playerIndex, 
            GameTeam gameTeam) : base("MatchEngine", playerIndex) {

            this.gameTeam = gameTeam;
            InitializeControlSwitching();

            inputPointer = Object.Instantiate(Resources.Load<Transform>("UI/InputPointer"));
            inputPointerFollower = inputPointer.GetChild(0);
            shotPowerBar = new ShotPowerBar(inputPointer);
            shootAction = PlayerInput?.actions.FindActionMap("MatchEngine").FindAction("Shoot");
            passAction = PlayerInput?.actions.FindActionMap("MatchEngine").FindAction("Pass");
            throughAction = PlayerInput?.actions.FindActionMap("MatchEngine").FindAction("ThroughtPass");
            crossAction = PlayerInput?.actions.FindActionMap("MatchEngine").FindAction("Cross");
            sprintAction = PlayerInput?.actions.FindActionMap("MatchEngine").FindAction("Sprint");
            moveAction = PlayerInput?.actions.FindActionMap("MatchEngine").FindAction("Move");
            foreach (var action in new[] { shootAction, passAction, throughAction, crossAction })
                if (action != null) action.canceled += OnShootCanceled;
            EventManager.Subscribe<MatchPauseEvent>(OnMatchPause);
            Application.focusChanged += OnFocusChanged;

            // create direction listener.
            RegisterAction("Move", MoveInput);
            RegisterAction("ChangePlayer", ChangePlayerInput);
            RegisterAction("Pass", PassInput);
            RegisterAction("ThroughtPass", ThroughtPass);
            RegisterAction("Tackle", TackleInput);
            RegisterAction("Shoot", ShootInput);
            RegisterAction("Cross", CrossInput);
            RegisterAction("ChangeTacticHigh", ChangeTacticHighInput);
            RegisterAction("ChangeTacticLow", ChangeTacticLowInput);
            //

            Debug.Log("Team Input Listener Created.");
        }

        public override void Clear() {
            ClearControlSwitching();
            CancelShotCharge();
            EventManager.UnSubscribe<MatchPauseEvent>(OnMatchPause);
            Application.focusChanged -= OnFocusChanged;
            foreach (var action in new[] { shootAction, passAction, throughAction, crossAction })
                if (action != null) action.canceled -= OnShootCanceled;
            if (inputPointer != null) {
                Object.Destroy(inputPointer.gameObject);
            }
            base.Clear();
        }

        private bool ChangeTacticHighInput(InputAction.CallbackContext ctx) {
            UpdateTactic(true);
            return true;
        }

        private bool ChangeTacticLowInput(InputAction.CallbackContext ctx) {
            UpdateTactic(false);
            return true;
        }

        private void UpdateTactic(bool increase) {
            var currentTactic = (int)MatchManager.Current.UserTeam.Team.TacticPresetType;
            currentTactic += increase ? 1 : -1;
            if (currentTactic < 0) {
                currentTactic = (int)TacticPresetTypes.ParameterCount - 1;
            } else if (currentTactic >= (int)TacticPresetTypes.ParameterCount) {
                currentTactic = 0;
            }

            var newTactic = (TacticPresetTypes)currentTactic;

            MatchManager.Current.UserTeam.Team.TacticPresetType = newTactic;
            EventManager.Trigger(new TeamChangedTactic(MatchManager.Current.UserTeam, newTactic));
        }

        private bool MoveInput (InputAction.CallbackContext ctx) {
            ReadMovementDirection();
            return true;
        }

        private void ReadMovementDirection() {
            if (!Application.isFocused || MatchPause.IsPaused || moveAction?.enabled != true) { direction = Vector3.zero; return; }
            var value = moveAction.ReadValue<Vector2>();
            var yaw = CameraSystem.Current != null ? CameraSystem.Current.transform.rotation.eulerAngles.y : 0;
            direction = Quaternion.Euler(0, yaw, 0) * new Vector3(value.x, 0, value.y);
        }

        private void ActivatePlayerBehaviour<T>() where T : BaseBehaviour, IInputBehaviour {
            if (ActivePlayer == null) return;
            if (ActivePlayer.ActiveBehaviour is IInputBehaviour) {
                return;
            }

            var tBehaviour = ActivePlayer.Behaviours.
                Where(x => x is T).
                Select(x => (T)x).
                FirstOrDefault();

            if (tBehaviour == null) return;

            tBehaviour.IsTriggered = true;

            var typeName = typeof(T).Name.Split('.').Last();

            Debug.Log("[TeamInputListener]" + typeName);

            ActivePlayer.ActivateBehaviour(typeName);
        }

        private void DectivatePlayerBehaviour<T>() where T : BaseBehaviour, IInputBehaviour {
            var tBehaviour = ActivePlayer.Behaviours.
                Where(x => x is T).
                Select(x => (T)x).
                FirstOrDefault();

            if (tBehaviour == null) return;

            tBehaviour.IsTriggered = false;

            ActivePlayer.ResetBehaviours();
        }

        private bool PassInput(InputAction.CallbackContext ctx) {
            var pressed = ctx.ReadValue<float>() > .5f;
            if (!pressed) StopPursuit();
            if (pressed && CanDefend()) {
                CancelShotCharge();
                var pursuit = ActivePlayer.Behaviours.OfType<InputTackleBehaviour>().FirstOrDefault();
                if (pursuit != null) {
                    pursuit.SprintHeld = IsSprintHeld;
                    ActivatePlayerBehaviour<InputTackleBehaviour>();
                }
                return true;
            }
            return ChargedInput(ChargedBallAction.ShortPass, pressed);
        }

        private bool ShootInput(InputAction.CallbackContext ctx) => ChargedInput(ChargedBallAction.Shot, ctx.ReadValue<float>() > .5f);
        private bool ThroughtPass(InputAction.CallbackContext ctx) => ChargedInput(ChargedBallAction.ThroughPass, ctx.ReadValue<float>() > .5f);

        private bool CrossInput(InputAction.CallbackContext ctx) {
            var pressed = ctx.ReadValue<float>() > .5f;
            if (pressed && CanDefend()) {
                CancelShotCharge();
                ActivatePlayerBehaviour<InputSlideTackleBehaviour>();
                return true;
            }
            return ChargedInput(ChargedBallAction.Cross, pressed);
        }

        // Legacy action name is retained for old bindings; the authored bindings
        // now route defence contextually through Pass and Cross only.
        private bool TackleInput(InputAction.CallbackContext ctx) => PassInput(ctx);

        private bool ChargedInput(ChargedBallAction action, bool pressed) {
            if (MatchPause.IsPaused || !Application.isFocused) {
                CancelShotCharge(); return false;
            }
            if (pressed) {
                if (CanChargeBallAction(ActivePlayer, action) && actionCharge.TryBegin(action, ActivePlayer, Time.unscaledTime,
                    action == ChargedBallAction.Shot && Ball.Current.HolderPlayer == null)) {
                    chargingBall = Ball.Current;
                    ShowActionCharge();
                }
                return true;
            }
            var valid = chargingBall == Ball.Current && actionCharge.MatchesPossession(Ball.Current?.HolderPlayer) && CanChargeBallAction(ActivePlayer, action);
            if (!actionCharge.TryRelease(action, ActivePlayer, Time.unscaledTime, MatchControlSettings.Current.ChargeDuration, out var charge)) return true;
            shotPowerBar.Hide();
            chargingBall = null;
            if (!valid) return true;
            switch (action) {
                case ChargedBallAction.Shot:
                    if (ActivePlayer.IsGK) { TryActivateGoalkeeperClearance(charge); break; }
                    if (TryActivateAerialShot(charge)) break;
                    var shoot = ActivePlayer.Behaviours.OfType<InputShootBehaviour>().FirstOrDefault();
                    if (shoot != null) { shoot.SetCharge(charge); ActivatePlayerBehaviour<InputShootBehaviour>(); }
                    break;
                case ChargedBallAction.ShortPass: ReleasePass<InputShortPassBehaviour>(charge); break;
                case ChargedBallAction.ThroughPass: ReleasePass<InputThroughtPassBehaviour>(charge); break;
                case ChargedBallAction.Cross: ReleasePass<InputCrossBehaviour>(charge); break;
            }
            return true;
        }

        private void ReleasePass<T>(float charge) where T : AbstractInputPassBehaviour {
            var behaviour = ActivePlayer.Behaviours.OfType<T>().FirstOrDefault();
            if (behaviour == null) return;
            behaviour.SetCharge(charge);
            ActivatePlayerBehaviour<T>();
        }

        private InputAction ActionFor(ChargedBallAction action) {
            switch (action) {
                case ChargedBallAction.ShortPass: return passAction;
                case ChargedBallAction.ThroughPass: return throughAction;
                case ChargedBallAction.Cross: return crossAction;
                case ChargedBallAction.Shot: return shootAction;
                default: return null;
            }
        }

        private bool CanChargeBallAction(PlayerBase player, ChargedBallAction action) {
            if (player == null || player != ActivePlayer || MatchPause.IsPaused || !Application.isFocused ||
                ActionFor(action)?.enabled != true || MatchManager.Current == null ||
                !player.PlayerController.IsPhysicsEnabled || player.ActiveBehaviour is IInputBehaviour) return false;
            var status = MatchManager.Current.MatchFlags;
            if (status != MatchStatus.Playing && status != MatchStatus.WaitingForKickOff) return false;
            if (action == ChargedBallAction.Shot && player.IsGK) return CanChargeGoalkeeperClearance(player);
            if (action == ChargedBallAction.Shot && CanChargeAerialShot(player)) return true;
            return Ball.Current != null && Ball.Current.HolderPlayer == player &&
                (action != ChargedBallAction.Shot || (!player.IsThrowHolder && !player.IsCornerHolder));
        }

        private bool CanDefend() => ActivePlayer != null && !ActivePlayer.IsGK && !MatchPause.IsPaused && Application.isFocused &&
            MatchManager.Current?.MatchFlags == MatchStatus.Playing && ActivePlayer.PlayerController.IsPhysicsEnabled &&
            Ball.Current != null && Ball.Current.HolderTeam != gameTeam;

        private bool IsSprintHeld => Application.isFocused && !MatchPause.IsPaused && sprintAction?.enabled == true && sprintAction.IsPressed();

        public static MovementType MovementForInput(float intensity, bool sprintHeld)
            => intensity < .5f ? MovementType.Relax : sprintHeld && intensity >= .75f ? MovementType.BestHeCanDo : MovementType.Normal;

        private void StopPursuit() {
            var pursuit = ActivePlayer?.Behaviours.OfType<InputTackleBehaviour>().FirstOrDefault();
            if (pursuit == null) return;
            pursuit.IsTriggered = false;
            if (ActivePlayer.ActiveBehaviour == pursuit) ActivePlayer.ResetBehaviours();
        }

        private void ShowActionCharge() {
            if (actionCharge.Owner is PlayerBase player)
                shotPowerBar.Show(player.Position + MatchControlSettings.Current.PowerBarWorldOffset,
                    actionCharge.Power(Time.unscaledTime, MatchControlSettings.Current.ChargeDuration));
        }

        // The historical method name is retained by GameTeam's per-frame hook.
        public void UpdateShotCharge() {
            UpdateAerialShotBuffer();
            var pursuit = ActivePlayer?.Behaviours.OfType<InputTackleBehaviour>().FirstOrDefault();
            if (pursuit != null) pursuit.SprintHeld = IsSprintHeld;
            if (!actionCharge.IsActive) return;
            if (chargingBall != Ball.Current || !actionCharge.MatchesPossession(Ball.Current?.HolderPlayer) ||
                !CanChargeBallAction(actionCharge.Owner as PlayerBase, actionCharge.Action)) { CancelShotCharge(); return; }
            ShowActionCharge();
        }

        private void CancelShotCharge() {
            actionCharge.Cancel();
            chargingBall = null;
            StopPursuit();
            CancelAerialShotInput();
            shotPowerBar?.Hide();
        }
        private void OnShootCanceled(InputAction.CallbackContext _) => CancelShotCharge();
        private void OnMatchPause(MatchPauseEvent _) => CancelShotCharge();
        private void OnFocusChanged(bool hasFocus) { if (!hasFocus) CancelShotCharge(); }

        private bool ChangePlayerInput(InputAction.CallbackContext ctx) {
            if (MatchPause.IsPaused) {
                return false;
            }

            var value = ctx.ReadValue<float>();
            if (value == 1) {
                Debug.Log("[TeamInputListener] Change Player");
                AssignPlayer();
            }

            return true;
        }

        private void AssignPlayer () {
            receiverControl.Cancel();
            var currentBallHolder = Ball.Current.HolderPlayer;

            if (currentBallHolder != null && ActivePlayer == currentBallHolder) {
                return;// cannot select another player.
            }

            var possibleTargets = gameTeam.GamePlayers.Where(x => !x.IsGK && x != ActivePlayer && EligibleControlledPlayer(x));

            if (currentBallHolder != null && currentBallHolder.GameTeam == gameTeam) {
                ActivePlayer = currentBallHolder;
                return;
            }

            if (currentBallHolder != null && currentBallHolder.GameTeam != gameTeam) {
                // opponent has the ball.
                // select tacklers.

                #region select from tacklers
                var tacklers = possibleTargets.Where(x => x.ActiveBehaviour is TryToTackleBehaviour).Select (x=>x.ActiveBehaviour as TryToTackleBehaviour).ToArray ();

                if (tacklers.Length > 0) {
                    //select one of the tacklers.
                    ActivePlayer = tacklers.OrderBy(x => x.MovementType).FirstOrDefault ().Player;
                    return;
                }
                #endregion

                ActivePlayer = possibleTargets.OrderBy(player => BallChasingBehaviour.BallChasingDistance(player)).FirstOrDefault() ?? ActivePlayer;
            }
            else {
                // ball is free, select one of chasers.

                #region select from receivers
                var receivers = possibleTargets.Where(x => x.ActiveBehaviour is BallChasingWithoutCondition).Select(x => x.ActiveBehaviour as BallChasingWithoutCondition).ToArray();
                if (receivers.Length > 0) {
                    ActivePlayer = receivers.OrderBy (x=>x.ChasingDistance).FirstOrDefault ().Player;
                    return;
                }
                #endregion

                #region select from chasers
                var chasers = possibleTargets.Where(x => x.ActiveBehaviour is BallChasingBehaviour).ToArray();

                if (chasers.Length > 0) {
                    //select one of the tacklers.
                    ActivePlayer = chasers.OrderBy(x => BallChasingBehaviour.BallChasingDistance(x)).FirstOrDefault();
                    return;
                }
                #endregion

                // select the best option.
                var bestOption = possibleTargets.
                    OrderBy(x => BallChasingBehaviour.BallChasingDistance(x)).FirstOrDefault();

                ActivePlayer = bestOption;
            }
        }

        public void Update (in float deltaTime) {
            ReadMovementDirection();
            UpdateAutomaticControl();
            if (ActivePlayer == null) {
                Debug.Log("[TeamInputListener] Input listener doesnt have a player. Looking for a player to control.");

                AssignPlayer();
            }

            var holderPlayer = Ball.Current.HolderPlayer;

            if (ActivePlayer != holderPlayer && holderPlayer != null && holderPlayer.GameTeam == gameTeam) {
                ActivePlayer = holderPlayer;
            }

            if (ActivePlayer == null) {
                return;
            }

            if (direction.magnitude > 0.2f) {
                m_lastValidDirection = direction;
            }

            if (m_lastValidDirection.magnitude < 0.2f) {
                m_lastValidDirection = ActivePlayer.Rotation * Vector3.forward;
            }

            var behaviours = ActivePlayer.Behaviours.
                Where(x => x is IInputBehaviour).
                Select(x => (IInputBehaviour)x);

            foreach (var ib in behaviours) {
                ib.InputDirection = ib.InputDirection = m_lastValidDirection;
            }

            if (ActivePlayer.PlayerController.IsPhysicsEnabled) {
                if (!MatchManager.Current.MatchFlags.HasFlag( MatchStatus.Playing )) {
                    if (ActivePlayer.IsHoldingBall) {
                        ActivePlayer.LookTo(in deltaTime, direction);
                    }
                }
            }


            if (ActivePlayer.PlayerController.IsPhysicsEnabled && MatchManager.Current.MatchFlags == MatchStatus.Playing &&
                ActivePlayer.ActiveBehaviour is not IInputBehaviour && !IsAssistingReceiver(ActivePlayer)) {
                var length = direction.magnitude;

                var activePosition = ActivePlayer.Position;
                activePosition += direction.normalized * 2;

                if (length < MOVE_DEADZONE) {
                    ActivePlayer.Stop(in deltaTime);
                } else {
                    var movementType = MovementForInput(length, IsSprintHeld);
                    ActivePlayer.MoveTo(in deltaTime, activePosition, true, movementType);
                }
            }

            // reposition UI Input pointer.
            var arrowIndicatorTransform =
                GetArrowIndicatorPositionAndAngle(Camera.main.WorldToScreenPoint(ActivePlayer.Position + inputWorldOffset));

            inputPointerFollower.position = arrowIndicatorTransform.position;
            inputPointerFollower.rotation = arrowIndicatorTransform.rotation;
        }

        private (Vector2 position, Quaternion rotation) GetArrowIndicatorPositionAndAngle(
                    Vector3 originalPosition) {

            bool isInBounds(Vector3 pos, Vector2 screen) {
                return !(pos.x > screen.x - 50 || pos.x < 50 || pos.y > screen.y - 50 || pos.y < 50);
            }

            var screen = new Vector2(Screen.width, Screen.height);

            if (!isInBounds(originalPosition, screen)) {
                var ballOnScreen = Camera.main.WorldToScreenPoint(Ball.Current.transform.position);
                var dir = (originalPosition - ballOnScreen).normalized;

                originalPosition = ballOnScreen;

                while (isInBounds(originalPosition, screen)) {
                    originalPosition += dir;
                }

                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Quaternion q = Quaternion.AngleAxis(angle, Vector3.forward);

                return (originalPosition, q);
            }

            return (originalPosition, Quaternion.Euler(0, 0, -90));
        }
    }
}
