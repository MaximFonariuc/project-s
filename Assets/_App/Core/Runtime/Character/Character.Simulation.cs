using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    public partial class Character
    {
        private void BeforeSimulationUpdate(float deltaTime)
        {
            if (IsWalking() && !IsGrounded())
                SetMovementMode(EMovementMode.Falling);

            if (IsFalling() && IsGrounded())
                SetMovementMode(EMovementMode.Walking);

            UpdatePhysicsVolumes();
            CheckCrouchInput();
            CheckJumpInput();
            UpdateJumpTimers(deltaTime);
            OnBeforeSimulationUpdate(deltaTime);
        }

        private void SimulationUpdate(float deltaTime)
        {
            CalcDesiredVelocity(deltaTime);

            switch (_movementMode)
            {
                case EMovementMode.None:
                    break;

                case EMovementMode.Walking:
                    WalkingMovementMode(deltaTime);
                    break;

                case EMovementMode.Falling:
                    FallingMovementMode(deltaTime);
                    break;

                case EMovementMode.Flying:
                    FlyingMovementMode(deltaTime);
                    break;

                case EMovementMode.Swimming:
                    SwimmingMovementMode(deltaTime);
                    break;

                case EMovementMode.Custom:
                    CustomMovementMode(deltaTime);
                    break;
            }

            UpdateRotation(deltaTime);

            ConsumeRotationInput();
        }

        private void AfterSimulationUpdate(float deltaTime)
        {
            NotifyJumpApex();

            OnAfterSimulationUpdate(deltaTime);
        }

        private void CharacterMovementUpdate(float deltaTime)
        {
            CharacterMovement.Move(deltaTime);

            OnCharacterMovementUpdated(deltaTime);
        }

        private void Simulate(float deltaTime)
        {
            if (IsPaused)
                return;

            BeforeSimulationUpdate(deltaTime);
            SimulationUpdate(deltaTime);
            AfterSimulationUpdate(deltaTime);
            CharacterMovementUpdate(deltaTime);
        }

        private void OnLateFixedUpdate()
        {
            Simulate(Time.fixedDeltaTime);
        }

        public void Pause(bool pause, bool clearState = true, bool useAutoUnpause = false, float pauseDuration = 0f)
        {
            IsPaused = pause;
            CharacterMovement.Collider.enabled = !IsPaused;

            if (clearState)
            {
                _movementDirection = Vector3.zero;
                _rotationInput = Vector3.zero;

                CharacterMovement.Velocity = Vector3.zero;
                CharacterMovement.ClearAccumulatedForces();
            }

            if (useAutoUnpause)
                ScheduleAutoUnpause(pause, clearState, pauseDuration, _enableGeneration).Forget();
        }

        private async UniTask ScheduleAutoUnpause(
            bool pausedState,
            bool clearedState,
            float pauseDuration,
            int enableGeneration)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(Mathf.Max(0.0f, pauseDuration)),
                cancellationToken: this.GetCancellationTokenOnDestroy());

            if (!isActiveAndEnabled || enableGeneration != _enableGeneration || IsPaused != pausedState)
                return;

            Pause(!pausedState, !clearedState);
        }

    }
}
