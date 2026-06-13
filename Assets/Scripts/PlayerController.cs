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
    [SerializeField] private float DashCooldown = 1f;
    private Vector3 velocity;
    private bool suppressGravity;

    [Header("Stamina Costs")]
    [SerializeField] private int doubleJumpStaminaCost = 20;
    [SerializeField] private int sprintStaminaCost = 20;
    [SerializeField] private int dashStaminaCost = 20;

    // Dash Variables
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    // Component references
    private CharacterController cc;
    private StaminaSystem stamina;
    private InputManager input;

    // Jump state
    private bool hasDoubleJump;

    // Sprint state
    private bool isSprinting;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        CameraController fpscam = Camera.main.GetComponent<CameraController>();
        if (fpscam != null)
        {
            fpscam.SetTarget(transform);
            fpscam.Activate();
        }
    }

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        stamina = GetComponent<StaminaSystem>();
        input = GetComponent<InputManager>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (cc.isGrounded) hasDoubleJump = true;
        HandleMovement();
        HandleJump();
        HandleDash();
        ApplyGravity();

        cc.Move(velocity * Time.deltaTime);
    }

    private void HandleMovement()
    {
        if (isDashing) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Build move direction relative to where the player is facing.
        Vector3 camForward = Vector3.Scale(Camera.main.transform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight   = Camera.main.transform.right;
        Vector3 move       = camRight * h + camForward * v;

        // Sprint: hold Left Shift, must have stamina, must be moving.
        isSprinting = Input.GetKey(input.SprintKey) && move.magnitude > 0.1f && stamina.Current > 0;

        float speed = walkSpeed * (isSprinting ? sprintMultiplier : 1f);

        if (isSprinting) stamina.Drain(sprintStaminaCost * Time.deltaTime);
        if (move.magnitude > 0.1f || cc.isGrounded)
        {
            velocity.x = move.x * speed;
            velocity.z = move.z * speed;
        }
    }

    private void HandleJump()
    {
        if (!Input.GetKeyDown(input.JumpKey)) return;

        if (cc.isGrounded)
        {
            velocity.y = jumpForce;
            hasDoubleJump = true;
            return;
        }

        // Double jump: available in air if not already used.
        if (hasDoubleJump && stamina.Current >= doubleJumpStaminaCost)
        {
            velocity.y = jumpForce;
            hasDoubleJump = false;
            stamina.Drain(doubleJumpStaminaCost);
        }
    }

    private void HandleDash()
    {
        // Tick down cooldown.
        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0f)
            {
                isDashing = false;
                velocity.x = 0f;
                velocity.z = 0f;
            }
            else
            {
                // Override horizontal velocity with dash direction for the duration.
                float speed = dashDistance / dashDuration;
                velocity.x = dashDirection.x * speed;
                velocity.z = dashDirection.z * speed;
            }
            return;
        }

        if (!Input.GetKeyDown(input.DashKey)) return;
        if (dashCooldownTimer > 0f) return;
        if (stamina.Current < dashStaminaCost) return;

        // Use current horizontal input as dash direction.
        // Falls back to forward if no input is held.
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 inputDir = transform.right * h + transform.forward * v;

        if (inputDir.magnitude < 0.1f)
            inputDir = transform.forward;

        dashDirection = inputDir.normalized;
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = DashCooldown;
        stamina.Drain(dashStaminaCost);
    }

    public void SuppressGravity()
    {
        suppressGravity = true;
    }
    
    private void ApplyGravity()
    {
        if (suppressGravity)
        {
            suppressGravity = false;
            velocity.y = 0f;
            return;
        }
    
        if (isDashing) return;
    
        if (cc.isGrounded && velocity.y < 0f)
        {
            velocity.y = groundedGravity;
            return;
        }
    
        velocity.y += gravity * Time.deltaTime;
    }

    public void AddVelocity(Vector3 velocity)
    {
        this.velocity += velocity;
    }
}