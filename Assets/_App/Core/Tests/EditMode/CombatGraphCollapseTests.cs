using System;
using System.Collections.Generic;
using _App;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class CombatGraphCollapseTests
    {
        private const string Profile = "TestProfile";

        private static readonly NormalizedActionWindow Chain =
            new NormalizedActionWindow(0.4f, 0.9f);

        private List<PlayerCombatAction> _actions;
        private List<PlayerCombatEntryRule> _entries;
        private List<PlayerCombatTransitionRule> _transitions;

        [SetUp]
        public void SetUp()
        {
            _actions = new List<PlayerCombatAction>
            {
                CombatGraphFixture.Attack(
                    "combo.0",
                    comboIndex: 0,
                    definition: CombatGraphFixture.Definition(chain: Chain)),
                CombatGraphFixture.Attack(
                    "combo.1",
                    comboIndex: 1,
                    definition: CombatGraphFixture.Definition(chain: Chain)),
                CombatGraphFixture.Attack(
                    "combo.2",
                    comboIndex: 2,
                    definition: CombatGraphFixture.Definition(chain: Chain))
            };
            _entries = new List<PlayerCombatEntryRule>
            {
                new PlayerCombatEntryRule(
                    CombatGraphFixture.Light,
                    CombatGraphFixture.Action("combo.0"))
            };
            _transitions = new List<PlayerCombatTransitionRule>
            {
                Link("combo.0", "combo.1"),
                Link("combo.1", "combo.2")
            };
        }

        private static PlayerCombatTransitionRule Link(string from, string to)
        {
            return new PlayerCombatTransitionRule(
                CombatGraphFixture.Action(from),
                CombatGraphFixture.Light,
                CombatGraphFixture.Action(to),
                PlayerCombatTransitionWindow.Chain);
        }

        private void Collapse(params string[] unavailable)
        {
            var ids = new List<PlayerCombatActionId>();
            for (int i = 0; i < unavailable.Length; i++)
            {
                ids.Add(CombatGraphFixture.Action(unavailable[i]));
            }

            PlayerCombatGraphCollapse.Apply(
                ids,
                _actions,
                _entries,
                _transitions,
                Profile);
        }

        private bool HasAction(string id)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Id == CombatGraphFixture.Action(id))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void RemovingTheLastStepKeepsTheRestOfTheChain()
        {
            Collapse("combo.2");

            Assert.AreEqual(2, _actions.Count);
            Assert.IsTrue(HasAction("combo.0"));
            Assert.IsTrue(HasAction("combo.1"));
            Assert.AreEqual(1, _transitions.Count);
        }

        [Test]
        public void RemovingAMiddleStepCollapsesEverythingBehindIt()
        {
            Collapse("combo.1");

            Assert.AreEqual(1, _actions.Count);
            Assert.IsTrue(HasAction("combo.0"));
            Assert.IsFalse(
                HasAction("combo.2"),
                "A step reachable only through a removed one must collapse too.");
            Assert.IsEmpty(_transitions);
        }

        [Test]
        public void CollapsedGraphStillCompiles()
        {
            Collapse("combo.1");

            Assert.DoesNotThrow(
                () => CombatGraphFixture.Graph(_actions, _entries, _transitions));
        }

        [Test]
        public void AStepReachableByAnotherRouteSurvives()
        {
            _transitions.Add(new PlayerCombatTransitionRule(
                CombatGraphFixture.Action("combo.0"),
                CombatGraphFixture.Heavy,
                CombatGraphFixture.Action("combo.2"),
                PlayerCombatTransitionWindow.Chain));

            Collapse("combo.1");

            Assert.IsTrue(HasAction("combo.2"));
            Assert.AreEqual(2, _actions.Count);
        }

        [Test]
        public void RemovingTheOnlyEntryTargetIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => Collapse("combo.0"));
        }

        [Test]
        public void AnEmptyRemovalSetChangesNothing()
        {
            Collapse();

            Assert.AreEqual(3, _actions.Count);
            Assert.AreEqual(2, _transitions.Count);
            Assert.AreEqual(1, _entries.Count);
        }

        [Test]
        public void EntriesTargetingARemovedStepAreDropped()
        {
            _entries.Add(new PlayerCombatEntryRule(
                CombatGraphFixture.Heavy,
                CombatGraphFixture.Action("combo.2")));

            Collapse("combo.2");

            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual(CombatGraphFixture.Light, _entries[0].CommandId);
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            var ids = new List<PlayerCombatActionId>();
            Assert.Throws<ArgumentNullException>(() =>
                PlayerCombatGraphCollapse.Apply(
                    null,
                    _actions,
                    _entries,
                    _transitions,
                    Profile));
            Assert.Throws<ArgumentNullException>(() =>
                PlayerCombatGraphCollapse.Apply(
                    ids,
                    null,
                    _entries,
                    _transitions,
                    Profile));
            Assert.Throws<ArgumentNullException>(() =>
                PlayerCombatGraphCollapse.Apply(
                    ids,
                    _actions,
                    null,
                    _transitions,
                    Profile));
            Assert.Throws<ArgumentNullException>(() =>
                PlayerCombatGraphCollapse.Apply(
                    ids,
                    _actions,
                    _entries,
                    null,
                    Profile));
        }
    }
}