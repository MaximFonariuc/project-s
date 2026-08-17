using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ModularAnimation.Unity
{
    public sealed class MecanimCharacterAnimationDriver :
        ICharacterAnimationDriver,
        IAnimationClipActionDriver,
        IDisposable
    {
        private const int BaseInput = 0;
        private const int FullBodyActionInput = 1;
        private const int MaskedActionInput = 2;
        private const int ActionLaneCount = 2;
        private const int ClipSlotCount = 2;
        private const int BaseInputCount = 3;
        private const float BlendCompletionExponent = 4.0f;

        private readonly Animator _animator;
        private readonly MecanimAnimationGraphConfiguration _configuration;
        private readonly RuntimeAnimatorController _sourceController;
        private readonly bool _sourceApplyRootMotion;

        private readonly AnimationClipPlayable[] _locomotionClips =
            new AnimationClipPlayable[MecanimAnimationGraphConfiguration.LocomotionInputCount];
        private readonly float[] _locomotionWeights =
            new float[MecanimAnimationGraphConfiguration.LocomotionInputCount];
        private readonly float[] _locomotionTargets =
            new float[MecanimAnimationGraphConfiguration.LocomotionInputCount];
        private readonly AnimationClipPlayable[] _stateClips =
            new AnimationClipPlayable[ClipSlotCount];
        private readonly bool[] _stateClipValid = new bool[ClipSlotCount];
        private readonly float[] _stateSpeedMultipliers = { 1.0f, 1.0f };
        private readonly AnimationMixerPlayable[] _actionMixers =
            new AnimationMixerPlayable[ActionLaneCount];
        private readonly AnimationClipPlayable[,] _actionClips =
            new AnimationClipPlayable[ActionLaneCount, ClipSlotCount];
        private readonly bool[,] _actionClipValid = new bool[ActionLaneCount, ClipSlotCount];
        private readonly int[] _activeClipSlots = { -1, -1 };
        private readonly float[] _laneWeights = new float[ActionLaneCount];
        private readonly float[] _laneStartWeights = new float[ActionLaneCount];
        private readonly float[] _laneTargetWeights = new float[ActionLaneCount];

        private PlayableGraph _graph;
        private AnimationMixerPlayable _locomotionMixer;
        private AnimationMixerPlayable _baseMixer;
        private AnimationLayerMixerPlayable _layerMixer;

        private int _baseCurrentInput;
        private AnimationStateId _baseCurrentState;
        private int _baseBlendFrom = -1;
        private int _baseBlendTo = -1;
        private float _baseBlendDuration;
        private float _baseBlendElapsed;

        private int _activeLane = -1;
        private int _targetLane = -1;
        private int _clipBlendLane = -1;
        private int _clipBlendFromSlot = -1;
        private int _clipBlendToSlot = -1;
        private float _clipBlendDuration;
        private float _clipBlendElapsed;
        private float _laneBlendDuration;
        private float _laneBlendElapsed;
        private int _nextActionSequence;

        private float _playbackSpeed = 1.0f;
        private bool _disposed;

        public Animator Animator => _animator;
        public bool IsActionPlaying =>
            _activeLane >= 0 || _laneWeights[0] > 0.0001f || _laneWeights[1] > 0.0001f;

        public MecanimCharacterAnimationDriver(
            Animator animator,
            MecanimAnimationGraphConfiguration configuration)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _sourceController = _animator.runtimeAnimatorController;
            _sourceApplyRootMotion = _animator.applyRootMotion;
            _animator.runtimeAnimatorController = null;
            _animator.applyRootMotion = false;

            CreateGraph();
            RefreshBindings();
        }

        public void ApplyLocomotion(in CharacterLocomotionFrame frame, float deltaTime)
        {
            ThrowIfDisposed();
            ApplyLocomotionWeights(in frame, Mathf.Max(0.0f, deltaTime));
        }

        public AnimationPlayback CrossFade(
            AnimationStateId state,
            AnimationLayerId layer,
            float transitionSeconds,
            float expectedDuration = 0.0f)
        {
            ThrowIfDisposed();
            if (layer != AnimationLayerId.Base ||
                !_configuration.TryGetState(state, out MecanimAnimationGraphState graphState))
            {
                return AnimationPlayback.Invalid;
            }

            if (_baseBlendTo < 0 && _baseCurrentState == state)
            {
                float activeDuration = expectedDuration > 0.0f
                    ? expectedDuration
                    : graphState.Duration;
                return new AnimationPlayback(state, layer, activeDuration);
            }

            FinishBaseBlend();
            int targetInput = graphState.UsesLocomotion
                ? 0
                : CreateStateClip(in graphState, expectedDuration);
            StartBaseBlend(targetInput, state, transitionSeconds);

            float duration = expectedDuration > 0.0f
                ? expectedDuration
                : graphState.Duration;
            return new AnimationPlayback(state, layer, duration);
        }

        public AnimationActionPlayback PlayAction(in AnimationClipActionRequest request)
        {
            ThrowIfDisposed();
            if (!request.IsValid)
            {
                return AnimationActionPlayback.Invalid;
            }

            if (request.Clip.legacy ||
                request.Clip.humanMotion != _animator.avatar.isHuman)
            {
                string rig = _animator.avatar.isHuman ? "Humanoid" : "Generic";
                Debug.LogError(
                    $"Action clip '{request.Clip.name}' is incompatible with the {rig} Avatar on Animator '{_animator.name}'.",
                    _animator);
                return AnimationActionPlayback.Invalid;
            }

            FinishClipBlend();

            int lane = request.AvatarMask == null ? 0 : 1;
            if (lane == 1)
            {
                _layerMixer.SetLayerMaskFromAvatarMask(MaskedActionInput, request.AvatarMask);
            }

            int currentSlot = _activeClipSlots[lane];
            if (_activeLane == lane && currentSlot >= 0 && _actionClipValid[lane, currentSlot])
            {
                int nextSlot = 1 - currentSlot;
                DestroyActionClip(lane, nextSlot);
                CreateActionClip(lane, nextSlot, in request);
                _actionMixers[lane].SetInputWeight(currentSlot, 1.0f);
                _actionMixers[lane].SetInputWeight(nextSlot, 0.0f);
                StartClipBlend(lane, currentSlot, nextSlot, request.BlendSeconds);
                if (_targetLane != lane)
                {
                    StartLaneBlend(lane, request.BlendSeconds);
                }
            }
            else
            {
                ClearLane(lane);
                CreateActionClip(lane, 0, in request);
                _activeClipSlots[lane] = 0;
                _actionMixers[lane].SetInputWeight(0, 1.0f);
                StartLaneBlend(lane, request.BlendSeconds);
            }

            _activeLane = lane;
            _nextActionSequence++;
            if (_nextActionSequence == 0)
            {
                _nextActionSequence = 1;
            }

            return new AnimationActionPlayback(
                _nextActionSequence,
                request.Clip.length / request.PlaybackSpeed);
        }

        public void StopAction(float blendSeconds)
        {
            if (_disposed || !IsActionPlaying)
            {
                return;
            }

            FinishClipBlend();
            StartLaneBlend(-1, Mathf.Max(0.0f, blendSeconds));
        }

        public void Tick(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            float clampedDeltaTime = Mathf.Max(0.0f, deltaTime);
            UpdateBaseBlend(clampedDeltaTime);
            UpdateClipBlend(clampedDeltaTime);
            UpdateLaneBlend(clampedDeltaTime);
        }

        public void SetPlaybackSpeed(float speed)
        {
            ThrowIfDisposed();
            _playbackSpeed = Mathf.Max(0.0f, speed);
            for (int i = 0; i < _locomotionClips.Length; i++)
            {
                _locomotionClips[i].SetSpeed(_playbackSpeed);
            }

            for (int i = 0; i < _stateClips.Length; i++)
            {
                if (_stateClipValid[i])
                {
                    _stateClips[i].SetSpeed(
                        _playbackSpeed * _stateSpeedMultipliers[i]);
                }
            }
        }

        public void RefreshBindings()
        {
            ThrowIfDisposed();
            if (_animator.avatar == null || !_animator.avatar.isValid)
            {
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' lost its valid Avatar binding.");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            if (_animator != null)
            {
                _animator.runtimeAnimatorController = _sourceController;
                _animator.applyRootMotion = _sourceApplyRootMotion;
            }

            _disposed = true;
        }

        private void CreateGraph()
        {
            _graph = PlayableGraph.Create($"{_animator.name}.CharacterAnimation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _locomotionMixer = AnimationMixerPlayable.Create(
                _graph,
                MecanimAnimationGraphConfiguration.LocomotionInputCount);
            _baseMixer = AnimationMixerPlayable.Create(_graph, BaseInputCount);
            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, 3);
            _actionMixers[0] = AnimationMixerPlayable.Create(_graph, ClipSlotCount);
            _actionMixers[1] = AnimationMixerPlayable.Create(_graph, ClipSlotCount);

            for (int i = 0; i < _locomotionClips.Length; i++)
            {
                AnimationClip clip = _configuration.GetLocomotionClip(i);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetSpeed(_playbackSpeed);
                _graph.Connect(playable, 0, _locomotionMixer, i);
                _locomotionClips[i] = playable;
                _locomotionWeights[i] = i == 0 ? 1.0f : 0.0f;
                _locomotionMixer.SetInputWeight(i, _locomotionWeights[i]);
            }

            _graph.Connect(_locomotionMixer, 0, _baseMixer, 0);
            _baseMixer.SetInputWeight(0, 1.0f);
            _graph.Connect(_baseMixer, 0, _layerMixer, BaseInput);
            _graph.Connect(_actionMixers[0], 0, _layerMixer, FullBodyActionInput);
            _graph.Connect(_actionMixers[1], 0, _layerMixer, MaskedActionInput);
            _layerMixer.SetInputWeight(BaseInput, 1.0f);
            _layerMixer.SetInputWeight(FullBodyActionInput, 0.0f);
            _layerMixer.SetInputWeight(MaskedActionInput, 0.0f);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(_graph, "CharacterAnimation", _animator);
            if (string.IsNullOrEmpty(_configuration.PlanarMotionRootPath))
            {
                output.SetSourcePlayable(_layerMixer);
            }
            else
            {
                Transform motionRoot = _animator.transform.Find(
                    _configuration.PlanarMotionRootPath);
                if (motionRoot == null)
                {
                    throw new InvalidOperationException(
                        $"Animator '{_animator.name}' has no planar motion root " +
                        $"'{_configuration.PlanarMotionRootPath}'.");
                }

                var filterJob = new MecanimPlanarMotionFilterJob(
                    _animator.BindStreamTransform(motionRoot),
                    motionRoot.localPosition);
                AnimationScriptPlayable motionFilter =
                    AnimationScriptPlayable.Create(_graph, filterJob, 1);
                _graph.Connect(_layerMixer, 0, motionFilter, 0);
                motionFilter.SetInputWeight(0, 1.0f);
                output.SetSourcePlayable(motionFilter);
            }

            _graph.Play();
        }

        private void ApplyLocomotionWeights(
            in CharacterLocomotionFrame frame,
            float deltaTime)
        {
            Array.Clear(_locomotionTargets, 0, _locomotionTargets.Length);

            float movementWeight = Mathf.Clamp01(frame.NormalizedSpeed);
            _locomotionTargets[0] = 1.0f - movementWeight;
            if (movementWeight > 0.0001f)
            {
                float forwardWeight = Mathf.Max(0.0f, frame.ForwardSpeed);
                float backwardWeight = Mathf.Max(0.0f, -frame.ForwardSpeed);
                float leftWeight = Mathf.Max(0.0f, -frame.LateralSpeed);
                float rightWeight = Mathf.Max(0.0f, frame.LateralSpeed);
                float directionTotal =
                    forwardWeight + backwardWeight + leftWeight + rightWeight;
                if (directionTotal > 0.0001f)
                {
                    int gaitIndex = frame.Gait switch
                    {
                        CharacterGait.Walk => 0,
                        CharacterGait.Sprint => 2,
                        _ => 1
                    };
                    int offset =
                        1 + gaitIndex * MecanimAnimationGraphConfiguration.DirectionCount;
                    _locomotionTargets[offset] =
                        movementWeight * forwardWeight / directionTotal;
                    _locomotionTargets[offset + 1] =
                        movementWeight * backwardWeight / directionTotal;
                    _locomotionTargets[offset + 2] =
                        movementWeight * leftWeight / directionTotal;
                    _locomotionTargets[offset + 3] =
                        movementWeight * rightWeight / directionTotal;
                }
                else
                {
                    _locomotionTargets[0] = 1.0f;
                }
            }

            float blend = CalculateBlendFactor(
                _configuration.LocomotionBlendSeconds,
                deltaTime);
            float weightTotal = 0.0f;
            for (int i = 0; i < _locomotionWeights.Length; i++)
            {
                _locomotionWeights[i] = Mathf.Lerp(
                    _locomotionWeights[i],
                    _locomotionTargets[i],
                    blend);
                weightTotal += _locomotionWeights[i];
            }

            float normalization = weightTotal > 0.0001f ? 1.0f / weightTotal : 1.0f;
            for (int i = 0; i < _locomotionWeights.Length; i++)
            {
                _locomotionWeights[i] *= normalization;
                _locomotionMixer.SetInputWeight(i, _locomotionWeights[i]);
            }
        }

        private int CreateStateClip(
            in MecanimAnimationGraphState state,
            float expectedDuration)
        {
            int currentSlot = _baseCurrentInput > 0 ? _baseCurrentInput - 1 : -1;
            int slot = currentSlot == 0 ? 1 : 0;
            DestroyStateClip(slot);

            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, state.Clip);
            playable.SetApplyFootIK(state.ApplyFootIk);
            playable.SetTime(0.0d);
            float speedMultiplier = expectedDuration > 0.0001f
                ? state.Clip.length / expectedDuration
                : 1.0f;
            playable.SetSpeed(_playbackSpeed * speedMultiplier);
            _graph.Connect(playable, 0, _baseMixer, slot + 1);
            _baseMixer.SetInputWeight(slot + 1, 0.0f);
            _stateClips[slot] = playable;
            _stateClipValid[slot] = true;
            _stateSpeedMultipliers[slot] = speedMultiplier;
            return slot + 1;
        }

        private void StartBaseBlend(
            int targetInput,
            AnimationStateId targetState,
            float duration)
        {
            _baseBlendFrom = _baseCurrentInput;
            _baseBlendTo = targetInput;
            _baseCurrentState = targetState;
            _baseBlendDuration = Mathf.Max(0.0f, duration);
            _baseBlendElapsed = 0.0f;
            _baseMixer.SetInputWeight(_baseBlendFrom, 1.0f);
            _baseMixer.SetInputWeight(_baseBlendTo, _baseBlendFrom == _baseBlendTo ? 1.0f : 0.0f);
            if (_baseBlendDuration <= 0.0f || _baseBlendFrom == _baseBlendTo)
            {
                FinishBaseBlend();
            }
        }

        private void UpdateBaseBlend(float deltaTime)
        {
            if (_baseBlendTo < 0)
            {
                return;
            }

            _baseBlendElapsed += deltaTime;
            float progress = _baseBlendDuration > 0.0f
                ? Mathf.Clamp01(_baseBlendElapsed / _baseBlendDuration)
                : 1.0f;
            _baseMixer.SetInputWeight(_baseBlendFrom, 1.0f - progress);
            _baseMixer.SetInputWeight(_baseBlendTo, progress);
            if (progress >= 1.0f)
            {
                FinishBaseBlend();
            }
        }

        private void FinishBaseBlend()
        {
            if (_baseBlendTo < 0)
            {
                return;
            }

            int previous = _baseBlendFrom;
            _baseCurrentInput = _baseBlendTo;
            for (int input = 0; input < BaseInputCount; input++)
            {
                _baseMixer.SetInputWeight(input, input == _baseCurrentInput ? 1.0f : 0.0f);
            }

            if (previous > 0 && previous != _baseCurrentInput)
            {
                DestroyStateClip(previous - 1);
            }

            _baseBlendFrom = -1;
            _baseBlendTo = -1;
            _baseBlendDuration = 0.0f;
            _baseBlendElapsed = 0.0f;
        }

        private void DestroyStateClip(int slot)
        {
            if (!_stateClipValid[slot])
            {
                return;
            }

            _graph.Disconnect(_baseMixer, slot + 1);
            AnimationClipPlayable playable = _stateClips[slot];
            if (playable.IsValid())
            {
                _graph.DestroyPlayable(playable);
            }

            _stateClipValid[slot] = false;
            _stateSpeedMultipliers[slot] = 1.0f;
            _baseMixer.SetInputWeight(slot + 1, 0.0f);
        }

        private void CreateActionClip(
            int lane,
            int slot,
            in AnimationClipActionRequest request)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, request.Clip);
            playable.SetApplyFootIK(request.ApplyFootIk);
            playable.SetTime(0.0d);
            playable.SetSpeed(request.PlaybackSpeed);
            _graph.Connect(playable, 0, _actionMixers[lane], slot);
            _actionClips[lane, slot] = playable;
            _actionClipValid[lane, slot] = true;
        }

        private void StartClipBlend(int lane, int fromSlot, int toSlot, float duration)
        {
            _clipBlendLane = lane;
            _clipBlendFromSlot = fromSlot;
            _clipBlendToSlot = toSlot;
            _clipBlendDuration = Mathf.Max(0.0f, duration);
            _clipBlendElapsed = 0.0f;
            if (_clipBlendDuration <= 0.0f)
            {
                FinishClipBlend();
            }
        }

        private void UpdateClipBlend(float deltaTime)
        {
            if (_clipBlendLane < 0)
            {
                return;
            }

            _clipBlendElapsed += deltaTime;
            float progress = _clipBlendDuration > 0.0f
                ? Mathf.Clamp01(_clipBlendElapsed / _clipBlendDuration)
                : 1.0f;
            AnimationMixerPlayable mixer = _actionMixers[_clipBlendLane];
            mixer.SetInputWeight(_clipBlendFromSlot, 1.0f - progress);
            mixer.SetInputWeight(_clipBlendToSlot, progress);
            if (progress >= 1.0f)
            {
                FinishClipBlend();
            }
        }

        private void FinishClipBlend()
        {
            if (_clipBlendLane < 0)
            {
                return;
            }

            int lane = _clipBlendLane;
            int fromSlot = _clipBlendFromSlot;
            int toSlot = _clipBlendToSlot;
            _actionMixers[lane].SetInputWeight(toSlot, 1.0f);
            DestroyActionClip(lane, fromSlot);
            _activeClipSlots[lane] = toSlot;
            _clipBlendLane = -1;
            _clipBlendFromSlot = -1;
            _clipBlendToSlot = -1;
            _clipBlendDuration = 0.0f;
            _clipBlendElapsed = 0.0f;
        }

        private void StartLaneBlend(int targetLane, float duration)
        {
            _targetLane = targetLane;
            for (int lane = 0; lane < ActionLaneCount; lane++)
            {
                _laneStartWeights[lane] = _laneWeights[lane];
                _laneTargetWeights[lane] = lane == targetLane ? 1.0f : 0.0f;
            }

            _laneBlendDuration = Mathf.Max(0.0f, duration);
            _laneBlendElapsed = 0.0f;
            if (_laneBlendDuration <= 0.0f)
            {
                ApplyLaneBlend(1.0f);
                FinishLaneBlend();
            }
        }

        private void UpdateLaneBlend(float deltaTime)
        {
            if (_laneBlendDuration <= 0.0f)
            {
                return;
            }

            _laneBlendElapsed += deltaTime;
            float progress = Mathf.Clamp01(_laneBlendElapsed / _laneBlendDuration);
            ApplyLaneBlend(progress);
            if (progress >= 1.0f)
            {
                FinishLaneBlend();
            }
        }

        private void ApplyLaneBlend(float progress)
        {
            for (int lane = 0; lane < ActionLaneCount; lane++)
            {
                _laneWeights[lane] = Mathf.Lerp(
                    _laneStartWeights[lane],
                    _laneTargetWeights[lane],
                    progress);
                _layerMixer.SetInputWeight(lane + FullBodyActionInput, _laneWeights[lane]);
            }
        }

        private void FinishLaneBlend()
        {
            for (int lane = 0; lane < ActionLaneCount; lane++)
            {
                _laneWeights[lane] = _laneTargetWeights[lane];
                _layerMixer.SetInputWeight(lane + FullBodyActionInput, _laneWeights[lane]);
                if (lane != _targetLane)
                {
                    ClearLane(lane);
                }
            }

            _activeLane = _targetLane;
            _laneBlendDuration = 0.0f;
            _laneBlendElapsed = 0.0f;
        }

        private void ClearLane(int lane)
        {
            if (_clipBlendLane == lane)
            {
                _clipBlendLane = -1;
                _clipBlendFromSlot = -1;
                _clipBlendToSlot = -1;
            }

            for (int slot = 0; slot < ClipSlotCount; slot++)
            {
                DestroyActionClip(lane, slot);
            }

            _activeClipSlots[lane] = -1;
        }

        private void DestroyActionClip(int lane, int slot)
        {
            if (!_actionClipValid[lane, slot])
            {
                return;
            }

            _graph.Disconnect(_actionMixers[lane], slot);
            AnimationClipPlayable playable = _actionClips[lane, slot];
            if (playable.IsValid())
            {
                _graph.DestroyPlayable(playable);
            }

            _actionClipValid[lane, slot] = false;
            _actionMixers[lane].SetInputWeight(slot, 0.0f);
        }

        private static float CalculateBlendFactor(float blendSeconds, float deltaTime)
        {
            if (blendSeconds <= 0.0f)
            {
                return 1.0f;
            }

            return 1.0f - Mathf.Exp(
                -BlendCompletionExponent * deltaTime / blendSeconds);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MecanimCharacterAnimationDriver));
            }
        }
    }
}
