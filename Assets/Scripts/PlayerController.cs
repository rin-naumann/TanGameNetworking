using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float gravity = -9.8f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private float dashDistance = 15f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Stamina Costs")]
    [SerializeField] private int doubleJumpStaminaCost = 20;
    [SerializeField] private int sprintStaminaCost = 20;
    [SerializeField] private int dashStaminaCost = 20;

    private CharacterController _cc;
    private StaminaSystem _stamina;
    private InputManager _input;
    private CameraController _camera;
    private SoundManager _sound;
    private GrappleController _grapple;

    private Vector3 _velocity;
    private bool _hasDoubleJump;
    private bool _isSprinting;

    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;

    public bool IsDashing    => _isDashing;
    public bool IsSprinting  => _isSprinting;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
    }

    public void Activate()
    {
        if (!IsOwner) return;
        enabled = true;
        _camera = Camera.main.GetComponent<CameraController>();
        if (_camera != null)
        {
            _camera.SetTarget(transform);
        }
    }

    private void Awake()
    {
        _cc     = GetComponent<CharacterController>();
        _stamina = GetComponent<StaminaSystem>();
        _input  = GetComponent<InputManager>();
        _sound  = SoundManager.Instance;
        _grapple = GetComponent<GrappleController>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (_cc.isGrounded) _hasDoubleJump = true;

        HandleMovement();
        HandleJump();
        HandleDash();
        ApplyGravity();

        _cc.Move(_velocity * Time.deltaTime);

        UpdateCameraEffects();
        UpdateSound();
    }

    // Builds move direction from camera orientation. Handles sprint and stamina drain.
    private void HandleMovement()
    {
        if (_isDashing) return;
        if (_grapple != null && _grapple.IsGrappling) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 camForward = Vector3.Scale(Camera.main.transform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight   = Camera.main.transform.right;
        Vector3 move       = camRight * h + camForward * v;

        _isSprinting = Input.GetKey(_input.SprintKey) && move.magnitude > 0.1f && _stamina.Current > 0;

        float speed = walkSpeed * (_isSprinting ? sprintMultiplier : 1f);
        if (_isSprinting) _stamina.Drain(sprintStaminaCost * Time.deltaTime);

        if (move.magnitude > 0.1f || _cc.isGrounded)
        {
            _velocity.x = move.x * speed;
            _velocity.z = move.z * speed;
        }
    }

    // Handles grounded jump and air double jump. Double jump costs stamina.
    private void HandleJump()
    {
        if (_grapple != null && _grapple.IsGrappling) return;
        if (!Input.GetKeyDown(_input.JumpKey)) return;

        if (_cc.isGrounded)
        {
            _velocity.y = jumpForce;
            _hasDoubleJump = true;
            _sound?.PlayJump();
            return;
        }

        if (_hasDoubleJump && _stamina.Current >= doubleJumpStaminaCost)
        {
            _velocity.y = jumpForce;
            _hasDoubleJump = false;
            _stamina.Drain(doubleJumpStaminaCost);
            _sound?.PlayDoubleJump();
        }
    }

    // Triggers a horizontal velocity burst in the current input direction. Works in air.
    private void HandleDash()
    {
        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
            {
                _isDashing  = false;
                _velocity.x = 0f;
                _velocity.z = 0f;
                _camera?.SetFOVBoosted(false);
            }
            else
            {
                float speed = dashDistance / dashDuration;
                _velocity.x = _dashDirection.x * speed;
                _velocity.z = _dashDirection.z * speed;
            }
            return;
        }

        if (!Input.GetKeyDown(_input.DashKey)) return;
        if (_dashCooldownTimer > 0f) return;
        if (_stamina.Current < dashStaminaCost) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 camForward = Vector3.Scale(Camera.main.transform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 inputDir   = Camera.main.transform.right * h + camForward * v;
        if (inputDir.magnitude < 0.1f) inputDir = camForward;

        _dashDirection     = inputDir.normalized;
        _isDashing         = true;
        _dashTimer         = dashDuration;
        _dashCooldownTimer = dashCooldown;
        _stamina.Drain(dashStaminaCost);

        _camera?.SetFOVBoosted(true);
        _sound?.PlayDash();
    }

    // Accumulates gravity each frame. Skipped when GrappleController suppresses it.
    private void ApplyGravity()
    {
        if (_grapple != null && _grapple.IsGrappling) return;
        if (_isDashing) return;

        if (_cc.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = groundedGravity;
            return;
        }

        _velocity.y += gravity * Time.deltaTime;
    }

    // Tells CameraController whether to bob. Bob is active only when grounded, moving, and not dashing.
    private void UpdateCameraEffects()
    {
        if (_camera == null) return;
        bool shouldBob = _cc.isGrounded && !_isDashing &&
                         (_velocity.x != 0f || _velocity.z != 0f);
        _camera.SetBobbing(shouldBob);
    }

    // Passes movement state to SoundManager to control footstep loop.
    private void UpdateSound()
    {
        if (_sound == null) return;
        bool isMoving = _velocity.x != 0f || _velocity.z != 0f;
        _sound.UpdateFootsteps(isMoving, _isSprinting, _cc.isGrounded);
    }
    public void SetVelocity(Vector3 velocity)    => _velocity = velocity;
    public void AddVelocity(Vector3 velocity)    => _velocity += velocity;
}