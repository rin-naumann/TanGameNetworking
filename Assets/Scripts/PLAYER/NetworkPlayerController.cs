using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float gravity = -60f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Dash Settings")]
    [SerializeField] private float dashForce = 15f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Wall Running Settings")]
    [SerializeField] private float wallRunGravity = -10f;
    [SerializeField] private float wallDetectionDistance = 0.5f;
    [SerializeField] private LayerMask wallLayerMask;

    [Header("Control Settings")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;

    // References
    private CharacterController cc;
    private FirstPersonCamera fpsCamera;

    // Movement Variables
    private float verticalVelocity;
    private int jumpCount;

    // Dash Variables
    private bool isDashing;
    private float lastDashTime;

    // Wall Running Variables
    private bool isWallRunning;
    private Vector3 wallNormal;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        fpsCamera = GetComponentInChildren<FirstPersonCamera>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) cc.enabled = false;
    }

    void Update()
    {
        if (!IsOwner) return;

        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        bool isSprinting = Input.GetKey(sprintKey);
        bool jumpPressed = Input.GetKeyDown(jumpKey);
        bool dashPressed = Input.GetKeyDown(dashKey);
        float yaw = transform.eulerAngles.y;
        if (IsServer) HandleMovement(input, isSprinting, jumpPressed, dashPressed, yaw);
        else HandleMovementRpc(input, isSprinting, jumpPressed, dashPressed, yaw);
    }

    [Rpc(SendTo.Server)]
    private void HandleMovementRpc(Vector2 input, bool isSprinting, bool jumpPressed, bool dashPressed, float yaw)
    {
        HandleMovement(input, isSprinting, jumpPressed, dashPressed, yaw);
    }

    private void HandleMovement(Vector2 input, bool isSprinting, bool jumpPressed, bool dashPressed, float yaw)
    {
        transform.eulerAngles = new Vector3(transform.eulerAngles.x, yaw, transform.eulerAngles.z);
        isWallRunning = CheckWallRun();
        PlayerMovement(input, isSprinting);
        if (jumpPressed) HandleJump();
        if (dashPressed) HandleDash();
        ApplyGravity();
    }

    private void ApplyGravity()
    {
        if (cc.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = groundedGravity;
                jumpCount = 0;
            }
        }
        else if (isWallRunning)
        {
            verticalVelocity += wallRunGravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private bool CheckWallRun()
    {
        if (cc.isGrounded) return false;

        Vector3[] directions = { transform.right, -transform.right, transform.forward, -transform.forward };
        foreach (var dir in directions)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, wallDetectionDistance, wallLayerMask))
            {
                wallNormal = hit.normal;
                return true;
            }
        }
        return false;
    }

    private void PlayerMovement(Vector2 input, bool isSprinting)
    {
        Vector3 moveDirection = (transform.forward * input.y + transform.right * input.x).normalized;
        float currentSpeed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);

        cc.Move(moveDirection * currentSpeed * Time.deltaTime + Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (isWallRunning)
        {
            verticalVelocity = jumpForce;
            cc.Move(wallNormal * moveSpeed * Time.deltaTime);
        }
        else if (cc.isGrounded || jumpCount < maxJumps)
        {
            verticalVelocity = jumpForce;
            jumpCount++;
        }
    }

    private void HandleDash()
    {
        if (isDashing || Time.time - lastDashTime < dashCooldown) return;

        isDashing = true;
        lastDashTime = Time.time;
        Vector3 dashDirection = fpsCamera.GetCameraForward().normalized;
        cc.Move(dashDirection * dashForce);
        Invoke(nameof(ResetDash), 0.2f);
    }

    private void ResetDash() => isDashing = false;
}

