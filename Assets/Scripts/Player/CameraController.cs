using UnityEngine;

// Trimmed from the original: this is genuinely first-person already (camera
// sits at _target.position, rotates with mouse), so FOV zoom/boost, Recoil,
// and Head Bob — all leftovers from the arena-shooter reference game — are
// gone. What's left is exactly HandleLook, SetTarget, Activate, Deactivate.
public class CameraController : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private float sensitivity = 200f;
    [SerializeField] private float verticalClampMin = -80f;
    [SerializeField] private float verticalClampMax = 80f;

    private Transform _target;
    private float _xRotation;
    private float _yaw;

    // Local-only look yaw, in degrees. NetworkPlayerController reads this to
    // compute movement direction and to send up via UpdateLookYawRpc, instead
    // of reading the networked transform's rotation directly - that transform
    // is now server-authoritative (NetworkTransform.SyncRotAngleY), so a
    // non-host client rotating it locally would just get fought and
    // overwritten by the next network sync. This field is never touched by
    // Netcode at all, so there's nothing to fight.
    public float Yaw => _yaw;
    private bool _isActive;

    public void SetTarget(Transform target) => _target = target;

    public void Activate()
    {
        _isActive = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Deactivate()
    {
        _isActive = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LateUpdate()
    {
        if (!_isActive || _target == null) return;
        HandleLook();
    }

    private void HandleLook()
    {
        transform.position = _target.position;

        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, verticalClampMin, verticalClampMax);

        _yaw += mouseX;

        transform.rotation = Quaternion.Euler(_xRotation, _yaw, 0f);

    }
}