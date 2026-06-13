using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float sensitivity = 200f;
    [SerializeField] private float verticalClampMin = -80f;
    [SerializeField] private float verticalClampMax =  80f;

    private Transform _target;
    private float _xRotation = 0f;
    private bool _isActive = false;

    public void SetTarget(Transform target)
    {
        _target = target;
    }

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

    private void Update()
    {
        if (!_isActive || _target == null) return;

        transform.position = _target.position;

        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, verticalClampMin, verticalClampMax);

        transform.localRotation = Quaternion.Euler(_xRotation, _target.eulerAngles.y, 0f);
        _target.Rotate(Vector3.up * mouseX);
    }

    public void ApplyRecoil(float upIntensity)
    {
        if (!_isActive) return;
        _xRotation -= upIntensity;
        _xRotation = Mathf.Clamp(_xRotation, verticalClampMin, verticalClampMax);
    }

}
