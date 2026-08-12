using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    public class AnimationController : ITestable
    {
        public const int BASE_LAYER_INDEX = 0;
        public const int SWITCH_LAYER_INDEX = 1;

        private readonly Character _character;
        
        private bool _doubleJumpAnim;

        public Animator Animator { get; private set; }

        private int _currentMovementType = 2;
        
        public bool IsWalking
        {
	        get => _currentMovementType == (int)EAnimationMoveMode.Walking;
            set => _currentMovementType = value ? 1 : 2;
        }

        public bool IsSprinting
        {
	        get => _currentMovementType == (int)EAnimationMoveMode.Sprinting;
	        set => _currentMovementType = value ? 3 : 2;
        }

        public AnimationController(Character character, Animator animator)
        {
            _character = character;
            Animator = animator;
        }
        
        public void Update()
        {
            if (Animator == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            Vector3 move = _character.transform.InverseTransformDirection(_character.GetMovementDirection());

            float moveAmount = Mathf.Clamp(_character.GetSpeed() / _character.GetMaxSpeed(), -1.0f, 1.0f) * Mathf.Sign(move.z);

            if (move == Vector3.zero)
            {
                moveAmount = 0.0f;
            }
            
            OnMove(moveAmount, deltaTime);
            OnJump(deltaTime);
        }

        private void OnMove(float moveAmount, float deltaTime)
        {
            SetFloat(AnimationHashes.MovementType, moveAmount != 0 ? _currentMovementType : 0, 0.33f, deltaTime);            
            SetFloat(AnimationHashes.MovementDirection, moveAmount, 0.1f, deltaTime);

            SetBool(AnimationHashes.OnGround, _character.IsGrounded());
        }

        private void OnJump(float deltaTime)
        {
            var characterVelocity = _character.GetVelocity().y;
            
            if (_character.IsFalling())
            {
                SetFloat(AnimationHashes.FallingDuration, characterVelocity, 0.1f, deltaTime);
            }
            else
            {
                var fallingDuration = Animator.GetFloat(AnimationHashes.FallingDuration);
                if (_character.IsGrounded() && fallingDuration <= -7)
                {
                    _character.Pause(true, true, true, fallingDuration <= -10 ? 1f : 0.5f);
                }
                SetFloat(AnimationHashes.FallingDuration, 0);
            }

            if (_character.JumpCurrentCount > 1 && !_doubleJumpAnim)
            {
                _doubleJumpAnim = true;
                SetTrigger(AnimationHashes.DoubleJumpTrigger);
            }
            
            if (_character.IsGrounded())
            {
                if (_doubleJumpAnim)
                {
                    _doubleJumpAnim = false;
                    ResetTrigger(AnimationHashes.DoubleJumpTrigger);
                }
            }
        }

        public void OnSwimDirectionChanged(SwimDirection swimDirection)
        {
            SetInteger(AnimationHashes.SwimDirection, (int)swimDirection);
            ResetSwimDirectionAfterDelay().Forget();
        }

        private async UniTaskVoid ResetSwimDirectionAfterDelay()
        {
            await UniTask.Delay(500);
            SetInteger(AnimationHashes.SwimDirection, 0);
        }

        public void OnPhysicsVolumeChanged(PhysicsVolume newPhysicsVolume)
        {
            if (newPhysicsVolume != null && newPhysicsVolume.WaterVolume)
            {
                SetTrigger(AnimationHashes.SwimTrigger);
            }
        }
        
        public void SetInteger(int intHash, int value) => Animator.SetInteger(intHash, value);
		public void ResetInteger(int intHash) => Animator.SetInteger(intHash, 0);

		public void SetFloat(int floatHash, float value) => Animator.SetFloat(floatHash, value);
		public void SetFloat(int floatHash, float value, float dampTime, float deltaTime) => Animator.SetFloat(floatHash, value, dampTime, deltaTime);
		public void ResetFloat(int floatHash) => Animator.SetFloat(floatHash, 0);
		public void SetBool(int boolHash, bool value) => Animator.SetBool(boolHash, value);
		public void ResetBool(int boolHash) => Animator.SetBool(boolHash, false);
		
		public void SetTrigger(int triggerHash) => Animator.SetTrigger(triggerHash);
		public void ResetTrigger(int triggerHash) => Animator.ResetTrigger(triggerHash);

		public void Play<T>(T key, int layer)
		{
			switch (key)
			{
				case string name:
					Animator.Play(name, layer);
					break;
				case int hash:
					Animator.Play(hash, layer);
					break;
			}
		}

		public void CrossFade<T>(T key, float normalizedTransitionDuration, int layer)
		{
			switch (key)
			{
				case string name:
					Animator.CrossFade(name, normalizedTransitionDuration, layer);
					break;
				case int hash:
					Animator.CrossFade(hash, normalizedTransitionDuration, layer);
					break;
			}
		}

		public float GetAnimationClipLength<T>(T clipKey)
		{
			if (Animator.runtimeAnimatorController == null) return -1f;

			foreach (var clip in Animator.runtimeAnimatorController.animationClips)
			{
				switch (clipKey)
				{
					case string n when clip.name == n:
						return clip.length;
					case int h when Animator.StringToHash(clip.name) == h:
						return clip.length;
				}
			}
			return -1f;
		}

		public bool IsInState<T>(T key, int layerIndex)
		{
			var stateInfo = Animator.GetCurrentAnimatorStateInfo(layerIndex);

			return key switch
			{
				string n => stateInfo.IsName(n),
				int h => stateInfo.shortNameHash == h,
				_ => false
			};
		}

		public void SetSpeed(float speed = 1f)
		{
			if (Animator != null)
			{
				SetFloat(AnimationHashes.SpeedMultiplier, speed);
			}
		}

		public AnimatorStateInfo GetCurrentStateInfo(int layer) => Animator.GetCurrentAnimatorStateInfo(layer);
		public float GetNormalizedTime(int layer) => Animator.GetCurrentAnimatorStateInfo(layer).normalizedTime % 1f;
    }

    public enum EAnimationMoveMode
    {
	    Walking = 1,
	    Running = 2,
	    Sprinting = 3
    }
}
