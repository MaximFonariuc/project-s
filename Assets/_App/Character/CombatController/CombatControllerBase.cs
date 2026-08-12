using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace _App
{
	public abstract class CombatControllerBase : ITestable
	{
		protected const float ATTACK_WINDOW_START = 0.5f;
		protected const float ATTACK_WINDOW_END = 0.75f;
		protected const float HIT_WINDOW_START = 0.2f;
		protected const float HIT_WINDOW_END = 0.55f;

		protected readonly AnimationController AnimationController;
		protected readonly AnimationsData SkillsData;

		protected AnimatorOverrideController OverrideController;
		protected List<AnimationClip> ComboClips;

		protected readonly Dictionary<ESkillType, float> SkillCooldowns = new();
		protected readonly Dictionary<ESkillType, float> LastSkillUseTime = new();

		protected CancellationTokenSource ComboCancellation;
		protected CancellationTokenSource SkillCancellation;
		protected CancellationTokenSource SwitchCancellation;

		protected bool InputBuffered;
		protected int ComboIndex;
		private bool _isHitWindowOpen;

		public event Action HitWindowOpened;
		public event Action HitWindowClosed;

		public bool IsSwitching { get; private set; }
		public bool IsComboAttack { get; private set; }
		public bool IsSkillAttack { get; private set; }
		
		protected abstract ECharacterType CharacterType { get; }
		public EWeaponType CurrentWeapon { get; private set; }

		protected CombatControllerBase(AnimationController animationController, AnimationsData skillsData)
		{
			AnimationController = animationController;
			SkillsData = skillsData;

			SkillCooldowns[ESkillType.Default] = 0f;
			SkillCooldowns[ESkillType.Special] = 1.25f;
		}


		public async UniTask ToggleWeaponAsync(EWeaponType targetWeapon, Action onSwitch = null)
		{
			if (IsSwitching || IsComboAttack || IsSkillAttack || targetWeapon == EWeaponType.None)
				return;

			const string UNSHEATH_POSTFIX = "_Unsheathe";
			const string SHEATH_POSTFIX = "_Sheathe";

			if (CurrentWeapon == targetWeapon)
			{
				var sheatheAnim = targetWeapon + SHEATH_POSTFIX;
				await SwitchCombatStateAsync(sheatheAnim, AnimationController.SWITCH_LAYER_INDEX, () =>
				{
					CurrentWeapon = EWeaponType.Unarmed;
					onSwitch?.Invoke();
				});
				return;
			}

			if (CurrentWeapon != EWeaponType.Unarmed)
			{
				var sheatheAnim = targetWeapon + SHEATH_POSTFIX;
				await SwitchCombatStateAsync(sheatheAnim, AnimationController.SWITCH_LAYER_INDEX);
			}

			var unsheatheAnim = targetWeapon + UNSHEATH_POSTFIX;
			if (string.IsNullOrEmpty(unsheatheAnim)) return;

			await SwitchCombatStateAsync(unsheatheAnim, AnimationController.SWITCH_LAYER_INDEX, () =>
			{
				CurrentWeapon = targetWeapon;
				onSwitch?.Invoke();
			});
		}

		protected async UniTask SwitchCombatStateAsync(string animationName, int layerIndex, Action onComplete = null)
		{
			if (IsSwitching || string.IsNullOrEmpty(animationName))
				return;

			SwitchCancellation?.Cancel();
			SwitchCancellation = new CancellationTokenSource();
			var token = SwitchCancellation.Token;

			IsSwitching = true;
			AnimationController?.CrossFade(animationName, 0.1f, layer: layerIndex);

			try
			{
				await UniTask.WaitUntil(() => AnimationController.IsInState(animationName, layerIndex), cancellationToken: token);
				await UniTask.WaitUntil(() => AnimationController.GetNormalizedTime(layerIndex) >= 0.5f, cancellationToken: token).ContinueWith(onComplete);
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				await SwitchAnimations(CharacterType, CurrentWeapon);
				IsSwitching = false;
			}
		}

		public async UniTask SwitchAnimations(ECharacterType characterType, EWeaponType weaponType = EWeaponType.Unarmed)
		{
			CurrentWeapon = weaponType;

			OverrideController = new AnimatorOverrideController(AnimationController.Animator.runtimeAnimatorController);
			AnimationController.Animator.runtimeAnimatorController = OverrideController;

			await UniTask.Delay(100);

			var defaultAnimation = SkillsData?.GetDefaultsAnimationClips(characterType, CurrentWeapon);

			if (!defaultAnimation.IsNullOrEmpty())
			{
				foreach (var clip in defaultAnimation)
					OverrideController[clip.name] = clip;
			}

			SwitchDefaultAttacks(characterType);
		}

		protected void SwitchDefaultAttacks(ECharacterType characterType)
		{
			var animationClips = SkillsData?.GetSkill<DefaultSkillCombo>(characterType, CurrentWeapon)?.ComboClips;
			if (animationClips != null)
			{
				ComboClips = new List<AnimationClip>(animationClips);
				foreach (var clip in ComboClips)
					OverrideController[clip.name] = clip;
			}
		}

		protected async UniTaskVoid PlayComboAttackAsync()
		{
			if (ComboClips.IsNullOrEmpty())
				return;

			ComboCancellation?.Cancel();
			ComboCancellation = new CancellationTokenSource();
			var token = ComboCancellation.Token;

			IsComboAttack = true;
			InputBuffered = false;

			string stateName = ComboClips[ComboIndex]?.name;
			AnimationController.CrossFade(stateName, 0.1f, AnimationController.BASE_LAYER_INDEX);

			try
			{
				await UniTask.WaitUntil(() => AnimationController.IsInState(stateName, AnimationController.BASE_LAYER_INDEX), cancellationToken: token);

				while (true)
				{
					float normalizedTime = AnimationController.GetNormalizedTime(AnimationController.BASE_LAYER_INDEX);
					SetHitWindow(normalizedTime is >= HIT_WINDOW_START and < HIT_WINDOW_END);

					if (normalizedTime is >= ATTACK_WINDOW_START and <= ATTACK_WINDOW_END)
					{
						if (InputBuffered && ComboIndex < ComboClips.Count - 1)
						{
							ComboIndex++;
							InputBuffered = false;
							SetHitWindow(false);

							string nextState = ComboClips[ComboIndex].name;
							AnimationController.CrossFade(nextState, 0.1f, AnimationController.BASE_LAYER_INDEX);

							await UniTask.WaitUntil(() => AnimationController.IsInState(nextState, AnimationController.BASE_LAYER_INDEX), cancellationToken: token);
							continue;
						}
					}

					if (normalizedTime >= ATTACK_WINDOW_END)
						break;

					await UniTask.Yield(token);
				}

				await UniTask.WaitForSeconds(0.1f, cancellationToken: token);
			}
			catch (OperationCanceledException)
			{
				InterruptCombo();
			}
			finally
			{
				SetHitWindow(false);
				ResetCombo();
			}
		}

		protected async UniTaskVoid TrySkillAttack(ESkillType skillType)
		{
			if (!SkillCooldowns.TryGetValue(skillType, out var cooldown) || IsSkillAttack)
				return;

			if (LastSkillUseTime.TryGetValue(skillType, out var lastUsedTime))
			{
				if (Time.time - lastUsedTime < cooldown)
					return;
			}

			SkillCancellation?.Cancel();
			SkillCancellation = new CancellationTokenSource();
			var token = SkillCancellation.Token;

			var skill = SkillsData?.GetSkill<Skill>(CharacterType, CurrentWeapon, skillType);
			if (skill == null)
				return;

			IsSkillAttack = true;

			try
			{
				await UniTask.WaitUntil(CanTransitionToNextAnim, cancellationToken: token);

				InterruptCombo();
				OverrideController[skill.AnimationClip.name] = skill.AnimationClip;
				AnimationController.CrossFade(AnimationHashes.Skill, 0.1f, AnimationController.BASE_LAYER_INDEX);

				await UniTask.WaitUntil(() => AnimationController.IsInState(skill.AnimationClip.name, AnimationController.BASE_LAYER_INDEX), cancellationToken: token);
				await UniTask.WaitUntil(() => AnimationController.GetNormalizedTime(AnimationController.BASE_LAYER_INDEX) >= HIT_WINDOW_START, cancellationToken: token);
				SetHitWindow(true);
				await UniTask.WaitUntil(() => AnimationController.GetNormalizedTime(AnimationController.BASE_LAYER_INDEX) >= HIT_WINDOW_END, cancellationToken: token);
				SetHitWindow(false);
				await UniTask.WaitUntil(() => AnimationController.GetNormalizedTime(AnimationController.BASE_LAYER_INDEX) >= ATTACK_WINDOW_END, cancellationToken: token);

				LastSkillUseTime[skillType] = Time.time;
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				SetHitWindow(false);
				IsSkillAttack = false;
			}
		}

		public virtual void TryAttack(ESkillType skillType = ESkillType.Default)
		{
			if (skillType != ESkillType.Default)
			{
				TrySkillAttack(skillType).Forget();
				return;
			}

			if (!IsSkillAttack)
				TryComboAttack();
		}

		protected void TryComboAttack()
		{
			if (IsComboAttack)
			{
				InputBuffered = true;
				return;
			}
			PlayComboAttackAsync().Forget();
		}

		public virtual void InterruptCombo()
		{
			if (!IsComboAttack)
				return;

			ComboCancellation?.Cancel();
			SetHitWindow(false);
			ResetCombo();
		}

		public virtual void InterruptSkill()
		{
			if (!IsSkillAttack)
				return;

			SkillCancellation?.Cancel();
			SetHitWindow(false);
			IsSkillAttack = false;
		}

		private void SetHitWindow(bool isOpen)
		{
			if (_isHitWindowOpen == isOpen)
			{
				return;
			}

			_isHitWindowOpen = isOpen;
			if (isOpen)
			{
				HitWindowOpened?.Invoke();
			}
			else
			{
				HitWindowClosed?.Invoke();
			}
		}

		protected void ResetCombo()
		{
			ComboIndex = 0;
			InputBuffered = false;
			IsComboAttack = false;
		}

		protected bool CanTransitionToNextAnim()
		{
			var stateInfo = AnimationController.GetCurrentStateInfo(AnimationController.BASE_LAYER_INDEX);
			float normalizedTime = stateInfo.normalizedTime % 1f;
			float attackWindowStart = stateInfo.IsTag("Attack") ? ATTACK_WINDOW_START : 0;
			return normalizedTime >= attackWindowStart && normalizedTime <= ATTACK_WINDOW_END;
		}
	}
}
