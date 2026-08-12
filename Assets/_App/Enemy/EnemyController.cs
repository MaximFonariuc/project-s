using UnityEngine;

namespace _App
{
	[RequireComponent(typeof(Health))]
	public class EnemyController : MonoBehaviour
	{
		[SerializeField] private ECharacterType _characterType;
		[SerializeField] private AnimationsData _animationsData;
		[SerializeField] private Animator _animator;

		[SerializeField] private float _viewRadius = 10f;
		[SerializeField, Range(0, 180f)] private float _viewAngle = 90f;

		[SerializeField] private LayerMask _targetMask;
		[SerializeField] private LayerMask _obstacleMask;

		public Character Character { get; private set; }
		public EnemyCombatController CombatController  { get; private set; }
		public EnemyTargetLockSystem TargetLockSystem  { get; private set; }
		public AnimationController AnimationController  { get; private set; }
		public EnemyStateMachine StateMachine  { get; private set; }
		public Health Health { get; private set; }
		
		public Vector3 StartPosition { get; private set; }
		public Quaternion StartRotation { get; private set; }

		private readonly Collider[] _detectedTargets = new Collider[2];
		
		private void Awake()
		{
			Character = GetComponent<Character>();
			Health = GetComponent<Health>();
			AnimationController = new AnimationController(Character, _animator);
			CombatController = new EnemyCombatController(_characterType, AnimationController, _animationsData);
			TargetLockSystem = new EnemyTargetLockSystem(Character);

			StateMachine = new EnemyStateMachine();
			StateMachine.Initialize(new EnemyIdleState(this));
			
			StartPosition = transform.position;
			StartRotation = transform.rotation;

			Health.Damaged += OnDamaged;
			Health.Died += OnDied;
		}

		private void OnDestroy()
		{
			if (Health == null)
			{
				return;
			}

			Health.Damaged -= OnDamaged;
			Health.Died -= OnDied;
		}

		private void Update()
		{
			if (!Health.IsAlive)
			{
				AnimationController.Update();
				return;
			}

			DetectTargets();
			StateMachine.CurrentState?.Tick();
			AnimationController.Update();
		}
		
		private void DetectTargets()
		{
			if (TargetLockSystem.IsTargetLocked)
			{
				return;
			}
			
			var hits = Physics.OverlapSphereNonAlloc(transform.position, _viewRadius, _detectedTargets, _targetMask);
			
			for (int i = 0; i < hits; i++)
			{
				Transform target = _detectedTargets[i].transform;
				Vector3 dirToTarget = (target.position - transform.position).normalized;

				if (Vector3.Angle(transform.forward, dirToTarget) <= _viewAngle)
				{
					float dist = Vector3.Distance(transform.position, target.position);
					if (!Physics.Raycast(transform.position, dirToTarget, dist, _obstacleMask))
					{
						TargetLockSystem.AcquireTarget();
						SwitchState(new EnemyChaseState(this));
						return;
					}
				}
			}
		}
		
		public void SwitchState(EnemyBaseState newState)
		{
			StateMachine.ChangeState(newState);
		}

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireSphere(transform.position, _viewRadius);

			Vector3 leftBoundary = DirectionFromAngle(-_viewAngle);
			Vector3 rightBoundary = DirectionFromAngle(_viewAngle);

			Gizmos.color = Color.red;
			Gizmos.DrawLine(transform.position, transform.position + leftBoundary * _viewRadius);
			Gizmos.DrawLine(transform.position, transform.position + rightBoundary * _viewRadius);
		}

		private Vector3 DirectionFromAngle(float angleInDegrees)
		{
			angleInDegrees += transform.eulerAngles.y;
			float rad = angleInDegrees * Mathf.Deg2Rad;
			return new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
		}
		
		private void OnDamaged(DamageInfo damage)
		{
			if (Health.IsAlive)
			{
				ReactToDamage();
			}
		}

		private void ReactToDamage()
		{
			if (CombatController.IsSkillAttack)
			{
				return;
			}
			
			CombatController.InterruptCombo();
			SwitchState(new EnemyHitState(this));
		}

		private void OnDied(DamageInfo damage)
		{
			CombatController.InterruptCombo();
			SwitchState(new EnemyDeadState(this));
		}
	}
}
