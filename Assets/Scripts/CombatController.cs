using Unity.Netcode;
using UnityEngine;

public class CombatController : NetworkBehaviour
{
    [Header("Scope")]
    [SerializeField] private float normalFOV = 90f;
    [SerializeField] private float scopedFOV = 9f;
    [SerializeField] private float scopeLerpSpeed = 10f;
    [SerializeField] private GameObject scopeUI;

    [Header("Weapon Stats & Recoil")]
    [SerializeField] private int maxAmmo = 10;
    [SerializeField] private float fireRate = 3f;
    [SerializeField] private float recoilIntensity = 30f;
    [SerializeField] private float reloadDuration = 2.0f;

    [Header("Respawn")]
    [SerializeField] private string spawnPointTag = "SpawnPoint";
    private CharacterController cc;
    private InputManager input;
    private CameraController cameraController;
    private bool isScoped;
    private float fireCooldown;
    // Ammo state
    private int currentAmmo;
    private bool isReloading;
    private float reloadTimer;
    private bool _combatLocked = true;
    public bool IsScoped => isScoped;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) enabled = false;
    }

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        input = GetComponent<InputManager>();
        cameraController = Camera.main.GetComponent<CameraController>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (fireCooldown > 0f)
            fireCooldown -= Time.deltaTime;

        HandleScope();
        HandleShoot();
    }

    private void HandleScope()
    {
        if (Input.GetKey(input.ScopeKey)) isScoped = true;
        else isScoped = false;

        if (scopeUI != null) scopeUI.SetActive(isScoped);

        float targetFOV = isScoped ? scopedFOV : normalFOV;
        Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, targetFOV, scopeLerpSpeed * Time.deltaTime);
    }

    private void HandleShoot()
    {
        if (_combatLocked) return;
        if (!Input.GetKeyDown(input.FireKey)) return;
        if (fireCooldown > 0f) return;

        // Auto reload if pulling trigger empty
        if (currentAmmo <= 0)
        {
            StartReload();
            return;
        }

        fireCooldown = fireRate;
        currentAmmo--;

        isScoped = false;
        if (scopeUI != null) scopeUI.SetActive(false);
        Camera.main.fieldOfView = normalFOV;

        // 2. RECOIL APPLICATION
        if (cameraController != null)
        {
            cameraController.ApplyRecoil(recoilIntensity);
        }

        // Raycast logic
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        NetworkObject netObj = hit.collider.GetComponent<NetworkObject>();
        if (netObj == null || netObj.IsOwner) return;

        ShootServerRpc(netObj.NetworkObjectId);
    }

    private void StartReload()
    {
        isReloading = true;
        reloadTimer = reloadDuration;
        
        // Force unscope during reload sequence
        isScoped = false;
        if (scopeUI != null) scopeUI.SetActive(false);
    }

    private void HandleReload()
    {
        reloadTimer -= Time.deltaTime;
        if (reloadTimer <= 0f)
        {
            currentAmmo = maxAmmo;
            isReloading = false;
        }
    }

    [ServerRpc]
    private void ShootServerRpc(ulong targetNetworkObjectId)
    {
        Debug.Log($"ShootServerRpc called, target ID: {targetNetworkObjectId}");
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject target))
            return;

        CombatController targetCombat = target.GetComponent<CombatController>();
        if (targetCombat != null)
            targetCombat.RespawnClientRpc();
    }

    [ClientRpc]
    private void RespawnClientRpc()
    {
        // Only the actual owner of this object should respawn.
        if (!IsOwner) return;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag(spawnPointTag);
        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points found with tag: " + spawnPointTag);
            return;
        }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)].transform;

        // CharacterController must be disabled to teleport, otherwise it fights the position change.
        cc.enabled = false;
        transform.position = spawnPoint.position;
        cc.enabled = true;

        // Unscope on death.
        isScoped = false;
        if (scopeUI != null) scopeUI.SetActive(false);
    }

    public void UnlockCombat()
    {
        _combatLocked = false;
    }
}