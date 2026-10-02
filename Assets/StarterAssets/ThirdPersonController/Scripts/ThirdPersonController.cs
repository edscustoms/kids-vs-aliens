using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioSource AudioFootsteps;
        public AudioSource LandingAudio;
        public AudioSource AudioFoley;
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;

        [Range(0, 1)]
        public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip(
            "Time required to pass before being able to jump again. Set to 0f to instantly jump again"
        )]
        public float JumpTimeout = 0.50f;

        [Tooltip(
            "Time required to pass before entering the fall state. Useful for walking down stairs"
        )]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip(
            "True when the CharacterController's latest Move was supported by a walkable surface"
        )]
        public bool Grounded = true;

        // Presentation observes the pre-collision downward speed of a real walkable landing.
        public event System.Action<float> Landed;

        // Latched across small gaps/contact loss; only walkable support releases the jump lock.
        public bool OnSteepSlope { get; private set; }
        private bool _walkableContact;
        private bool _steepContact;
        private bool _collectGroundContacts;
        private bool _touchingSteepSurface;
        private Vector3 _steepNormal = Vector3.up;
        private Vector3 _groundNormal = Vector3.up;
        private float _walkableNormalY;

        [Header("Cinemachine")]
        [Tooltip(
            "The follow target set in the Cinemachine Virtual Camera that the camera will follow"
        )]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degrees to override the camera")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // Cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // Player
        private float _speed;
        private float _animationBlend;
        private float _verticalVelocity;
        public float RunVerticalVelocity => _verticalVelocity;
        public void RestoreRunVerticalVelocity(float value) { _verticalVelocity = value; }
        private float _terminalVelocity = 53.0f;

        // Timeout delta time
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // Animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private PlayerMeleeController _melee;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _melee = GetComponent<PlayerMeleeController>();

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError(
                "Starter Assets package is missing dependencies. "
                    + "Please use Tools/Starter Assets/Reinstall Dependencies to fix it"
            );
#endif

            AssignAnimationIDs();

            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            JumpAndGravity();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        // The bike owns world movement; the existing camera target still owns free look.
        public void UpdateMountedCamera() => CameraRotation();

        public void ResetMotion()
        {
            _speed = _animationBlend = _verticalVelocity = 0f;
            OnSteepSlope = _touchingSteepSurface = false;
            _steepNormal = Vector3.up;
            _groundNormal = Vector3.up;
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void CameraRotation()
        {
            if (InputModeController.IsMobile)
                return;
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;

                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            _cinemachineTargetYaw = ClampAngle(
                _cinemachineTargetYaw,
                float.MinValue,
                float.MaxValue
            );

            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
                _cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw,
                0.0f
            );
        }

        private void Move()
        {
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // A committed planted attack uses the same acceleration/deceleration path.
            // Input, gravity, collisions and the CharacterController stay authoritative.
            if (_input.move == Vector2.zero || (Grounded && _melee != null && _melee.RequiresPlantedFeet))
            {
                targetSpeed = 0.0f;
            }

            float currentHorizontalSpeed = new Vector3(
                _controller.velocity.x,
                0.0f,
                _controller.velocity.z
            ).magnitude;
            // Sliding velocity must not feed back into the player's steering speed.
            if (OnSteepSlope) currentHorizontalSpeed = _speed;

            float speedOffset = 0.1f;

            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (
                currentHorizontalSpeed < targetSpeed - speedOffset
                || currentHorizontalSpeed > targetSpeed + speedOffset
            )
            {
                _speed = Mathf.Lerp(
                    currentHorizontalSpeed,
                    targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate
                );

                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(
                _animationBlend,
                targetSpeed,
                Time.deltaTime * SpeedChangeRate
            );

            if (_animationBlend < 0.01f)
            {
                _animationBlend = 0f;
            }

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y);

            Vector3 cameraForward = _mainCamera.transform.forward;
            cameraForward.y = 0.0f;
            cameraForward.Normalize();

            Vector3 cameraRight = _mainCamera.transform.right;
            cameraRight.y = 0.0f;
            cameraRight.Normalize();

            Vector3 targetDirection =
                cameraForward * inputDirection.z + cameraRight * inputDirection.x;

            if (targetDirection.sqrMagnitude > 0.01f)
            {
                targetDirection.Normalize();
            }

            Vector3 inputVelocity = targetDirection.normalized * _speed;
            Vector3 gravityVelocity = Vector3.up * _verticalVelocity;
            if (OnSteepSlope)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _steepNormal);
                Vector3 downhillHorizontal = new Vector3(downhill.x, 0f, downhill.z).normalized;
                // Retain lateral/downhill steering, but never let input cancel gravity uphill.
                float downhillInput = Vector3.Dot(inputVelocity, downhillHorizontal);
                if (downhillInput < 0f) inputVelocity -= downhillHorizontal * downhillInput;
                if (_touchingSteepSurface)
                    gravityVelocity = Vector3.ProjectOnPlane(gravityVelocity, _steepNormal);
            }

            bool wasGrounded = Grounded;
            float downwardLandingSpeed = Mathf.Max(0f, -gravityVelocity.y);
            _walkableContact = _steepContact = false;
            _walkableNormalY = Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);
            float authoredStepOffset = _controller.stepOffset;
            float authoredSlopeLimit = _controller.slopeLimit;
            CollisionFlags collisionFlags;
            _collectGroundContacts = true;
            try
            {
                if (OnSteepSlope) _controller.stepOffset = 0f;
                // PhysX can reject an exactly-at-limit plane due to float rounding.
                // Only relax its internal comparison on already confirmed walkable
                // support; contact classification still uses the authored limit.
                if (Grounded && Mathf.Abs(_groundNormal.y - _walkableNormalY) < .00001f)
                    _controller.slopeLimit = Mathf.Min(90f, authoredSlopeLimit + .01f);
                collisionFlags = _controller.Move((inputVelocity + gravityVelocity) * Time.deltaTime);
            }
            finally
            {
                _collectGroundContacts = false;
                if (_controller.stepOffset != authoredStepOffset) _controller.stepOffset = authoredStepOffset;
                if (_controller.slopeLimit != authoredSlopeLimit) _controller.slopeLimit = authoredSlopeLimit;
            }

            // Below alone also reports unwalkable slopes. Require the actual supporting
            // collision normal, and let real ground at the foot of a slope take precedence.
            Grounded = (collisionFlags & CollisionFlags.Below) != 0 && _walkableContact;
            if (!wasGrounded && Grounded && downwardLandingSpeed > 0f)
                Landed?.Invoke(downwardLandingSpeed);
            _touchingSteepSurface = !Grounded && _steepContact;
            if (Grounded) OnSteepSlope = false;
            else if (_steepContact)
            {
                OnSteepSlope = true;
                _verticalVelocity = Mathf.Min(0f, _verticalVelocity);
                _input.jump = false;
            }

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }

            // ----------------------------
            // ANIMATION DIRECTION
            // ----------------------------

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);

                float animationDirection = 1f;

                if (_input.move != Vector2.zero)
                {
                    /*
                     * Compare the direction we're MOVING
                     * against the direction we're AIMING/FACING.
                     *
                     *  1  = moving forward
                     *  0  = moving sideways
                     * -1  = moving backwards
                     */

                    float forwardAmount = Vector3.Dot(
                        transform.forward,
                        targetDirection.normalized
                    );

                    // If movement is substantially opposite
                    // the aim direction, reverse the animation.
                    if (forwardAmount < -0.25f)
                    {
                        animationDirection = -1f;
                    }
                }

                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude * animationDirection);
            }
        }

        private void JumpAndGravity()
        {
            if (OnSteepSlope)
            {
                Grounded = false;
                _input.jump = false;
                _verticalVelocity = Mathf.Min(0f, _verticalVelocity);
            }
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!_collectGroundContacts) return;
            // Side walls and stair risers are not supporting ground. Use the lower
            // hemisphere of the real controller, not a separate authored ground mask.
            Vector3 lowerCenter = transform.TransformPoint(_controller.center)
                - Vector3.up * ((_controller.height * .5f - _controller.radius) * transform.lossyScale.y);
            if (hit.point.y > lowerCenter.y + _controller.skinWidth * 2f) return;
            Vector3 normal = hit.normal;
            // Capsule/edge contacts may return a rounded collision normal at a stair
            // nosing. Query the same collider for its actual face normal so an ordinary
            // step does not masquerade as a steep ramp.
            float probe = Mathf.Max(.01f, _controller.skinWidth * 2f);
            if (hit.collider.Raycast(new Ray(hit.point + normal * probe, -normal), out RaycastHit face, probe * 2f))
                normal = face.normal;
            if (normal.y <= .001f) return;
            if (normal.y >= _walkableNormalY - .00001f)
            {
                _walkableContact = true;
                _groundNormal = normal;
            }
            else
            {
                _steepContact = true;
                _steepNormal = normal;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f)
            {
                lfAngle += 360f;
            }

            if (lfAngle > 360f)
            {
                lfAngle -= 360f;
            }

            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            // Animation events can still arrive while locomotion is suspended by Beam Transport.
            if (!isActiveAndEnabled) return;
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (AudioFootsteps != null)
                {
                    AudioFootsteps.Play();
                }

                if (AudioFoley != null)
                {
                    AudioFoley.Play();
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (LandingAudio != null)
                {
                    LandingAudio.Play();
                }
            }
        }
    }
}
