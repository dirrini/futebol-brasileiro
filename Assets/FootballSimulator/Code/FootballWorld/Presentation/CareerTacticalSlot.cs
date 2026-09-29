using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FStudio.FootballWorld.Presentation
{
    // The marker and hit area are authored in the prefab. Input only edits the board's draft.
    public sealed class CareerTacticalSlot : Selectable, IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        [SerializeField] private CareerTacticalBoard board;
        [SerializeField] private int slotIndex;
        [SerializeField, Range(.005f, .1f)] private float keyboardStep = .02f;
        private Vector2 pointerOrigin, positionOrigin;
        private bool dragging;

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (IsInteractable()) board?.SelectSlot(slotIndex);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsInteractable() || eventData.button != PointerEventData.InputButton.Left || board == null) return;
            board.SelectSlot(slotIndex);
            dragging = board.TryGetPointerPosition(eventData, out pointerOrigin);
            positionOrigin = board.GetSlotPosition(slotIndex);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging && IsInteractable() && board.TryGetPointerPosition(eventData, out var pointer))
                board.MoveSlot(slotIndex, positionOrigin + pointer - pointerOrigin);
        }

        public void OnEndDrag(PointerEventData eventData) { dragging = false; }

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsInteractable() || board == null) { base.OnMove(eventData); return; }
            var direction = eventData.moveDir == MoveDirection.Left ? Vector2.left
                : eventData.moveDir == MoveDirection.Right ? Vector2.right
                : eventData.moveDir == MoveDirection.Up ? Vector2.down
                : eventData.moveDir == MoveDirection.Down ? Vector2.up : Vector2.zero;
            if (direction == Vector2.zero) { base.OnMove(eventData); return; }
            board.MoveSlot(slotIndex, board.GetSlotPosition(slotIndex) + direction * keyboardStep);
            eventData.Use();
        }

        public void OnCancel(BaseEventData eventData)
        {
            dragging = false;
            board?.FocusRoleSelector();
            eventData.Use();
        }

        protected override void OnDisable() { dragging = false; base.OnDisable(); }
    }
}
