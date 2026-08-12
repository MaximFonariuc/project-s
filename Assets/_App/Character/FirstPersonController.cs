using UnityEngine;

namespace _App
{
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Cinemachine")]
        [SerializeField] private GameObject _cameraTarget;
        [SerializeField] private float _maxPitch = 80.0f;
        [SerializeField] private float _minPitch = -80.0f;
        
        [Space(15.0f)]
        [SerializeField] private GameObject _crouchedCamera;
        [SerializeField] private GameObject _unCrouchedCamera;
        
        [Space(15.0f)]
        [SerializeField] private Vector2 _lookSensitivity = new Vector2(1.5f, 1.25f);
        
        private Character _character;
        private float _cameraTargetPitch;

        private void AddControlYawInput(float value)
        {
            _character.AddYawInput(value);
        }

        private void AddControlPitchInput(float value, float minValue = -80.0f, float maxValue = 80.0f)
        {
            if (value == 0.0f)
                return;
            
            _cameraTargetPitch = MathLib.ClampAngle(_cameraTargetPitch + value, minValue, maxValue);
            _cameraTarget.transform.localRotation = Quaternion.Euler(-_cameraTargetPitch, 0.0f, 0.0f);
        }
        
        private void OnCrouched()
        {
            _crouchedCamera.SetActive(true);
            _unCrouchedCamera.SetActive(false);
        }
        
        private void OnUnCrouched()
        {
            _crouchedCamera.SetActive(false);
            _unCrouchedCamera.SetActive(true);
        }

        private void Awake()
        {
            _character = GetComponent<Character>();
        }
        
        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            
            _character.SetRotationMode(ERotationMode.None);
        }
        
        private void OnEnable()
        {
            _character.Crouched += OnCrouched;
            _character.UnCrouched += OnUnCrouched;
        }
        
        private void OnDisable()
        {
            _character.Crouched -= OnCrouched;
            _character.UnCrouched -= OnUnCrouched;
        }

        private void Update()
        {
            // Movement input
            
            Vector2 moveInput = new Vector2
            {
                x = Input.GetAxisRaw("Horizontal"),
                y = Input.GetAxisRaw("Vertical")
            };
            
            // Movement direction relative to Character's forward

            Vector3 movementDirection = Vector3.zero;

            movementDirection += _character.GetRightVector() * moveInput.x;
            movementDirection += _character.GetForwardVector() * moveInput.y;
            
            // Set Character movement direction

            _character.SetMovementDirection(movementDirection);
            
            // Look input

            Vector2 lookInput = new Vector2
            {
                x = Input.GetAxisRaw("Mouse X"),
                y = Input.GetAxisRaw("Mouse Y")
            };
            
            // Add yaw input, this update character's yaw rotation

            AddControlYawInput(lookInput.x * _lookSensitivity.x);
            
            // Add pitch input (look up / look down), this update cameraTarget's local rotation
            
            AddControlPitchInput(lookInput.y * _lookSensitivity.y, _minPitch, _maxPitch);
            
            // Crouch input

            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
                _character.Crouch();
            else if (Input.GetKeyUp(KeyCode.LeftControl) || Input.GetKeyUp(KeyCode.C))
                _character.UnCrouch();
            
            // Jump input

            if (Input.GetButtonDown("Jump"))
                _character.Jump();
            else if (Input.GetButtonUp("Jump"))
                _character.StopJumping();
        }
    }
}