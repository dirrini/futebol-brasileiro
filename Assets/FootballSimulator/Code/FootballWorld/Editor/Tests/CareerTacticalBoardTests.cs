#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using FStudio.FootballWorld.Application;
using FStudio.FootballWorld.Domain;
using FStudio.FootballWorld.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FStudio.FootballWorld.Editor.Tests
{
    public sealed class CareerTacticalBoardTests
    {
        private GameObject root;
        private CareerTacticalBoard board;
        private CareerTacticPlan saved, changed;

        [SetUp]
        public void CreateBoard()
        {
            root = new GameObject("Tactical board test");
            board = root.AddComponent<CareerTacticalBoard>();
            saved = CareerTacticPlan.CreateDefault(CareerFormation.FourFourTwo);
            board.DraftChanged += plan => changed = plan;
            board.Bind(saved, Array.Empty<PlayerDefinition>(), true);
        }

        [TearDown]
        public void DestroyBoard() { UnityEngine.Object.DestroyImmediate(root); }

        [Test]
        public void DragCoordinatesUseAttackingDepthAndLeaveSavedPlanUnchanged()
        {
            board.MoveSlot(2, new Vector2(.40f, .65f));
            Assert.That(changed.GetSlot("left-centre-back").X, Is.EqualTo(.40f));
            Assert.That(changed.GetSlot("left-centre-back").Depth, Is.EqualTo(.35f).Within(.00001f));
            Assert.That(saved.IsDefault, Is.True);
            Assert.That(board.GetSlotPosition(2).y, Is.EqualTo(.65f).Within(.00001f));
        }

        [Test]
        public void DragStopsAtSlotBoundsAndReadOnlyBoardDoesNotEmitChanges()
        {
            board.MoveSlot(0, new Vector2(-5, -5));
            var definition = CareerTacticPlan.GetSlotDefinitions(saved.Formation)[0];
            Assert.That(changed.GetSlot("goalkeeper").X, Is.EqualTo(definition.MinX));
            Assert.That(changed.GetSlot("goalkeeper").Depth, Is.EqualTo(definition.MaxDepth));
            changed = null; board.Bind(saved, Array.Empty<PlayerDefinition>(), false);
            board.MoveSlot(0, new Vector2(.6f, .82f));
            Assert.That(changed, Is.Null);
        }

        [Test]
        public void KeyboardUpMovesTowardAttackInsteadOfTowardOwnGoal()
        {
            var marker = new GameObject("Marker", typeof(RectTransform)); marker.transform.SetParent(root.transform);
            var input = marker.AddComponent<CareerTacticalSlot>();
            var data = new SerializedObject(input); data.FindProperty("board").objectReferenceValue = board;
            data.FindProperty("slotIndex").intValue = 2; data.ApplyModifiedPropertiesWithoutUndo();
            var move = new AxisEventData(null) { moveDir = MoveDirection.Up };
            input.OnMove(move);
            Assert.That(changed.GetSlot("left-centre-back").Depth, Is.GreaterThan(saved.GetSlot("left-centre-back").Depth));
            Assert.That(changed.GetSlot("left-centre-back").X, Is.EqualTo(saved.GetSlot("left-centre-back").X));
            Assert.That(move.used, Is.True);
        }
    }
}
#endif
