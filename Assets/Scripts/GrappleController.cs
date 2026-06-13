using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class GrappleController : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxRange = 50f;
    [SerializeField] private float minPullSpeed = 15f; // Increased for feel
    [SerializeField] private float maxPullSpeed = 60f; // Increased for feel
    [SerializeField] private float arrivalThreshold = 1.5f;
    [SerializeField] private LayerMask hookableMask;

    [Header("Hook Projectile")]
    [SerializeField] private float projectileSpeed = 150f; // Rapid straight-line speed
    [SerializeField] private GameObject hookPrefab; 

    private CharacterController cc;
    private PlayerController player;
    private InputManager input;

    private bool isGrappling;
    private bool isProjectileFlying;
    private Vector3 hookPoint;
    private Vector3 projectilePosition;
    private Vector3 lastPullVelocity;

    private GameObject hookInstance;

    public bool IsGrappling => isGrappling;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        player = GetComponent<PlayerController>();
        input = GetComponent<InputManager>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(input.GrappleKey))
        {
            if (isGrappling || isProjectileFlying)
                ReleaseGrapple();
            else
                TryFireHook();
        }

        if (isProjectileFlying)
            HandleProjectile();

        if (isGrappling)
            HandlePull();
        
        // Ensure hook remains between player and target while active
        UpdateHookPosition();
    }

    private void TryFireHook()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRange, hookableMask)) return;

        hookPoint = hit.point;
        projectilePosition = transform.position; // Fire from player body[cite: 1]
        isProjectileFlying = true;

        if (hookPrefab != null)
        {
            hookInstance = Instantiate(hookPrefab, projectilePosition, Quaternion.identity);
        }
        else
        {
            hookInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hookInstance.transform.localScale = new Vector3(0.1f, 0.1f, 0.5f);
            Destroy(hookInstance.GetComponent<Collider>());
        }
    }

    private void HandleProjectile()
    {
        // Move straight toward the hook point[cite: 1]
        projectilePosition = Vector3.MoveTowards(projectilePosition, hookPoint, projectileSpeed * Time.deltaTime);

        if (Vector3.Distance(projectilePosition, hookPoint) < 0.1f)
        {
            projectilePosition = hookPoint;
            isProjectileFlying = false;
            isGrappling = true;
        }
    }

    private void UpdateHookPosition()
    {
        if (hookInstance == null) return;

        // Origin is the player, target is either the flying tip or the wall[cite: 1]
        Vector3 origin = transform.position;
        Vector3 target = isProjectileFlying ? projectilePosition : hookPoint;

        // Position the hook at the midpoint between player and target[cite: 1]
        hookInstance.transform.position = (origin + target) / 2f;
        
        // Rotate to look at the target point[cite: 1]
        hookInstance.transform.LookAt(target);

        // Stretch the hook to bridge the full gap[cite: 1]
        float distance = Vector3.Distance(origin, target);
        Vector3 scale = hookInstance.transform.localScale;
        scale.z = distance; 
        hookInstance.transform.localScale = scale;
    }

    private void HandlePull()
    {
        Vector3 toHook = hookPoint - transform.position;
        float distance = toHook.magnitude;

        if (distance <= arrivalThreshold)
        {
            ReleaseGrapple();
            return;
        }

        float t = 1f - Mathf.Clamp01((distance - arrivalThreshold) / (maxRange - arrivalThreshold));
        float speed = Mathf.Lerp(minPullSpeed, maxPullSpeed, t);

        player.SuppressGravity();
        lastPullVelocity = toHook.normalized * speed;
        cc.Move(lastPullVelocity * Time.deltaTime);
    }

    private void ReleaseGrapple()
    {
        if (isGrappling) player.AddVelocity(lastPullVelocity);

        isGrappling = false;
        isProjectileFlying = false;

        if (hookInstance != null)
        {
            Destroy(hookInstance);
            hookInstance = null;
        }
    }
}