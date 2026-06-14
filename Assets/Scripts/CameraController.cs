using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private float sensitivity = 200f;
    [SerializeField] private float verticalClampMin = -80f;
    [SerializeField] private float verticalClampMax = 80f;
    [SerializeField] private float scopedSensMultiplier = 0.25f;

    [Header("FOV")]
    [SerializeField] private float normalFOV    = 90f;
    [SerializeField] private float scopedFOV    = 9f;
    [SerializeField] private float boostedFOV   = 110f;
    [SerializeField] private float fovLerpSpeed = 10f;

    [Header("Recoil")]
    [SerializeField] private float recoilSpeed = 8f;

    [Header("Head Bob")]
    [SerializeField] private float bobFrequency  = 10f;
    [SerializeField] private float bobHorizontal = 0.05f;
    [SerializeField] private float bobVertical   = 0.08f;

    private Transform _target;
    private float _xRotation;
    private bool _isActive;

    // FOV state
    private float _targetFOV;
    private bool _isScoped;
    private bool _isBoosted;

    // Recoil
    private float _recoilTarget;
    private float _recoilCurrent;

    // Head bob
    private float _bobTimer;
    private Vector3 _bobOffset;
    private bool _isBobbing;
    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _targetFOV = normalFOV;
    }

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
        HandleFOV();
        HandleBob();
    }

    private void HandleLook()
    {
        transform.position = _target.position + _bobOffset;
        float finalSensitivity = _isScoped ? sensitivity * scopedSensMultiplier : sensitivity;

        float mouseX = Input.GetAxis("Mouse X") * finalSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * finalSensitivity * Time.deltaTime;

        _xRotation -= mouseY;

        // Mouse moving up counteracts recoil target
        if (mouseY < 0f)
        {
            float counteract = Mathf.Min(-mouseY, Mathf.Abs(_recoilTarget));
            _recoilTarget += counteract * (_recoilTarget < 0f ? 1f : -1f);
        }

        _xRotation = Mathf.Clamp(_xRotation, verticalClampMin, verticalClampMax);

        // Smoothly move current recoil toward target
        _recoilCurrent = Mathf.Lerp(_recoilCurrent, _recoilTarget, recoilSpeed * Time.deltaTime);

        float totalRotation = Mathf.Clamp(_xRotation + _recoilCurrent, verticalClampMin, verticalClampMax);

        transform.rotation = Quaternion.Euler(totalRotation, _target.eulerAngles.y, 0f);
        _target.Rotate(Vector3.up * mouseX);
    }

    // Smoothly lerps current FOV toward the active target FOV.
    private void HandleFOV()
    {
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _targetFOV, fovLerpSpeed * Time.deltaTime);
    }

    // Applies a sine-wave positional offset to simulate head movement while walking or running.
    // Stops immediately when _isBobbing is false.
    private void HandleBob()
    {
        if (!_isBobbing)
        {
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * bobFrequency);
            _bobTimer = 0f;
            return;
        }

        _bobTimer += Time.deltaTime * bobFrequency;
        float horizontal = Mathf.Cos(_bobTimer) * bobHorizontal;
        float vertical   = Mathf.Sin(_bobTimer * 2f) * bobVertical;
        _bobOffset = new Vector3(horizontal, vertical, 0f);
    }

    // Called by CombatController when the player fires.
    public void ApplyRecoil(float intensity)
    {
        if (!_isActive) return;
        _recoilTarget -= intensity;
        _recoilTarget = Mathf.Clamp(_recoilTarget, verticalClampMin, verticalClampMax);
    }

    // Called by CombatController to toggle scope FOV.
    public void SetScoped(bool scoped)
    {
        _isScoped  = scoped;
        _isBoosted = false;
        _targetFOV = _isScoped ? scopedFOV : normalFOV;
    }

    // Called by PlayerController on dash start/end, and GrappleController on grapple start/end.
    public void SetFOVBoosted(bool boosted)
    {
        if (_isScoped) return; // Scope takes priority.
        _isBoosted = boosted;
        _targetFOV = _isBoosted ? boostedFOV : normalFOV;
    }

    // Called every frame by PlayerController with current movement state.
    public void SetBobbing(bool bobbing) => _isBobbing = bobbing;
}