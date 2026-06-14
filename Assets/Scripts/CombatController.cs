using Unity.Netcode;
using UnityEngine;

public class CombatController : NetworkBehaviour
{
    [Header("Weapon")]
    [SerializeField] private int maxAmmo = 10;
    [SerializeField] private float fireRate = 3f;
    [SerializeField] private float recoilIntensity = 30f;
    [SerializeField] private float reloadDuration = 3f;

    [Header("Respawn")]
    [SerializeField] private string spawnPointTag = "SpawnPoint";

    private CharacterController _cc;
    private InputManager _input;
    private CameraController _camera;
    private GameManager _gameManager;
    private SoundManager _sound;

    private bool _isScoped;
    private float _fireCooldown;
    private int _currentAmmo;
    private bool _isReloading;
    private float _reloadTimer;

    public bool IsScoped    => _isScoped;
    public bool IsReloading => _isReloading;
    public int  CurrentAmmo => _currentAmmo;
    public int  MaxAmmo     => maxAmmo;

    public override void OnNetworkSpawn()
    {
        _currentAmmo = maxAmmo;
        enabled = false; // add this
    }

    public void ActivatePlayer()
    {
        if (!IsOwner) return;
        enabled = true;
    }

    private void Awake()
    {
        _cc          = GetComponent<CharacterController>();
        _input       = GetComponent<InputManager>();
        _camera      = Camera.main.GetComponent<CameraController>();
        _gameManager = FindFirstObjectByType<GameManager>();
        _sound       = SoundManager.Instance;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (_fireCooldown > 0f) _fireCooldown -= Time.deltaTime;

        if (_isReloading)
        {
            HandleReload();
            return;
        }

        HandleScope();
        HandleShoot();
        HandleManualReload();
    }

    // Toggles scope while right mouse is held. Tells CameraController to set scoped FOV.
    private void HandleScope()
    {
        bool wasScoped = _isScoped;
        _isScoped = Input.GetKey(_input.ScopeKey);

        if (_isScoped != wasScoped)
            _camera?.SetScoped(_isScoped);
    }

    // Fires hitscan shot. Decrements ammo, applies recoil, sends kill RPC if target hit.
    private void HandleShoot()
    {
        if (!Input.GetKeyDown(_input.FireKey)) return;
        if (_fireCooldown > 0f)
        {
            Debug.Log("Fire on cooldown: " + _fireCooldown);
            return;
        }

        if (_currentAmmo <= 0)
        {
            StartReload();
            return;
        }

        _fireCooldown = fireRate;
        _currentAmmo--;

        _camera?.SetScoped(false);
        _camera?.ApplyRecoil(recoilIntensity);
        _sound?.PlayShoot();
        _isScoped = false;

        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        Debug.Log("Firing ray from: " + ray.origin + " direction: " + ray.direction);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.Log("Ray hit nothing");
            return;
        }

        Debug.Log("Ray hit: " + hit.collider.gameObject.name);

        NetworkObject netObj = hit.collider.GetComponentInParent<NetworkObject>();
        if (netObj == null)
        {
            Debug.Log("No NetworkObject on hit object");
            return;
        }
        if (netObj.IsOwner)
        {
            Debug.Log("Hit own NetworkObject");
            return;
        }

        Debug.Log("Sending ShootServerRpc for: " + netObj.NetworkObjectId);
        ShootServerRpc(netObj.NetworkObjectId);
    }

    // Allows manual reload with R key if not full and not already reloading.
    private void HandleManualReload()
    {
        if (!Input.GetKeyDown(_input.ReloadKey)) return;
        if (_currentAmmo == maxAmmo) return;
        StartReload();
    }

    // Begins the reload sequence. Forces unscope.
    private void StartReload()
    {
        if (_isReloading) return;
        _isReloading = true;
        _reloadTimer = reloadDuration;
        _isScoped    = false;
        _camera?.SetScoped(false);
        _sound?.PlayReload();
    }

    // Ticks the reload timer. Refills ammo when complete.
    private void HandleReload()
    {
        _reloadTimer -= Time.deltaTime;
        if (_reloadTimer <= 0f)
        {
            _currentAmmo = maxAmmo;
            _isReloading = false;
        }
    }

    // Sent to server when a hit is detected on the client. Server validates and triggers respawn.
    [ServerRpc]
    private void ShootServerRpc(ulong targetNetworkObjectId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
            targetNetworkObjectId, out NetworkObject target)) return;

        CombatController targetCombat = target.GetComponent<CombatController>();
        if (targetCombat == null) return;

        targetCombat.RespawnClientRpc();
        _gameManager?.RegisterKill(OwnerClientId);
    }

    // Fires on all clients but only executes on the victim's machine.
    [ClientRpc]
    private void RespawnClientRpc()
    {
        if (!IsOwner) return;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag(spawnPointTag);
        if (spawnPoints.Length == 0) return;

        Transform spawn = spawnPoints[Random.Range(0, spawnPoints.Length)].transform;
        _cc.enabled = false;
        transform.position = spawn.position;
        _cc.enabled = true;

        _isScoped    = false;
        _isReloading = false;
        _currentAmmo = maxAmmo;
        _camera?.SetScoped(false);
    }
}