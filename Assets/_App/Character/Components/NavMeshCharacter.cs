using UnityEngine;
using UnityEngine.AI;
namespace _App
{
    [RequireComponent(typeof(Character)), RequireComponent(typeof(NavMeshAgent))]
    public class NavMeshCharacter : MonoBehaviour
    {
        [Space(15f)]
        [SerializeField] private bool _autoBraking;
        [SerializeField] private float _brakingDistance;
        [SerializeField] private float _stoppingDistance;
        
        private NavMeshAgent _agent;
        private Character _character;

        public NavMeshAgent Agent => _agent;

        public Character Character => _character;

        public bool AutoBraking
        {
            get => _autoBraking;
            set
            {
                _autoBraking = value;
                Agent.autoBraking = _autoBraking;
            }
        }
        
        public float BrakingDistance
        {
            get => _brakingDistance;
            set => _brakingDistance = Mathf.Max(0.0001f, value);
        }

        public float BrakingRatio
        {
            get
            {
                if (!AutoBraking)
                    return 1f;

                return Agent.hasPath ? Mathf.InverseLerp(0.0f, BrakingDistance, Agent.remainingDistance) : 1.0f;
            }
        }

        public float StoppingDistance
        {
            get => _stoppingDistance;
            set
            {
                _stoppingDistance = Mathf.Max(0.0f, value);
                Agent.stoppingDistance = _stoppingDistance;
            }
        }
        
        public delegate void DestinationReachedEventHandler();
        public event DestinationReachedEventHandler DestinationReached;

        public virtual void OnDestinationReached()
        {
            DestinationReached?.Invoke();
        }
        
        protected virtual void CacheComponents()
        {
            _agent = GetComponent<NavMeshAgent>();
            _character = GetComponent<Character>();
        }
        
        public virtual bool HasPath()
        {
            return Agent.hasPath;
        }
        
        public virtual bool IsPathFollowing()
        {
            return Agent.hasPath && !Agent.isStopped;
        }
        
        public virtual Vector3 GetDestination()
        {
            return Agent.destination;
        }
        
        public virtual void MoveToDestination(Vector3 destination)
        {
            Vector3 worldUp = -Character.GetGravityDirection();
            Vector3 toDestination2D = Vector3.ProjectOnPlane(destination - Character.Position, worldUp);
            
            if (toDestination2D.sqrMagnitude >= MathLib.Square(StoppingDistance))
                Agent.SetDestination(destination);
        }
        
        public virtual void PauseMovement(bool pause)
        {
            Agent.isStopped = pause;
            Character.SetMovementDirection(Vector3.zero);
        }
        
        public virtual void StopMovement()
        {
            Agent.ResetPath();
            Character.SetMovementDirection(Vector3.zero);
        }
        
        protected virtual float ComputeAnalogInputModifier(Vector3 desiredVelocity)
        {
            float maxSpeed = _character.GetMaxSpeed();
            if (desiredVelocity.sqrMagnitude > 0.0f && maxSpeed > 0.00000001f)
                return Mathf.Clamp01(desiredVelocity.magnitude / maxSpeed);

            return 0.0f;
        }

        protected virtual Vector3 CalcMovementDirection(Vector3 desiredVelocity)
        {
            Vector3 worldUp = -Character.GetGravityDirection();
            Vector3 desiredVelocity2D = Vector3.ProjectOnPlane(desiredVelocity, worldUp);
            
            Vector3 scaledDesiredVelocity2D = desiredVelocity2D * BrakingRatio;

            float minAnalogSpeed = _character.GetMinAnalogSpeed();
            if (scaledDesiredVelocity2D.sqrMagnitude < MathLib.Square(minAnalogSpeed))
                scaledDesiredVelocity2D = scaledDesiredVelocity2D.normalized * minAnalogSpeed;

            return Vector3.ClampMagnitude(scaledDesiredVelocity2D, ComputeAnalogInputModifier(scaledDesiredVelocity2D));
        }

        protected virtual void DoPathFollowing()
        {
            if (!IsPathFollowing())
                return;
            
            if (Agent.remainingDistance <= StoppingDistance)
            {
                StopMovement();
                OnDestinationReached();
            }
            else
            {
                Vector3 movementDirection = CalcMovementDirection(Agent.desiredVelocity);
                Character.SetMovementDirection(movementDirection);
            }
        }

        protected virtual void SyncNavMeshAgent()
        {
            Agent.angularSpeed = _character.RotationRate;

            Agent.speed = _character.GetMaxSpeed();
            Agent.acceleration = _character.GetMaxAcceleration();
            
            Agent.velocity = _character.GetVelocity();
            Agent.nextPosition = _character.GetPosition();

            Agent.radius = _character.Radius;
            Agent.height = _character.Height;
        }
        
        protected virtual void OnMovementModeChanged(EMovementMode prevMovementMode, int prevCustomMovementMode)
        {
            if (!Character.IsWalking() || !Character.IsFalling())
            {
                StopMovement();
            }
        }
        
        protected virtual void OnBeforeSimulationUpdated(float deltaTime)
        {
            DoPathFollowing();
        }
        
        private void Reset()
        {
            _autoBraking = true;

            _brakingDistance = 2.0f;
            _stoppingDistance = 1.0f;
        }

        private void OnValidate()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
            
            BrakingDistance = _brakingDistance;
            StoppingDistance = _stoppingDistance;
        }

        protected virtual void Awake()
        {
            CacheComponents();
            
            Agent.autoBraking = AutoBraking;
            Agent.stoppingDistance = StoppingDistance;

            Agent.updatePosition = false;
            Agent.updateRotation = false;

            Agent.updateUpAxis = false;
        }

        protected virtual void OnEnable()
        {
            Character.MovementModeChanged += OnMovementModeChanged;
            Character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
        }
        
        protected virtual void OnDisable()
        {
            Character.MovementModeChanged -= OnMovementModeChanged;
            Character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
        }

        protected virtual void LateUpdate()
        {
            SyncNavMeshAgent();
        }
    }
}