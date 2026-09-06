using System;
using System.Collections.Generic;
using ModularStateMachine;
using NUnit.Framework;

namespace ProjectS.Core.Tests
{
    public sealed class StateMachineTests
    {
        private enum Phase
        {
            Idle,
            Running,
            Done
        }

        private class RecordingState : IState
        {
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }

            public void Enter() => EnterCount++;
            public void Exit() => ExitCount++;
        }

        private sealed class IdleState : RecordingState { }

        private sealed class DoneState : RecordingState { }

        private sealed class RunningState : RecordingState, IUpdateState, IFixedUpdateState
        {
            public float Ticked { get; private set; }
            public float FixedTicked { get; private set; }

            public void Tick(float deltaTime) => Ticked += deltaTime;
            public void FixedTick(float fixedDeltaTime) => FixedTicked += fixedDeltaTime;
        }

        private sealed class Gate : IStateTransitionGuard
        {
            public bool CanTransition { get; set; } = true;
        }

        private static StateMachine<Phase> Create(
            out IdleState idle,
            out RunningState running,
            out DoneState done,
            Gate gate = null)
        {
            idle = new IdleState();
            running = new RunningState();
            done = new DoneState();
            var states = new List<StateRegistration<Phase>>
            {
                StateRegistration<Phase>.Create(Phase.Idle, idle),
                StateRegistration<Phase>.Create(Phase.Running, running),
                StateRegistration<Phase>.Create(Phase.Done, done)
            };
            var transitions = new List<StateTransition<Phase>>
            {
                new StateTransition<Phase>(Phase.Idle, Phase.Running),
                new StateTransition<Phase>(Phase.Running, Phase.Done, gate),
                new StateTransition<Phase>(Phase.Done, Phase.Idle)
            };
            return new StateMachine<Phase>(states, transitions);
        }

        [Test]
        public void Start_EntersRequestedState()
        {
            StateMachine<Phase> machine = Create(out IdleState idle, out _, out _);

            machine.Start<IdleState>();

            Assert.IsTrue(machine.IsRunning);
            Assert.AreEqual(Phase.Idle, machine.CurrentStateId);
            Assert.AreEqual(1, idle.EnterCount);
        }

        [Test]
        public void ChangeState_ExitsPreviousAndEntersNext()
        {
            StateMachine<Phase> machine = Create(out IdleState idle, out RunningState running, out _);
            machine.Start<IdleState>();

            machine.ChangeState<RunningState>();

            Assert.AreEqual(Phase.Running, machine.CurrentStateId);
            Assert.AreEqual(1, idle.ExitCount);
            Assert.AreEqual(1, running.EnterCount);
        }

        [Test]
        public void StateChanged_ReportsPreviousAndCurrent()
        {
            StateMachine<Phase> machine = Create(out _, out _, out _);
            machine.Start<IdleState>();
            Phase? from = null;
            Phase? to = null;
            machine.StateChanged += (previous, current) => { from = previous; to = current; };

            machine.ChangeState<RunningState>();

            Assert.AreEqual(Phase.Idle, from);
            Assert.AreEqual(Phase.Running, to);
        }

        [Test]
        public void UndeclaredTransition_IsRefused()
        {
            StateMachine<Phase> machine = Create(out _, out _, out DoneState done);
            machine.Start<IdleState>();

            Assert.IsFalse(machine.CanChangeState<DoneState>());
            Assert.Throws<StateTransitionNotAllowedException>(() => machine.ChangeState<DoneState>());
            Assert.AreEqual(Phase.Idle, machine.CurrentStateId);
            Assert.AreEqual(0, done.EnterCount);
        }

        [Test]
        public void ClosedGuard_BlocksDeclaredTransition()
        {
            var gate = new Gate { CanTransition = false };
            StateMachine<Phase> machine = Create(out _, out _, out _, gate);
            machine.Start<IdleState>();
            machine.ChangeState<RunningState>();

            Assert.IsFalse(machine.CanChangeState<DoneState>());

            gate.CanTransition = true;
            Assert.IsTrue(machine.CanChangeState<DoneState>());
        }

        [Test]
        public void Tick_ReachesOnlyTheCurrentState()
        {
            StateMachine<Phase> machine = Create(out _, out RunningState running, out _);
            machine.Start<IdleState>();

            machine.Tick(0.5f);
            Assert.AreEqual(0.0f, running.Ticked, 1e-5f);

            machine.ChangeState<RunningState>();
            machine.Tick(0.25f);
            machine.FixedTick(0.02f);

            Assert.AreEqual(0.25f, running.Ticked, 1e-5f);
            Assert.AreEqual(0.02f, running.FixedTicked, 1e-5f);
        }

        [Test]
        public void Stop_ExitsCurrentStateAndClearsId()
        {
            StateMachine<Phase> machine = Create(out IdleState idle, out _, out _);
            machine.Start<IdleState>();

            machine.Stop();

            Assert.IsFalse(machine.IsRunning);
            Assert.IsNull(machine.CurrentStateId);
            Assert.AreEqual(1, idle.ExitCount);
        }

        [Test]
        public void ChangeStateBeforeStart_IsRefused()
        {
            StateMachine<Phase> machine = Create(out _, out _, out _);

            Assert.Throws<StateMachineException>(() => machine.ChangeState<RunningState>());
        }

        [Test]
        public void ReenteringSameState_RunsExitAndEnterAgain()
        {
            StateMachine<Phase> machine = Create(out IdleState idle, out RunningState running, out DoneState done);
            machine.Start<IdleState>();
            machine.ChangeState<RunningState>();
            machine.ChangeState<DoneState>();
            machine.ChangeState<IdleState>();

            Assert.AreEqual(Phase.Idle, machine.CurrentStateId);
            Assert.AreEqual(2, idle.EnterCount);
            Assert.AreEqual(1, idle.ExitCount);
            Assert.AreEqual(1, running.ExitCount);
            Assert.AreEqual(1, done.ExitCount);
        }

        [Test]
        public void NullRegistrations_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => new StateMachine<Phase>(null, Array.Empty<StateTransition<Phase>>()));
            Assert.Throws<ArgumentNullException>(
                () => new StateMachine<Phase>(Array.Empty<StateRegistration<Phase>>(), null));
        }
    }
}
