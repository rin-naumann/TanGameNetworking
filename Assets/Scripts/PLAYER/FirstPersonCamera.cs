using UnityEngine;
using Unity.Netcode;

public class FirstPersonCamera : NetworkBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private bool cursorLocked = true;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform playerBody;

    [Header("Camera Clamping")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private float pitch = 0f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) {
            if (playerCamera != null) {
                Camera cam = playerCamera.GetComponent<Camera>();
                AudioListener audioListener = playerCamera.GetComponent<AudioListener>();
                if (audioListener != null) audioListener.enabled = false;
                if (cam != null) cam.enabled = false;
            }
            return;
        }

        if (cursorLocked) Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (!IsOwner) return;
        HandleCamera();
    }

    private void HandleCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        transform.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        playerCamera.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    public Vector3 GetCameraForward() => playerCamera != null ? playerCamera.forward : transform.forward;

    public Transform GetCameraTransform() => playerCamera != null ? playerCamera : transform;


}
