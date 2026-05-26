using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerController : NetworkBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float gravity = -9.81f;
    [SerializeField] float groundedGravity = -2f;
    [SerializeField] float jumpForce = 5f;
    [SerializeField] KeyCode jumpKey = KeyCode.Space;

    CharacterController characterController;
    float verticalVelocity;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!IsOwner) return;

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector2 inputDir = new Vector2(horizontalInput, verticalInput);
        bool jumpPressed = Input.GetKeyDown(jumpKey);

        MovePlayer(inputDir, jumpPressed, Time.deltaTime);
        if (!IsServer) MovePlayerRpc(inputDir, jumpPressed, Time.deltaTime); 
    }

    [Rpc(SendTo.Server)]
    private void MovePlayerRpc(Vector2 movementInput, bool jumpPressed, float deltaTime)
    {
        MovePlayer(movementInput, jumpPressed, deltaTime); 
    }

    private void MovePlayer(Vector2 movementInput, bool jumpPressed, float deltaTime)
    {
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = groundedGravity;
            if (jumpPressed) verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
        else verticalVelocity += gravity * deltaTime; 

        Vector3 moveDir = new Vector3(movementInput.x, 0, movementInput.y).normalized;
        Vector3 finalMovement = moveDir * moveSpeed + new Vector3(0, verticalVelocity, 0);
        characterController.Move(finalMovement * deltaTime);
    }
}
