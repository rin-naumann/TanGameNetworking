using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class GrappleController : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxRange = 50f;
    [SerializeField] private float minPullSpeed = 15f;
    [SerializeField] private float maxPullSpeed = 60f;
    [SerializeField] private float arrivalThreshold = 1.5f;
    [SerializeField] private LayerMask hookableMask;

    [Header("Hook Projectile")]
    [SerializeField] private float projectileSpeed = 150f;
    [SerializeField] private GameObject hookPrefab;

    private CharacterController _cc;
    private PlayerController _player;
    private InputManager _input;
    private CameraController _camera;
    private SoundManager _sound;

    private bool _isGrappling;
    private bool _isProjectileFlying;
    private Vector3 _hookPoint;
    private Vector3 _projectilePosition;
    private Vector3 _lastPullVelocity;
    private GameObject _hookInstance;
    private Vector3 _fireOrigin;
    public bool IsGrappling => _isGrappling;

    private void Awake()
    {
        enabled = false;
    }

    public void Activate()
    {
        if (!IsOwner) return;
        enabled = true;
        _cc     = GetComponent<CharacterController>();
        _player = GetComponent<PlayerController>();
        _input  = GetComponent<InputManager>();
        _camera = Camera.main.GetComponent<CameraController>();
        _sound  = SoundManager.Instance;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(_input.GrappleKey))
        {
            if (_isGrappling || _isProjectileFlying)
                ReleaseGrapple();
            else
                TryFireHook();
        }

        if (_isProjectileFlying) HandleProjectile();
        if (_isGrappling)        HandlePull();

        UpdateHookVisual();
    }

    // Raycasts from screen center. If a hookable surface is in range, fires the hook projectile.
    private void TryFireHook()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRange, hookableMask)) return;

        _hookPoint          = hit.point;
        _fireOrigin         = transform.position;
        _projectilePosition = _fireOrigin;
        _isProjectileFlying = true;

        _hookInstance = hookPrefab != null
            ? Instantiate(hookPrefab, _projectilePosition, Quaternion.identity)
            : CreateDefaultHook();
    }

    // Creates a stretched cube as a fallback hook visual when no prefab is assigned.
    private GameObject CreateDefaultHook()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.transform.localScale = new Vector3(0.1f, 0.1f, 0.5f);
        Destroy(obj.GetComponent<Collider>());
        return obj;
    }

    // Moves the projectile toward the hook point. Switches to pulling when it arrives.
    private void HandleProjectile()
    {
        _projectilePosition = Vector3.MoveTowards(
            _projectilePosition, _hookPoint, projectileSpeed * Time.deltaTime);

        if (Vector3.Distance(_projectilePosition, _hookPoint) < 0.1f)
        {
            _projectilePosition = _hookPoint;
            _isProjectileFlying = false;
            _isGrappling        = true;
            _camera?.SetFOVBoosted(true);
        }
    }

    // Positions and stretches the hook visual between the player and the current target point.
    private void UpdateHookVisual()
    {
        if (_hookInstance == null) return;

        if (_isProjectileFlying)
        {
            // During travel: move the hook to the current projectile position
            // and face it toward the hook point
            _hookInstance.transform.position = _projectilePosition;
            _hookInstance.transform.LookAt(_hookPoint);
        }
        else if (_isGrappling)
        {
            // Once landed: lock it to the hook point, facing the player
            _hookInstance.transform.position = _hookPoint;
            _hookInstance.transform.LookAt(transform.position);
        }
    }

    // Pulls the player toward the hook point with accelerating speed. Suppresses gravity each frame.
    private void HandlePull()
    {
        Vector3 toHook   = _hookPoint - transform.position;
        float   distance = toHook.magnitude;

        if (distance <= arrivalThreshold)
        {
            ReleaseGrapple();
            return;
        }

        float t     = 1f - Mathf.Clamp01((distance - arrivalThreshold) / (maxRange - arrivalThreshold));
        float speed = Mathf.Lerp(minPullSpeed, maxPullSpeed, t);

        _lastPullVelocity = toHook.normalized * speed;
        _player.SetVelocity(_lastPullVelocity);
        _sound?.StartGrappleLoop();
    }

    // Releases the hook, transfers momentum to PlayerController, and restores FOV.
    private void ReleaseGrapple()
    {
        if (_isGrappling)
            _player.AddVelocity(_lastPullVelocity);

        _isGrappling        = false;
        _isProjectileFlying = false;

        _sound?.StopGrappleLoop();
        _camera?.SetFOVBoosted(false);

        if (_hookInstance != null)
        {
            Destroy(_hookInstance);
            _hookInstance = null;
        }
    }
}