using System;
using System.Collections.Generic;
using _App;
using NUnit.Framework;
using static ProjectS.Core.Tests.CombatGraphFixture;

namespace ProjectS.Core.Tests
{
    public sealed class CombatActionGraphTests
    {
        private static PlayerCombatActionGraph TwoStepChain(
            PlayerCombatTransitionWindow window = PlayerCombatTransitionWindow.Chain,
            PlayerCombatTransitionCondition condition = PlayerCombatTransitionCondition.Any,
            NormalizedActionWindow acceptance = default,
            NormalizedActionWindow input = default)
        {
            var actions = new List<PlayerCombatAction>
            {
                Attack("combo.0", comboIndex: 0, definition: Definition(
                    chain: new NormalizedActionWindow(0.4f, 0.8f),
                    cancel: new NormalizedActionWindow(0.5f, 0.9f),
                    hitbox: new NormalizedActionWindow(0.2f, 0.35f))),
                Attack("combo.1", comboIndex: 1)
            };
            var entries = new List<PlayerCombatEntryRule>
            {
                new PlayerCombatEntryRule(Light, Action("combo.0"))
            };
            var transitions = new List<PlayerCombatTransitionRule>
            {
                new PlayerCombatTransitionRule(
                    Action("combo.0"), Light, Action("combo.1"),
                    window, condition, acceptance, input)
            };
            return Graph(actions, entries, transitions);
        }

        [Test]
        public void EntryResolvesToItsAuthoredAction()
        {
            PlayerCombatActionGraph graph = TwoStepChain();

            Assert.IsTrue(graph.TryResolveEntry(
                Light, PlayerCombatEnvironment.Grounded, out PlayerCombatAction action));
            Assert.AreEqual(Action("combo.0"), action.Id);
        }

        [Test]
        public void UnknownCommandHasNoEntry()
        {
            PlayerCombatActionGraph graph = TwoStepChain();

            Assert.IsFalse(graph.TryResolveEntry(
                Heavy, PlayerCombatEnvironment.Grounded, out _));
        }

        [Test]
        public void GroundedEntryIsNotOfferedWhileAirborne()
        {
            PlayerCombatActionGraph graph = TwoStepChain();

            Assert.IsFalse(graph.TryResolveEntry(
                Light, PlayerCombatEnvironment.Airborne, out _));
        }

        [Test]
        public void EntryPicksTheCandidateMatchingTheEnvironment()
        {
            var actions = new List<PlayerCombatAction>
            {
                Attack("ground.0", PlayerCombatEnvironment.Grounded),
                Attack("air.0", PlayerCombatEnvironment.Airborne)
            };
            var entries = new List<PlayerCombatEntryRule>
            {
                new PlayerCombatEntryRule(Light, Action("ground.0")),
                new PlayerCombatEntryRule(Light, Action("air.0"))
            };
            PlayerCombatActionGraph graph = Graph(
                actions, entries, Array.Empty<PlayerCombatTransitionRule>());

            graph.TryResolveEntry(Light, PlayerCombatEnvironment.Grounded, out PlayerCombatAction ground);
            graph.TryResolveEntry(Light, PlayerCombatEnvironment.Airborne, out PlayerCombatAction air);

            Assert.AreEqual(Action("ground.0"), ground.Id);
            Assert.AreEqual(Action("air.0"), air.Id);
        }

        [Test]
        public void ChainTransitionOpensOnlyWhileTheFrameAllowsChaining()
        {
            PlayerCombatActionGraph graph = TwoStepChain();
            graph.TryResolveEntry(Light, PlayerCombatEnvironment.Grounded, out PlayerCombatAction source);
            PlayerCombatCommandSample command =
                PlayerCombatCommandSample.Transition(Light, source.Id, 0.5f);

            CombatActionFrame closed = Frame(canChain: false);
            CombatActionFrame open = Frame(canChain: true);

            Assert.IsFalse(graph.TryResolveTransition(
                source, in command, PlayerCombatEnvironment.Grounded,
                in closed, Outcome(), out _));
            Assert.IsTrue(graph.TryResolveTransition(
                source, in command, PlayerCombatEnvironment.Grounded,
                in open, Outcome(), out PlayerCombatAction next));
            Assert.AreEqual(Action("combo.1"), next.Id);
        }

        [Test]
        public void CancelTransitionIgnoresTheChainFlag()
        {
            PlayerCombatActionGraph graph = TwoStepChain(
                window: PlayerCombatTransitionWindow.Cancel);
            graph.TryResolveEntry(Light, PlayerCombatEnvironment.Grounded, out PlayerCombatAction source);
            PlayerCombatCommandSample command =
                PlayerCombatCommandSample.Transition(Light, source.Id, 0.5f);

            CombatActionFrame chainOnly = Frame(canChain: true, canCancel: false);
            CombatActionFrame cancelOpen = Frame(canChain: false, canCancel: true);

            Assert.IsFalse(graph.TryResolveTransition(
                source, in command, PlayerCombatEnvironment.Grounded,
                in chainOnly, Outcome(), out _));
            Assert.IsTrue(graph.TryResolveTransition(
                source, in command, PlayerCombatEnvironment.Grounded,
                in cancelOpen, Outcome(), out _));
        }

        [Test]
        public void CommandFromAnotherActionIsRefused()
        {
            PlayerCombatActionGraph graph = TwoStepChain();
            graph.TryResolveEntry(Light, PlayerCombatEnvironment.Grounded, out PlayerCombatAction source);
            PlayerCombatCommandSample foreign =
                PlayerCombatCommandSample.Transition(Light, Action("combo.1"), 0.5f);
            CombatActionFrame frame = Frame(canChain: true);

            Assert.IsFalse(graph.TryResolveTransition(
                source, in foreign, PlayerCombatEnvironment.Grounded,
                in frame, Outcome(), out _));
        }

        [Test]
        public void EntryCommandIsNotAcceptedAsATransition()
        {
            PlayerCombatActionGraph graph = TwoStepChain();
            graph.TryResolveEntry(Light, PlayerCombatEnvironment.Grounded, out PlayerCombatAction source);
            PlayerCombatCommandSample entry = PlayerCombatCommandSample.Entry(Light);
            CombatActionFrame frame = Frame(canChain: true);

            Assert.IsFalse(graph.TryResolveTransition(
                source, in entry, PlayerCombatEnvironment.Grounded,
                in frame, Outcome(), out _));
        }

        [Test]
        public void AcceptanceWindowNarrowsTheChainWindow()
        {
            PlayerCombatActionGraph graph = TwoStepChain(
                acceptance: new NormalizedActionWindow(0.6f, 0.7f));
            graph.TryResolveEntry(Light, PlayerCombatEnvironment.Grounded, out PlayerCombatAction source);
            PlayerCombatCommandSample command =
                PlayerCombatCommandSample.Transition(Light, source.Id, 0.5f);

            CombatActionFrame early = Frame(normalizedTime: 0.50f, canChain: true);
            CombatActionFrame inside = Frame(normalizedTime: 0.65f, canChain: true);

            Assert.IsFalse(graph.TryResolveTransition(
                source, in command, PlayerCombatEnvironment.Grounded,
                in early, Outcome(), out _));
            Assert.IsTrue(graph.TryResolveTransition(
                source, in command, PlayerCombatEnvironment.Grounded,
                in inside, Outcome(), out _));
        }

        [Test]
        public void InputWindowFiltersByWhenTheCommandArrived()
        {
            PlayerCombatActionGraph graph = TwoStepChain(
                input: new NormalizedActionWindow(0.6f, 0.9f));
            graph.TryResolveEntry(Light, PlayerCombatEnvironment.Grounded, out PlayerCombatAction source);
            CombatActionFrame frame = Frame(canChain: true);

            PlayerCombatCommandSample tooEarly =
                PlayerCombatCommandSample.Transition(Light, source.Id, 0.30f);
            PlayerCombatCommandSample inWindow =
                PlayerCombatCommandSample.Transition(Light, source.Id, 0.70f);

            Assert.IsFalse(graph.TryResolveTransition(
                source, in tooEarly, PlayerCombatEnvironment.Grounded,
                in frame, Outcome(), out _));
            Assert.IsTrue(graph.TryResolveTransition(
                source, in inWindow, PlayerCombatEnvironment.Grounded,
                in frame, Outcome(), out _));
        }

        [Test]
        public void HasConditionalTransitionReportsAuthoredIntent()
        {
            PlayerCombatActionGraph any = TwoStepChain();
            PlayerCombatActionGraph onHit = TwoStepChain(
                condition: PlayerCombatTransitionCondition.OnHit);

            Assert.IsFalse(any.HasConditionalTransition(Action("combo.0"), Light));
            Assert.IsTrue(onHit.HasConditionalTransition(Action("combo.0"), Light));
            Assert.IsFalse(onHit.HasConditionalTransition(Action("combo.0"), Heavy));
        }

        [Test]
        public void DuplicateActionIdIsRejected()
        {
            var actions = new List<PlayerCombatAction>
            {
                Attack("combo.0"),
                Attack("combo.0")
            };

            Assert.Throws<ArgumentException>(() => Graph(
                actions,
                new[] { new PlayerCombatEntryRule(Light, Action("combo.0")) },
                Array.Empty<PlayerCombatTransitionRule>()));
        }

        [Test]
        public void EntryPointingAtAMissingActionIsRejected()
        {
            Assert.Throws<ArgumentException>(() => Graph(
                new[] { Attack("combo.0") },
                new[] { new PlayerCombatEntryRule(Light, Action("missing")) },
                Array.Empty<PlayerCombatTransitionRule>()));
        }

        [Test]
        public void TransitionPointingAtAMissingActionIsRejected()
        {
            Assert.Throws<ArgumentException>(() => Graph(
                new[] { Attack("combo.0") },
                new[] { new PlayerCombatEntryRule(Light, Action("combo.0")) },
                new[]
                {
                    new PlayerCombatTransitionRule(
                        Action("combo.0"), Light, Action("missing"),
                        PlayerCombatTransitionWindow.Chain)
                }));
        }
    }
}
