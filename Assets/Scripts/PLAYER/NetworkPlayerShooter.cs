using UnityEngine;
using Unity.Netcode;


public class NetworkPlayerShooter : NetworkBehaviour
{
    [SerializeField] GameObject bulletPrefab;//projectile prefab
    [SerializeField] Transform firePoint;//where the projectile spawns
    [SerializeField] float fireRate = 0.25f;//firate / attack Cooldown
    [SerializeField] KeyCode keyCode = KeyCode.Mouse1;//key to fire
    private float fireTimer;//timer to track fire rate

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) return;//only allow the owner to shoot
        if (Input.GetKeyDown(keyCode) && Time.time >= fireTimer)
        {
            fireTimer = Time.time + fireRate;//reset fire timer
            RequestShootServerRpc(firePoint.position, firePoint.forward);//call the server rpc to shoot
        }
    }
    [ServerRpc]
    private void RequestShootServerRpc(Vector3 spawnPosition, Vector3 shootDirection)
    {
        //Instantiate = create the projectile on the server
        //Spawn = tells unity to show this object on connected players
        GameObject projectileInstantiate = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.LookRotation(shootDirection)
        );
        NetworkObject networkObject = projectileInstantiate.GetComponent<NetworkObject>();
        networkObject.Spawn();
    }
}